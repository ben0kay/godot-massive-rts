// Converts authored definitions into a compact lookup once per arena.
using System;
using System.Collections.Generic;
using Godot;
namespace MassiveRts;

public readonly record struct ShockwaveStats(int EveryAttacks, float Radius, float Damage, float EdgeDamageFraction, bool RespectsCover);
public readonly record struct RangedRecoilStats(float Distance, float ReturnSeconds);

// Ground outline shared by the renderer and the circular simulation footprint.
public readonly record struct UnitOutline(int Sides, float RadiusX, float RadiusZ, float Rotation, bool IsChevron = false, bool IsHFrame = false, bool IsDrillRig = false, bool IsPike = false, bool IsDreadnaught = false, bool IsDeepFork = false, float ChevronBarrelScale = 1f, float ChevronBarrelElevation = 0f)
{
    private static readonly Vector2[] ChevronVertices =
    {
        new(0f, -0.40f), new(0.5f, 0.09f), new(0.5f, 0.25f),
        new(0f, -0.10f), new(-0.5f, 0.25f), new(-0.5f, 0.09f)
    };
    private static readonly Vector2[] HVertices =
    {
        new(-1f, -1f), new(-0.34f, -1f), new(-0.34f, -0.23f), new(0.34f, -0.23f),
        new(0.34f, -1f), new(1f, -1f), new(1f, 0.84f), new(0.34f, 0.84f),
        new(0.34f, 0.23f), new(-0.34f, 0.23f), new(-0.34f, 0.84f), new(-1f, 0.84f)
    };
    private static readonly Vector2[] DrillRigVertices =
    {
        new(-1f, -0.5f), new(-0.36f, -0.5f), new(0f, -1.5f), new(0.36f, -0.5f),
        new(1f, -0.5f), new(1f, 0.72f), new(-1f, 0.72f)
    };

    // The tip is forward (-Z). A wide rear bar joins the narrow spear shaft.
    private static readonly Vector2[] PikeVertices =
    {
        new(0f, -1f), new(0.24f, -0.62f), new(0.24f, 0.18f),
        new(1f, 0.18f), new(1f, 0.82f), new(-1f, 0.82f),
        new(-1f, 0.18f), new(-0.24f, 0.18f), new(-0.24f, -0.62f)
    };

    private static readonly Vector2[] DreadnaughtVertices =
    {
        new(0, -1), new(1, -0.48f), new(1, 1), new(-1, 1), new(-1, -0.48f)
    };

    private static readonly Vector2[] DeepForkVertices =
    {
        new(0.44f, -1f), new(0.82f, -1f), new(1f, -0.70f), new(1f, 0.72f),
        new(0.80f, 1f), new(-0.80f, 1f), new(-1f, 0.72f), new(-1f, -0.70f),
        new(-0.82f, -1f), new(-0.44f, -1f), new(-0.34f, 0.20f), new(0.34f, 0.20f)
    };

    public float DrillLengthFromOrigin => -DrillRigVertices[2].Y * RadiusZ;

    public float Footprint => IsChevron ? Mathf.Max(RadiusX, BarrelBack + BarrelLength) : IsDeepFork ? Mathf.Max(
        new Vector2(RadiusX * 0.82f, RadiusZ).Length(), new Vector2(RadiusX, RadiusZ * 0.72f).Length()) : IsDreadnaught ? 1f : IsDrillRig
        ? Mathf.Max(DrillLengthFromOrigin, new Vector2(RadiusX, RadiusZ * 0.72f).Length())
        : IsPike ? Mathf.Max(RadiusZ, new Vector2(RadiusX, RadiusZ * 0.82f).Length())
        : IsHFrame ? new Vector2(RadiusX, RadiusZ).Length() : Mathf.Max(RadiusX, RadiusZ);
    public float BarrelWidth => RadiusX * 0.25f;
    public float BarrelBack => RadiusZ * 0.38f;
    public float BarrelLength => (RadiusZ - BarrelBack) * ChevronBarrelScale;
    // Preserve the original full-length collision clearance while projecting the
    // elevated barrel into XZ for the editor and the real muzzle position.
    public float BarrelFront => IsChevron
        ? BarrelBack + BarrelLength * Mathf.Cos(ChevronBarrelElevation) : RadiusZ;
    public float BarrelRise => BarrelLength * Mathf.Sin(ChevronBarrelElevation);
    public Vector3 ArtilleryBarrelMount(float height) => new(0f, height * 0.32f, -BarrelBack);
    public Vector3 ArtilleryBarrelTip(float height) => new(0f, height * 0.32f + BarrelRise, -BarrelFront);

    public Vector2 Vertex(int index)
    {
        if (IsDeepFork) return DeepForkVertices[index] * new Vector2(RadiusX, RadiusZ);
        if (IsDreadnaught) return DreadnaughtVertices[index] * new Vector2(RadiusX, RadiusZ);
        if (IsPike) return PikeVertices[index] * new Vector2(RadiusX, RadiusZ);
        if (IsDrillRig) return DrillRigVertices[index] * new Vector2(RadiusX, RadiusZ);
        if (IsHFrame) return HVertices[index] * new Vector2(RadiusX, RadiusZ);
        if (IsChevron) return ChevronVertices[index] * (RadiusX * 2f);
        float angle = Rotation + Mathf.Tau * index / Sides;
        return new Vector2(Mathf.Cos(angle) * RadiusX, Mathf.Sin(angle) * RadiusZ);
    }

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
        if (definition.Shape == UnitShape.Dreadnaught)
            return new UnitOutline(5, Mathf.Clamp(width, 2f, 20f) * size * 0.5f,
                (float.IsFinite(definition.HullLength) ? Mathf.Clamp(definition.HullLength, 3f, 30f) : 14f) * size * 0.5f, 0f, IsDreadnaught: true);
        UnitOutline outline = FromDimensions(definition.Shape, width * size, diamondLongRadius);
        if (outline.IsChevron)
        {
            float scale = float.IsFinite(definition.ArtilleryBarrelLengthMultiplier)
                ? Mathf.Clamp(definition.ArtilleryBarrelLengthMultiplier, 0.5f, 4f) : 1f;
            float elevation = float.IsFinite(definition.ArtilleryBarrelElevationDegrees)
                ? Mathf.Clamp(definition.ArtilleryBarrelElevationDegrees, 0f, 75f) : 0f;
            return outline with { ChevronBarrelScale = scale, ChevronBarrelElevation = Mathf.DegToRad(elevation) };
        }
        return outline;
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
            // Same short rear edge as Triangle, but twice the nose-to-tail length.
            UnitShape.LongTriangle => new(3, triangleRadius, triangleRadius * 2f, -Mathf.Pi * 0.5f),
            UnitShape.Diamond => new(4, Mathf.Max(halfWidth, diamondLongRadius), halfWidth, 0f),
            UnitShape.Pentagon => new(5, pentagonRadius, pentagonRadius, -Mathf.Pi * 0.5f),
            UnitShape.Chevron => new(6, halfWidth, width * 0.60f, 0f, IsChevron: true),
            // Siege Ram: 1.5x authored size plus a 17/15 length proportion factor.
            // Together these give 1.7x the previous longitudinal dimensions;
            // the same outline drives placement, rendering and circular clearance.
            UnitShape.SiegeRam => new(4, halfWidth * 1.41421356f, width * 1.12f * (17f / 15f), Mathf.Pi * 0.25f),
            UnitShape.Obliterator => new(5, halfWidth, width * 0.78f, -Mathf.Pi * 0.5f),
            UnitShape.Prism => new(6, halfWidth, width * 0.88f, -Mathf.Pi * 0.5f),
            UnitShape.DeepFork => new(DeepForkVertices.Length, halfWidth, width * 0.38f, 0f, IsDeepFork: true),
            UnitShape.Pike => new(PikeVertices.Length, halfWidth, width * 0.74f, 0f, IsPike: true),
            UnitShape.DrillRig => new(DrillRigVertices.Length, halfWidth, width * 0.70f, 0f, IsDrillRig: true),
            UnitShape.HFrame => new(12, halfWidth, width * 0.57f, 0f, IsHFrame: true),
            UnitShape.Hexagon => new(6, halfWidth, halfWidth, -Mathf.Pi * 0.5f),
            _ => throw new ArgumentOutOfRangeException(nameof(shape))
        };
    }
}

