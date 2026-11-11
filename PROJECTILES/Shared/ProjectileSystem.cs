// Fixed-capacity projectile pool with shallow arcs, swept hits and packed removal.
using Godot;

namespace MassiveRts;

public sealed class ProjectileSystem
{
    public readonly Vector3[] Position, Direction;
    public readonly int[] Type;
    private readonly Vector3[] origin, displacement;
    private readonly float[] elapsed, duration, arc, damage;
    private readonly byte[] enemyFaction;
    private readonly ProjectileCatalog catalog;
    public int Count { get; private set; }
    public int Dropped { get; private set; }
    public bool Dirty { get; set; }

    public ProjectileSystem(int capacity, ProjectileCatalog catalog)
    {
        this.catalog = catalog;
        Position = new Vector3[capacity]; Direction = new Vector3[capacity];
        origin = new Vector3[capacity]; displacement = new Vector3[capacity];
        elapsed = new float[capacity]; duration = new float[capacity]; arc = new float[capacity];
        damage = new float[capacity]; Type = new int[capacity]; enemyFaction = new byte[capacity];
    }

    public void Fire(int shooter, int victim, UnitState units)
    {
        if (Count == Position.Length) { Dropped++; return; }
        ref readonly UnitStats type = ref units.Catalog[units.Type[shooter]];
        ProjectileFlight flight = catalog.Flight(type.ProjectileType);
        int i = Count++;
        origin[i] = Position[i] = units.Position[shooter] + Vector3.Up * (type.Height * 0.5f);
        Vector3 aim = units.Position[victim] + Vector3.Up * (units.Catalog[units.Type[victim]].Height * 0.5f);
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
        enemyFaction[i] = units.Faction[victim];
        Dirty = true;
    }

    public void Step(UnitState units, SpatialGrid grid, BenchmarkConfig config, float dt, float separationPadding)
    {
        if (Count == 0) return;
        Dirty = true;
        float movementPadding = units.Catalog.MaxSpeed * dt + separationPadding;
        for (int i = Count - 1; i >= 0; i--)
        {
            float endTime = Mathf.Min(duration[i], config.ProjectileLifetime);
            elapsed[i] = Mathf.Min(elapsed[i] + dt, endTime);
            float t = elapsed[i] / duration[i];
            Vector3 next = origin[i] + displacement[i] * t + Vector3.Up * (4f * arc[i] * t * (1f - t));
            Vector3 segment = next - Position[i];
            int victim = grid.FirstProjectileHit(units, enemyFaction[i], Position[i], segment, config.ProjectileHitRadius, movementPadding);
            if (victim >= 0) units.Damage(victim, damage[i]);
            else if (elapsed[i] < endTime)
            {
                Position[i] = next;
                Direction[i] = displacement[i] + Vector3.Up * (4f * arc[i] * (1f - 2f * t));
                continue;
            }
            Remove(i);
        }
    }

    private void Remove(int index)
    {
        int last = --Count;
        if (index == last) return;
        Position[index] = Position[last]; Direction[index] = Direction[last];
        origin[index] = origin[last]; displacement[index] = displacement[last];
        elapsed[index] = elapsed[last]; duration[index] = duration[last]; arc[index] = arc[last];
        damage[index] = damage[last]; Type[index] = Type[last]; enemyFaction[index] = enemyFaction[last];
    }
}
