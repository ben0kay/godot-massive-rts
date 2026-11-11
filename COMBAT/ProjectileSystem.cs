// Fixed-capacity projectile pool; swept hits against the intended target, with swap removal.
using Godot;
namespace MassiveRts;

public sealed class ProjectileSystem
{
    public readonly Vector3[] Position;
    private readonly Vector3[] velocity;
    private readonly float[] lifetime;
    private readonly int[] target;
    public int Count { get; private set; }
    public int Dropped { get; private set; }
    public bool Dirty { get; set; }
    public ProjectileSystem(int capacity)
    { Position = new Vector3[capacity]; velocity = new Vector3[capacity]; lifetime = new float[capacity]; target = new int[capacity]; }
    // =========================================================
    public void Fire(Vector3 origin, int victim, UnitState units, BenchmarkConfig config)
    {
        if (Count == Position.Length) { Dropped++; return; }
        int i = Count++; Position[i] = origin + Vector3.Up * 0.7f;
        velocity[i] = (units.Position[victim] + Vector3.Up * 0.7f - Position[i]).Normalized() * config.ProjectileSpeed;
        target[i] = victim; lifetime[i] = config.ProjectileLifetime; Dirty = true;
    }
    // =========================================================
    public void Step(UnitState units, BenchmarkConfig config, float dt)
    {
        if (Count == 0) return;
        Dirty = true;
        for (int i = Count - 1; i >= 0; i--)
        {
            Vector3 from = Position[i], segment = velocity[i] * dt;
            Position[i] += segment; lifetime[i] -= dt;
            int victim = target[i]; bool hit = false;
            if (units.Health[victim] > 0)
            {
                Vector3 relative = units.Position[victim] + Vector3.Up * 0.7f - from;
                float fraction = Mathf.Clamp(relative.Dot(segment) / Mathf.Max(segment.LengthSquared(), 0.00001f), 0, 1);
                hit = (relative - segment * fraction).LengthSquared() <= config.ProjectileHitRadius * config.ProjectileHitRadius;
                if (hit) units.Damage(victim, config.Damage, config.FlashSeconds);
            }
            if (!hit && lifetime[i] > 0) continue;
            int last = --Count;
            Position[i] = Position[last]; velocity[i] = velocity[last]; lifetime[i] = lifetime[last]; target[i] = target[last];
        }
    }
}
