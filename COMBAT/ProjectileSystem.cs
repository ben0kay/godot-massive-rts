// Fixed-capacity projectile pool; swept hits against opposing soldiers, with swap removal.
using Godot;
namespace MassiveRts;

public sealed class ProjectileSystem
{
    public readonly Vector3[] Position;
    private readonly Vector3[] velocity;
    private readonly float[] lifetime;
    private readonly byte[] enemyFaction;
    public int Count { get; private set; }
    public int Dropped { get; private set; }
    public bool Dirty { get; set; }
    public ProjectileSystem(int capacity)
    { Position = new Vector3[capacity]; velocity = new Vector3[capacity]; lifetime = new float[capacity]; enemyFaction = new byte[capacity]; }
    // =========================================================
    public void Fire(Vector3 origin, int victim, UnitState units, BenchmarkConfig config)
    {
        if (Count == Position.Length) { Dropped++; return; }
        int i = Count++; Position[i] = origin + Vector3.Up * 0.7f;
        velocity[i] = (units.Position[victim] + Vector3.Up * 0.7f - Position[i]).Normalized() * config.ProjectileSpeed;
        enemyFaction[i] = units.Faction[victim]; lifetime[i] = config.ProjectileLifetime; Dirty = true;
    }
    // =========================================================
    public void Step(UnitState units, SpatialGrid grid, BenchmarkConfig config, float dt)
    {
        if (Count == 0) return;
        Dirty = true;
        float movementPadding = Mathf.Abs(config.Speed * dt);
        for (int i = Count - 1; i >= 0; i--)
        {
            Vector3 from = Position[i];
            Vector3 segment = velocity[i] * Mathf.Min(dt, lifetime[i]);
            int victim = grid.FirstProjectileHit(units, enemyFaction[i], from, segment, config.ProjectileHitRadius, movementPadding);
            if (victim >= 0) units.Damage(victim, config.Damage, config.FlashSeconds);
            else
            {
                Position[i] += segment;
                lifetime[i] -= dt;
                if (lifetime[i] > 0) continue;
            }
            int last = --Count;
            Position[i] = Position[last]; velocity[i] = velocity[last]; lifetime[i] = lifetime[last]; enemyFaction[i] = enemyFaction[last];
        }
    }
}
