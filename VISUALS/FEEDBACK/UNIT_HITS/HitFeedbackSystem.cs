// Evaluates hit flash and grounded mesh pulses from simulation time.
using Godot;

namespace MassiveRts;

public sealed class HitFeedbackSystem
{
    private readonly float[] remaining, riseStart;
    private readonly int[] activeIds;
    private readonly float duration, flashDuration, pulseDuration, riseDuration, fallDuration, peakExpansion;
    private int activeCount;

    public HitFeedbackSystem(int capacity, HitFeedbackSettings settings)
    {
        flashDuration = Clamp(settings.WhiteFlashDuration, 0f, 2f, 0.12f);
        pulseDuration = settings.PulseEnabled ? Clamp(settings.PulseDuration, 0.01f, 2f, 0.18f) : 0f;
        peakExpansion = Clamp(settings.PulseMaxScale, 1f, 1.25f, 1.06f) - 1f;
        riseDuration = pulseDuration * Clamp(settings.PulseRiseFraction, 0.01f, 0.99f, 0.20f);
        fallDuration = pulseDuration - riseDuration;
        duration = Mathf.Max(flashDuration, pulseDuration);
        remaining = new float[capacity];
        riseStart = new float[capacity];
        activeIds = new int[capacity];
    }

    public void OnHit(int id)
    {
        if (duration <= 0f) return;
        bool active = remaining[id] > 0f;
        // Refresh from the current size, so repeated hits never dip or stack scale.
        riseStart[id] = active ? Expansion(id, duration - remaining[id]) : 0f;
        if (!active) activeIds[activeCount++] = id;
        remaining[id] = duration;
    }

    public void Advance(UnitState units, float dt)
    {
        for (int slot = activeCount - 1; slot >= 0; slot--)
        {
            int id = activeIds[slot];
            bool alive = units.Health[id] > 0f;
            float before = remaining[id];
            float after = alive ? Mathf.Max(0f, before - dt) : 0f;
            remaining[id] = after;
            if (after == 0f) activeIds[slot] = activeIds[--activeCount];
            if (!alive) continue;

            float previousAge = duration - before;
            if (after == 0f || (peakExpansion > 0f && previousAge < pulseDuration) ||
                (previousAge < flashDuration && duration - after >= flashDuration))
                units.Dirty[id] = true;
        }
    }

    public (bool White, float Scale) Evaluate(int id)
    {
        if (remaining[id] <= 0f) return (false, 1f);
        float age = duration - remaining[id];
        return (age < flashDuration, 1f + Expansion(id, age));
    }

    private float Expansion(int id, float age)
    {
        if (age >= pulseDuration || peakExpansion <= 0f) return 0f;
        if (age < riseDuration)
            return Mathf.Lerp(riseStart[id], peakExpansion, Smooth(age / riseDuration));
        return peakExpansion * Smooth((pulseDuration - age) / fallDuration);
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    private static float Clamp(float value, float min, float max, float fallback) =>
        float.IsFinite(value) ? Mathf.Clamp(value, min, max) : fallback;
}
