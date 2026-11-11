// Shared movement pass for live combat targets and movement-only march lanes.
using Godot;
namespace MassiveRts;

public static class MovementSystem
{
    private const float RangedStandOffBuffer = 0.25f;
    // =========================================================
    public static void Step(UnitState units, BenchmarkMode mode, float dt, float separationGap,
        byte[] voluntaryMovement, float meleeRampSeconds, float rangedRampSeconds)
    {
        var catalog = units.Catalog;
        bool movementMode = mode == BenchmarkMode.Movement;
        float meleeRampRate = meleeRampSeconds > 0f ? 1f / meleeRampSeconds : 0f;
        float rangedRampRate = rangedRampSeconds > 0f ? 1f / rangedRampSeconds : 0f;
        for (int i = 0; i < units.Count; i++)
        {
            if (voluntaryMovement != null) voluntaryMovement[i] = 0;
            if (units.Health[i] <= 0) continue;
            int target = units.Target[i];
            bool hasTarget = target >= 0 && units.Health[target] > 0;
            bool followingLastPosition = !hasTarget && !movementMode && target >= 0 &&
                (units.Faction[i] == 0 ? units.Red : units.Blue) > 0;
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
            if (hasTarget)
            {
                ref readonly UnitStats defender = ref catalog[units.Type[target]];
                float reach = type.AttackReach(defender);
                if (rangedCombat && units.MoveSpeed[i] == 0f && offset.LengthSquared() <= reach * reach) continue;
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
            offset.Y = 0f;
            bool marchingInLane = movementMode && offset.Z == 0f;
            float distance = marchingInLane ? Mathf.Abs(offset.X) : offset.Length();
            if (distance <= stop)
            {
                // Movement-only mode continuously marches back and forth along separate lanes.
                if (movementMode) units.Goal[i].X = -units.Goal[i].X;
                else units.MoveSpeed[i] = 0f;
                continue;
            }
            float remaining = distance - stop;
            float advanceSpeed = type.Speed;
            float speed = advanceSpeed;
            float rampRate = type.Ranged ? rangedRampRate : meleeRampRate;
            if (!movementMode && rampRate > 0f)
            {
                float acceleration = type.Speed * rampRate;
                advanceSpeed = Mathf.Min(type.Speed, units.MoveSpeed[i] + acceleration * dt);
                // Limit speed near the stopping point. Most soldiers can skip the square root.
                float brakingSpeedSquared = 2f * acceleration * remaining;
                speed = advanceSpeed * advanceSpeed > brakingSpeedSquared
                    ? Mathf.Sqrt(brakingSpeedSquared) : advanceSpeed;
            }
            float travel = Mathf.Min(speed * dt, remaining);
            if (!movementMode) units.MoveSpeed[i] = travel >= remaining ? 0f : speed;
            if (voluntaryMovement != null && travel > 0.00001f && (type.Ranged || travel >= advanceSpeed * dt * 0.5f))
                voluntaryMovement[i] = 1;
            if (marchingInLane)
                units.Position[i].X += offset.X < 0f ? -travel : travel;
            else
                units.Position[i] += offset / distance * travel;
            units.Position[i].Y = BattlefieldHeight.At(units.Position[i].X, units.Position[i].Z);
            units.Dirty[i] = true;
        }
    }
}
