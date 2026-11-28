// Staggered local acquisition and simple benchmark attacks; no per-unit nodes.
using Godot;
namespace MassiveRts;

public static class CombatSystem
{
    private const int ExtraPursuitQueriesPerAcquisitionStep = 128;
    private const int PursuitQueriesPerAcquisitionStep = 512;
    private const float FarMeleeRetargetDelaySeconds = 0.4f;
    internal const float MeleeLungeSeconds = 0.16f;
    internal const float MeleeContactTolerance = 0.08f;

    public static float MeleeContact(float attackerRadius, float defenderRadius, float separationGap) =>
        attackerRadius + defenderRadius + Mathf.Max(0f, separationGap);

    public static void PrepareTargets(UnitState units, SpatialGrid grid, int start, int end, int localOnlyFaction = -1)
    {
        for (int i = start; i < end; i++)
            units.Target[i] = units.Faction[i] == localOnlyFaction || !units.Catalog[units.Type[i]].TargetsUnits || units.OpeningOrders?.Active(i) == true ? -1 : grid.FindPursuitEnemy(units, i, -1, PursuitReason.Preparation);
    }

    // =========================================================
    public static void Acquire(UnitState units, SpatialGrid grid, BenchmarkConfig config, int tick, float acquisitionSeconds, ObstacleField obstacles = null, bool rangedVariety = false, uint battleSeed = 0, bool slowPursuitRefresh = false,
        bool delayFarMeleeRetarget = false, bool limitPursuitQueries = false, int end = -1, int[] activeIds = null, int localOnlyFaction = -1)
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
        for (int visited = 0, slot = first; visited < count; visited++, slot = slot + 1 == count ? 0 : slot + 1)
        {
            int i = activeIds == null ? slot : activeIds[slot];
            if (units.Health[i] <= 0 || units.IsAirborne(i)) continue;
            if (!units.Catalog[units.Type[i]].TargetsUnits) { units.Target[i] = -1; continue; }
            if (units.OpeningOrders?.Active(i) == true)
            {
                if (i % localInterval == localPhase) units.OpeningOrders.Acquire(units, grid, config, obstacles, i, tick);
                continue;
            }
            if (units.Faction[i] == localOnlyFaction)
            {
                // Objective attackers fight nearby defenders and leave distant targets to the assault route.
                ref readonly var attacker = ref units.Catalog[units.Type[i]];
                float reach = attacker.Ranged ? units.RangedReach(i, attacker) :
                    Mathf.Max(config.CellSize * 2f, attacker.MeleeDetectionReach + units.MaxFootprint);
                int current = units.Target[i];
                var cover = StructureCombat.Sight(attacker, units, obstacles);
                if (current >= 0 &&
                    (units.Health[current] <= 0f || !units.CanDetectGroundTarget(units.Position[i], current) || units.Position[i].DistanceSquaredTo(units.Position[current]) > reach * reach ||
                     cover?.BlocksSight(units.Position[i], units.Position[current]) == true))
                    current = -1;
                if (current < 0 && i % localInterval == localPhase)
                    current = grid.FindEnemy(units, i, reach, config.QueryCandidateLimit, tick, cover,
                        minimumRange: attacker.MinimumFireRange);
                units.Target[i] = current;
                continue;
            }
            int previous = units.Target[i];
            int target = previous >= 0 && units.Health[previous] > 0 && units.CanDetectGroundTarget(units.Position[i], previous) ? previous : -1;

            if (deadlines != null && target < 0 && previous >= 0 && deadlines[i] == 0)
                deadlines[i] = tick + delaySteps;

            bool refreshLocal = i % localInterval == localPhase;
            bool refreshPursuit = i % pursuitInterval == pursuitPhase;
            if (refreshLocal || refreshPursuit)
            {
                ref readonly UnitStats type = ref units.Catalog[units.Type[i]];
                float radius = type.Ranged ? units.RangedReach(i, type) : Mathf.Max(config.CellSize * 2, type.MeleeDetectionReach + units.MaxFootprint);
                float distance = target >= 0 ? units.Position[i].DistanceSquaredTo(units.Position[target]) : float.PositiveInfinity;
                if (refreshLocal)
                {
                    int rank = rangedVariety && type.Ranged && type.MinimumFireRange == 0f ? RangedRank(i, battleSeed) : 1;
                    if (type.MinimumFireRange > 0f || type.PrismBeam.Enabled)
                    {
                        ObstacleField cover = obstacles != null &&
                            type.DirectFire(units.Catalog.Projectiles)
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
                            type.DirectFire(units.Catalog.Projectiles)
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
                        if (obstacles != null && type.Ranged && target >= 0 && distance <= radius * radius &&
                            type.DirectFire(units.Catalog.Projectiles) &&
                            obstacles.BlocksSight(units.Position[i], units.Position[target]))
                        {
                            int visible = grid.FindEnemy(units, i, radius, config.QueryCandidateLimit, tick, obstacles);
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
    internal static bool Step(UnitState units, SpatialGrid grid, ProjectileSystem projectiles, float fireRateMultiplier, float dt, float separationPadding, float separationGap, TrampleSystem trample = null, ObstacleField obstacles = null,
        ShockwaveRenderer shockwaves = null, StructureCombat structureCombat = null, bool useDreadnaughtContact = false, CommandMovement commands = null,
        ObstacleField meleeObstacles = null)
    {
        var turnSteps = units.TurnSteps(dt);
        bool visualsChanged = false;
        int beamBudget = PrismLanceCombat.ShotsPerTick;
        for (int i = 0; i < units.Count; i++)
        {
            if (units.Health[i] <= 0 || units.IsAirborne(i)) continue;
            float beforeCooldown = units.Cooldown[i];
            units.Cooldown[i] = Mathf.Max(0, beforeCooldown - dt);
            ref readonly UnitStats type = ref units.Catalog[units.Type[i]];
            if (type.PrismBeam.Enabled)
            {
                bool interrupted = commands?.HasCommand(i) == true && !commands.IsAttackMove(i);
                visualsChanged |= PrismLanceCombat.Step(units, grid, projectiles, obstacles, i, type, dt,
                    units.MaximumTravelSpeed * dt + separationPadding, interrupted, ref beamBudget);
                continue;
            }
            if (units.HasMeleeLunge && beforeCooldown > 0f)
            {
                ref readonly var animation = ref units.Catalog[units.Type[i]];
                if (animation.MeleeLungeDistance > 0f && animation.Interval - beforeCooldown < MeleeLungeSeconds)
                    visualsChanged = units.Dirty[i] = true;
            }
            var turret = units.HasIndependentPrimary ? units.Catalog.IndependentPrimary(units.Type[i]) : null;
            if (turret.HasValue) units.PrimaryTurrets?.BeginTick(i);
            if (trample?.OverridesAttack(i) == true || structureCombat?.Target(i)?.Destroyed == false) continue;
            int target = units.Target[i];
            bool robotCircle = type.Shape == UnitShape.Circle && units.CircleUsesFacing;
            if (target < 0 || units.Health[target] <= 0 || !units.CanDetectGroundTarget(units.Position[i], target) || (units.Cooldown[i] > 0 && !robotCircle && !turret.HasValue)) continue;
            if (!type.TargetsUnits || (useDreadnaughtContact && type.Outline.IsDreadnaught)) continue;
            bool turretAligned = !turret.HasValue || units.EnsurePrimaryTurrets().Aim(units, i,
                units.Position[target], turret.Value, dt);
            if (turret.HasValue) visualsChanged |= units.PrimaryTurrets.IsTurning(i);
            Vector3 offset = units.Position[target] - units.Position[i];
            float radius = type.Ranged ? units.RangedReach(i, type) :
                MeleeContact(type.MeleeAttackRadius, units.Catalog[units.Type[target]].Footprint, separationGap) + MeleeContactTolerance;
            if (useDreadnaughtContact && !type.Ranged && units.Type[target] == RuntimeUnitCatalog.Dreadnaught)
            {
                float reachFromEdge = CombatSystem.MeleeContact(type.MeleeAttackRadius, 0f, separationGap) + MeleeContactTolerance;
                if (DreadnaughtCombat.DistanceToHull(units, target, units.Position[i]) > reachFromEdge) continue;
            }
            else if (offset.LengthSquared() > radius * radius) continue;
            if (type.MinimumFireRange > 0f && ObstacleField.Xz(offset).LengthSquared() <
                type.MinimumFireRange * type.MinimumFireRange) continue;
            var attackSight = type.Ranged ? obstacles : meleeObstacles?.ForTier(type.SizeTier) ?? obstacles;
            if (attackSight != null && type.DirectFire(units.Catalog.Projectiles) &&
                attackSight.BlocksSight(units.Position[i], units.Position[target])) continue;
            Vector2 facing = units.Facing[i];
            if (!type.Ranged && (type.Shape != UnitShape.Circle || type.CleaveEnabled || robotCircle))
            {
                Vector2 aim = new(offset.X, offset.Z);
                float aimSquared = aim.LengthSquared();
                if (aimSquared <= 0.000001f)
                    facing = Vector2.Zero;
                else if (type.Shape == UnitShape.Circle && !robotCircle)
                    facing = aim.Normalized();
                else
                {
                    float forward = facing.Dot(aim);
                    float cone = robotCircle ? 0.99f : type.CleaveEnabled ? type.CleaveCosineSquared : type.MeleeFacingCosineSquared;
                    if (facing == Vector2.Zero || forward < 0f || forward * forward < aimSquared * cone)
                    {
                        SoldierFacing.Turn(units, i, aim / Mathf.Sqrt(aimSquared),
                            turnSteps[units.Type[i]]);
                        units.Dirty[i] = true;
                        continue;
                    }
                }
            }
            if (units.Cooldown[i] > 0f) continue;
            float previousHealth = units.Health[target];
            bool cleaved = false, stomped = false;
            if (type.Ranged)
            {
                if (turret.HasValue ? !turretAligned || !ProjectileSystem.TurretHasSight(units, i, turret.Value,
                    units.Position[target] + Vector3.Up * (units.Catalog[units.Type[target]].Height * .5f), attackSight) :
                    !SoldierFacing.Aim(units, i, ObstacleField.Xz(offset), dt, type.Outline.IsChevron ? 0.995f : 0.97f))
                    continue;
                if (!turret.HasValue && type.DirectFire(units.Catalog.Projectiles) && units.Trenches is { } trenches &&
                    trenches.BlocksFire(units.Position[i] + Vector3.Up * (type.Height * 0.5f),
                        units.Position[target] + Vector3.Up * (units.Catalog[units.Type[target]].Height * 0.5f))) continue;
                // Only recoil when the projectile pool accepted the shot.
                int before = projectiles.Count;
                projectiles.Fire(i, target, units);
                if (projectiles.Count > before)
                {
                    visualsChanged |= units.HitFeedback?.OnAttack(units, i) == true;
                    if (!turret.HasValue && type.RangedRecoil.Distance > 0f) units.RangedRecoil?.OnFire(units, i, target);
                }
                else if (turret.HasValue) continue;
            }
            else if (type.Shockwave.EveryAttacks > 0 && units.IsShockwaveAttack(i, type.Shockwave.EveryAttacks))
            {
                stomped = true;
                ShockwaveStats stomp = type.Shockwave;
                Vector3 impact = units.Position[i];
                AreaDamage.Radial(units, grid, units.Faction[i], impact, stomp.Radius, type.AgainstUnits(stomp.Damage) * units.AttackMultiplier(i), stomp.EdgeDamageFraction,
                    units.MaximumTravelSpeed * dt + separationPadding, stomp.RespectsCover ? obstacles : null, type.UnitDamageType, melee: true);
                shockwaves?.Emit(impact, stomp.Radius);
            }
            else if (type.CleaveEnabled)
            {
                cleaved = true;
                AreaDamage.Cleave(units, grid, i, target, type, facing, separationGap, MeleeContactTolerance, units.MaximumTravelSpeed * dt + separationPadding, attackSight);
            }
            else
            {
                float damage = type.AgainstUnits(type.Damage) * units.AttackMultiplier(i);
                if (units.Type[target] == type.MeleeBonusTarget) damage *= type.MeleeBonusMultiplier;
                if (damage > 0f && type.FirstHitMultiplier > 1f && units.RecordFirstHit(i, target))
                    damage *= type.FirstHitMultiplier;
                units.Damage(target, damage, type.UnitDamageType, melee: true);
            }
            if (!type.Ranged)
            {
                visualsChanged |= units.HitFeedback?.OnAttack(units, i, stomped) == true;
                if (shockwaves != null && (cleaved || units.Health[target] < previousHealth))
                {
                    var contact = units.Position[i] + offset * 0.5f + Vector3.Up * (type.Height * 0.52f);
                    shockwaves.EmitMelee(contact, ObstacleField.Xz(offset), type.Footprint, cleaved);
                }
            }
            if (type.MeleeLungeDistance > 0f) visualsChanged = units.Dirty[i] = true;
            units.Cooldown[i] = type.Ranged ? type.Interval / fireRateMultiplier : type.Interval;
        }
        return visualsChanged;
    }
}
