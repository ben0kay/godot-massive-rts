// Captures lethal-hit poses in a bounded, packed presentation pool.
using Godot;

namespace MassiveRts;

public sealed class DeathFeedbackSystem
{
    public const int Capacity = 512;
    public const int BlobsPerDeath = 3;
    public const float ShrinkSeconds = 0.20f;
    public const float BlobSeconds = 0.28f;

    public readonly record struct Effect(Vector3 Ground, byte Type, byte Faction, Vector2 Facing, uint Seed, float Age);
    private readonly Effect[] effects = new Effect[Capacity];
    private uint sequence;
    public uint Revision { get; private set; }
    public int Count { get; private set; }
    public bool Enabled { get; set; } = true;
    public Effect this[int index] => effects[index];

    public void Emit(Vector3 ground, byte type, byte faction, Vector2 facing)
    {
        if (!Enabled || Count == Capacity) return;
        Revision++;
        effects[Count++] = new Effect(ground, type, faction, facing, unchecked(++sequence * 2654435761u), 0f);
    }

    public void Advance(float delta)
    {
        if (Count == 0 || delta <= 0f) return;
        Revision++;
        for (int i = Count - 1; i >= 0; i--)
        {
            float age = effects[i].Age + delta;
            if (age >= BlobSeconds) effects[i] = effects[--Count];
            else effects[i] = effects[i] with { Age = age };
        }
    }

    public void Clear()
    {
        if (Count == 0) return;
        Count = 0;
        Revision++;
    }
}
