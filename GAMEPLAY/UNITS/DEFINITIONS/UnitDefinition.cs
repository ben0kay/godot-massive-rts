// Inspector-editable soldier definition. Stable IDs are used in deployment saves.
using Godot;
namespace MassiveRts;

public enum UnitShape { Circle, Square, Triangle, Diamond, Pentagon, Hexagon, Chevron, HFrame, LongTriangle, DrillRig, Pike, Dreadnaught, DeepFork, Prism, Obliterator, SiegeRam }
[System.Flags]
public enum TargetCategory { None = 0, Units = 1, Structures = 2, Both = Units | Structures }
public enum TargetPreference { Units, Structures }
public enum AttackStyle { Melee, Ranged }

[Tool]
[GlobalClass]
public partial class UnitDefinition : Resource
{
    [ExportGroup("Identity")]
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public string Role { get; set; } = "";

    public const int MaxBattleRating = 1000000;
    [Export(PropertyHint.Range, "0,1000000,1")] public int BattleRating { get; set; } = 100;

    [ExportGroup("Appearance and size")]
    [Export] public bool PulseOnAttack { get; set; } = true;
    [Export] public UnitShape Shape { get; set; }
    [Export] public float Width { get; set; } = 0.8f;
    [Export] public float Height { get; set; } = 1.1f;
    [Export(PropertyHint.Range, "0.5,3.0,0.05")] public float SizeMultiplier { get; set; } = 1f;
    [Export(PropertyHint.Range, "1,5,0.5,or_greater")] public float SizeTier { get; set; } = 1f;
    // Serialized legacy input for Diamond mesh length. Collision uses the compiled outline.
    [Export] public float FootprintRadius { get; set; } = 0.57f;

    [ExportGroup("Prism Beam")]
    [Export] public bool PrismBeamEnabled { get; set; }
    [Export] public float PrismChargeSeconds { get; set; } = 3f;
    [Export] public float PrismBeamRadius { get; set; } = 0.25f;
    [Export] public int PrismMaxHits { get; set; } = 24;
    [Export] public float PrismFlashSeconds { get; set; } = 0.18f;

    [ExportGroup("Chevron artillery")]
    // Multiplies barrel length from its fixed rear anchor; affects mesh, muzzle and clearance.
    // Unchanged legacy art keeps the default value of one.
    [Export(PropertyHint.Range, "0.5,4,0.1")] public float ArtilleryBarrelLengthMultiplier { get; set; } = 1f;
    [Export(PropertyHint.Range, "0,75,1,degrees")] public float ArtilleryBarrelElevationDegrees { get; set; }

    [ExportGroup("Dreadnaught")]
    [Export] public float HullLength { get; set; } = 14f;
    [Export] public bool UseLegacyMiniguns { get; set; } = true;
    [Export] public Vector4 TurretMinimumRanges { get; set; } = new(3, 3, 3, 3);
    [Export] public Vector4 TurretMaximumRanges { get; set; } = new(30, 30, 30, 30);
    [Export] public float TurretShotInterval { get; set; } = 0.15f;
    [Export] public float TurretDamage { get; set; } = 3f;

    [Export] public ProjectileDefinition TurretRocketProjectile { get; set; }
    [Export] public float TurretRocketMinimumRange { get; set; } = 7f;
    [Export] public float TurretRocketMaximumRange { get; set; } = 38f;
    [Export] public float TurretRocketInterval { get; set; } = 3f;
    [Export] public float TurretRocketDamage { get; set; } = 24f;

    [ExportGroup("Weapon mounts")]
    [Export] public Godot.Collections.Array<RocketMount> RocketMounts { get; set; } = new();

    [ExportGroup("Aircraft")]
    [Export] public Texture2D AircraftIcon { get; set; }
    [Export] public Mesh AircraftMesh { get; set; }
    [Export] public bool CanTargetAircraft { get; set; }
    [Export] public bool CanBombGround { get; set; }
    [Export] public ProjectileDefinition BombProjectile { get; set; }

    [ExportGroup("Movement")]
    [Export] public float Speed { get; set; } = 5f;
    [Export(PropertyHint.Range, "0,5,0.05,suffix:s")] public float AccelerationSecondsOverride { get; set; }

    [Export(PropertyHint.Range, "0,720,1,degrees")] public float TurnDegreesPerSecond { get; set; }

    [ExportGroup("Defense")]
    [Export] public string ArmourClass { get; set; } = "Light";
    [Export] public float Health { get; set; } = 100f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float Armour { get; set; }
    [Export(PropertyHint.Range, "0.01,4,0.05")] public float ChargeResistance { get; set; } = 0.30f;

    [ExportGroup("Primary attack")]
    // -1 uses the damage-type matchup, 0–1000 replaces it for this target category.
    [Export(PropertyHint.Range, "-1,1000,1")] public float UnitDamagePercent { get; set; } = -1f;
    [Export(PropertyHint.Range, "-1,1000,1")] public float StructureDamagePercent { get; set; } = -1f;
    [Export] public string DamageType { get; set; } = "Standard";
    [Export] public TargetCategory AllowedTargets { get; set; } = TargetCategory.Units;
    [Export] public TargetPreference PreferredTarget { get; set; }
    [Export(PropertyHint.Range, "0,100,0.1,suffix:u")] public float StructureDetectionRange { get; set; } = 12f;
    [Export(PropertyHint.Range, "0,1,0.01,suffix:u")] public float MeleeLungeDistance { get; set; }
    [Export] public AttackStyle Style { get; set; }
    [Export] public float Damage { get; set; } = 20f;
    [Export(PropertyHint.Range, "0.001,10,0.001,or_greater,suffix:s")] public float Interval { get; set; } = 0.9f;
    [Export] public float Reach { get; set; } = 1.6f;
    [Export(PropertyHint.Range, "1,180,1,degrees")] public float MeleeFacingAngleDegrees { get; set; } = 100f;

