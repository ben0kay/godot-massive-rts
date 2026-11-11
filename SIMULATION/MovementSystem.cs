// Shared movement pass for live combat targets and movement-only march lanes.
using Godot;
namespace MassiveRts;

public static class MovementSystem
{
    // =========================================================
    public static void Step(UnitState units, BenchmarkMode mode, float dt)
    {
        var catalog = units.Catalog;
        bool movementMode = mode == BenchmarkMode.Movement;
        for (int i = 0; i < units.Count; i++)
        {
            if (units.Health[i] <= 0) continue;
            if (units.Flash[i] > 0) { units.Flash[i] = Mathf.Max(0, units.Flash[i] - dt); units.Dirty[i] = true; }
            int target = units.Target[i];
            bool hasTarget = target >= 0 && units.Health[target] > 0;
            bool followingLastPosition = !hasTarget && !movementMode && target >= 0 &&
                (units.Faction[i] == 0 ? units.Red : units.Blue) > 0;
            if (!hasTarget && !movementMode && !followingLastPosition) continue;
            Vector3 destination = movementMode ? units.Goal[i] : units.Position[target];
            Vector3 offset = destination - units.Position[i]; offset.Y = 0;
            ref readonly UnitStats type = ref catalog[units.Type[i]];
            float stop = 0.2f;
            if (hasTarget)
            {
                ref readonly UnitStats defender = ref catalog[units.Type[target]];
                float reach = type.AttackReach(defender);
                stop = type.Ranged ? reach * 0.85f :
                    Mathf.Min(reach * 0.99f, Mathf.Max(reach * 0.85f, type.Footprint + defender.Footprint));
            }
            bool marchingInLane = movementMode && offset.Z == 0f;
            float distance = marchingInLane ? Mathf.Abs(offset.X) : offset.Length();
            if (distance <= stop)
            {
                // Movement-only mode continuously marches back and forth along separate lanes.
                if (movementMode) units.Goal[i].X = -units.Goal[i].X;
                continue;
            }
            float travel = Mathf.Min(type.Speed * dt, distance - stop);
            if (marchingInLane)
                units.Position[i].X += offset.X < 0f ? -travel : travel;
            else
                units.Position[i] += offset / distance * travel;
            units.Position[i].Y = BattlefieldHeight.At(units.Position[i].X, units.Position[i].Z);
            units.Dirty[i] = true;
        }
    }
}
