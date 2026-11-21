// Inspector-authored static defense settings, independent of soldier definitions.
using Godot;

namespace MassiveRts;

[Tool]
[GlobalClass]
public partial class BuildingDefinition : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public int Width { get; set; } = 2;
    [Export] public int Depth { get; set; } = 2;
    [Export] public float MinimumRange { get; set; } = 4f;
    [Export] public float MaximumRange { get; set; } = 30f;
    [Export(PropertyHint.Range, "1,50000,1,or_greater")] public float Health { get; set; } = 1000f;
    [Export] public string ArmourClass { get; set; } = "Fortified";
    [Export(PropertyHint.Range, "0,1,0.01")] public float Armour { get; set; }
    [Export] public string DamageType { get; set; } = "Standard";
    // Beam weapons deal Damage per second, with recovery equal to 1 / ShotsPerSecond.
    [Export] public float Damage { get; set; } = 12f;
    [Export] public float ShotsPerSecond { get; set; } = 2f;
    [Export] public float TurretDegreesPerSecond { get; set; } = 360f;
    [Export] public float BeamSeconds { get; set; } = 3f;

    [ExportGroup("Projectile weapon")]
    // Null uses a sustained beam; rocket towers use the existing pooled projectile.
    [Export] public ProjectileDefinition Projectile { get; set; }
    [Export(PropertyHint.Range, "0.5,4,0.05")] public float ProjectileVisualScale { get; set; } = 1f;
    [Export(PropertyHint.Range, "0.5,8,0.1")] public float ExplosionRadiusMultiplier { get; set; } = 1f;
    [Export] public bool AttacksSoldiers { get; set; } = true;
    [Export] public bool RequiresLineOfSight { get; set; } = true;
    [ExportGroup("Shield field")]
    [Export] public int BuildCost { get; set; } = 1000;
    [Export] public bool ShieldEmitterEnabled { get; set; }
    [Export] public float ShieldRadius { get; set; } = 14f;
    [Export] public float ShieldCapacity { get; set; } = 20f;
    [Export] public float ShieldRegenerationPerSecond { get; set; } = 4f;
    [Export] public float ShieldDecayPerSecond { get; set; } = 2f;

}
