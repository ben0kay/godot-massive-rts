// Captures bounded crumble events and independent fading splats from lethal hits.
using System;
using Godot;

namespace MassiveRts;

public sealed class DeathFeedbackSystem
{
    public const float SplatSeconds = 3f;
    public const float SplatFadeSeconds = 1.2f;
    public const int SplatCapacity = 1024;

    public readonly record struct Fragment(Vector3 Start, Vector2 Travel, Vector3 Size,
        float MiddleGround, float EndGround, float Lift, float Flight, float Bounce,
        float Yaw, float Spin, Color Tint);

    public readonly record struct Splat(Vector3 Ground, byte Type, byte Faction, uint Seed, float Age);
    private readonly Fragment[] fragments;
    private readonly float[] ages;
    private readonly Splat[] splats = new Splat[SplatCapacity];
    private readonly int deathsPerTick;
    private readonly float spread, upward, bounce;
    private uint sequence;
    private int emittedThisTick;

    public uint Revision { get; private set; }
    public int Count { get; private set; }
    public int SplatCount { get; private set; }
    public bool Enabled { get; set; } = true;
    internal int FragmentsPerDeath { get; }
    internal int Capacity => ages.Length;
    internal float Gravity { get; }
    internal float Lifetime { get; }
    internal float FadeDuration { get; }

    public DeathFeedbackSystem(UnitVisualConfig settings)
    {
        FragmentsPerDeath = Mathf.Clamp(settings.FragmentsPerDeath, 4, 6);
        int capacity = settings.UseUnitFracture && settings.MaxDetailedDeathsPerTick > 0
            ? Math.Min(Mathf.Clamp(settings.MaxDetailedDeaths, 0, 512),
                Mathf.Clamp(settings.MaxActiveFragments, 0, 3000) / FragmentsPerDeath)
            : 0;
        ages = new float[capacity];
        fragments = new Fragment[capacity * FragmentsPerDeath];
        deathsPerTick = Mathf.Clamp(settings.MaxDetailedDeathsPerTick, 0, 128);
        spread = FiniteRange(settings.FragmentSpread, 0f, 2f, 0.86f);
        upward = FiniteRange(settings.FragmentUpwardVelocity, 0f, 4f, 1.1f);
        Gravity = FiniteRange(settings.FragmentGravity, 1f, 30f, 11f);
        bounce = FiniteRange(settings.FragmentBounce, 0f, 0.5f, 0.18f);
        Lifetime = FiniteRange(settings.FragmentLifetime, 0.3f, 2f, 0.9f);
        FadeDuration = FiniteRange(settings.FragmentFadeDuration, 0.05f, Lifetime, 0.35f);
    }

    internal float GetAge(int index) => ages[index];
    internal ref readonly Fragment GetFragment(int index, int piece) =>
        ref fragments[index * FragmentsPerDeath + piece];
    public Splat GetSplat(int index) => splats[index];

    public void BeginTick() => emittedThisTick = 0;

    // =========================================================
    // Excess details disappear immediately. Splats have their own independent cap.
    public void Emit(Vector3 ground, byte type, byte faction, Vector2 facing,
        in UnitStats defender, TerrainHeightField terrain)
    {
        if (!Enabled) return;
        bool detailed = Count < Capacity && emittedThisTick < deathsPerTick;
        if (!detailed && SplatCount == SplatCapacity) return;

        uint seed = unchecked(++sequence * 2654435761u);
        if (detailed)
        {
            CaptureFragments(Count, ground, faction, facing, defender, terrain, seed);
            ages[Count++] = 0f;
            emittedThisTick++;
        }
        if (SplatCount < SplatCapacity)
            splats[SplatCount++] = new Splat(ground, type, faction, seed, 0f);
        Revision++;
    }

