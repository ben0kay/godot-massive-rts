// Packed, presentation-only ranged recoil; no per-soldier nodes or physics.
using Godot;

namespace MassiveRts;

public sealed class RangedRecoilSystem
{
    private readonly float[] remaining;
    private readonly Vector2[] shotDirections;
    private readonly int[] activeIds;
    private int activeCount;

    public RangedRecoilSystem(int capacity)
    {
        remaining = new float[capacity];
        shotDirections = new Vector2[capacity];
        activeIds = new int[capacity];
    }

    // Trigger on a successfully spawned projectile; repeated shots restart the kick.
    public void OnFire(UnitState units, int shooter, int target)
    {
        RangedRecoilStats recoil = units.Catalog[units.Type[shooter]].RangedRecoil;
        if (recoil.Distance <= 0f) return;
        Vector3 delta = units.Position[target] - units.Position[shooter];
        float lengthSquared = delta.X * delta.X + delta.Z * delta.Z;
        if (lengthSquared <= 0.000001f) return;
        float invLength = 1f / Mathf.Sqrt(lengthSquared);
        shotDirections[shooter] = new Vector2(delta.X * invLength, delta.Z * invLength);
        if (remaining[shooter] <= 0f) activeIds[activeCount++] = shooter;
        remaining[shooter] = recoil.ReturnSeconds;
        units.Dirty[shooter] = true;
    }

    // Process only currently recoiling soldiers; always write their final resting pose.
    public void Advance(UnitState units, float dt)
    {
        for (int slot = activeCount - 1; slot >= 0; slot--)
        {
            int id = activeIds[slot];
            bool alive = units.Health[id] > 0f;
            float next = alive ? Mathf.Max(0f, remaining[id] - dt) : 0f;
            remaining[id] = next;
            if (next <= 0f) activeIds[slot] = activeIds[--activeCount];
            if (alive) units.Dirty[id] = true;
        }
    }

    // The transform offset is world-space XZ, independent of facing and simulation position.
    public Vector2 VisualOffset(int id, in RangedRecoilStats recoil)
    {
        if (remaining[id] <= 0f || recoil.Distance <= 0f) return Vector2.Zero;
        float t = Mathf.Clamp(remaining[id] / recoil.ReturnSeconds, 0f, 1f);
        float ease = t * t * (3f - 2f * t);
        return shotDirections[id] * (-recoil.Distance * ease);
    }
}
