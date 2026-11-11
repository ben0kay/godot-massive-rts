// Shared movement pass for live combat targets and movement-only march lanes.
using Godot;
namespace MassiveRts;

public static class MovementSystem
{
    private const float RangedStandOffBuffer = 0.25f;
    // =========================================================
    public static void Step(UnitState units, BenchmarkMode mode, float dt, float separationGap,
        byte[] voluntaryMovement, float meleeRampSeconds, float rangedRampSeconds, TrampleSystem trample = null,
        ObstacleField obstacles = null, ObstacleNavigation navigation = null)
    {
        var catalog = units.Catalog;
        Vector2 turnStep = SoldierFacing.TurnStep(dt);
        bool movementMode = mode == BenchmarkMode.Movement;
        float meleeRampRate = meleeRampSeconds > 0f ? 1f / meleeRampSeconds : 0f;
        float rangedRampRate = rangedRampSeconds > 0f ? 1f / rangedRampSeconds : 0f;
        for (int i = 0; i < units.Count; i++)
        {
            if (voluntaryMovement != null) voluntaryMovement[i] = 0;
            if (units.Health[i] <= 0 || trample?.OverridesMovement(i) == true) continue;
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
            // Hold the firing line until there is a living target worth moving toward.
            if (rangedCombat && !hasTarget && units.MoveSpeed[i] == 0f) continue;
            Vector3 destination = movementMode ? units.Goal[i] : units.Position[target];
            Vector3 offset = destination - units.Position[i];
            float stop = 0.2f;
            bool hasClearShot = false;
            if (hasTarget)
            {
                ref readonly UnitStats defender = ref catalog[units.Type[target]];
                float reach = type.AttackReach(defender);
                bool inRange = (rangedCombat || obstacles != null) && offset.LengthSquared() <= reach * reach;
                bool blockedInRange = inRange && obstacles != null &&
                    (!type.Ranged || catalog.Projectiles.Flight(type.ProjectileType).ObstacleFire == ObstacleFireMode.RequiresDirectLineOfSight) &&
                    obstacles.BlocksSight(units.Position[i], destination);
                hasClearShot = inRange && !blockedInRange;
                if (rangedCombat && units.MoveSpeed[i] == 0f && hasClearShot) continue;
                // Once advancing, settle slightly inside range so small target changes
                // can be absorbed without another step. Combat still uses exact reach.
                stop = rangedCombat ? reach - Mathf.Min(RangedStandOffBuffer, reach * 0.15f) :
                    Mathf.Min(reach * 0.99f, Mathf.Max(reach * 0.85f, type.Footprint + defender.Footprint));
                if (!type.Ranged && separationGap >= 0f)
                {
                    // Hold once in reach so front-line soldiers do not continually
                    // advance into packed bodies just to reach an inner stop distance.
                    stop = Mathf.Max(stop, reach - 0.02f);
                }
            }
            bool detouring = false, routeCorner = false;
            if (navigation != null)
            {
                if (hasClearShot && offset.X * offset.X + offset.Z * offset.Z <= stop * stop)
                {
                    units.MoveSpeed[i] = 0f;
                    continue;
                }
                Vector2 waypoint = navigation.Steer(i, units.Type[i], ObstacleField.Xz(units.Position[i]),
                    ObstacleField.Xz(destination), hasTarget ? stop : 0f, out detouring, out routeCorner);
                if (detouring)
                {
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
            if (travel > 0.00001f && type.Shape != UnitShape.Circle)
                SoldierFacing.Turn(units, i, new Vector2(direction.X, direction.Z), turnStep);
            units.Position[i] = next;
            units.Position[i].Y = BattlefieldHeight.At(units.Position[i].X, units.Position[i].Z);
            units.Dirty[i] = true;
        }
    }
}
