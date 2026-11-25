// Evaluates hit flash and grounded mesh pulses from simulation time.
using Godot;

namespace MassiveRts;

public sealed class HitFeedbackSystem
{
    private readonly float[] remaining, riseStart;
    private readonly int[] activeIds;
    private readonly bool[] meleeFlash;
    private const float AttackDuration = 0.16f;
    private const float CircleSwordDuration = 0.32f;
    private const float SquareAttackDuration = 0.40f;
    private const float HexagonAttackDuration = 0.58f;
    private bool[] hexagonStomp;
    internal void EnableHexagonAttack(bool enabled) => hexagonStomp = enabled ? new bool[attackRemaining.Length] : null;
    internal bool IsHexagonStomp(int id) => hexagonStomp != null && attackRemaining[id] > 0f && hexagonStomp[id];
    internal bool ExtendCircleSword { get; set; }
    internal bool ExtendSquareAttack { get; set; }
    private readonly float[] attackRemaining;
    private readonly int[] attackingIds;
    private int attackingCount;
    private readonly float duration, flashDuration, pulseDuration, riseDuration, fallDuration, peakExpansion;
    private int activeCount;
    private bool hitRegistered;

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
        meleeFlash = new bool[capacity];
        attackRemaining = new float[capacity];
        attackingIds = new int[capacity];
    }

    public void OnHit(int id, bool melee)
    {
        if (duration <= 0f) return;
        bool active = remaining[id] > 0f;
        // Refresh from the current size, so repeated hits never dip or stack scale.
        riseStart[id] = active ? Expansion(id, duration - remaining[id]) : 0f;
        if (!active) activeIds[activeCount++] = id;
        remaining[id] = duration;
        meleeFlash[id] = melee;
        hitRegistered = true;
    }

    public bool OnAttack(UnitState units, int id, bool stomp = false)
    {
        if (attackRemaining[id] <= 0f) attackingIds[attackingCount++] = id;
        attackRemaining[id] = AttackDurationFor(units.Type[id]);
        if (hexagonStomp != null && units.Type[id] == RuntimeUnitCatalog.Hexagon) hexagonStomp[id] = stomp;
        return units.Dirty[id] = true;
    }

    public bool Advance(UnitState units, float dt)
    {
        bool changed = hitRegistered;
        hitRegistered = false;
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
            {
                units.Dirty[id] = true;
                changed = true;
            }
        }
        for (int slot = attackingCount - 1; slot >= 0; slot--)
        {
            int id = attackingIds[slot];
            bool alive = units.Health[id] > 0f;
            attackRemaining[id] = alive ? Mathf.Max(0f, attackRemaining[id] - dt) : 0f;
            if (attackRemaining[id] == 0f) attackingIds[slot] = attackingIds[--attackingCount];
            if (alive) changed = units.Dirty[id] = true;
        }
        return changed;
    }

    // Longer weapon motion is purely visual; the original attack pulse stays 0.16s.
    private float AttackDurationFor(byte type) =>
        ExtendCircleSword && type == RuntimeUnitCatalog.Circle ? CircleSwordDuration :
        ExtendSquareAttack && type == RuntimeUnitCatalog.Square ? SquareAttackDuration :
        hexagonStomp != null && type == RuntimeUnitCatalog.Hexagon ? HexagonAttackDuration : AttackDuration;

    internal float AttackPhase(int id, byte type) => attackRemaining[id] > 0f
        ? Mathf.Clamp(1f - attackRemaining[id] / AttackDurationFor(type), 0f, 1f) : -1f;

    public (float Flash, float Scale) Evaluate(int id, byte type = byte.MaxValue)
    {
        float attackAge = (AttackDurationFor(type) - attackRemaining[id]) / AttackDuration;
        float attackScale = attackRemaining[id] > 0f && attackAge >= 0f && attackAge < 1f ? 0.15f * (attackAge < 0.28f
            ? Smooth(attackAge / 0.28f) : Smooth((1f - attackAge) / 0.72f)) : 0f;
        if (remaining[id] <= 0f) return (0f, 1f + attackScale);
        float age = duration - remaining[id];
        float flash = age < flashDuration ? (meleeFlash[id] ? 0.35f : 1f) : 0f;
        return (flash, 1f + Mathf.Max(attackScale, Expansion(id, age)));
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
