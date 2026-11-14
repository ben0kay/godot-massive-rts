// Shared movement pass for live combat targets and movement-only march lanes.
using System.Diagnostics;
using Godot;
namespace MassiveRts;

public static class MovementSystem
{
    private const float RangedStandOffBuffer = 0.25f;
    // =========================================================
    public static void Step(UnitState units, BenchmarkMode mode, float dt, float separationGap,
        ref MovementProfile profile, int samplePhase, byte[] voluntaryMovement, float meleeRampSeconds, float rangedRampSeconds, TrampleSystem trample = null,
        ObstacleField obstacles = null, ObstacleNavigation navigation = null, SpatialGrid grid = null, MeleeSteering melee = null, int end = -1, int[] activeIds = null, CrowdMovement crowd = default)
    {
        var catalog = units.Catalog;
        Vector2 turnStep = SoldierFacing.TurnStep(dt);
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
            bool hasTarget = target >= 0 && units.Health[target] > 0;
            bool followingLastPosition = !hasTarget && !movementMode && target >= 0 &&
                units.Alive > units.FactionAlive[units.Faction[i]];
            if (!hasTarget && !movementMode && !followingLastPosition)
            {
                units.MoveSpeed[i] = 0f;
                continue;
            }
            ref readonly UnitStats type = ref catalog[units.Type[i]];
            bool rangedCombat = !movementMode && type.Ranged;
            bool steerMelee = melee != null && !movementMode && !type.Ranged && hasTarget;
            if (!hasTarget) melee?.Reset(i);
            // Hold the firing line until there is a living target worth moving toward.
            if (rangedCombat && !hasTarget && units.MoveSpeed[i] == 0f) continue;
            Vector3 destination = movementMode ? units.Goal[i] : units.Position[target];
            Vector3 offset = destination - units.Position[i];
            float stop = 0.2f;
            bool hasClearShot = false;
            if (hasTarget)
            {
                ref readonly UnitStats defender = ref catalog[units.Type[target]];
                float reach = type.Ranged ? type.Reach :
                    CombatSystem.MeleeContact(type.Footprint, defender.Footprint, separationGap);
                float distanceSquared = offset.LengthSquared();
                bool inRange = (rangedCombat || obstacles != null) && distanceSquared <= reach * reach;
                float sightRange = steerMelee ? reach + 0.075f : reach;
                bool blockedInRange = obstacles != null && distanceSquared <= sightRange * sightRange &&
                    (!type.Ranged || catalog.Projectiles.Flight(type.ProjectileType).ObstacleFire == ObstacleFireMode.RequiresDirectLineOfSight) &&
                    obstacles.BlocksSight(units.Position[i], destination);
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
            float advanceSpeed = type.Speed;
            float speed = advanceSpeed;
            float rampRate = type.Ranged ? rangedRampRate : meleeRampRate;
            float accelerationRate = type.AccelerationRateOverride > 0f ? type.AccelerationRateOverride : rampRate;
            if (!movementMode && accelerationRate > 0f)
            {
                advanceSpeed = Mathf.Min(type.Speed, units.MoveSpeed[i] + type.Speed * accelerationRate * dt);
                // Limit speed near the stopping point. Most soldiers can skip the square root.
                float brakingSpeedSquared = 2f * type.Speed * rampRate * remaining;
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
            if (crowd.Enabled)
            {
                Vector2 from = ObstacleField.Xz(units.Position[i]);
                Vector2 desired = ObstacleField.Xz(next);
                Vector2 safe = crowd.Clip(units, i, from, desired, type.Footprint);
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
            if (travel > 0.00001f && type.Shape != UnitShape.Circle)
                SoldierFacing.Turn(units, i, new Vector2(direction.X, direction.Z), turnStep);
            units.Position[i] = next;
            units.Grounding?.MarkMoved(i);
            units.Dirty[i] = true;
        }
    }
}
