// Shared marching pass; stable lanes avoid a single converging destination.
using Godot;
namespace MassiveRts;

public static class MovementSystem
{
    // =========================================================
    public static void Step(UnitState units, BenchmarkConfig config, BenchmarkMode mode, float dt)
    {
        float stepDistance = config.Speed * dt;
        float meleeStop = config.MeleeReach * 0.85f;
        float rangedStop = config.RangedReach * 0.85f;
        bool rangedMode = mode == BenchmarkMode.Ranged;
        bool movementMode = mode == BenchmarkMode.Movement;
        for (int i = 0; i < units.Count; i++)
        {
            if (units.Health[i] <= 0) continue;
            if (units.Flash[i] > 0) { units.Flash[i] = Mathf.Max(0, units.Flash[i] - dt); units.Dirty[i] = true; }
            int target = units.Target[i];
            bool hasTarget = target >= 0 && units.Health[target] > 0;
            Vector3 destination = hasTarget ? units.Position[target] : units.Goal[i];
            Vector3 offset = destination - units.Position[i]; offset.Y = 0;
            float stop = hasTarget ? (rangedMode && units.Kind[i] == 1 ? rangedStop : meleeStop) : 0.2f;
            bool marchingInLane = !hasTarget && offset.Z == 0f;
            float distance = marchingInLane ? Mathf.Abs(offset.X) : offset.Length();
            if (distance <= stop)
            {
                // Movement-only mode continuously marches back and forth along separate lanes.
                if (movementMode) units.Goal[i].X = -units.Goal[i].X;
                continue;
            }
            float travel = Mathf.Min(stepDistance, distance - stop);
            if (marchingInLane)
                units.Position[i].X += offset.X < 0f ? -travel : travel;
            else
                units.Position[i] += offset / distance * travel;
            units.Position[i].Y = BattlefieldHeight.At(units.Position[i].X, units.Position[i].Z);
            units.Dirty[i] = true;
        }
    }
}
