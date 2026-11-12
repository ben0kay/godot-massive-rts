// Fixed-capacity projectile pool with swept arrow and explosive shell impacts.
using Godot;

namespace MassiveRts;

public sealed class ProjectileSystem
{
    public readonly Vector3[] Position, Direction;
    public readonly int[] Type;
    private readonly Vector3[] origin, displacement;
    private readonly float[] elapsed, duration, arc, damage;
    private readonly byte[] sourceFaction;
    private readonly ProjectileCatalog catalog;
    private readonly float lifetime;
    public int Count { get; private set; }
    public int Dropped { get; private set; }
    public bool Dirty { get; set; }

    public ProjectileSystem(int capacity, ProjectileCatalog catalog, float lifetime)
    {
        this.catalog = catalog;
        this.lifetime = lifetime;
        Position = new Vector3[capacity]; Direction = new Vector3[capacity];
        origin = new Vector3[capacity]; displacement = new Vector3[capacity];
        elapsed = new float[capacity]; duration = new float[capacity]; arc = new float[capacity];
        damage = new float[capacity]; Type = new int[capacity]; sourceFaction = new byte[capacity];
    }

    public void Fire(int shooter, int victim, UnitState units)
    {
        if (Count == Position.Length) { Dropped++; return; }
        ref readonly UnitStats type = ref units.Catalog[units.Type[shooter]];
        ProjectileFlight flight = catalog.Flight(type.ProjectileType);
        int i = Count++;
        origin[i] = Position[i] = units.Position[shooter] + Vector3.Up * (type.Height * 0.5f);
        if (type.Outline.IsChevron)
        {
            Vector2 heading = units.Facing[shooter];
            origin[i] = Position[i] = units.Position[shooter] +
                new Vector3(heading.X * type.Outline.BarrelFront, type.Height * 0.82f, heading.Y * type.Outline.BarrelFront);
        }
        Vector3 aim = units.Position[victim];
        if (flight.ExplosionRadius <= 0f) aim.Y += units.Catalog[units.Type[victim]].Height * 0.5f;
        Vector3 path = aim - origin[i];
        displacement[i] = path;
        float distance = path.Length();
        float horizontal = new Vector2(path.X, path.Z).Length();
        arc[i] = Mathf.Min(flight.ArcHeight, horizontal * flight.ArcHeightPerDistance);
        duration[i] = Mathf.Max(0.0001f, distance / flight.Speed);
        elapsed[i] = 0;
        Direction[i] = path + Vector3.Up * (4f * arc[i]);
        damage[i] = type.Damage;
        Type[i] = type.ProjectileType;
        sourceFaction[i] = units.Faction[shooter];
        Dirty = true;
    }

    public void Step(UnitState units, SpatialGrid grid, BenchmarkConfig config, float dt, float separationPadding, ObstacleField obstacles = null, ShockwaveRenderer rings = null)
    {
        if (Count == 0) return;
        Dirty = true;
        float movementPadding = units.Catalog.MaxSpeed * dt + separationPadding;
        for (int i = Count - 1; i >= 0; i--)
        {
            float endTime = Mathf.Min(duration[i], lifetime);
            elapsed[i] = Mathf.Min(elapsed[i] + dt, endTime);
            float t = elapsed[i] / duration[i];
            Vector3 next = origin[i] + displacement[i] * t + Vector3.Up * (4f * arc[i] * t * (1f - t));
            Vector3 segment = next - Position[i];
            ProjectileFlight flight = catalog.Flight(Type[i]);
            if (flight.ExplosionRadius > 0f)
            {
                if (ImpactShell(i, units, grid, config, movementPadding, obstacles, rings, flight, segment))
                {
                    Remove(i);
                    continue;
                }
            }
            else
            {
                float wallHit = float.PositiveInfinity;
                bool blocked = obstacles != null && flight.BlocksOnObstacles &&
                    obstacles.FirstHit(ObstacleField.Xz(Position[i]), ObstacleField.Xz(next), 0f, out wallHit);
                int victim = wallHit > 0f
                    ? grid.FirstProjectileHit(units, sourceFaction[i], Position[i], segment, config.ProjectileHitRadius, movementPadding, out _, wallHit)
                    : -1;
                if (victim >= 0 || blocked)
                {
                    if (victim >= 0) units.Damage(victim, damage[i]);
                    Remove(i);
                    continue;
                }
            }
            if (elapsed[i] >= endTime)
            {
                Remove(i);
                continue;
            }
            Position[i] = next;
            Direction[i] = displacement[i] + Vector3.Up * (4f * arc[i] * (1f - 2f * t));
        }
    }

    // =========================================================
    // Resolve the first contact before querying the explosion radius.
    private bool ImpactShell(int index, UnitState units, SpatialGrid grid, BenchmarkConfig config,
        float movementPadding, ObstacleField obstacles, ShockwaveRenderer rings, ProjectileFlight flight, Vector3 segment)
    {
        Vector3 from = Position[index];
        var terrain = units.Grounding?.Field;
        float hit = flight.BlocksOnObstacles
            ? obstacles?.AirborneHit(from, segment, config.ObstacleHeight) ?? float.PositiveInfinity
            : float.PositiveInfinity;
        float groundHit = terrain != null
            ? terrain.SurfaceHit(from, segment, Mathf.Min(hit, 1f))
            : (from.Y + segment.Y <= 0f && segment.Y < 0f ? Mathf.Clamp(-from.Y / segment.Y, 0f, 1f) : float.PositiveInfinity);
        hit = Mathf.Min(hit, groundHit);
        grid.FirstProjectileHit(units, sourceFaction[index], from, segment, config.ProjectileHitRadius,
            movementPadding, out float firstHit, hit);
        hit = firstHit;
        // Completing the flight lands at the recorded ground aim point. Expiring in flight does not explode.
        if (hit > 1f && elapsed[index] < duration[index]) return false;
        Vector3 impact = from + segment * Mathf.Min(hit, 1f);
        AreaDamage.Radial(units, grid, sourceFaction[index], impact, flight.ExplosionRadius,
            damage[index], flight.ExplosionEdgeFraction, movementPadding);
        impact.Y = terrain?.At(impact.X, impact.Z) ?? 0f;
        rings?.Emit(impact, flight.ExplosionRadius);
        return true;
    }

    private void Remove(int index)
    {
        int last = --Count;
        if (index == last) return;
        Position[index] = Position[last]; Direction[index] = Direction[last];
        origin[index] = origin[last]; displacement[index] = displacement[last];
        elapsed[index] = elapsed[last]; duration[index] = duration[last]; arc[index] = arc[last];
        damage[index] = damage[last]; Type[index] = Type[last]; sourceFaction[index] = sourceFaction[last];
    }
}
