// Staggered local acquisition and simple benchmark attacks; no per-unit nodes.
using Godot;
namespace MassiveRts;

public static class CombatSystem
{
    private const int ExtraPursuitQueriesPerAcquisitionStep = 128;
    private const int PursuitQueriesPerAcquisitionStep = 512;
    private const float FarMeleeRetargetDelaySeconds = 0.4f;
    private const float MeleeContactTolerance = 0.08f;

    public static float MeleeContact(float attackerRadius, float defenderRadius, float separationGap) =>
        attackerRadius + defenderRadius + Mathf.Max(0f, separationGap);

    public static void PrepareTargets(UnitState units, SpatialGrid grid, int start, int end)
    {
        for (int i = start; i < end; i++)
            units.Target[i] = grid.FindPursuitEnemy(units, i, -1, PursuitReason.Preparation);
    }

    // =========================================================
    public static void Acquire(UnitState units, SpatialGrid grid, BenchmarkConfig config, int tick, float acquisitionSeconds, ObstacleField obstacles = null, bool rangedVariety = false, uint battleSeed = 0, bool slowPursuitRefresh = false,
        bool delayFarMeleeRetarget = false, bool limitPursuitQueries = false, int end = -1)
    {
        int localInterval = Mathf.Max(1, Mathf.RoundToInt(config.TargetRefreshTicks * BenchmarkConfig.TargetingStepSeconds / acquisitionSeconds));
        int pursuitInterval = Mathf.Max(localInterval, Mathf.RoundToInt(0.8f / acquisitionSeconds));
        int localPhase = tick % localInterval;
        int pursuitPhase = tick % pursuitInterval;
        int retainedPursuitInterval = pursuitInterval * 2;
        int retainedPursuitPhase = tick % retainedPursuitInterval;
        int extraPursuitQueries = ExtraPursuitQueriesPerAcquisitionStep;
        int deferredReplacements = 0;
        int remainingPursuitQueries = limitPursuitQueries ? PursuitQueriesPerAcquisitionStep : int.MaxValue;
        int budgetDeferrals = 0, delayDeferrals = 0;
        int[] deadlines = units.RetargetDeadlines(delayFarMeleeRetarget);
        int delaySteps = Mathf.CeilToInt(FarMeleeRetargetDelaySeconds / acquisitionSeconds);
        // Rotate the starting ID so recurring budget pressure is spread across the formation.
        int count = end < 0 ? units.Count : end;
        int first = limitPursuitQueries && count > 0 ? (int)(tick * 997L % count) : 0;
        for (int visited = 0, i = first; visited < count; visited++, i = i + 1 == count ? 0 : i + 1)
        {
            if (units.Health[i] <= 0) continue;
            int previous = units.Target[i];
            int target = previous >= 0 && units.Health[previous] > 0 ? previous : -1;

            if (deadlines != null && target < 0 && previous >= 0 && deadlines[i] == 0)
                deadlines[i] = tick + delaySteps;

            bool refreshLocal = i % localInterval == localPhase;
            bool refreshPursuit = i % pursuitInterval == pursuitPhase;
            if (refreshLocal || refreshPursuit)
            {
                ref readonly UnitStats type = ref units.Catalog[units.Type[i]];
                float radius = type.Ranged ? type.Reach : Mathf.Max(config.CellSize * 2, type.Reach + units.MaxFootprint);
                float distance = target >= 0 ? units.Position[i].DistanceSquaredTo(units.Position[target]) : float.PositiveInfinity;
                if (refreshLocal)
                {
                    int rank = rangedVariety && type.Ranged && type.MinimumFireRange == 0f ? RangedRank(i, battleSeed) : 1;
                    if (type.MinimumFireRange > 0f)
                    {
                        ObstacleField cover = obstacles != null &&
                            units.Catalog.Projectiles.Flight(type.ProjectileType).ObstacleFire == ObstacleFireMode.RequiresDirectLineOfSight
                            ? obstacles : null;
                        int found = grid.FindEnemy(units, i, radius, config.QueryCandidateLimit, tick, cover,
                            minimumRange: type.MinimumFireRange);
                        if (found >= 0)
                        {
                            target = found;
                            distance = units.Position[i].DistanceSquaredTo(units.Position[target]);
                        }
                    }
                    else if (rank > 1)
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
                float radiusSquared = radius * radius;
                bool scheduled = refreshPursuit && distance >= radiusSquared &&
                    (!slowPursuitRefresh || target < 0 || i % retainedPursuitInterval == retainedPursuitPhase);
                bool extra = !scheduled && refreshLocal && target < 0 && previous >= 0 &&
                    (extraPursuitQueries > 0 || grid.ProfileTargeting) &&
                    units.Alive > units.FactionAlive[units.Faction[i]];
                if (scheduled || extra)
                {
                    bool delayed = deadlines != null && !type.Ranged && target < 0 && previous >= 0 &&
                        tick < deadlines[i] &&
                        units.Position[i].DistanceSquaredTo(units.Position[previous]) > radiusSquared;
                    if (delayed) delayDeferrals++;
                    else if (extra && extraPursuitQueries == 0) deferredReplacements++;
                    else if (remainingPursuitQueries == 0) budgetDeferrals++;
                    else
                    {
                        remainingPursuitQueries--;
                        if (extra) extraPursuitQueries--;
                        PursuitReason reason = extra ? PursuitReason.ExtraReplacement :
                            target >= 0 ? PursuitReason.LiveRefresh : PursuitReason.ScheduledAcquisition;
                        target = grid.FindPursuitEnemy(units, i, target, reason, previous, radius, refreshLocal);
                    }
                }
            }
            if (deadlines != null && target >= 0) deadlines[i] = 0;
            // A dead target remains a movement waypoint until replacement. Attacks require a live target.
            units.Target[i] = target >= 0 ? target : previous;
        }
        if (grid.ProfileTargeting) grid.RecordReplacementBudget(extraPursuitQueries == 0, deferredReplacements, budgetDeferrals, delayDeferrals);
    }
    // Holders use the same staggered local search without pursuit or voluntary movement.
    public static void AcquireHolding(UnitState units, SpatialGrid grid, BenchmarkConfig config, int start,
        int tick, float acquisitionSeconds, float separationGap, ObstacleField obstacles)
    {
        int interval = Mathf.Max(1, Mathf.RoundToInt(config.TargetRefreshTicks * BenchmarkConfig.TargetingStepSeconds / acquisitionSeconds));
        int offset = (tick % interval - start % interval + interval) % interval;
        for (int id = start + offset; id < units.Count; id += interval)
        {
            if (units.Health[id] <= 0f) continue;
            ref readonly UnitStats type = ref units.Catalog[units.Type[id]];
            float range = type.Ranged ? type.Reach :
                MeleeContact(type.Footprint, units.MaxFootprint, separationGap) + MeleeContactTolerance;
            ObstacleField cover = !type.Ranged ||
                units.Catalog.Projectiles.Flight(type.ProjectileType).ObstacleFire == ObstacleFireMode.RequiresDirectLineOfSight
                ? obstacles : null;
            units.Target[id] = grid.FindEnemy(units, id, range, config.QueryCandidateLimit, tick, cover,
                minimumRange: type.MinimumFireRange, meleeReachPadding: type.Ranged ? -1f : separationGap + MeleeContactTolerance);
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
            if (type.MinimumFireRange > 0f && ObstacleField.Xz(offset).LengthSquared() <
                type.MinimumFireRange * type.MinimumFireRange) continue;
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
                if (type.Outline.IsChevron)
                {
                    Vector2 aim = ObstacleField.Xz(offset).Normalized();
                    if (aim != Vector2.Zero && facing.Dot(aim) < 0.995f)
                    {
                        SoldierFacing.Turn(units, i, aim, turnStep);
                        units.Dirty[i] = true;
                        continue;
                    }
                }
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
