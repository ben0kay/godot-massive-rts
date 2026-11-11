// Visual-only heading from deliberate XZ motion.
using Godot;

namespace MassiveRts;

public static class SoldierFacing
{
    private const float TurnRadiansPerSecond = 300f * (Mathf.Pi / 180f);

    public static Vector2 TurnStep(float dt)
    {
        float angle = Mathf.Min(TurnRadiansPerSecond * dt, Mathf.Pi);
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
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
