// Converts authored definitions into a compact lookup once per arena.
using System;
using System.Collections.Generic;
using Godot;
namespace MassiveRts;

public readonly record struct UnitStats(string Id, string Name, UnitShape Shape, float Width, float Height,
    float Footprint, float Health, float Speed, float Damage, float Interval, float Reach,
    float DamageTaken, bool Ranged, int ProjectileType,
    bool CleaveEnabled, float CleaveCosineSquared, float CleaveSecondaryFraction, TrampleStats Trample)
{
    public float AttackReach(in UnitStats defender) => Ranged ? Reach : Reach + defender.Footprint;
}

[GlobalClass]
public partial class UnitCatalog : Resource
{
    [Export] public Godot.Collections.Array<UnitDefinition> Definitions { get; set; } = new();

    public RuntimeUnitCatalog Compile() => new(Definitions);
}

public sealed class RuntimeUnitCatalog
{
    public const string CircleId = "circle_infantry", SquareId = "square_infantry", TriangleId = "triangle_archer",
        DiamondId = "diamond_cavalry", PentagonId = "pentagon_elite";
    // Explicit dense mapping. Save files store IDs and never depend on these indices.
    public const byte Circle = 0, Square = 1, Triangle = 2, Diamond = 3, Pentagon = 4;
    private static readonly string[] OrderedIds = { CircleId, SquareId, TriangleId, DiamondId, PentagonId };
    private readonly UnitStats[] stats = new UnitStats[OrderedIds.Length];
    private readonly Dictionary<string, byte> indices = new(StringComparer.Ordinal);
    public ProjectileCatalog Projectiles { get; } = new();
    public int Count => stats.Length;
    public float MaxSpeed { get; }
    public float MaxFootprint { get; }
    public ref readonly UnitStats this[int index] => ref stats[index];
    public bool TryResolve(string id, out byte index)
    {
        index = 0;
        return id != null && indices.TryGetValue(id, out index);
    }

    public RuntimeUnitCatalog(IEnumerable<UnitDefinition> definitions)
    {
        var byId = new Dictionary<string, UnitDefinition>(StringComparer.Ordinal);
        if (definitions == null) throw new ArgumentNullException(nameof(definitions));
        foreach (UnitDefinition definition in definitions)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id) || !byId.TryAdd(definition.Id, definition))
                throw new ArgumentException("Unit definitions require unique, nonempty IDs.");
        }
        if (byId.Count != Count) throw new ArgumentException("UNIT0 requires five unit definitions.");
        for (byte i = 0; i < Count; i++)
        {
            string id = OrderedIds[i];
            if (!byId.TryGetValue(id, out UnitDefinition d)) throw new ArgumentException($"Missing unit definition {id}.");
            if (!Enum.IsDefined(d.Shape) || !Enum.IsDefined(d.Style)) throw new ArgumentException($"Invalid shape or attack style for {id}.");
            float width = Positive(d.Width, 0.8f);
            float footprint = Mathf.Max(width * 0.707107f, Positive(d.FootprintRadius, 0.57f));
            float angle = float.IsFinite(d.CleaveAngleDegrees) ? Mathf.Clamp(d.CleaveAngleDegrees, 1f, 180f) : 100f;
            float cosine = Mathf.Max(0f, Mathf.Cos(Mathf.DegToRad(angle * 0.5f)));
            float secondary = float.IsFinite(d.CleaveSecondaryFraction) ? Mathf.Clamp(d.CleaveSecondaryFraction, 0f, 1f) : 0.65f;
            var trample = new TrampleStats(d.TrampleEnabled && d.Style == AttackStyle.Melee,
                ClampStat(d.TrampleRunUp, 0.5f, 10f, 2f),
                ClampStat(d.TrampleDistance, 0.5f, 12f, 5.5f),
                ClampStat(d.TrampleEfficiency, 0.1f, 4f, 1f),
                ClampStat(d.TrampleDamageMultiplier, 0f, 4f, 1.35f),
                ClampStat(d.ChargeResistance, 0.01f, 4f, 0.30f),
                Mathf.Clamp(d.TrampleMaxHits, 1, 6));
            stats[i] = new UnitStats(id, string.IsNullOrWhiteSpace(d.DisplayName) ? id : d.DisplayName, d.Shape,
                width, Positive(d.Height, 1.1f), footprint, Positive(d.Health, 100f), Positive(d.Speed, 5f),
                Positive(d.Damage, 20f), Positive(d.Interval, 0.9f), Positive(d.Reach, 1.6f),
                1f - Mathf.Clamp(float.IsFinite(d.Armour) ? d.Armour : 0f, 0f, 1f), d.Style == AttackStyle.Ranged,
                d.Style == AttackStyle.Ranged ? Projectiles.Register(d.Projectile) : -1,
                d.CleaveEnabled && d.Style == AttackStyle.Melee, cosine * cosine, secondary, trample);
            indices.Add(id, i);
            MaxSpeed = Mathf.Max(MaxSpeed, stats[i].Speed);
            MaxFootprint = Mathf.Max(MaxFootprint, footprint);
        }
        if (Array.TrueForAll(stats, type => type.Ranged)) throw new ArgumentException("The catalogue needs a melee unit.");
    }

    private static float ClampStat(float value, float min, float max, float fallback) =>
        float.IsFinite(value) ? Mathf.Clamp(value, min, max) : fallback;

    private static float Positive(float value, float fallback) => float.IsFinite(value) && value > 0 ? value : fallback;
}
