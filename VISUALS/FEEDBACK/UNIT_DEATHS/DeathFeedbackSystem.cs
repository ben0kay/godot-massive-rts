// Captures brief lethal-hit effects and longer-lived faction-colored ground splats in bounded pools.
using Godot;

namespace MassiveRts;

public sealed class DeathFeedbackSystem
{
    public const int Capacity = 512;
    public const int BlobsPerDeath = 4;
    public const float ShrinkSeconds = 0.20f;
    public const float BlobSeconds = 0.42f;
    public const float SplatSeconds = 3.0f;
    public const float SplatFadeSeconds = 1.2f;
    // Visual-only cap independent of the shorter-lived body/puff capacity.
    public const int SplatCapacity = 1024;

    public readonly record struct Effect(Vector3 Ground, byte Type, byte Faction, Vector2 Facing, uint Seed, float Age);
    public readonly record struct Splat(Vector3 Ground, byte Type, byte Faction, uint Seed, float Age);
    private readonly Effect[] effects = new Effect[Capacity];
    private readonly Splat[] splats = new Splat[SplatCapacity];
    private uint sequence;
    public uint Revision { get; private set; }
    public int Count { get; private set; }
    public int SplatCount { get; private set; }
    public bool Enabled { get; set; } = true;
    public Effect this[int index] => effects[index];
    public Splat GetSplat(int index) => splats[index];

    public void Emit(Vector3 ground, byte type, byte faction, Vector2 facing)
    {
        if (!Enabled || (Count == Capacity && SplatCount == SplatCapacity)) return;
        uint seed = unchecked(++sequence * 2654435761u);
        if (Count < Capacity)
            effects[Count++] = new Effect(ground, type, faction, facing, seed, 0f);
        // Still leave a small splat when the short-lived death pool is saturated.
        if (SplatCount < SplatCapacity)
            splats[SplatCount++] = new Splat(ground, type, faction, seed, 0f);
        Revision++;
    }

    public void Advance(float delta)
    {
        if ((Count == 0 && SplatCount == 0) || delta <= 0f) return;
        Revision++;
        for (int i = Count - 1; i >= 0; i--)
        {
            float age = effects[i].Age + delta;
            if (age >= BlobSeconds) effects[i] = effects[--Count];
            else effects[i] = effects[i] with { Age = age };
        }
        for (int i = SplatCount - 1; i >= 0; i--)
        {
            float age = splats[i].Age + delta;
            if (age >= SplatSeconds) splats[i] = splats[--SplatCount];
            else splats[i] = splats[i] with { Age = age };
        }
    }

    public void Clear()
    {
        if (Count == 0 && SplatCount == 0) return;
        Count = SplatCount = 0;
        Revision++;
    }
}
