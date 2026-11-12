// Registers authored buildings and compiles stable type indices and shot timing.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

[GlobalClass]
public partial class BuildingCatalog : Resource
{
    [Export] public Godot.Collections.Array<BuildingDefinition> Definitions { get; set; } = new();
}

public sealed class RuntimeBuildingCatalog
{
    private readonly List<BuildingStats> types = new();
    private readonly Dictionary<string, int> ids = new(StringComparer.Ordinal);
    public int Count => types.Count;
    public BuildingStats this[int type] => types[type];
    public bool TryResolve(string id, out int type)
    {
        type = -1;
        return id != null && ids.TryGetValue(id, out type);
    }

    public RuntimeBuildingCatalog(BuildingCatalog catalog)
    {
        foreach (var definition in catalog.Definitions)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id) ||
                !ids.TryAdd(definition.Id, types.Count))
                throw new ArgumentException("Building definitions require unique, nonempty IDs.");
            float range = Clamp(definition.MaximumRange, 0.1f, 100f, 30f);
            types.Add(new BuildingStats(definition.Id, definition.DisplayName,
                Mathf.Clamp(definition.Width, 1, 16), Mathf.Clamp(definition.Depth, 1, 16),
                Clamp(definition.MinimumRange, 0f, range, Mathf.Min(4f, range)), range,
                Clamp(definition.Damage, 0f, 100000f, 12f),
                1f / Clamp(definition.ShotsPerSecond, 0.05f, 60f, 2f),
                Mathf.DegToRad(Clamp(definition.TurretDegreesPerSecond, 1f, 3600f, 360f)),
                Clamp(definition.BeamSeconds, 0.01f, 2f, 0.08f), definition.RequiresLineOfSight, definition.AttacksSoldiers));
        }
    }

    private static float Clamp(float value, float min, float max, float fallback) =>
        float.IsFinite(value) ? Mathf.Clamp(value, min, max) : fallback;
}

public readonly record struct BuildingStats(string Id, string Name, int Width, int Depth, float MinimumRange,
    float MaximumRange, float Damage, float Interval, float TurnRate, float BeamSeconds, bool RequiresLineOfSight, bool AttacksSoldiers);

public readonly record struct BuildingPlacement(int X, int Z, byte Faction, int Type)
{
    public ObstacleRectangle Footprint(RuntimeBuildingCatalog catalog) => new(X, Z, catalog[Type].Width, catalog[Type].Depth);
}
