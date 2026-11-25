// Shared heading for deliberate XZ movement and facing melee swings.
using Godot;

namespace MassiveRts;

public static class SoldierFacing
{
    private const float TurnRadiansPerSecond = 300f * (Mathf.Pi / 180f);

    public static Vector2 TurnStep(float dt, float degreesPerSecond = 0f)
    {
        float speed = degreesPerSecond > 0f ? Mathf.DegToRad(degreesPerSecond) : TurnRadiansPerSecond;
        float angle = Mathf.Min(speed * dt, Mathf.Pi);
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    internal static bool Aim(UnitState units, int id, Vector2 offset, float dt, float alignment)
    {
        byte type = units.Type[id];
        if ((units.Catalog[type].Shape == UnitShape.Circle && !units.CircleUsesFacing) || offset.LengthSquared() <= 0.000001f) return true;
        Vector2 direction = offset.Normalized();
        if (units.Facing[id].Dot(direction) >= alignment) return true;
        Turn(units, id, direction, units.TurnSteps(dt)[type]);
        units.Dirty[id] = true;
        return false;
    }

    public static void Turn(UnitState units, int id, Vector2 desired, Vector2 step)
    {
        Vector2 facing = units.Facing[id];
        if (facing == Vector2.Zero)
            facing = units.Catalog[units.Type[id]].Shape == UnitShape.Diamond ? Vector2.Right : Vector2.Up;
        if (facing.Dot(desired) >= step.X)
        {
            units.Facing[id] = desired;
            return;
        }

        float sine = facing.Cross(desired) < 0f ? -step.Y : step.Y;
        units.Facing[id] = new Vector2(facing.X * step.X - facing.Y * sine,
            facing.X * sine + facing.Y * step.X).Normalized();
    }
}
