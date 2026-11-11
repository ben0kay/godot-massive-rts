// Staggered local acquisition and simple benchmark attacks; no per-unit nodes.
using Godot;
using Godot;
namespace MassiveRts;

public static class CombatSystem
{
    // =========================================================
    public static void Acquire(UnitState units, SpatialGrid grid, BenchmarkConfig config, BenchmarkMode mode, int tick)
    {
        int localInterval = config.TargetRefreshTicks;
        int pursuitInterval = Mathf.Max(localInterval, Mathf.CeilToInt(0.8f / config.TickSeconds));
        int localPhase = tick % localInterval;
        int pursuitPhase = tick % pursuitInterval;
        for (int i = 0; i < units.Count; i++)
        {
            if (units.Health[i] <= 0) continue;
            int target = units.Target[i];
            if (target >= 0 && units.Health[target] <= 0) target = -1;

            bool refreshLocal = i % localInterval == localPhase;
            bool refreshPursuit = i % pursuitInterval == pursuitPhase;
            if (refreshLocal || refreshPursuit)
            {
                float radius = mode == BenchmarkMode.Ranged && units.Kind[i] == 1 ? config.RangedReach : config.CellSize * 2;
                float distance = target >= 0 ? units.Position[i].DistanceSquaredTo(units.Position[target]) : float.PositiveInfinity;
                if (refreshLocal)
                {
                    int found = grid.FindEnemy(units, i, radius, config.QueryCandidateLimit, tick);
                    if (found >= 0)
                    {
                        float foundDistance = units.Position[i].DistanceSquaredTo(units.Position[found]);
                        if (foundDistance < distance)
                        {
                            target = found;
                            distance = foundDistance;
                        }
                    }
                }
                if (refreshPursuit && distance >= radius * radius)
                    target = grid.FindPursuitEnemy(units, i, target);
            }
            units.Target[i] = target;
        }
    }
    // =========================================================
    public static void Step(UnitState units, ProjectileSystem projectiles, BenchmarkConfig config, BenchmarkMode mode, float fireRate, float dt)
    {
        for (int i = 0; i < units.Count; i++)
        {
            if (units.Health[i] <= 0) continue;
            units.Cooldown[i] = Mathf.Max(0, units.Cooldown[i] - dt);
            int target = units.Target[i];
            if (target < 0 || units.Health[target] <= 0 || units.Cooldown[i] > 0) continue;
            bool ranged = mode == BenchmarkMode.Ranged && units.Kind[i] == 1;
            float radius = ranged ? config.RangedReach : config.MeleeReach;
            if (units.Position[i].DistanceSquaredTo(units.Position[target]) > radius * radius) continue;
            if (ranged) { projectiles.Fire(units.Position[i], target, units, config); units.Cooldown[i] = 1f / fireRate; }
            else { units.Damage(target, config.Damage, config.FlashSeconds); units.Cooldown[i] = config.MeleeInterval; }
        }
    }
}
