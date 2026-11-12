// Converts authored definitions into a compact lookup once per arena.
using System;
using System.Collections.Generic;
using Godot;
namespace MassiveRts;

public readonly record struct ShockwaveStats(int EveryAttacks, float Radius, float Damage, float EdgeDamageFraction, bool RespectsCover);

// Ground outline shared by the renderer and the circular simulation footprint.
public readonly record struct UnitOutline(int Sides, float RadiusX, float RadiusZ, float Rotation)
{
    public float Footprint => Mathf.Max(RadiusX, RadiusZ);

    public static float EffectiveSize(UnitDefinition definition) =>
        float.IsFinite(definition.SizeMultiplier) && definition.SizeMultiplier > 0f
            ? Mathf.Clamp(definition.SizeMultiplier, 0.5f, 3f) : 1f;

    public static UnitOutline FromDefinition(UnitDefinition definition, float size)
    {
        float width = float.IsFinite(definition.Width) && definition.Width > 0f ? definition.Width : 0.8f;
        float legacyRadius = float.IsFinite(definition.FootprintRadius) && definition.FootprintRadius > 0f
            ? definition.FootprintRadius : 0.57f;
        float diamondLongRadius = definition.Shape == UnitShape.Diamond
            ? Mathf.Max(width * 0.707107f, legacyRadius) * size * 0.96f : 0f;
        return FromDimensions(definition.Shape, width * size, diamondLongRadius);
    }

    private static UnitOutline FromDimensions(UnitShape shape, float width, float diamondLongRadius)
    {
        float halfWidth = width * 0.5f;
        float squareRadius = width / Mathf.Sqrt(2f);
        float triangleRadius = width / Mathf.Sqrt(3f);
        float pentagonRadius = width / (1f + Mathf.Cos(Mathf.Pi / 5f));
        return shape switch
        {
            UnitShape.Circle => new(16, halfWidth, halfWidth, 0f),
            UnitShape.Square => new(4, squareRadius, squareRadius, Mathf.Pi * 0.25f),
            UnitShape.Triangle => new(3, triangleRadius, triangleRadius, -Mathf.Pi * 0.5f),
            UnitShape.Diamond => new(4, Mathf.Max(halfWidth, diamondLongRadius), halfWidth, 0f),
            UnitShape.Pentagon => new(5, pentagonRadius, pentagonRadius, -Mathf.Pi * 0.5f),
            UnitShape.Hexagon => new(6, halfWidth, halfWidth, -Mathf.Pi * 0.5f),
            _ => throw new ArgumentOutOfRangeException(nameof(shape))
        };
    }
}

public readonly record struct UnitStats(string Id, string Name, UnitShape Shape, UnitOutline Outline, float Height,
    float Footprint, float Health, float Speed, float Damage, float Interval, float Reach,
    float DamageTaken, bool Ranged, int ProjectileType,
    bool CleaveEnabled, float CleaveCosineSquared, float CleaveSecondaryFraction, int CleaveMaxAdditionalTargets, float MeleeFacingCosineSquared, TrampleStats Trample, float AccelerationRateOverride, ShockwaveStats Shockwave)
{
    public float AttackReach(in UnitStats defender) => Ranged ? Reach : Reach + defender.Footprint;
}

[Tool]
[GlobalClass]
public partial class UnitCatalog : Resource
{
    [Export] public Godot.Collections.Array<UnitDefinition> Definitions { get; set; } = new();

    public RuntimeUnitCatalog Compile(float movementScale) => new(Definitions, movementScale);
}

public sealed class RuntimeUnitCatalog
{
    public const string CircleId = "circle_infantry", SquareId = "square_infantry", TriangleId = "triangle_archer",
        DiamondId = "diamond_cavalry", PentagonId = "pentagon_elite", HexagonId = "hexagon_siege";
    // Explicit dense mapping. Save files store IDs and never depend on these indices.
    public const byte Circle = 0, Square = 1, Triangle = 2, Diamond = 3, Pentagon = 4, Hexagon = 5;
    private static readonly string[] OrderedIds = { CircleId, SquareId, TriangleId, DiamondId, PentagonId, HexagonId };
    private readonly UnitStats[] stats = new UnitStats[OrderedIds.Length];
    private readonly Dictionary<string, byte> indices = new(StringComparer.Ordinal);
    public ProjectileCatalog Projectiles { get; }
    public int Count => stats.Length;
    public float MaxSpeed { get; }
    public float MaxFootprint { get; }
    public ref readonly UnitStats this[int index] => ref stats[index];
    public bool TryResolve(string id, out byte index)
    {
        index = 0;
        return id != null && indices.TryGetValue(id, out index);
    }

