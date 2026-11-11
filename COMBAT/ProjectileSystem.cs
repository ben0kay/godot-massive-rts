// Fixed-capacity projectile pool; swept hits against opposing soldiers, with swap removal.
using Godot;
namespace MassiveRts;

public sealed class ProjectileSystem
{
    public readonly Vector3[] Position;
    private readonly Vector3[] velocity;
    private readonly float[] lifetime, damage;
    private readonly byte[] enemyFaction;
    public int Count { get; private set; }
    public int Dropped { get; private set; }
    public bool Dirty { get; set; }
    public ProjectileSystem(int capacity)
    { Position = new Vector3[capacity]; velocity = new Vector3[capacity]; lifetime = new float[capacity]; damage = new float[capacity]; enemyFaction = new byte[capacity]; }
    // =========================================================
    public void Fire(int shooter, int victim, UnitState units, BenchmarkConfig config)
    {
        if (Count == Position.Length) { Dropped++; return; }
        ref readonly UnitStats type = ref units.Catalog[units.Type[shooter]];
        int i = Count++;
        Position[i] = units.Position[shooter] + Vector3.Up * (type.Height * 0.5f);
        Vector3 aim = units.Position[victim] + Vector3.Up * (units.Catalog[units.Type[victim]].Height * 0.5f);
        velocity[i] = (aim - Position[i]).Normalized() * type.ProjectileSpeed;
        damage[i] = type.Damage;
        enemyFaction[i] = units.Faction[victim]; lifetime[i] = config.ProjectileLifetime; Dirty = true;
    }
    // =========================================================
    public void Step(UnitState units, SpatialGrid grid, BenchmarkConfig config, float dt)
    {
        if (Count == 0) return;
        Dirty = true;
        float movementPadding = units.Catalog.MaxSpeed * dt;
        for (int i = Count - 1; i >= 0; i--)
        {
            Vector3 from = Position[i];
            Vector3 segment = velocity[i] * Mathf.Min(dt, lifetime[i]);
            int victim = grid.FirstProjectileHit(units, enemyFaction[i], from, segment, config.ProjectileHitRadius, movementPadding);
            if (victim >= 0) units.Damage(victim, damage[i], config.FlashSeconds);
            else
            {
                Position[i] += segment;
                lifetime[i] -= dt;
                if (lifetime[i] > 0) continue;
            }
            int last = --Count;
            Position[i] = Position[last]; velocity[i] = velocity[last]; lifetime[i] = lifetime[last]; damage[i] = damage[last]; enemyFaction[i] = enemyFaction[last];
        }
    }
}