    // =========================================================
    // Approximate the original body with angular chunks and sample terrain along each short path once.
    private void CaptureFragments(int slot, Vector3 ground, byte faction, Vector2 facing,
        in UnitStats stats, TerrainHeightField terrain, uint seed)
    {
        float heading = facing == Vector2.Zero || stats.Shape == UnitShape.Circle ? 0f
            : stats.Shape == UnitShape.Diamond ? Mathf.Atan2(-facing.Y, facing.X)
            : Mathf.Atan2(-facing.X, -facing.Y);
        var orientation = new Basis(Vector3.Up, heading);
        float rotation = Next(ref seed) * Mathf.Tau;
        Color tint = ArmyRenderer.FactionColor(faction);
        float movementScale = Mathf.Sqrt(Mathf.Max(0.04f, stats.Footprint));
        for (int piece = 0; piece < FragmentsPerDeath; piece++)
        {
            float angle = rotation + (piece + 0.35f * Next(ref seed)) * Mathf.Tau / FragmentsPerDeath;
            Vector3 radial = orientation * new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 offset = orientation * new Vector3(Mathf.Cos(angle) * stats.Outline.RadiusX, 0f,
                Mathf.Sin(angle) * stats.Outline.RadiusZ) * 0.37f;
            Vector3 origin = ground + offset;
            origin.Y = terrain?.At(origin.X, origin.Z) ?? ground.Y;

            float scale = Mathf.Lerp(0.75f, 1.15f, Next(ref seed)) * 0.85f;
            var size = new Vector3(stats.Outline.RadiusX * 0.96f * scale,
                stats.Height * 0.66f * scale, stats.Outline.RadiusZ * 0.96f * scale);
            float lift = stats.Height * Mathf.Lerp(0.13f, 0.25f, Next(ref seed));
            origin.Y += size.Y * 0.5f + lift;
            float launch = upward * Mathf.Lerp(0.68f, 1.24f, Next(ref seed)) * movementScale;
            float flight = (launch + Mathf.Sqrt(launch * launch + 2f * Gravity * lift)) / Gravity;
            flight = Mathf.Min(flight, Lifetime * 0.53f);
            Vector2 travel = new Vector2(radial.X, radial.Z) * spread * movementScale *
                Mathf.Lerp(0.50f, 1.15f, Next(ref seed));
            float middleGround = terrain?.At(origin.X + travel.X * 0.5f,
                origin.Z + travel.Y * 0.5f) ?? ground.Y;
            float endGround = terrain?.At(origin.X + travel.X, origin.Z + travel.Y) ?? ground.Y;
            float rebound = Mathf.Min((Gravity * flight - launch) * bounce, 1.1f);
            float yaw = heading + angle;
            float spin = Mathf.Lerp(-1.4f, 1.4f, Next(ref seed));
            fragments[slot * FragmentsPerDeath + piece] = new Fragment(origin, travel, size,
                middleGround, endGround, lift, flight, Mathf.Max(0f, rebound), yaw, spin,
                tint.Darkened(Mathf.Lerp(0.03f, 0.21f, Next(ref seed))));
        }
    }

    private static float Next(ref uint seed)
    {
        seed ^= seed << 13;
        seed ^= seed >> 17;
        seed ^= seed << 5;
        return (seed >> 8) * (1f / 16777216f);
    }

    private static float FiniteRange(float value, float min, float max, float fallback) =>
        Mathf.Clamp(float.IsFinite(value) ? value : fallback, min, max);

    // =========================================================
    public void Advance(float delta)
    {
        if ((Count == 0 && SplatCount == 0) || delta <= 0f) return;
        Revision++;
        for (int i = Count - 1; i >= 0; i--)
        {
            ages[i] += delta;
            if (ages[i] < Lifetime) continue;
            int last = --Count;
            if (i == last) continue;
            ages[i] = ages[last];
            Array.Copy(fragments, last * FragmentsPerDeath, fragments, i * FragmentsPerDeath, FragmentsPerDeath);
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
        sequence = 0;
        emittedThisTick = 0;
        if (Count == 0 && SplatCount == 0) return;
        Count = SplatCount = 0;
        Revision++;
    }
}
