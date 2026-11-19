// Stores one authored defense and preserves the laser-tower default for existing scenes.
using Godot;

namespace MassiveRts;

public partial class CommandLaserPlacement : Marker3D
{
    internal const string DefinitionPath = "res://WORLD/BUILDINGS/DEFINITIONS/LaserTower001.tres";
    internal const float BaseHeight = 1f;
    private static BuildingDefinition definition;
    [Export] public BuildingDefinition Definition { get; set; }
    internal BuildingDefinition EffectiveDefinition => Definition ?? (definition ??= GD.Load<BuildingDefinition>(DefinitionPath));
    [Export] public bool Red { get; set; }
    internal byte Faction => Red ? Factions.Red : Factions.Blue;

    internal ObstacleRectangle Footprint(Vector3 center) => FootprintAt(center, EffectiveDefinition);

    internal static ObstacleRectangle FootprintAt(Vector3 center, BuildingDefinition definition)
    {
        int width = Mathf.Clamp(definition.Width, 1, 16), depth = Mathf.Clamp(definition.Depth, 1, 16);
        const float grid = DeploymentLayout.CellSize;
        return new ObstacleRectangle(Mathf.RoundToInt(center.X / grid - width * 0.5f),
            Mathf.RoundToInt(center.Z / grid - depth * 0.5f), width, depth);
    }

    internal Vector3 SnapCenter(Vector3 point)
    {
        var area = Footprint(point);
        var center = (area.Min + area.Max) * 0.5f;
        return new Vector3(center.X, point.Y, center.Y);
    }

    internal bool FitsSide(PresetBattleRules rules, Vector3 point)
    {
        var area = Footprint(point);
        var size = (area.Max - area.Min) * 0.5f;
        return rules.AllowsSide(SnapCenter(point), rules.LineAxis == PresetBattleRules.DeploymentAxis.X ? size.X : size.Y, !Red);
    }
}
