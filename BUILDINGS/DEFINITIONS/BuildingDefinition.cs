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
    [Export] public float Damage { get; set; } = 12f;
    [Export] public float ShotsPerSecond { get; set; } = 2f;
    [Export] public float TurretDegreesPerSecond { get; set; } = 360f;
    [Export] public float BeamSeconds { get; set; } = 0.08f;
    [Export] public bool AttacksSoldiers { get; set; } = true;
    [Export] public bool RequiresLineOfSight { get; set; } = true;
}
