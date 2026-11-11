// Staggered local acquisition and simple benchmark attacks; no per-unit nodes.
using Godot;
namespace MassiveRts;

public static class CombatSystem
{
    // =========================================================
    public static void Acquire(UnitState units, SpatialGrid grid, BenchmarkConfig config, BenchmarkMode mode, int tick)
    {
        for (int i = 0; i < units.Count; i++)
        {
            if (units.Health[i] <= 0) continue;
            int target = units.Target[i];
            float radius = mode == BenchmarkMode.Ranged && units.Kind[i] == 1 ? config.RangedReach : config.CellSize * 2;
            if (target >= 0 && (units.Health[target] <= 0 || units.Position[i].DistanceSquaredTo(units.Position[target]) > radius * radius)) units.Target[i] = -1;
            if (i % config.TargetRefreshTicks == tick % config.TargetRefreshTicks)
            {
                int found = grid.FindEnemy(units, i, radius, config.QueryCandidateLimit, tick);
                if (found < 0 && units.SeekingSurvivors[i])
                {
                    // A capped local query can miss the enemy the soldier is already pursuing.
                    int enemy = grid.LivingRepresentative(1 - units.Faction[i]);
                    if (enemy >= 0 && units.Position[i].DistanceSquaredTo(units.Position[enemy]) < radius * radius)
                        found = enemy;
                }
                units.Target[i] = found;
            }
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
