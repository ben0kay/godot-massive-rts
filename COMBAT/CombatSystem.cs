// Staggered local acquisition and simple benchmark attacks; no per-unit nodes.
using Godot;
namespace MassiveRts;

public static class CombatSystem
{
    private const int ExtraPursuitQueriesPerTick = 128;
    private const float MeleeContactTolerance = 0.08f;

    public static float MeleeContact(float attackerRadius, float defenderRadius, float separationGap) =>
        attackerRadius + defenderRadius + Mathf.Max(0f, separationGap);

    public static void PrepareTargets(UnitState units, SpatialGrid grid, int start, int end)
    {
        for (int i = start; i < end; i++)
            units.Target[i] = grid.FindPursuitEnemy(units, i, -1);
    }

    // =========================================================
    public static void Acquire(UnitState units, SpatialGrid grid, BenchmarkConfig config, int tick, ObstacleField obstacles = null, bool rangedVariety = false, uint battleSeed = 0)
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
                float radius = type.Ranged ? type.Reach : Mathf.Max(config.CellSize * 2, type.Reach + units.MaxFootprint);
                float distance = target >= 0 ? units.Position[i].DistanceSquaredTo(units.Position[target]) : float.PositiveInfinity;
                if (refreshLocal)
                {
                    int rank = rangedVariety && type.Ranged ? RangedRank(i, battleSeed) : 1;
                    if (rank > 1)
                    {
                        ObstacleField cover = obstacles != null &&
                            units.Catalog.Projectiles.Flight(type.ProjectileType).ObstacleFire == ObstacleFireMode.RequiresDirectLineOfSight
                            ? obstacles : null;
                        int varied = grid.FindEnemy(units, i, radius, config.QueryCandidateLimit, tick, cover, rank, target);
                        if (varied >= 0)
                        {
                            target = varied;
                            distance = units.Position[i].DistanceSquaredTo(units.Position[target]);
                        }
                    }
                    else
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
    // Fixed per soldier and battle seed. No random calls or per-shot retargets.
    private static int RangedRank(int id, uint seed)
    {
        uint hash = unchecked((uint)id * 0x9E3779B9u + seed);
        hash = unchecked((hash ^ (hash >> 16)) * 0x7FEB352Du);
        hash = unchecked((hash ^ (hash >> 15)) * 0x846CA68Bu);
        uint roll = (hash ^ (hash >> 16)) % 100u;
        return roll < 80u ? 1 : roll < 95u ? 2 : 3;
    }

    // =========================================================
    public static void Step(UnitState units, SpatialGrid grid, ProjectileSystem projectiles, float fireRateMultiplier, float dt, float separationPadding, float separationGap, TrampleSystem trample = null, ObstacleField obstacles = null,
        ShockwaveRenderer shockwaves = null)
    {
        Vector2 turnStep = SoldierFacing.TurnStep(dt);
        for (int i = 0; i < units.Count; i++)
        {
            if (units.Health[i] <= 0) continue;
            units.Cooldown[i] = Mathf.Max(0, units.Cooldown[i] - dt);
            if (trample?.OverridesAttack(i) == true) continue;
            int target = units.Target[i];
            if (target < 0 || units.Health[target] <= 0 || units.Cooldown[i] > 0) continue;
            ref readonly UnitStats type = ref units.Catalog[units.Type[i]];
            Vector3 offset = units.Position[target] - units.Position[i];
            float radius = type.Ranged ? type.Reach :
                MeleeContact(type.Footprint, units.Catalog[units.Type[target]].Footprint, separationGap) + MeleeContactTolerance;
            if (offset.LengthSquared() > radius * radius) continue;
            if (obstacles != null &&
                (!type.Ranged || units.Catalog.Projectiles.Flight(type.ProjectileType).ObstacleFire == ObstacleFireMode.RequiresDirectLineOfSight) &&
                obstacles.BlocksSight(units.Position[i], units.Position[target])) continue;
            Vector2 facing = units.Facing[i];
            if (!type.Ranged)
            {
                Vector2 aim = new(offset.X, offset.Z);
                float aimSquared = aim.LengthSquared();
                if (aimSquared <= 0.000001f)
                    facing = Vector2.Zero;
                else if (type.Shape == UnitShape.Circle)
                    facing = aim.Normalized();
                else
                {
                    float forward = facing.Dot(aim);
                    float cone = type.CleaveEnabled ? type.CleaveCosineSquared : type.MeleeFacingCosineSquared;
                    if (facing == Vector2.Zero || forward < 0f || forward * forward < aimSquared * cone)
                    {
                        SoldierFacing.Turn(units, i, aim / Mathf.Sqrt(aimSquared), turnStep);
                        units.Dirty[i] = true;
                        continue;
                    }
                }
            }
            if (type.Ranged)
            {
                // Only recoil when the projectile pool accepted the shot.
                int before = projectiles.Count;
                projectiles.Fire(i, target, units);
                if (projectiles.Count > before && type.RangedRecoil.Distance > 0f)
                    units.RangedRecoil?.OnFire(units, i, target);
            }
            else if (type.Shockwave.EveryAttacks > 0 && units.IsShockwaveAttack(i, type.Shockwave.EveryAttacks))
            {
                ShockwaveStats stomp = type.Shockwave;
                Vector3 impact = units.Position[i];
                AreaDamage.Radial(units, grid, units.Faction[i], impact, stomp.Radius, stomp.Damage, stomp.EdgeDamageFraction,
                    units.Catalog.MaxSpeed * dt + separationPadding, stomp.RespectsCover ? obstacles : null);
                shockwaves?.Emit(impact, stomp.Radius);
            }
            else if (type.CleaveEnabled)
                AreaDamage.Cleave(units, grid, i, target, type, facing, separationGap, MeleeContactTolerance, units.Catalog.MaxSpeed * dt + separationPadding, obstacles);
            else units.Damage(target, type.Damage);
            units.Cooldown[i] = type.Ranged ? type.Interval / fireRateMultiplier : type.Interval;
        }
    }
}
