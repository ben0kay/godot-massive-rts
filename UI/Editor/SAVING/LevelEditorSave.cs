// Stages Godot resources, registers new levels and retains recovery copies on overwrite.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Godot;

namespace MassiveRts;

internal static class LevelEditorSave
{
    private const string WorkFolder = "res://.godot/level-editor";
    internal record Destination(string Id, string Scene, string Metadata, string Campaign, LevelDefinition Existing,
        string SceneRevision, string MetadataRevision, string CampaignRevision);

    public static bool CanWrite => OS.HasFeature("editor") && File.Exists(Disk("res://project.godot"));

    // =========================================================
    public static Destination Inspect(LevelEditorMode mode, string id)
    {
        if (!CampaignCatalog.ValidId(id) || IsDeviceName(id))
            throw new InvalidOperationException("Use a level ID with letters, digits, underscores or hyphens, such as Level003.");
        var catalog = CampaignCatalog.Load(fresh: true);
        var campaign = catalog.Campaigns.Single(entry => entry.Id == mode.CampaignId);
        string folder = $"{mode.Folder}/{id}";
        string scene = $"{folder}/{id}.tscn", metadata = $"{folder}/{id}.tres";
        var entry = campaign.Levels.FirstOrDefault(level => EqualPath(level.Id, id));
        if (entry != null && (entry.Id != id || entry.ScenePath != scene || entry.ResourcePath != metadata))
            throw new InvalidOperationException("That ID already belongs to a differently named or located level.");
        if (catalog.Campaigns.Any(other => other.Levels.Any(level =>
            (other != campaign || level != entry) && (EqualPath(level.ScenePath, scene) || EqualPath(level.ResourcePath, metadata)))))
            throw new InvalidOperationException("The destination paths are registered to another level.");
        if (entry == null && Directory.EnumerateFileSystemEntries(Disk(mode.Folder))
            .Any(path => EqualPath(Path.GetFileName(path), id)))
            throw new InvalidOperationException("That folder already exists outside the catalogue. Choose another ID.");
        RejectLink(mode.Folder);
        RejectLink(folder);
        RejectLink(scene);
        RejectLink(metadata);
        RejectLink(campaign.ResourcePath);
        if (entry != null && (!File.Exists(Disk(scene)) || !File.Exists(Disk(metadata))))
            throw new InvalidOperationException("The registered level is missing a scene or settings file.");
        return new(id, scene, metadata, campaign.ResourcePath, entry, Revision(scene), Revision(metadata), Revision(campaign.ResourcePath));
    }

