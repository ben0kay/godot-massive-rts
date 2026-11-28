// Charges individual Prism weapons and resolves a bounded, one-tick piercing discharge.
using System;
using Godot;

namespace MassiveRts;

public readonly record struct PrismBeamStats(bool Enabled, float ChargeSeconds, float Radius, int MaxHits, float FlashSeconds)
{
    internal const int MaxHitCapacity = 64;
}

internal static class PrismLanceCombat
{
    internal const int ShotsPerTick = 32;
    private readonly record struct Hit(int Id, float Distance);

    internal static bool Step(UnitState units, SpatialGrid grid, ProjectileSystem effects, ObstacleField cover,
        int id, in UnitStats type, float dt, float padding, bool interrupted, ref int budget)
    {
        int target = units.Target[id];
        if (interrupted || units.Cooldown[id] > 0f || !type.TargetsUnits || target < 0 ||
            units.Health[target] <= 0f || units.Faction[id] == units.Faction[target])
            return units.SetPrismCharge(id, 0f);

        Vector3 origin = units.Position[id];
        Vector3 targetPosition = units.Position[target];
        Vector2 offset = ObstacleField.Xz(targetPosition - origin);
        float distance = offset.Length();
        float reach = units.RangedReach(id, type);
        float threshold = units.PrismCharge(id) > 0f ? 0.06f : 0f;
        if (distance <= 0.001f || distance < type.MinimumFireRange - threshold || distance > reach + threshold)
            return units.SetPrismCharge(id, 0f);

        AimSegment(units, id, target, type, out Vector3 muzzle, out Vector3 toTarget, out float muzzleOffset);
        if (Clip(units, cover, muzzle, toTarget) < 0.9999f)
            return units.SetPrismCharge(id, 0f);

        Vector2 before = units.Facing[id];
        bool aligned = SoldierFacing.Aim(units, id, offset, dt, 0.9998f);
        bool changed = before != units.Facing[id];
        if (!aligned) return changed || units.PrismCharge(id) > 0f;
        float charge = Mathf.Min(type.PrismBeam.ChargeSeconds, units.PrismCharge(id) + dt);
        changed |= units.SetPrismCharge(id, charge);
        if (charge < type.PrismBeam.ChargeSeconds || budget == 0) return changed;

        // Aim exactly through the selected enemy at discharge; the facing test keeps the snap small.
        Vector3 segment = toTarget * (Mathf.Max(0f, reach - muzzleOffset) / (distance - muzzleOffset));
        segment *= Clip(units, cover, muzzle, segment);
        budget--;
        Strike(units, grid, id, type, muzzle, segment, padding);
        units.RevealFromTrench(id);
        effects.EmitPrismBeam(muzzle, muzzle + segment, units.Faction[id], type.PrismBeam.FlashSeconds);
        units.HitFeedback?.OnAttack(units, id);
        units.RangedRecoil?.OnFire(units, id, target);
        units.SetPrismCharge(id, 0f);
        units.Cooldown[id] = type.Interval;
        return true;
    }

    internal static bool HasClearAim(UnitState units, int id, int target, in UnitStats type, ObstacleField cover)
    {
        AimSegment(units, id, target, type, out Vector3 muzzle, out Vector3 segment, out _);
        return Clip(units, cover, muzzle, segment) >= 0.9999f;
    }

    private static void AimSegment(UnitState units, int id, int target, in UnitStats type,
        out Vector3 muzzle, out Vector3 segment, out float muzzleOffset)
    {
        Vector3 from = units.Position[id], to = units.Position[target];
        Vector2 offset = ObstacleField.Xz(to - from);
        float length = offset.Length();
        muzzleOffset = Mathf.Min(type.Outline.RadiusZ * 0.92f, length * 0.5f);
        Vector2 forward = length > 0.001f ? offset * (muzzleOffset / length) : Vector2.Zero;
        muzzle = from + new Vector3(forward.X, type.Height * 0.85f, forward.Y);
        segment = to + Vector3.Up * (units.Catalog[units.Type[target]].Height * 0.5f) - muzzle;
    }

    private static float Clip(UnitState units, ObstacleField cover, Vector3 from, Vector3 segment)
    {
        float end = cover != null && cover.FirstHit(ObstacleField.Xz(from), ObstacleField.Xz(from + segment), 0f, out float hit)
            ? hit : 1f;
        return PiercingBeam.ClipTerrain(units.Grounding?.Field, from, segment, end);
    }

    private static void Strike(UnitState units, SpatialGrid grid, int shooter, in UnitStats type,
        Vector3 from, Vector3 segment, float padding)
    {
        Vector2 flat = ObstacleField.Xz(segment);
        float reach = flat.Length();
        if (reach <= 0.001f) return;
        Vector2 along = flat / reach;
        float slope = segment.Y / reach, radius = type.PrismBeam.Radius;
        byte faction = units.Faction[shooter];
        var candidates = grid.GetSegmentCandidates(from, segment, units.MaxFootprint + radius + padding, faction);
        Span<Hit> closest = stackalloc Hit[type.PrismBeam.MaxHits];
        int count = 0;
        while (candidates.MoveNext())
        {
            int id = candidates.Current;
            if (units.Health[id] <= 0f || units.Faction[id] == faction) continue;
            ref readonly var defender = ref units.Catalog[units.Type[id]];
            if (!PiercingBeam.Intersect(units.Position[id] - from, along, reach, slope, defender.Footprint,
                defender.Height, radius, out float distance)) continue;
            int index = count;
            if (index == closest.Length)
            {
                Hit last = closest[index - 1];
                if (distance > last.Distance || distance == last.Distance && id >= last.Id) continue;
                index--;
            }
            else count++;
            while (index > 0)
            {
                Hit previous = closest[index - 1];
                if (previous.Distance < distance || previous.Distance == distance && previous.Id < id) break;
                closest[index--] = previous;
            }
            closest[index] = new Hit(id, distance);
        }
        float damage = type.AgainstUnits(type.Damage) * units.AttackMultiplier(shooter);
        for (int i = 0; i < count; i++) units.Damage(closest[i].Id, damage, type.UnitDamageType);
    }
}