    public RuntimeUnitCatalog(IEnumerable<UnitDefinition> definitions, float movementScale)
    {
        Projectiles = new ProjectileCatalog(movementScale);
        var byId = new Dictionary<string, UnitDefinition>(StringComparer.Ordinal);
        if (definitions == null) throw new ArgumentNullException(nameof(definitions));
        foreach (UnitDefinition definition in definitions)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id) || !byId.TryAdd(definition.Id, definition))
                throw new ArgumentException("Unit definitions require unique, nonempty IDs.");
        }
        // Older custom catalogues can keep their five authored definitions.
        if (byId.Count == Count - 1 && !byId.ContainsKey(HexagonId))
        {
            var hexagon = GD.Load<UnitDefinition>("res://UNITS/DEFINITIONS/HexagonSiege.tres");
            if (hexagon != null) byId.Add(HexagonId, hexagon);
        }
        if (byId.Count != Count) throw new ArgumentException($"The catalogue requires {Count} unit definitions.");
        for (byte i = 0; i < Count; i++)
        {
            string id = OrderedIds[i];
            if (!byId.TryGetValue(id, out UnitDefinition d)) throw new ArgumentException($"Missing unit definition {id}.");
            if (!Enum.IsDefined(d.Shape) || !Enum.IsDefined(d.Style)) throw new ArgumentException($"Invalid shape or attack style for {id}.");
            float size = UnitOutline.EffectiveSize(d);
            UnitOutline outline = UnitOutline.FromDefinition(d, size);
            float footprint = outline.Footprint;
            bool ranged = d.Style == AttackStyle.Ranged;
            float reach = Positive(d.Reach, 1.6f);
            if (!ranged) reach *= size;
            float angle = float.IsFinite(d.CleaveAngleDegrees) ? Mathf.Clamp(d.CleaveAngleDegrees, 1f, 180f) : 100f;
            float cosine = Mathf.Max(0f, Mathf.Cos(Mathf.DegToRad(angle * 0.5f)));
            float facingCosine = Mathf.Max(0f, Mathf.Cos(Mathf.DegToRad(ClampStat(d.MeleeFacingAngleDegrees, 1f, 180f, 100f) * 0.5f)));
            float secondary = float.IsFinite(d.CleaveSecondaryFraction) ? Mathf.Clamp(d.CleaveSecondaryFraction, 0f, 1f) : 0.65f;
            var trample = new TrampleStats(d.TrampleEnabled && d.Style == AttackStyle.Melee,
                ClampStat(d.TrampleRunUp, 0.5f, 10f, 2f),
                ClampStat(d.TrampleDistance, 0.5f, 12f, 5.5f),
                ClampStat(d.TrampleEfficiency, 0.1f, 4f, 1f),
                ClampStat(d.TrampleDamageMultiplier, 0f, 4f, 1.35f),
                ClampStat(d.ChargeResistance, 0.01f, 4f, 0.30f),
                Mathf.Clamp(d.TrampleMaxHits, 1, 6));
            float accelerationSeconds = ClampStat(d.AccelerationSecondsOverride, 0f, 5f, 0f);
            int shockwaveCadence = ranged ? 0 : Mathf.Clamp(d.ShockwaveEveryAttacks, 0, byte.MaxValue);
            float shockwaveDamage = Positive(d.ShockwaveDamage, 50f);
            var shockwave = new ShockwaveStats(shockwaveCadence,
                ClampStat(Positive(d.ShockwaveBaseRadius, 4f), 0.1f, 64f, 4f) * size,
                shockwaveDamage, d.EffectiveShockwaveOuterDamage(shockwaveDamage) / shockwaveDamage, d.ShockwaveRespectsCover);
            stats[i] = new UnitStats(id, string.IsNullOrWhiteSpace(d.DisplayName) ? id : d.DisplayName, d.Shape,
                outline, Positive(d.Height, 1.1f) * size, footprint, Positive(d.Health, 100f), Positive(d.Speed, 5f) * movementScale,
                Positive(d.Damage, 20f), Positive(d.Interval, 0.9f), reach,
                1f - Mathf.Clamp(float.IsFinite(d.Armour) ? d.Armour : 0f, 0f, 1f), ranged,
                ranged ? Projectiles.Register(d.Projectile) : -1,
                d.CleaveEnabled && d.Style == AttackStyle.Melee, cosine * cosine, secondary, Mathf.Clamp(d.CleaveMaxAdditionalTargets, 0, UnitDefinition.MaxCleaveAdditionalTargets),
                facingCosine * facingCosine, trample,
                accelerationSeconds > 0f ? 1f / accelerationSeconds : 0f, shockwave);
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