    // =========================================================
    public static void Write(LevelEditorMode mode, Destination confirmed, CommandSandbox map, LevelDefinition settings, string title)
    {
        if (!CanWrite) throw new InvalidOperationException("Source saving requires the Godot editor's project build.");
        Directory.CreateDirectory(Disk(WorkFolder));
        using var writer = File.Open(Disk(WorkFolder + "/write.lock"), FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite, FileShare.None);
        var target = Inspect(mode, confirmed.Id);
        if (target.SceneRevision != confirmed.SceneRevision || target.MetadataRevision != confirmed.MetadataRevision ||
            target.CampaignRevision != confirmed.CampaignRevision)
            throw new InvalidOperationException("The level or campaign changed since you selected Save. Review it and try again.");

        var campaign = ResourceLoader.Load<CampaignDefinition>(target.Campaign, cacheMode: ResourceLoader.CacheMode.IgnoreDeep);
        var level = (LevelDefinition)settings.Duplicate();
        level.Id = target.Id;
        level.DisplayName = title;
        level.ScenePath = target.Scene;
        level.Order = target.Existing?.Order ?? checked(campaign.Levels.Max(entry => entry.Order) + 1);

        ValidateContent(mode, map);

        string job = $"{WorkFolder}/{mode.CampaignId}-{target.Id}-{Guid.NewGuid():N}";
        Directory.CreateDirectory(Disk(job));
        var packed = new PackedScene();
        var ground = map.GetNodeOrNull<MeshInstance3D>("Ground");
        var previewMaterial = ground?.MaterialOverride;
        var originalName = map.Name;
        try
        {
            // The runtime uses the adjacent LevelDefinition. This avoids references to a staging or source level.
            map.LevelSettings = null;
            map.Name = target.Id;
            if (ground != null) ground.MaterialOverride = null;
            Check(packed.Pack(map), "pack scene");
        }
        finally
        {
            map.LevelSettings = settings;
            map.Name = originalName;
            if (ground != null) ground.MaterialOverride = previewMaterial;
        }

        string stagedScene = job + "/level.tscn", stagedLevel = job + "/level.tres";
        string stagedCampaign = job + "/campaign.tres";
        Check(ResourceSaver.Save(packed, stagedScene), "stage scene");
        Check(ResourceSaver.Save(level, stagedLevel), "stage level metadata");
        PreserveUid(target.Scene, stagedScene);
        PreserveUid(target.Metadata, stagedLevel);
        var verified = ResourceLoader.Load<PackedScene>(stagedScene, cacheMode: ResourceLoader.CacheMode.IgnoreDeep)?.Instantiate();
        try
        {
            if (verified is not CommandSandbox restored ||
                (restored.PresetRules != null) != mode.Preset || !SameSceneNodes(map, restored))
                throw new InvalidOperationException("The staged scene could not restore the editable map.");
        }
        finally { verified?.Free(); }

        var checkedLevel = ResourceLoader.Load<LevelDefinition>(stagedLevel, cacheMode: ResourceLoader.CacheMode.IgnoreDeep);
        if (checkedLevel?.Id != target.Id || checkedLevel.ScenePath != target.Scene)
            throw new InvalidOperationException("The staged level metadata could not be reopened.");
        if (target.Existing == null)
        {
            level.TakeOverPath(target.Metadata);
            campaign.Levels.Add(level);
            Check(ResourceSaver.Save(campaign, stagedCampaign), "stage campaign registration");
            PreserveUid(target.Campaign, stagedCampaign);
        }

        var files = new List<(string Staged, string Target, string Previous, string Backup)>
        {
            (stagedScene, target.Scene, target.SceneRevision, job + "/previous.tscn"),
            (stagedLevel, target.Metadata, target.MetadataRevision, job + "/previous.tres")
        };
        if (target.Existing == null)
            files.Add((stagedCampaign, target.Campaign, target.CampaignRevision, job + "/previous-campaign.tres"));

        foreach (var file in files)
        {
            if (Revision(file.Target) != file.Previous) throw new IOException($"Changed during save: {file.Target}");
            if (file.Previous != null) File.Copy(Disk(file.Target), Disk(file.Backup));
        }
        File.WriteAllLines(Disk(job + "/recovery.txt"), files.Select(file => $"{file.Backup} -> {file.Target}"));
        string destinationFolder = Disk(target.Scene.GetBaseDir());
        Directory.CreateDirectory(destinationFolder);
        int published = 0;
        try
        {
            foreach (var file in files)
            {
                if (Revision(file.Target) != file.Previous) throw new IOException($"Changed during save: {file.Target}");
                if (file.Staged == stagedCampaign)
                {
                    var registered = ResourceLoader.Load<CampaignDefinition>(stagedCampaign, cacheMode: ResourceLoader.CacheMode.IgnoreDeep);
                    if (registered?.Id != mode.CampaignId ||
                        !registered.Levels.Select(entry => (entry?.Id, entry?.ScenePath, entry?.ResourcePath))
                            .SequenceEqual(campaign.Levels.Select(entry => (entry.Id, entry.ScenePath, entry.ResourcePath))))
                        throw new IOException("The staged campaign could not reopen the new level.");
                }
                File.Move(Disk(file.Staged), Disk(file.Target), file.Previous != null);
                published++;
            }
        }
        catch (Exception error)
        {
            try
            {
                for (int i = published - 1; i >= 0; i--)
                {
                    var file = files[i];
                    if (file.Previous == null) File.Delete(Disk(file.Target));
                    else File.Move(Disk(file.Backup), Disk(file.Target), overwrite: true);
                }
                if (target.Existing == null && !Directory.EnumerateFileSystemEntries(destinationFolder).Any())
                    Directory.Delete(destinationFolder);
            }
            catch (Exception recoveryError)
            {
                throw new IOException($"Save interrupted. Recovery files are at {Disk(job)}.",
                    new AggregateException(error, recoveryError));
            }
            throw;
        }
    }

