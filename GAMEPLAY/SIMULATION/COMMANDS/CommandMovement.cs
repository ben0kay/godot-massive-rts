// Computes formation slots and advances the commanded squad independently of auto-battle AI.
using Godot;

namespace MassiveRts;

internal sealed class CommandMovement
{
    public const int SquadSize = 20;
    public const int DefaultColumns = 5;
    public float Spacing { get; }
    public float Radius { get; }
    private readonly UnitState units;
    private readonly Vector3[] goals = new Vector3[SquadSize];
    private Vector3 facing;
    private bool moving;

    public CommandMovement(UnitState units)
    {
        this.units = units;
        Radius = units.Catalog[RuntimeUnitCatalog.Circle].Footprint;
        Spacing = Radius * 2f + 0.3f;
    }

    public Vector3 Slot(Vector3 front, Vector3 right, int columns, int index)
    {
        int row = index / columns;
        int rankSize = Mathf.Min(columns, SquadSize - row * columns);
        return front + right * ((index % columns - (rankSize - 1) * 0.5f) * Spacing)
            + right.Cross(Vector3.Up) * (row * Spacing);
    }

    public void Issue(Vector3 front, Vector3 right, int columns)
    {
        facing = -right.Cross(Vector3.Up);
        for (int i = 0; i < SquadSize; i++) goals[i] = Slot(front, right, columns, i);
        moving = true;
    }

    public bool Step(float delta)
    {
        if (!moving) return false;
        moving = false;
        float speed = units.Catalog[RuntimeUnitCatalog.Circle].Speed;
        for (int i = 0; i < SquadSize; i++)
        {
            Vector3 previous = units.Position[i];
            Vector3 next = previous.MoveToward(goals[i], speed * delta);
            if (next == previous) continue;
            units.Position[i] = next;
            Vector3 direction = next == goals[i] ? facing : (next - previous).Normalized();
            units.Facing[i] = new Vector2(direction.X, direction.Z);
            units.Dirty[i] = true;
            moving |= next != goals[i];
        }
        return true;
    }
}
