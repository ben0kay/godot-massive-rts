// Tracks independent short-range weapons attached to a rotating primary turret.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal readonly record struct RoofWeapon(MountedRocket Weapon, Vector3 Muzzle, float TurnSpeed, float Tolerance);

internal sealed class RoofWeaponState
{
    private struct State
    {
        public int Target;
        public float PreviousYaw, Yaw, Reload, SearchDelay;
    }

    private readonly Dictionary<int, State> states = new();

    internal void Remove(int id) => states.Remove(id);
    internal bool IsTurning(int id) => states.TryGetValue(id, out var state) && state.PreviousYaw != state.Yaw;

    internal float WorldYaw(int id, float fallback, float blend) =>
        states.TryGetValue(id, out var state) ? Mathf.LerpAngle(state.PreviousYaw, state.Yaw, blend) : fallback;

    internal bool Step(UnitState units, int id, in PrimaryTurret primary, in RoofWeapon roof, SpatialGrid grid,
        ProjectileSystem projectiles, ObstacleField obstacles, float dt, float fireRate)
    {
        float primaryYaw = units.PrimaryTurrets?.WorldYaw(units, id, primary) ??
            PrimaryTurretState.HullYaw(units, id) + primary.RestYaw;
        Vector3 pivot = TurretAiming.Mount(units, id, primary.Weapon.Pose.Origin) +
            TurretAiming.Rotation(primaryYaw) * roof.Weapon.Pose.Origin;
        if (!states.TryGetValue(id, out var state))
            state = new State { Target = -1, Yaw = primaryYaw, SearchDelay = (id % 12) * dt };
        state.PreviousYaw = state.Yaw;
        state.Reload -= dt;
        state.SearchDelay -= dt;
        float range = roof.Weapon.MaximumRange * units.ForestRangeMultiplier(units.Position[id]) * units.AttackMultiplier(id);
        float minSquared = roof.Weapon.MinimumRange * roof.Weapon.MinimumRange;
        float maxSquared = range * range;
        byte faction = units.Faction[id];
        bool Eligible(int target, float limit) => target >= 0 && target != id && units.Health[target] > 0f &&
            units.Faction[target] != faction && !units.IsAirborne(target) &&
            pivot.DistanceSquaredTo(units.Position[target]) >= minSquared &&
            pivot.DistanceSquaredTo(units.Position[target]) <= limit &&
            units.CanDetectGroundTarget(pivot, target);

        Vector3 AimPoint(int target) => units.Position[target] + Vector3.Up * (units.Catalog[units.Type[target]].Height * .5f);
        bool Clear(Vector3 from, Vector3 to) => obstacles?.BlocksSight(from, to) != true &&
            units.Trenches?.BlocksFire(from, to) != true;

        if (!Eligible(state.Target, maxSquared) || (state.SearchDelay <= 0f && !Clear(pivot, AimPoint(state.Target))))
        {
            state.Target = -1;
            if (state.SearchDelay <= 0f)
            {
                state.SearchDelay = .2f;
                float nearest = maxSquared;
                int budget = 32;
                var candidates = grid.GetCandidates(pivot, range, faction);
                while (budget-- > 0 && candidates.MoveNext())
                {
                    int candidate = candidates.Current;
                    if (!Eligible(candidate, nearest) || !Clear(pivot, AimPoint(candidate))) continue;
                    nearest = pivot.DistanceSquaredTo(units.Position[candidate]);
                    state.Target = candidate;
                }
            }
        }

        if (state.Target >= 0)
        {
            Vector3 aim = AimPoint(state.Target);
            float desired = TurretAiming.Yaw(ObstacleField.Xz(aim - pivot));
            TurretAiming.Turn(ref state.Yaw, desired, roof.TurnSpeed * dt);
            if (state.Reload <= 0f && Mathf.Abs(TurretAiming.Wrap(desired - state.Yaw)) <= roof.Tolerance)
            {
                Vector3 muzzle = pivot + TurretAiming.Rotation(state.Yaw) * roof.Muzzle;
                if (Clear(muzzle, aim) && projectiles.LaunchFrom(muzzle, aim, faction, roof.Weapon.Projectile,
                    roof.Weapon.Damage * units.AttackMultiplier(id), DamageRules.NeutralType))
                {
                    state.Reload = roof.Weapon.Interval / Mathf.Max(.01f, fireRate);
                    units.RevealFromTrench(id);
                    projectiles.EmitMuzzleSmoke(muzzle, .22f);
                }
            }
        }
        if (state.SearchDelay <= 0f) state.SearchDelay = .2f;
        states[id] = state;
        if (state.PreviousYaw == state.Yaw) return false;
        return units.Dirty[id] = true;
    }
}
