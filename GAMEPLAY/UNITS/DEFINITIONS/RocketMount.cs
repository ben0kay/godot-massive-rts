// Authoring data for a hull-mounted rocket weapon.
using Godot;

namespace MassiveRts;

[GlobalClass]
public partial class RocketMount : Resource
{
    [Export] public Vector3 Position { get; set; }
    [Export] public float YawDegrees { get; set; }
    [Export] public Vector3 ModelScale { get; set; } = Vector3.One;
    [Export] public bool TwinMinigunModel { get; set; }
    [Export] public bool IndependentAim { get; set; }
    [Export] public ProjectileDefinition Projectile { get; set; }
    [Export] public float Damage { get; set; } = -1f;
    [Export] public float MinimumRange { get; set; } = -1f;
    [Export] public float MaximumRange { get; set; } = -1f;
    [Export] public float Interval { get; set; } = -1f;
    [Export] public float ProjectileScale { get; set; } = 1f;
    [Export(PropertyHint.Range, "0.5,3,0.05")] public float ArcScale { get; set; } = 1f;
    [Export] public float SplashRadiusOverride { get; set; } = -1f;
    [Export] public float SplashEdgeFractionOverride { get; set; } = -1f;
}

internal readonly record struct MountedRocket(Transform3D Pose, int Projectile, float Damage,
    float MinimumRange, float MaximumRange, float Interval, bool TwinMinigunModel)
{
    internal static Transform3D LocalPose(RocketMount mount, float size) =>
        new(new Basis(Vector3.Up, Mathf.DegToRad(mount.YawDegrees)) *
            new Basis(Vector3.Right * (mount.ModelScale.X * size),
                Vector3.Up * (mount.ModelScale.Y * size), Vector3.Back * (mount.ModelScale.Z * size)), mount.Position * size);

    internal static Basis HullBasis(Vector2 heading, UnitShape shape)
    {
        if (heading == Vector2.Zero || shape == UnitShape.Circle) return Basis.Identity;
        float yaw = Mathf.Atan2(-heading.X, -heading.Y);
        if (shape == UnitShape.Diamond) yaw += Mathf.Pi * 0.5f;
        return new Basis(Vector3.Up, yaw);
    }

    // The shared model runs from its pivot to the muzzle along local -Z.
    internal static readonly Vector3 Muzzle = new(0f, 0f, -1f);
}
