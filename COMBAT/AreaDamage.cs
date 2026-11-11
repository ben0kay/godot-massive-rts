// Shared XZ area attacks using the existing faction grid and authoritative damage path.
using Godot;

namespace MassiveRts;

public static class AreaDamage
{
    public static void Cleave(UnitState units, SpatialGrid grid, int attacker, int primary,
        in UnitStats type, Vector2 direction, float separationGap, float tolerance, float movementPadding, ObstacleField obstacles = null)
    {
        Vector3 origin = units.Position[attacker];
        units.Damage(primary, type.Damage);
        if (direction == Vector2.Zero || type.CleaveSecondaryFraction <= 0f) return;

        int sourceFaction = units.Faction[attacker];
        float queryRadius = CombatSystem.MeleeContact(type.Footprint, units.MaxFootprint, separationGap) + tolerance + movementPadding;
        int first = -1, second = -1;
        float firstDistance = float.PositiveInfinity, secondDistance = float.PositiveInfinity;

        var candidates = grid.GetCandidates(origin, queryRadius, sourceFaction);
        while (candidates.MoveNext())
        {
            int id = candidates.Current;
            if (id == primary || units.Health[id] <= 0f) continue;
            Vector3 offset = units.Position[id] - origin;
            float distance = offset.LengthSquared();
            float reach = CombatSystem.MeleeContact(type.Footprint, units.Catalog[units.Type[id]].Footprint, separationGap) + tolerance;
            if (distance > reach * reach) continue;
            float planarDistance = offset.X * offset.X + offset.Z * offset.Z;
            float forward = direction.X * offset.X + direction.Y * offset.Z;
            if (forward < 0f || forward * forward < planarDistance * type.CleaveCosineSquared) continue;

            if (distance > secondDistance || obstacles?.BlocksSight(origin, units.Position[id]) == true) continue;

            if (distance < firstDistance || (distance == firstDistance && id < first))
            {
                second = first;
                secondDistance = firstDistance;
                first = id;
                firstDistance = distance;
            }
            else if (distance < secondDistance || (distance == secondDistance && id < second))
            {
                second = id;
                secondDistance = distance;
            }
        }

        float secondaryDamage = type.Damage * type.CleaveSecondaryFraction;
        if (first >= 0) units.Damage(first, secondaryDamage);
        if (second >= 0) units.Damage(second, secondaryDamage);
    }

    // Reusable impact circle. Caller supplies maximum displacement since the grid rebuild.
    public static void Radial(UnitState units, SpatialGrid grid, int sourceFaction, Vector3 impact,
        float radius, float damage, float edgeDamageFraction, float movementPadding, ObstacleField cover = null)
    {
        if (!float.IsFinite(radius) || radius <= 0f || damage <= 0f) return;
        float edge = Mathf.Clamp(edgeDamageFraction, 0f, 1f);
        float radiusSquared = radius * radius;
        var candidates = grid.GetCandidates(impact, radius + movementPadding, sourceFaction);
        while (candidates.MoveNext())
        {
            int id = candidates.Current;
            if (units.Health[id] <= 0f) continue;
            Vector3 offset = units.Position[id] - impact;
            float distanceSquared = offset.X * offset.X + offset.Z * offset.Z;
            if (distanceSquared > radiusSquared || cover?.BlocksSight(impact, units.Position[id]) == true) continue;
            float fraction = Mathf.Sqrt(distanceSquared) / radius;
            units.Damage(id, damage * Mathf.Lerp(1f, edge, fraction));
        }
    }
}
