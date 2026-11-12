// Inspector-editable soldier definition. Stable IDs are used in deployment saves.
using Godot;
namespace MassiveRts;

public enum UnitShape { Circle, Square, Triangle, Diamond, Pentagon, Hexagon }
public enum AttackStyle { Melee, Ranged }

[Tool]
[GlobalClass]
public partial class UnitDefinition : Resource
{
    [ExportGroup("Identity")]
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public string Role { get; set; } = "";

    [ExportGroup("Appearance and size")]
    [Export] public UnitShape Shape { get; set; }
    [Export] public float Width { get; set; } = 0.8f;
    [Export] public float Height { get; set; } = 1.1f;
    [Export(PropertyHint.Range, "0.5,3.0,0.05")] public float SizeMultiplier { get; set; } = 1f;
    // Serialized legacy input for Diamond mesh length. Collision uses the compiled outline.
    [Export] public float FootprintRadius { get; set; } = 0.57f;

    [ExportGroup("Movement")]
    [Export] public float Speed { get; set; } = 5f;
    [Export(PropertyHint.Range, "0,5,0.05,suffix:s")] public float AccelerationSecondsOverride { get; set; }

    [ExportGroup("Defense")]
    [Export] public float Health { get; set; } = 100f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float Armour { get; set; }
    [Export(PropertyHint.Range, "0.01,4,0.05")] public float ChargeResistance { get; set; } = 0.30f;

    [ExportGroup("Primary attack")]
    [Export] public AttackStyle Style { get; set; }
    [Export] public float Damage { get; set; } = 20f;
    [Export(PropertyHint.Range, "0.001,10,0.001,or_greater,suffix:s")] public float Interval { get; set; } = 0.9f;
    [Export] public float Reach { get; set; } = 1.6f;
    [Export(PropertyHint.Range, "1,180,1,degrees")] public float MeleeFacingAngleDegrees { get; set; } = 100f;

    [ExportGroup("Melee cleave")]
    [Export] public bool CleaveEnabled { get; set; }
    [Export(PropertyHint.Range, "1,180,1")] public float CleaveAngleDegrees { get; set; } = 100f;
    [Export(PropertyHint.Range, "0,1,0.05")] public float CleaveSecondaryFraction { get; set; } = 0.65f;
    public const int MaxCleaveAdditionalTargets = 8;
    [Export(PropertyHint.Range, "0,8,1")] public int CleaveMaxAdditionalTargets { get; set; } = 2;

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
}
