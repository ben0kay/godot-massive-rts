// Tracks melee contact and short, deterministic sidesteps for blocked followers.
using Godot;

namespace MassiveRts;

public sealed class MeleeSteering
{
    private const byte StallTicks = 5;
    private const byte SidestepTicks = 9;
    private const int ProbeBudget = 96;

    private struct Soldier
    {
        public int Target;
        public bool Holding;
        public byte Stalled, StepsLeft, WaitLeft;
        public sbyte Side;
        public Vector2 Start, Direction;
        public float Attempt;
    }

    private readonly Soldier[] soldiers;
    private readonly bool hold, sidestep;
    public bool Matches(bool holdEnabled, bool sidestepEnabled) => hold == holdEnabled && sidestep == sidestepEnabled;

    public MeleeSteering(int count, bool holdEnabled, bool sidestepEnabled)
    {
        soldiers = new Soldier[count];
        hold = holdEnabled;
        sidestep = sidestepEnabled;
    }

    public void Reset(int id) => soldiers[id] = default;

    public void ResetSidestep(int id)
    {
        ref Soldier state = ref soldiers[id];
        state.Attempt = 0f;
        state.Stalled = state.StepsLeft = state.WaitLeft = 0;
    }

    public bool Hold(int id, int target, float distanceSquared, float contact, bool blocked)
    {
        ref Soldier state = ref soldiers[id];
        if (state.Target != target) state = new Soldier { Target = target };
        float threshold = contact + (state.Holding ? 0.075f : 0.02f);
        state.Holding = hold && !blocked && distanceSquared <= threshold * threshold;
        if (state.Holding) ResetSidestep(id);
        return state.Holding;
    }

    public Vector3 Steer(UnitState units, SpatialGrid grid, ObstacleField obstacles, int id,
        Vector3 direction, float footprint, float travel, float padding)
    {
        if (!sidestep || travel <= 0f) return direction;
        ref Soldier state = ref soldiers[id];
        if (state.WaitLeft > 0)
        {
            state.WaitLeft--;
            return Vector3.Zero;
        }
        Vector2 start = ObstacleField.Xz(units.Position[id]);
        Vector2 forward = ObstacleField.Xz(direction);
        if (state.Attempt > 0.005f && state.Direction.Dot(forward) > 0.7f &&
            (start - state.Start).Dot(state.Direction) < state.Attempt * 0.35f)
            state.Stalled = (byte)Mathf.Min(state.Stalled + 1, StallTicks);
        else state.Stalled = 0;

        Vector2 lateral = new(-forward.Y, forward.X);
        if (state.StepsLeft == 0 && state.Stalled >= StallTicks)
        {
            sbyte preferred = (id & 1) == 0 ? (sbyte)1 : (sbyte)-1;
            if (CanStep(units, grid, obstacles, id, start, lateral * preferred, footprint, padding))
                state.Side = preferred;
            else if (CanStep(units, grid, obstacles, id, start, -lateral * preferred, footprint, padding))
                state.Side = (sbyte)-preferred;
            else state.Side = 0;
            state.Stalled = 0;
            if (state.Side == 0)
            {
                state.Attempt = 0f;
                state.WaitLeft = 4;
                return Vector3.Zero;
            }
            state.StepsLeft = SidestepTicks;
        }

        state.Start = start;
        state.Direction = forward;
        state.Attempt = travel;
        if (state.StepsLeft == 0) return direction;
        state.StepsLeft--;
        state.Attempt = 0f;
        Vector2 step = (lateral * state.Side + forward * 0.22f).Normalized();
        return new Vector3(step.X, 0f, step.Y);
    }

    private static bool CanStep(UnitState units, SpatialGrid grid, ObstacleField obstacles, int id,
        Vector2 start, Vector2 side, float footprint, float padding)
    {
        Vector2 end = start + side * Mathf.Max(0.32f, footprint * 0.8f);
        if (obstacles?.BlocksCorridor(start, end, footprint) == true) return false;

        var candidates = grid.GetNearbySoldiers(new Vector3(end.X, 0f, end.Y),
            footprint + units.MaxFootprint + padding);
        int examined = 0;
        while (candidates.MoveNext())
        {
            if (++examined > ProbeBudget) return false;
            int other = candidates.Current;
            if (other == id || units.Health[other] <= 0f) continue;
            float clearance = (footprint + units.Catalog[units.Type[other]].Footprint) * 0.9f;
            Vector2 position = ObstacleField.Xz(units.Position[other]);
            float endDistance = end.DistanceSquaredTo(position);
            if (endDistance < clearance * clearance && endDistance < start.DistanceSquaredTo(position))
                return false;
        }
        return true;
    }
}
