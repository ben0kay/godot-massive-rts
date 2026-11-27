// Maps registered campaigns to authoring defaults and existing scene types.
using System;
using Godot;

namespace MassiveRts;

internal enum LevelEditorSection { Skirmishes, Missions, FortressDefense }

internal sealed record LevelEditorMode(string CampaignId, string Folder, bool IsAutoBattler)
{
    internal LevelEditorSection Section => !IsAutoBattler ? LevelEditorSection.FortressDefense :
        CampaignCatalog.IsSkirmish(CampaignId) ? LevelEditorSection.Skirmishes : LevelEditorSection.Missions;
    internal string SectionName => Section == LevelEditorSection.FortressDefense ? "Fortress Defense" : Section.ToString();

    private const string Template = "res://BENCHMARK/RTS/CommandSandbox.tscn";
    public static readonly LevelEditorMode[] Supported =
    {
        new("SkirmishClash", "res://LEVELS/Skirmishes/Clash", true),
        new("SkirmishDefense", "res://LEVELS/Skirmishes/Defense", true),
        new("Offense", "res://LEVELS/Offense", true),
        new(AutoBattlerCampaignStore.CampaignId, "res://LEVELS/AutoBattler", true),
        new("CampaignOffense", "res://LEVELS/Campaign/Offense", true),
        new("CampaignDefense", "res://LEVELS/Campaign/Defense", true),
        new("OpenRTS", "res://LEVELS/FortressDefense", false)
    };

    // =========================================================
    public CommandSandbox Open(LevelDefinition existing, out LevelDefinition settings)
    {
        settings = existing == null
            ? new LevelDefinition { MapSize = 256 }
            : (LevelDefinition)existing.Duplicate();
        var source = existing == null ? null : ResourceLoader.Load<PackedScene>(existing.ScenePath, cacheMode: ResourceLoader.CacheMode.IgnoreDeep);
        if (existing != null && source == null) throw new InvalidOperationException("The registered scene could not be read.");
        var root = source?.Instantiate();
        if (root != null && root is not CommandSandbox)
        {
            bool blank = IsAutoBattler && root.GetType() == typeof(Node3D) && root.GetChildCount() == 0 &&
                source.GetState().GetNodePropertyCount(0) == 0;
            root.Free();
            if (!blank) throw new InvalidOperationException("This scene uses a root the level editor does not support.");
            root = null;
        }

        bool fresh = root == null;
        var level = (CommandSandbox)(root ?? GD.Load<PackedScene>(Template).Instantiate());
        level.AuthoringPreview = true;
        level.LevelSettings = settings;
        level.SceneFilePath = "";
        if (fresh)
        {
            level.SpawnDefaultBlueSquad = false;
            if (IsAutoBattler)
            {
                level.WaveDefinitions = new();
                level.AutoBattlerRules = new AutoBattlerRules { LineAxis = AutoBattlerRules.DeploymentAxis.Z };
                if (CampaignCatalog.IsSkirmish(CampaignId)) level.AutoBattlerRules.SetDefaultSkirmishArmy();
                foreach (string name in new[] { "Citadel", "BlueSpawn", "Resources" })
                    level.GetNode(name).Free();
            }
        }
        if ((level.AutoBattlerRules != null) != IsAutoBattler)
        {
            level.Free();
            throw new InvalidOperationException("The scene's authoring rules do not match this campaign.");
        }
        if (IsAutoBattler) level.AutoBattlerRules.Objective = CampaignCatalog.IsOffense(CampaignId)
            ? AutoBattlerRules.ObjectiveType.DestroyCitadel : CampaignCatalog.IsDefense(CampaignId)
            ? AutoBattlerRules.ObjectiveType.ProtectCitadel : AutoBattlerRules.ObjectiveType.EliminateEnemy;
        return level;
    }
}