public readonly record struct UnitStats(string Id, string Name, UnitShape Shape, UnitOutline Outline, float Height,
    float Footprint, float Health, float Speed, float Damage, float Interval, float Reach,
    float DamageTaken, bool Ranged, int ProjectileType, float MinimumFireRange,
    bool CleaveEnabled, float CleaveExtraReach, float CleaveCosineSquared, float CleaveSecondaryFraction, int CleaveMaxAdditionalTargets, float MeleeFacingCosineSquared, TrampleStats Trample, float AccelerationRateOverride, ShockwaveStats Shockwave, RangedRecoilStats RangedRecoil,
    int DamageType, int ArmourClass, TargetCategory AllowedTargets, TargetPreference PreferredTarget,
    float StructureDetectionRange, float TurnDegreesPerSecond, float MeleeLungeDistance, float UnitDamagePercent, float StructureDamagePercent,
    int MeleeBonusTarget, float MeleeBonusMultiplier, float SizeTier, PrismBeamStats PrismBeam, ShieldEmitterStats ShieldEmitter, float FirstHitMultiplier)
{
    internal bool DirectFire(ProjectileCatalog projectiles) =>
        !Ranged || PrismBeam.Enabled || projectiles.Flight(ProjectileType).ObstacleFire == ObstacleFireMode.RequiresDirectLineOfSight;

    public float AgainstUnits(float amount) => UnitDamagePercent < 0f ? amount : amount * (UnitDamagePercent * 0.01f);
    public float AgainstStructures(float amount) => StructureDamagePercent < 0f ? amount : amount * (StructureDamagePercent * 0.01f);
    public int UnitDamageType => UnitDamagePercent < 0f ? DamageType : DamageRules.NeutralType;
    public int StructureDamageType => StructureDamagePercent < 0f ? DamageType : DamageRules.NeutralType;
    public bool TargetsUnits => (AllowedTargets & TargetCategory.Units) != 0;
    public bool TargetsStructures => (AllowedTargets & TargetCategory.Structures) != 0;
    public float MeleeAttackRadius => Footprint + CleaveExtraReach;
    public float MeleeDetectionReach => CleaveExtraReach > 0f
        ? Mathf.Max(Reach, MeleeAttackRadius + CombatSystem.MeleeContactTolerance) : Reach;
    public float AttackReach(in UnitStats defender) => Ranged ? Reach : Reach + defender.Footprint;
}

