// Maps registered campaigns to authoring defaults and existing scene types.
using System;
using Godot;

namespace MassiveRts;

internal sealed record LevelEditorMode(string CampaignId, string Folder, bool Preset)
{
    private const string Template = "res://BENCHMARK/RTS/CommandSandbox.tscn";
    public static readonly LevelEditorMode[] Supported =
    {
        new("Campaign001", "res://LEVELS/PresetBattles", true),
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
            bool blank = Preset && root.GetType() == typeof(Node3D) && root.GetChildCount() == 0 &&
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
            if (Preset)
            {
                level.WaveDefinitions = new();
                level.PresetRules = new PresetBattleRules { LineAxis = PresetBattleRules.DeploymentAxis.Z };
                foreach (string name in new[] { "Citadel", "BlueSpawn", "Resources" })
                    level.GetNode(name).Free();
            }
        }
        if ((level.PresetRules != null) != Preset)
        {
            level.Free();
            throw new InvalidOperationException("The scene's authoring rules do not match this campaign.");
        }
        return level;
    }
}
