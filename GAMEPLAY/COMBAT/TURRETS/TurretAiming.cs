// Shares mount-space transforms and fixed-step yaw between independent weapons.
using Godot;

namespace MassiveRts;

internal static class TurretAiming
{
    internal static float Yaw(Vector2 direction) => Mathf.Atan2(-direction.X, -direction.Y);
    internal static float Wrap(float angle) => Mathf.Wrap(angle, -Mathf.Pi, Mathf.Pi);

    internal static float Turn(ref float yaw, float desired, float step)
    {
        float error = Wrap(desired - yaw);
        yaw = Wrap(yaw + Mathf.Clamp(error, -step, step));
        return error;
    }

    internal static Basis Rotation(float yaw) => new(Vector3.Up, yaw);

    internal static Vector3 Mount(UnitState units, int id, Vector3 offset)
    {
        ref readonly var stats = ref units.Catalog[units.Type[id]];
        return units.Position[id] + Vector3.Up * (stats.Height * .5f) +
            MountedRocket.HullBasis(units.Facing[id], stats.Shape) * offset;
    }
}

internal readonly record struct PrimaryTurret(MountedRocket Weapon, float RestYaw, float TurnSpeed,
    float HalfArc, float Tolerance, bool FireWhileMoving, TargetCategory Targets,
    Vector3 Muzzle, Vector3 CasingPort, bool EjectCasings, float CasingScale,
    float RecoilDistance, float RecoilSeconds);