[Tool]
[GlobalClass]
public partial class UnitCatalog : Resource
{
    [Export] public Godot.Collections.Array<UnitDefinition> Definitions { get; set; } = new();

    [Export] public Godot.Collections.Array<UnitDefinition> Aircraft { get; set; } = new();

    public RuntimeUnitCatalog Compile(float movementScale) => new(Definitions, movementScale);
}

public sealed class RuntimeUnitCatalog
{
    public const string CircleId = "circle_infantry", SquareId = "square_infantry", TriangleId = "triangle_archer",
        DiamondId = "diamond_cavalry", PentagonId = "pentagon_elite", HexagonId = "hexagon_siege", ChevronId = "chevron_artillery", BreacherId = "h_breacher",
        LongTriangleId = "long_triangle_marksman", WorkerId = "worker", PikemanId = "pikeman", DreadnaughtId = "dreadnaught", TwinRocketLauncherId = "twin_rocket_launcher", PrismLanceId = "prism_lance", MobileGeneratorId = "mobile_generator", ObliteratorId = "obliterator", SiegeRamId = "siege_ram", InterceptorId = "interceptor_infantry";
    // Explicit dense mapping. Save files store IDs and never depend on these indices.
    public const byte Circle = 0, Square = 1, Triangle = 2, Diamond = 3, Pentagon = 4, Hexagon = 5, Chevron = 6, Breacher = 7, LongTriangle = 8, Worker = 9, Pikeman = 10, Dreadnaught = 11, TwinRocketLauncher = 12, PrismLance = 13, MobileGenerator = 14, Obliterator = 15, SiegeRam = 16, Interceptor = 17;
    private static readonly string[] OrderedIds = { CircleId, SquareId, TriangleId, DiamondId, PentagonId, HexagonId, ChevronId, BreacherId, LongTriangleId, WorkerId, PikemanId, DreadnaughtId, TwinRocketLauncherId, PrismLanceId, MobileGeneratorId, ObliteratorId, SiegeRamId, InterceptorId };
    private readonly UnitStats[] stats = new UnitStats[OrderedIds.Length];
    private readonly int[] battleRatings = new int[OrderedIds.Length];
    private readonly Dictionary<string, byte> indices = new(StringComparer.Ordinal);
    public DamageRules DamageRules { get; }
    public ProjectileCatalog Projectiles { get; }
    private readonly MountedRocket[][] primaryRockets = new MountedRocket[OrderedIds.Length][];
    private readonly MountedRocket[][] independentRockets = new MountedRocket[OrderedIds.Length][];
    private readonly UnitDefinition[] definitionsByType = new UnitDefinition[OrderedIds.Length];
    internal MountedRocket[] PrimaryRockets(int type) => primaryRockets[type];
    internal MountedRocket[] IndependentRockets(int type) => independentRockets[type];
    internal UnitDefinition Definition(int type) => definitionsByType[type];
    internal (int Projectile, Vector4 Min, Vector4 Max, float Interval, float Damage) DreadnaughtGuns { get; }

