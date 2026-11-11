// Shared marching pass; stable lanes avoid a single converging destination.
using Godot;
namespace MassiveRts;

public static class MovementSystem
{
    // =========================================================
    public static void Step(UnitState units, BenchmarkConfig config, BenchmarkMode mode, float dt)
    {
        for (int i = 0; i < units.Count; i++)
        {
            if (units.Health[i] <= 0) continue;
            if (units.Flash[i] > 0) { units.Flash[i] = Mathf.Max(0, units.Flash[i] - dt); units.Dirty[i] = true; }
            int target = units.Target[i];
            bool hasTarget = target >= 0 && units.Health[target] > 0;
            Vector3 destination = hasTarget ? units.Position[target] : units.Goal[i];
            Vector3 offset = destination - units.Position[i]; offset.Y = 0;
            float distance = offset.Length();
            float stop = hasTarget ? (mode == BenchmarkMode.Ranged && units.Kind[i] == 1 ? config.RangedReach * 0.85f : config.MeleeReach * 0.85f) : 0.2f;
            if (distance <= stop)
            {
                // Movement-only mode continuously marches back and forth along separate lanes.
                if (mode == BenchmarkMode.Movement) units.Goal[i].X = -units.Goal[i].X;
                continue;
            }
            units.Position[i] += offset / distance * Mathf.Min(config.Speed * dt, distance - stop);
            units.Position[i].Y = BattlefieldHeight.At(units.Position[i].X, units.Position[i].Z);
            units.Dirty[i] = true;
        }
    }
}
