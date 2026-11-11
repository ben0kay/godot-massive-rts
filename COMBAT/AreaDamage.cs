// Shared XZ area attacks using the existing faction grid and authoritative damage path.
using Godot;

namespace MassiveRts;

public static class AreaDamage
{
    public static void Cleave(UnitState units, SpatialGrid grid, int attacker, int primary,
        in UnitStats type, float movementPadding)
    {
        Vector3 origin = units.Position[attacker];
        Vector3 aim = units.Position[primary] - origin;
        Vector2 direction = new Vector2(aim.X, aim.Z).Normalized();
        units.Damage(primary, type.Damage);
        if (direction == Vector2.Zero || type.CleaveSecondaryFraction <= 0f) return;

        int enemy = 1 - units.Faction[attacker];
        float queryRadius = type.Reach + units.Catalog.MaxFootprint + movementPadding;
        int first = -1, second = -1;
        float firstDistance = float.PositiveInfinity, secondDistance = float.PositiveInfinity;

        var candidates = grid.GetCandidates(origin, queryRadius, enemy);
        while (candidates.MoveNext())
        {
            int id = candidates.Current;
            if (id == primary || units.Health[id] <= 0f) continue;
            Vector3 offset = units.Position[id] - origin;
            float distance = offset.LengthSquared();
            float reach = type.AttackReach(units.Catalog[units.Type[id]]);
            if (distance > reach * reach) continue;
            float planarDistance = offset.X * offset.X + offset.Z * offset.Z;
            float forward = direction.X * offset.X + direction.Y * offset.Z;
            if (forward < 0f || forward * forward < planarDistance * type.CleaveCosineSquared) continue;

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
        float radius, float damage, float edgeDamageFraction, float movementPadding)
    {
        if (!float.IsFinite(radius) || radius <= 0f || damage <= 0f) return;
        float edge = Mathf.Clamp(edgeDamageFraction, 0f, 1f);
        float radiusSquared = radius * radius;
        var candidates = grid.GetCandidates(impact, radius + movementPadding, 1 - sourceFaction);
        while (candidates.MoveNext())
        {
            int id = candidates.Current;
            if (units.Health[id] <= 0f) continue;
            Vector3 offset = units.Position[id] - impact;
            float distanceSquared = offset.X * offset.X + offset.Z * offset.Z;
            if (distanceSquared > radiusSquared) continue;
            float fraction = Mathf.Sqrt(distanceSquared) / radius;
            units.Damage(id, damage * Mathf.Lerp(1f, edge, fraction));
        }
    }
}