    // =========================================================
    internal static void ValidateContent(LevelEditorMode mode, CommandSandbox map)
    {
        var paint = map.GetNodeOrNull<WorldPaint>("WorldPaint");
        if (paint?.Data is WorldPaintData data && !paint.TryMerge(new(data.RockCells), new(data.WaterCells), out _, out _))
            throw new InvalidOperationException("The rock, water and bridge layout exceeds the navigation budget. Simplify the painted banks or bridge layout.");
        if (paint?.Data is WorldPaintData forestData && forestData.ForestCells.Count > WorldPaint.MaxPaintedCells)
            throw new InvalidOperationException("The forest layout exceeds the map paint budget.");

        var roster = GD.Load<UnitCatalog>("res://GAMEPLAY/UNITS/CATALOG/DefaultUnitCatalog.tres").Definitions
            .Where(unit => unit != null).Select(unit => unit.Id).ToHashSet();
        var groups = map.GetNode("StartingUnits").GetChildren().OfType<CommandUnitPlacement>().ToArray();
        if (groups.Any(group => group.Unit == null || !roster.Contains(group.Unit.Id) || group.Count is < 1 or > 256))
            throw new InvalidOperationException("Every soldier formation needs a registered unit type and a count between 1 and 256.");
        var towers = map.GetNodeOrNull<Node3D>("StartingDefenses")?.GetChildren().OfType<CommandLaserPlacement>().ToArray()
            ?? Array.Empty<CommandLaserPlacement>();
        if (towers.Length > CommandSandbox.MaxStartingTowers)
            throw new InvalidOperationException($"A level supports up to {CommandSandbox.MaxStartingTowers} starting laser towers.");
        if (mode.Preset)
        {
            var rules = map.PresetRules;
            if (rules.LineAxis != PresetBattleRules.DeploymentAxis.Disabled &&
                Mathf.Abs(rules.LineOffset) >= map.MapSize * 0.5f)
                throw new InvalidOperationException("Move the deployment line inside the map boundary.");
            if (groups.Any(group => !group.FitsSide(rules, group.GlobalPosition)) ||
                towers.Any(tower => !tower.Red || !tower.FitsSide(rules, tower.GlobalPosition)))
                throw new InvalidOperationException("Move preplaced formations and laser towers onto the Red side of the line.");
            if (groups.Sum(group => (long)group.SoldierCount) > 100000)
                throw new InvalidOperationException("Preset Battles supports up to 100,000 authored soldiers.");
            if (groups.Any(group => group.Faction != CommandUnitPlacement.Team.Red) ||
                map.PresetRules.BlueUnitLimit < 0 || map.PresetRules.AllowedBlueUnitIds.Any(id => !roster.Contains(id)))
                throw new InvalidOperationException("Check the Red formation factions and Blue deployment settings.");
            return;
        }

        if (map.WaveDefinitions.Count == 0 || map.WaveDefinitions.Any(wave => wave == null || wave.Enemies.Count == 0 ||
            wave.Enemies.Any(entry => !roster.Contains(entry.Key) || entry.Value < 1)))
            throw new InvalidOperationException("Fortress Defense requires at least one valid enemy wave.");
        if (map.WaveDefinitions.SelectMany(wave => wave.Enemies).Sum(entry => (long)entry.Value) > CommandSandbox.WaveUnitCapacity)
            throw new InvalidOperationException($"Fortress waves may contain at most {CommandSandbox.WaveUnitCapacity} soldiers in total.");
        if (groups.Sum(group => (long)group.SoldierCount) + (map.SpawnDefaultBlueSquad ? CommandMovement.SquadSize : 0) > CommandSandbox.NonWaveUnitCapacity)
            throw new InvalidOperationException($"Fortress may start with at most {CommandSandbox.NonWaveUnitCapacity} soldiers.");
    }

    private static bool SameSceneNodes(Node source, Node restored)
    {
        var expected = source.GetChildren().Where(child => child.Owner != null).ToArray();
        var actual = restored.GetChildren();
        return expected.Length == actual.Count && expected.All(child =>
            restored.GetNodeOrNull(new NodePath(child.Name.ToString())) is Node match &&
            match.GetClass() == child.GetClass() && SameSceneNodes(child, match));
    }

    private static void PreserveUid(string destination, string staged)
    {
        if (!File.Exists(Disk(destination))) return;
        long uid = ResourceLoader.GetResourceUid(destination);
        if (uid != -1) Check(ResourceSaver.SetUid(staged, uid), "preserve resource UID");
    }

    private static void RejectLink(string path)
    {
        var entry = new FileInfo(Disk(path));
        if (entry.LinkTarget != null) throw new IOException($"Source saving cannot replace a symbolic link: {path}");
    }

    private static string Revision(string path) => File.Exists(Disk(path))
        ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Disk(path)))) : null;

    private static bool EqualPath(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    private static bool IsDeviceName(string id)
    {
        string upper = id.ToUpperInvariant();
        return upper is "CON" or "PRN" or "AUX" or "NUL" ||
            (upper.Length == 4 && (upper.StartsWith("COM") || upper.StartsWith("LPT")) && char.IsAsciiDigit(upper[3]));
    }

    private static string Disk(string path) => ProjectSettings.GlobalizePath(path);

    private static void Check(Error error, string action)
    {
        if (error != Error.Ok) throw new IOException($"Could not {action} ({error}).");
    }
}
