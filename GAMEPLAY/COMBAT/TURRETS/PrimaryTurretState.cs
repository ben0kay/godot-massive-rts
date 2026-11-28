// Stores yaw and interpolation state only for units that use a primary turret.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed class PrimaryTurretState
{
    private readonly Dictionary<int, (float Before, float Yaw)> poses = new();

    internal void BeginTick(int id)
    {
        if (poses.TryGetValue(id, out var pose) && pose.Before != pose.Yaw)
            poses[id] = (pose.Yaw, pose.Yaw);
    }

    internal void Remove(int id) => poses.Remove(id);

    internal bool IsTurning(int id) => poses.TryGetValue(id, out var pose) && pose.Before != pose.Yaw;

    internal float WorldYaw(UnitState units, int id, in PrimaryTurret mount, float blend = 1f)
    {
        if (poses.TryGetValue(id, out var pose))
            return blend >= 1f ? pose.Yaw : Mathf.LerpAngle(pose.Before, pose.Yaw, blend);
        return HullYaw(units, id) + mount.RestYaw;
    }

    internal static float HullYaw(UnitState units, int id)
    {
        Vector3 front = MountedRocket.HullBasis(units.Facing[id], units.Catalog[units.Type[id]].Shape) * Vector3.Forward;
        return TurretAiming.Yaw(new Vector2(front.X, front.Z));
    }

    internal bool Aim(UnitState units, int id, Vector3 target, in PrimaryTurret mount, float dt)
    {
        Vector2 heading = ObstacleField.Xz(target - TurretAiming.Mount(units, id, mount.Weapon.Pose.Origin));
        if (heading.LengthSquared() < .00001f) return false;
        float neutral = HullYaw(units, id) + mount.RestYaw;
        float desired = TurretAiming.Yaw(heading);
        var pose = poses.TryGetValue(id, out var existing) ? existing : (Before: neutral, Yaw: neutral);
        float yaw = pose.Yaw;
        if (mount.HalfArc < Mathf.Pi)
        {
            float local = Mathf.Clamp(TurretAiming.Wrap(yaw - neutral), -mount.HalfArc, mount.HalfArc);
            float targetLocal = Mathf.Clamp(TurretAiming.Wrap(desired - neutral), -mount.HalfArc, mount.HalfArc);
            yaw = TurretAiming.Wrap(neutral + Mathf.MoveToward(local, targetLocal, mount.TurnSpeed * dt));
        }
        else TurretAiming.Turn(ref yaw, desired, mount.TurnSpeed * dt);
        poses[id] = (pose.Before, yaw);
        if (yaw != pose.Yaw) units.Dirty[id] = true;
        return Mathf.Abs(TurretAiming.Wrap(desired - yaw)) <= mount.Tolerance &&
            (mount.FireWhileMoving || units.MoveSpeed[id] <= .001f);
    }
}
