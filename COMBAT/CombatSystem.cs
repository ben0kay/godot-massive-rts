// Staggered local acquisition and simple benchmark attacks; no per-unit nodes.
using Godot;
namespace MassiveRts;

public static class CombatSystem
{
    private const int ExtraPursuitQueriesPerTick = 128;

    public static void PrepareTargets(UnitState units, SpatialGrid grid, int start, int end)
    {
        for (int i = start; i < end; i++)
            units.Target[i] = grid.FindPursuitEnemy(units, i, -1);
    }

    // =========================================================
    public static void Acquire(UnitState units, SpatialGrid grid, BenchmarkConfig config, int tick, ObstacleField obstacles = null)
    {
        int localInterval = config.TargetRefreshTicks;
        int pursuitInterval = Mathf.Max(localInterval, Mathf.CeilToInt(0.8f / config.TickSeconds));
        int localPhase = tick % localInterval;
        int pursuitPhase = tick % pursuitInterval;
        int extraPursuitQueries = ExtraPursuitQueriesPerTick;
        for (int i = 0; i < units.Count; i++)
        {
            if (units.Health[i] <= 0) continue;
            int previous = units.Target[i];
            int target = previous >= 0 && units.Health[previous] > 0 ? previous : -1;

            bool refreshLocal = i % localInterval == localPhase;
            bool refreshPursuit = i % pursuitInterval == pursuitPhase;
            if (refreshLocal || refreshPursuit)
            {
                ref readonly UnitStats type = ref units.Catalog[units.Type[i]];
                float radius = type.Ranged ? type.Reach : Mathf.Max(config.CellSize * 2, type.Reach + units.Catalog.MaxFootprint);
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
                    if (obstacles != null && type.Ranged && target >= 0 && distance <= type.Reach * type.Reach &&
                        units.Catalog.Projectiles.Flight(type.ProjectileType).ObstacleFire == ObstacleFireMode.RequiresDirectLineOfSight &&
                        obstacles.BlocksSight(units.Position[i], units.Position[target]))
                    {
                        int visible = grid.FindEnemy(units, i, type.Reach, config.QueryCandidateLimit, tick, obstacles);
                        if (visible >= 0)
                        {
                            target = visible;
                            distance = units.Position[i].DistanceSquaredTo(units.Position[target]);
                        }
                    }
                }
                if (refreshPursuit && distance >= radius * radius)
                    target = grid.FindPursuitEnemy(units, i, target);
                else if (refreshLocal && target < 0 && previous >= 0 && extraPursuitQueries > 0)
                {
                    int enemyAlive = units.Alive - units.FactionAlive[units.Faction[i]];
                    if (enemyAlive > 0)
                    {
                        extraPursuitQueries--;
                        target = grid.FindPursuitEnemy(units, i, -1);
                    }
                }
            }
            // A dead target remains a movement waypoint until replacement. Attacks require a live target.
            units.Target[i] = target >= 0 ? target : previous;
        }
    }
    // =========================================================
    public static void Step(UnitState units, SpatialGrid grid, ProjectileSystem projectiles, float fireRateMultiplier, float dt, float separationPadding, TrampleSystem trample = null, ObstacleField obstacles = null)
    {
        for (int i = 0; i < units.Count; i++)
        {
            if (units.Health[i] <= 0) continue;
            units.Cooldown[i] = Mathf.Max(0, units.Cooldown[i] - dt);
            if (trample?.OverridesAttack(i) == true) continue;
            int target = units.Target[i];
            if (target < 0 || units.Health[target] <= 0 || units.Cooldown[i] > 0) continue;
            ref readonly UnitStats type = ref units.Catalog[units.Type[i]];
            float radius = type.AttackReach(units.Catalog[units.Type[target]]);
            if (units.Position[i].DistanceSquaredTo(units.Position[target]) > radius * radius) continue;
            if (obstacles != null &&
                (!type.Ranged || units.Catalog.Projectiles.Flight(type.ProjectileType).ObstacleFire == ObstacleFireMode.RequiresDirectLineOfSight) &&
                obstacles.BlocksSight(units.Position[i], units.Position[target])) continue;
            if (type.Ranged) projectiles.Fire(i, target, units);
            else if (type.CleaveEnabled)
                AreaDamage.Cleave(units, grid, i, target, type, units.Catalog.MaxSpeed * dt + separationPadding, obstacles);
            else units.Damage(target, type.Damage);
            units.Cooldown[i] = type.Ranged ? type.Interval / fireRateMultiplier : type.Interval;
        }
    }
}
