// Limits voluntary steps against nearby soldier footprints before positions are written.
using Godot;

namespace MassiveRts;

public readonly struct CrowdMovement
{
    private readonly SpatialGrid grid;
    private readonly float maxStep;

    public CrowdMovement(SpatialGrid grid, float maxStep)
    {
        this.grid = grid;
        this.maxStep = maxStep;
    }

    public bool Enabled => grid != null;

    public Vector2 Clip(UnitState units, int id, Vector2 from, Vector2 to, float footprint)
    {
        Vector2 step = to - from;
        float lengthSquared = step.LengthSquared();
        if (lengthSquared <= 0f) return to;

        float length = Mathf.Sqrt(lengthSquared);
        // Grid cells are from the start of the tick; positions include accepted moves.
        Vector2 midpoint = (from + to) * 0.5f;
        var nearby = grid.GetNearbySoldiers(new Vector3(midpoint.X, 0f, midpoint.Y),
            length * 0.5f + footprint + units.MaxFootprint + maxStep);
        float fraction = 1f;
        while (nearby.MoveNext())
        {
            int other = nearby.Current;
            if (other == id || units.Health[other] <= 0f) continue;
            Vector2 offset = from - ObstacleField.Xz(units.Position[other]);
            float approach = offset.Dot(step);
            if (approach >= 0f) continue;

            float radius = footprint + units.Catalog[units.Type[other]].Footprint;
            float clearance = offset.LengthSquared() - radius * radius;
            if (clearance <= 0f) return from;
            float discriminant = approach * approach - lengthSquared * clearance;
            if (discriminant <= 0f) continue;
            float contact = clearance / (-approach + Mathf.Sqrt(discriminant));
            if (contact >= fraction) continue;
            fraction = Mathf.Max(0f, contact - 0.0001f / length);
            if (fraction == 0f) return from;
        }
        return fraction == 1f ? to : from + step * fraction;
    }
}
