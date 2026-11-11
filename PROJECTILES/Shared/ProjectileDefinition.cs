// Editable projectile flight and visual settings shared by ranged units.
using Godot;

namespace MassiveRts;

[GlobalClass]
public partial class ProjectileDefinition : Resource
{
    [Export] public Mesh VisualMesh { get; set; }
    [Export(PropertyHint.Range, "1,200,0.1,or_greater")] public float Speed { get; set; } = 45f;
    [Export(PropertyHint.Range, "0,5,0.05,or_greater")] public float ArcHeight { get; set; } = 0.65f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float ArcHeightPerDistance { get; set; } = 0.08f;
}

public readonly record struct ProjectileFlight(float Speed, float ArcHeight, float ArcHeightPerDistance);