    [ExportSubgroup("Melee target bonus")]
    [Export] public string MeleeBonusTargetId { get; set; } = "";
    [Export(PropertyHint.Range, "1,10,0.05")] public float MeleeBonusMultiplier { get; set; } = 1f;

    [ExportSubgroup("First hit")]
    [Export(PropertyHint.Range, "1,10,0.05")] public float FirstHitMultiplier { get; set; } = 1f;

    [ExportSubgroup("Cleave")]
    [Export] public bool CleaveEnabled { get; set; }
    public const float MaxCleaveExtraReach = 16f;
    [Export(PropertyHint.Range, "0,16,0.05,suffix:u")] public float CleaveExtraReach { get; set; }

    public float EffectiveCleaveExtraReach => CleaveEnabled && Style == AttackStyle.Melee && float.IsFinite(CleaveExtraReach)
        ? Mathf.Clamp(CleaveExtraReach, 0f, MaxCleaveExtraReach) : 0f;
    [Export(PropertyHint.Range, "1,180,1")] public float CleaveAngleDegrees { get; set; } = 100f;
    [Export(PropertyHint.Range, "0,1,0.05")] public float CleaveSecondaryFraction { get; set; } = 0.65f;
    public const int MaxCleaveAdditionalTargets = 50;
    [Export(PropertyHint.Range, "0,50,1")] public int CleaveMaxAdditionalTargets { get; set; } = 2;

    [ExportGroup("Shockwave")]
    [Export(PropertyHint.Range, "0,255,1")] public int ShockwaveEveryAttacks { get; set; }
    [Export(PropertyHint.Range, "0.1,64,0.1")] public float ShockwaveBaseRadius { get; set; } = 4f;
    [Export] public float ShockwaveDamage { get; set; } = 50f;
    // -1 uses the fraction stored by older definitions.
    [Export(PropertyHint.Range, "-1,100,0.1,or_greater")] public float ShockwaveOuterDamage { get; set; } = -1f;
    [Export] public float ShockwaveEdgeDamageFraction { get; set; } = 0.5f;
    [Export] public bool ShockwaveRespectsCover { get; set; } = true;
    [ExportGroup("Trample")]
    [Export] public bool TrampleEnabled { get; set; }
    [Export(PropertyHint.Range, "0.5,10,0.1")] public float TrampleRunUp { get; set; } = 2f;
    [Export(PropertyHint.Range, "0.5,12,0.1")] public float TrampleDistance { get; set; } = 5.5f;
    [Export(PropertyHint.Range, "0.1,4,0.05")] public float TrampleEfficiency { get; set; } = 1f;
    [Export(PropertyHint.Range, "0,4,0.05")] public float TrampleDamageMultiplier { get; set; } = 1.35f;
    [Export(PropertyHint.Range, "1,6,1")] public int TrampleMaxHits { get; set; } = 3;

    [ExportGroup("Ranged")]
    [Export] public ProjectileDefinition Projectile { get; set; }
    [Export(PropertyHint.Range, "0,15,0.1")] public float AimSpreadDegrees { get; set; }
    [Export(PropertyHint.Range, "0,1,0.01")] public float ForestDeflectionChance { get; set; }
    [Export(PropertyHint.Range, "0,100,0.1,or_greater,suffix:u")] public float MinimumFireRange { get; set; }

    [ExportGroup("Ranged recoil")]
    [Export] public bool RangedRecoilEnabled { get; set; }
    // Visual displacement only: no changes to physics, AI or projectile origins.
    [Export(PropertyHint.Range, "0,4,0.01")] public float RangedRecoilDistance { get; set; } = 0.12f;
    [Export(PropertyHint.Range, "0.05,2,0.01")] public float RangedRecoilReturnSeconds { get; set; } = 0.18f;

    public float EffectiveShockwaveOuterDamage(float centreDamage) =>
        float.IsFinite(ShockwaveOuterDamage) && ShockwaveOuterDamage >= 0f
            ? Mathf.Min(ShockwaveOuterDamage, centreDamage)
            : centreDamage * (float.IsFinite(ShockwaveEdgeDamageFraction)
                ? Mathf.Clamp(ShockwaveEdgeDamageFraction, 0f, 1f) : 0.5f);

    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        if (property["name"].AsString() != nameof(ShockwaveEdgeDamageFraction)) return;
        property["usage"] = (long)(PropertyUsageFlags.Storage | PropertyUsageFlags.ScriptVariable);
    }
    [ExportGroup("Shield field")]
    [Export] public bool ShieldEmitterEnabled { get; set; }
    [Export] public float ShieldRadius { get; set; } = 14f;
    [Export] public float ShieldCapacity { get; set; } = 20f;
    [Export] public float ShieldRegenerationPerSecond { get; set; } = 4f;
    [Export] public float ShieldDecayPerSecond { get; set; } = 2f;

}