    public int Count => stats.Length;
    public float MaxSpeed { get; }
    public float MaxFootprint { get; }
    public bool HasRangedRecoil { get; }
    public ref readonly UnitStats this[int index] => ref stats[index];
    internal int BattleRating(byte type) => battleRatings[type];
    public bool TryResolve(string id, out byte index)
    {
        index = 0;
        return id != null && indices.TryGetValue(id, out index);
    }

    public RuntimeUnitCatalog(IEnumerable<UnitDefinition> definitions, float movementScale)
    {
        DamageRules = new DamageRules(GD.Load<DamageMatrix>(DamageMatrix.DefaultPath));
        Projectiles = new ProjectileCatalog(movementScale);
        var byId = new Dictionary<string, UnitDefinition>(StringComparer.Ordinal);
        if (definitions == null) throw new ArgumentNullException(nameof(definitions));
        foreach (UnitDefinition definition in definitions)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id) || !byId.TryAdd(definition.Id, definition))
                throw new ArgumentException("Unit definitions require unique, nonempty IDs.");
        }
        // Older custom catalogues can keep their five authored definitions.
        if (byId.Count == Hexagon && !byId.ContainsKey(HexagonId))
        {
            var hexagon = GD.Load<UnitDefinition>("res://GAMEPLAY/UNITS/DEFINITIONS/HexagonSiege.tres");
            if (hexagon != null) byId.Add(HexagonId, hexagon);
        }
        // Custom catalogues authored before ART1 retain their existing overrides.
        if (byId.Count == Chevron && !byId.ContainsKey(ChevronId))
        {
            var artillery = GD.Load<UnitDefinition>("res://GAMEPLAY/UNITS/DEFINITIONS/ChevronArtillery.tres");
            if (artillery != null) byId.Add(ChevronId, artillery);
        }
        if (byId.Count == Breacher && !byId.ContainsKey(BreacherId))
        {
            var breacher = GD.Load<UnitDefinition>("res://GAMEPLAY/UNITS/DEFINITIONS/HBreacher.tres");
            if (breacher != null) byId.Add(BreacherId, breacher);
        }
        // Pre-marksman saved/custom catalogues can continue loading unchanged.
        if (byId.Count == LongTriangle && !byId.ContainsKey(LongTriangleId))
        {
            var marksman = GD.Load<UnitDefinition>("res://GAMEPLAY/UNITS/DEFINITIONS/LongTriangleMarksman.tres");
            if (marksman != null) byId.Add(LongTriangleId, marksman);
        }
        if (byId.Count == Worker && !byId.ContainsKey(WorkerId))
        {
            var worker = GD.Load<UnitDefinition>("res://GAMEPLAY/UNITS/DEFINITIONS/Worker.tres");
            if (worker != null) byId.Add(WorkerId, worker);
        }
        if (byId.Count == Pikeman && !byId.ContainsKey(PikemanId))
        {
            var pikeman = GD.Load<UnitDefinition>("res://GAMEPLAY/UNITS/DEFINITIONS/Pikeman.tres");
            if (pikeman != null) byId.Add(PikemanId, pikeman);
        }
        if (!byId.ContainsKey(DreadnaughtId))
        {
            var definition = GD.Load<UnitDefinition>("res://GAMEPLAY/UNITS/DEFINITIONS/Dreadnaught.tres");
            if (definition != null) byId.Add(DreadnaughtId, definition);
        }
        if (!byId.ContainsKey(TwinRocketLauncherId))
        {
            var definition = GD.Load<UnitDefinition>("res://GAMEPLAY/UNITS/DEFINITIONS/TwinRocketLauncher.tres");
            if (definition != null) byId.Add(TwinRocketLauncherId, definition);
        }
        if (!byId.ContainsKey(PrismLanceId))
        {
            var definition = GD.Load<UnitDefinition>("res://GAMEPLAY/UNITS/DEFINITIONS/PrismLance.tres");
            if (definition != null) byId.Add(PrismLanceId, definition);
        }
        if (!byId.ContainsKey(MobileGeneratorId))
        {
            var definition = GD.Load<UnitDefinition>("res://GAMEPLAY/UNITS/DEFINITIONS/MobileGenerator.tres");
            if (definition != null) byId.Add(MobileGeneratorId, definition);
        }
        if (!byId.ContainsKey(ObliteratorId))
        {
            var definition = GD.Load<UnitDefinition>("res://GAMEPLAY/UNITS/DEFINITIONS/Obliterator.tres");
            if (definition != null) byId.Add(ObliteratorId, definition);
        }
        if (!byId.ContainsKey(SiegeRamId))
        {
            var ram = GD.Load<UnitDefinition>("res://GAMEPLAY/UNITS/DEFINITIONS/SiegeRam.tres");
            if (ram != null) byId.Add(SiegeRamId, ram);
        }
        if (!byId.ContainsKey(InterceptorId))
        {
            var interceptor = GD.Load<UnitDefinition>("res://GAMEPLAY/UNITS/DEFINITIONS/Interceptor.tres");
            if (interceptor != null) byId.Add(InterceptorId, interceptor);
        }
        if (byId.Count != Count) throw new ArgumentException($"The catalogue requires {Count} unit definitions.");
        bool hasRangedRecoil = false;
        for (byte i = 0; i < Count; i++)
        {
            string id = OrderedIds[i];
            if (!byId.TryGetValue(id, out UnitDefinition d)) throw new ArgumentException($"Missing unit definition {id}.");
            if (!Enum.IsDefined(d.Shape) || !Enum.IsDefined(d.Style)) throw new ArgumentException($"Invalid shape or attack style for {id}.");
            float size = UnitOutline.EffectiveSize(d);
            UnitOutline outline = UnitOutline.FromDefinition(d, size);
            float footprint = outline.Footprint;
            if (outline.IsDreadnaught != (i == Dreadnaught))
                throw new ArgumentException("The Dreadnaught shape belongs to the dreadnaught unit definition.");
            if (i == Dreadnaught)
                DreadnaughtGuns = (Projectiles.Register(d.Projectile), d.TurretMinimumRanges, d.TurretMaximumRanges,
                    ClampStat(d.TurretShotInterval, 0.05f, 5f, 0.15f), ClampStat(d.TurretDamage, 0f, 200f, 3f));
            definitionsByType[i] = d;
            var primary = new List<MountedRocket>();
            var independent = new List<MountedRocket>();
            foreach (var mount in d.RocketMounts ?? new())
            {
                if (mount == null || (mount.IndependentAim && mount.Projectile == null && !mount.TwinMinigunModel))
                    continue;
                var destination = mount.IndependentAim ? independent : primary;
                destination.Add(CompileMount(mount, d, size));
            }
            // Older custom Dreadnaught definitions still carry the original cannon fields.
            if (i == Dreadnaught && independent.Count == 0 && d.TurretRocketProjectile != null)
            {
                independent.Add(CompileMount(new RocketMount
                {
                    IndependentAim = true, Position = Vector3.Up * (Positive(d.Height, 2.2f) * 0.9f),
                    ModelScale = new Vector3(0.52f, 0.48f, 2.05f),
                    Projectile = d.TurretRocketProjectile, MinimumRange = d.TurretRocketMinimumRange,
                    MaximumRange = d.TurretRocketMaximumRange, Interval = d.TurretRocketInterval,
                    Damage = d.TurretRocketDamage
                }, d, size));
            }
            primaryRockets[i] = primary.ToArray();
            independentRockets[i] = independent.ToArray();
            bool combat = d.AllowedTargets != TargetCategory.None;
            bool ranged = combat && d.Style == AttackStyle.Ranged;
            bool beam = ranged && d.PrismBeamEnabled;
            var prism = new PrismBeamStats(beam,
                ClampStat(d.PrismChargeSeconds, 0.05f, 20f, 3f),
                ClampStat(d.PrismBeamRadius, 0.01f, 2f, 0.25f),
                Mathf.Clamp(d.PrismMaxHits, 1, PrismBeamStats.MaxHitCapacity),
                ClampStat(d.PrismFlashSeconds, 0.02f, 1f, 0.18f));
            float reach = !beam && ranged && primary.Count > 0 ? primary[0].MaximumRange : Positive(d.Reach, 1.6f);
            if (!ranged) reach *= size;
            float angle = float.IsFinite(d.CleaveAngleDegrees) ? Mathf.Clamp(d.CleaveAngleDegrees, 1f, 180f) : 100f;
            float cosine = Mathf.Max(0f, Mathf.Cos(Mathf.DegToRad(angle * 0.5f)));
            float facingCosine = Mathf.Max(0f, Mathf.Cos(Mathf.DegToRad(ClampStat(d.MeleeFacingAngleDegrees, 1f, 180f, 100f) * 0.5f)));
            float secondary = float.IsFinite(d.CleaveSecondaryFraction) ? Mathf.Clamp(d.CleaveSecondaryFraction, 0f, 1f) : 0.65f;
            var trample = new TrampleStats(d.TrampleEnabled && combat && d.Style == AttackStyle.Melee,
                ClampStat(d.TrampleRunUp, 0.5f, 10f, 2f),
                ClampStat(d.TrampleDistance, 0.5f, 12f, 5.5f),
                ClampStat(d.TrampleEfficiency, 0.1f, 4f, 1f),
                ClampStat(d.TrampleDamageMultiplier, 0f, 4f, 1.35f),
                ClampStat(d.ChargeResistance, 0.01f, 4f, 0.30f),
                Mathf.Clamp(d.TrampleMaxHits, 1, 6));
            float accelerationSeconds = ClampStat(d.AccelerationSecondsOverride, 0f, 5f, 0f);
            int shockwaveCadence = !combat || ranged ? 0 : Mathf.Clamp(d.ShockwaveEveryAttacks, 0, byte.MaxValue);
            float shockwaveDamage = Positive(d.ShockwaveDamage, 50f);
            var recoil = new RangedRecoilStats(ranged && d.RangedRecoilEnabled
                    ? ClampStat(d.RangedRecoilDistance, 0f, 4f, 0.12f) : 0f,
                ClampStat(d.RangedRecoilReturnSeconds, 0.05f, 2f, 0.18f));
            hasRangedRecoil |= recoil.Distance > 0f;
            var shockwave = new ShockwaveStats(shockwaveCadence,
                ClampStat(Positive(d.ShockwaveBaseRadius, 4f), 0.1f, 64f, 4f) * size,
                shockwaveDamage, d.EffectiveShockwaveOuterDamage(shockwaveDamage) / shockwaveDamage, d.ShockwaveRespectsCover);
            stats[i] = new UnitStats(id, string.IsNullOrWhiteSpace(d.DisplayName) ? id : d.DisplayName, d.Shape,
                outline, Positive(d.Height, 1.1f) * size, footprint, Positive(d.Health, 100f), Positive(d.Speed, 5f) * movementScale,
                combat ? Positive(d.Damage, 20f) : 0f,
                !beam && ranged && primary.Count > 0 ? primary[0].Interval : Positive(d.Interval, 0.9f), reach,
                1f - Mathf.Clamp(float.IsFinite(d.Armour) ? d.Armour : 0f, 0f, 1f), ranged,
                ranged && !beam ? (primary.Count == 0 ? Projectiles.Register(d.Projectile) : primary[0].Projectile) : -1,
                ranged ? (!beam && primary.Count > 0 ? primary[0].MinimumRange : ClampStat(d.MinimumFireRange, 0f, reach, 0f)) : 0f,
                d.CleaveEnabled && d.Style == AttackStyle.Melee, d.EffectiveCleaveExtraReach, cosine * cosine, secondary, Mathf.Clamp(d.CleaveMaxAdditionalTargets, 0, UnitDefinition.MaxCleaveAdditionalTargets),
                facingCosine * facingCosine, trample,
                accelerationSeconds > 0f ? 1f / accelerationSeconds : 0f, shockwave, recoil,
                DamageRules.DamageType(d.DamageType), DamageRules.ArmourClass(d.ArmourClass), beam ? d.AllowedTargets & TargetCategory.Units : d.AllowedTargets, d.PreferredTarget,
                ClampStat(d.StructureDetectionRange, 0f, 100f, 12f), ClampStat(d.TurnDegreesPerSecond, 0f, 720f, 0f),
                ranged ? 0f : ClampStat(d.MeleeLungeDistance, 0f, 1f, 0f),
                ClampStat(d.UnitDamagePercent, -1f, 1000f, -1f), ClampStat(d.StructureDamagePercent, -1f, 1000f, -1f),
                Array.IndexOf(OrderedIds, d.MeleeBonusTargetId), ClampStat(d.MeleeBonusMultiplier, 1f, 10f, 1f), CompileSizeTier(d.SizeTier), prism,
                ShieldEmitterStats.Compile(d.ShieldEmitterEnabled, d.ShieldRadius, d.ShieldCapacity,
                    d.ShieldRegenerationPerSecond, d.ShieldDecayPerSecond),
                combat && !ranged && !d.CleaveEnabled ? ClampStat(d.FirstHitMultiplier, 1f, 10f, 1f) : 1f);
            battleRatings[i] = Mathf.Clamp(d.BattleRating, 0, UnitDefinition.MaxBattleRating);
            indices.Add(id, i);
            MaxSpeed = Mathf.Max(MaxSpeed, stats[i].Speed);
            MaxFootprint = Mathf.Max(MaxFootprint, footprint);
        }
        HasRangedRecoil = hasRangedRecoil;
        if (Array.TrueForAll(stats, type => type.Ranged)) throw new ArgumentException("The catalogue needs a melee unit.");
    }

    private MountedRocket CompileMount(RocketMount mount, UnitDefinition unit, float size)
    {
        int projectile = Projectiles.Register(mount.Projectile ?? unit.Projectile, mount.ProjectileScale,
            mount.SplashRadiusOverride, mount.SplashEdgeFractionOverride, arcScale: mount.ArcScale);
        float defaultReach = mount.TwinMinigunModel ? 30f : mount.IndependentAim ? 38f : Positive(unit.Reach, 1.6f);
        float maximum = Positive(mount.MaximumRange, defaultReach);
        float minimum = mount.MinimumRange >= 0f ? mount.MinimumRange :
            mount.TwinMinigunModel ? 3f : mount.IndependentAim ? 7f : unit.MinimumFireRange;
        float interval = Positive(mount.Interval, mount.TwinMinigunModel ? Positive(unit.TurretShotInterval, 0.15f) :
            mount.IndependentAim ? 3f : Positive(unit.Interval, 0.9f));
        float damage = mount.Damage >= 0f ? mount.Damage :
            mount.TwinMinigunModel ? ClampStat(unit.TurretDamage, 0f, 200f, 3f) : Positive(unit.Damage, 20f);
        return new MountedRocket(MountedRocket.LocalPose(mount, size), projectile,
            damage, ClampStat(minimum, 0f, maximum, 0f), maximum, interval, mount.TwinMinigunModel);
    }

    private static float CompileSizeTier(float tier) =>
        float.IsFinite(tier) && tier >= 1f ? (float)(Math.Round(tier * 2d, MidpointRounding.AwayFromZero) / 2d) : 1f;

    private static float ClampStat(float value, float min, float max, float fallback) =>
        float.IsFinite(value) ? Mathf.Clamp(value, min, max) : fallback;

    private static float Positive(float value, float fallback) => float.IsFinite(value) && value > 0 ? value : fallback;
}
