// Shared movement pass for live combat targets and movement-only march lanes.
using System.Diagnostics;
using Godot;
namespace MassiveRts;

public static class MovementSystem
{
    private const float RangedStandOffBuffer = 0.25f;
    // =========================================================
    internal static void Step(UnitState units, BenchmarkMode mode, float dt, float separationGap,
        ref MovementProfile profile, int samplePhase, byte[] voluntaryMovement, float meleeRampSeconds, float rangedRampSeconds, TrampleSystem trample = null,
        ObstacleField obstacles = null, ObstacleNavigation navigation = null, SpatialGrid grid = null, MeleeSteering melee = null, int end = -1, int[] activeIds = null, StructureCombat structureCombat = null,
        ObstacleField separateSight = null, bool useSeparateSight = false)
    {
        var catalog = units.Catalog;
        var sight = useSeparateSight ? separateSight : obstacles;
        var turnSteps = units.TurnSteps(dt);
        bool movementMode = mode == BenchmarkMode.Movement;
        float meleeRampRate = meleeRampSeconds > 0f ? 1f / meleeRampSeconds : 0f;
        float rangedRampRate = rangedRampSeconds > 0f ? 1f / rangedRampSeconds : 0f;
        int count = end < 0 ? units.Count : end;
        for (int slot = 0; slot < count; slot++)
        {
            int i = activeIds == null ? slot : activeIds[slot];
            if (voluntaryMovement != null) voluntaryMovement[i] = 0;
            if (units.Health[i] <= 0) continue;
            if (trample?.OverridesMovement(i) == true)
            {
                melee?.Reset(i);
                continue;
            }
            int target = units.Target[i];
            var structure = structureCombat?.Target(i);
            bool hasStructure = structure?.Destroyed == false;
            bool hasTarget = !hasStructure && target >= 0 && units.Health[target] > 0;
            bool followingLastPosition = !hasTarget && !movementMode && target >= 0 &&
                units.Alive > units.FactionAlive[units.Faction[i]];
            if (!hasTarget && !movementMode && !followingLastPosition && !hasStructure)
            {
                units.MoveSpeed[i] = 0f;
                continue;
            }
            ref readonly UnitStats type = ref catalog[units.Type[i]];
            bool rangedCombat = !movementMode && type.Ranged;
            bool steerMelee = melee != null && !movementMode && !type.Ranged && hasTarget && !type.Outline.IsDreadnaught;
            if (!hasTarget) melee?.Reset(i);
            // Hold the firing line until there is a living target worth moving toward.
            if (rangedCombat && !hasTarget && !hasStructure && units.MoveSpeed[i] == 0f) continue;
            Vector3 destination = movementMode ? units.Goal[i] : hasStructure
                ? structureCombat.ApproachGoal(i) : units.Position[target];
            if (hasStructure && structureCombat.CanHit(i, type, structure, StructureCombat.Sight(type, units, sight)))
            {
                units.MoveSpeed[i] = 0f;
                continue;
            }
            Vector3 offset = destination - units.Position[i];
            float stop = hasStructure ? 0f : 0.2f;
            bool hasClearShot = false;
            if (hasTarget)
            {
                ref readonly UnitStats defender = ref catalog[units.Type[target]];
                float reach = type.Ranged ? units.RangedReach(i, type) :
                    CombatSystem.MeleeContact(type.MeleeAttackRadius, defender.Footprint, separationGap);
                float distanceSquared = offset.LengthSquared();
                bool inRange = (rangedCombat || obstacles != null) && distanceSquared <= reach * reach;
                float sightRange = steerMelee ? reach + 0.075f : reach;
                bool blockedInRange = sight != null && distanceSquared <= sightRange * sightRange &&
                    (!type.Ranged || catalog.Projectiles.Flight(type.ProjectileType).ObstacleFire == ObstacleFireMode.RequiresDirectLineOfSight) &&
                    sight.BlocksSight(units.Position[i], destination);
                hasClearShot = inRange && !blockedInRange;
                bool holding = false;
                if (steerMelee)
                {
                    bool sample = (i & (MovementProfile.SampleStride - 1)) == samplePhase;
                    long start = sample ? Stopwatch.GetTimestamp() : 0;
                    holding = melee.Hold(i, target, distanceSquared, reach, blockedInRange);
                    if (sample) profile.HoldTicks += Stopwatch.GetTimestamp() - start;
                }
                if (holding)
                {
                    units.MoveSpeed[i] = 0f;
                    continue;
                }
                if (rangedCombat && units.MoveSpeed[i] == 0f && hasClearShot) continue;
                // A small inward margin maintains contact through separation corrections.
                stop = rangedCombat ? reach - Mathf.Min(RangedStandOffBuffer, reach * 0.15f) : reach - 0.02f;
            }
            bool detouring = false, routeCorner = false;
            if (navigation != null)
            {
                if (hasClearShot && offset.X * offset.X + offset.Z * offset.Z <= stop * stop)
                {
                    if (steerMelee) melee.ResetSidestep(i);
                    units.MoveSpeed[i] = 0f;
                    continue;
                }
                Vector2 waypoint = navigation.Steer(i, units.Type[i], ObstacleField.Xz(units.Position[i]),
                    ObstacleField.Xz(destination), hasTarget ? stop : 0f, out detouring, out routeCorner);
                if (detouring)
                {
                    if (steerMelee) melee.ResetSidestep(i);
                    if (hasClearShot)
                    {
                        units.MoveSpeed[i] = 0f;
                        continue;
                    }
                    offset = new Vector3(waypoint.X - units.Position[i].X, 0f, waypoint.Y - units.Position[i].Z);
                    stop = 0f;
                }
            }
            offset.Y = 0f;
            bool marchingInLane = movementMode && offset.Z == 0f;
            float distance = marchingInLane ? Mathf.Abs(offset.X) : offset.Length();
            if (distance <= stop)
            {
                if (steerMelee) melee.ResetSidestep(i);
                // Movement-only mode continuously marches back and forth along separate lanes.
                if (movementMode && !detouring) units.Goal[i].X = -units.Goal[i].X;
                else units.MoveSpeed[i] = 0f;
                continue;
            }
            float remaining = distance - stop;
            float topSpeed = units.TravelSpeed(i, type);
            float advanceSpeed = topSpeed;
            float speed = advanceSpeed;
            float rampRate = type.Ranged ? rangedRampRate : meleeRampRate;
            float accelerationRate = type.AccelerationRateOverride > 0f ? type.AccelerationRateOverride : rampRate;
            if (!movementMode && accelerationRate > 0f)
            {
                advanceSpeed = Mathf.Min(topSpeed, units.MoveSpeed[i] + topSpeed * accelerationRate * dt);
                // Limit speed near the stopping point. Most soldiers can skip the square root.
                float brakingSpeedSquared = 2f * topSpeed * rampRate * remaining;
                speed = !routeCorner && rampRate > 0f && advanceSpeed * advanceSpeed > brakingSpeedSquared
                    ? Mathf.Sqrt(brakingSpeedSquared) : advanceSpeed;
            }
            float travel = Mathf.Min(speed * dt, remaining);
            Vector3 direction = marchingInLane
                ? (offset.X < 0f ? Vector3.Left : Vector3.Right)
                : offset / distance;
            if (steerMelee)
            {
                if (!detouring && distance > stop + 0.10f)
                {
                    bool sample = (i & (MovementProfile.SampleStride - 1)) == samplePhase;
                    long start = sample ? Stopwatch.GetTimestamp() : 0;
                    direction = melee.Steer(units, grid, obstacles, i, direction, type.Footprint, travel, catalog.MaxSpeed * dt);
                    if (sample)
                    {
                        profile.SidestepTicks += Stopwatch.GetTimestamp() - start;
                        profile.SidestepCalls++;
                        if (direction == Vector3.Zero) profile.BlockedSidesteps++;
                    }
                }
                else melee.ResetSidestep(i);
                if (direction == Vector3.Zero)
                {
                    units.MoveSpeed[i] = 0f;
                    continue;
                }
            }
            if (type.Outline.IsDreadnaught)
            {
                Vector2 intended = ObstacleField.Xz(direction);
                SoldierFacing.Turn(units, i, intended, turnSteps[units.Type[i]]);
                Vector2 heading = units.Facing[i];
                travel *= Mathf.Max(0f, heading.Dot(intended));
                direction = new Vector3(heading.X, 0f, heading.Y);
                units.Dirty[i] = true;
            }
            Vector3 next = units.Position[i] + direction * travel;
            if (obstacles != null)
            {
                Vector2 from = ObstacleField.Xz(units.Position[i]);
                Vector2 desired = ObstacleField.Xz(next);
                Vector2 safe = obstacles.ClipMove(from, desired, type.Footprint);
                if (safe != desired)
                {
                    travel = from.DistanceTo(safe);
                    speed = travel / dt;
                    next.X = safe.X;
                    next.Z = safe.Y;
                }
            }
            if (!movementMode) units.MoveSpeed[i] = !routeCorner && travel >= remaining ? 0f : speed;
            if (voluntaryMovement != null && travel > 0.00001f && (type.Ranged || travel >= advanceSpeed * dt * 0.5f))
                voluntaryMovement[i] = 1;
            // Circle infantry is rotationally symmetric; skip heading work entirely.
            if (travel > 0.00001f && type.Shape != UnitShape.Circle && !type.Outline.IsDreadnaught)
                SoldierFacing.Turn(units, i, new Vector2(direction.X, direction.Z), turnSteps[units.Type[i]]);
            units.Position[i] = next;
            units.Grounding?.MarkMoved(i);
            units.Dirty[i] = true;
        }
    }
}
