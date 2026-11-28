// Sparse cosmetic tilt state for surviving heavy projectile impacts.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed class HeavyImpactFeedback
{
    private readonly record struct Reaction(Vector2 Start, Vector2 Peak, float Age);
    private readonly Dictionary<int, Reaction> reactions = new();
    private readonly List<int> active = new();
    private readonly float duration, strength, counter;
    private readonly Vector4 tierAngles;
    private readonly float tierFiveAngle;
    private const float ImmuneTier = 6f;
    private float MinimumTier { get; }
    internal bool IncludesTier(float tier) => tier >= MinimumTier && tier < ImmuneTier;
    internal float SplashStrength { get; }

    internal HeavyImpactFeedback(HitFeedbackSettings settings)
    {
        duration = Limit(settings.HeavyImpactDuration, .1f, 2f, .4f);
        strength = Limit(settings.HeavyImpactStrength, 0f, 2f, 1f);
        counter = Limit(settings.HeavyImpactCounterRock, 0f, .5f, .25f);
        SplashStrength = Limit(settings.HeavyImpactSplashStrength, 0f, 1f, .65f);
        MinimumTier = Limit(settings.HeavyImpactMinimumTier, 2f, 10f, 2f);
        var t = settings.HeavyImpactTierAngles;
        tierAngles = new Vector4(Limit(t.X, 0f, 12f, 8f), Limit(t.Y, 0f, 12f, 7f),
            Limit(t.Z, 0f, 12f, 6f), Limit(t.W, 0f, 12f, 4.5f));
        tierFiveAngle = Limit(settings.HeavyImpactTierFiveAngle, 0f, 12f, 2.5f);
    }

    private static float Limit(float value, float min, float max, float fallback) =>
        float.IsFinite(value) ? Mathf.Clamp(value, min, max) : fallback;

    internal void Register(UnitState units, int id, float tier, Vector2 direction, float intensity)
    {
        if (intensity <= 0f || strength <= 0f) return;
        float degrees = tier < 2.5f ? Mathf.Lerp(tierAngles.X, tierAngles.Y, (tier - 2f) * 2f) :
            tier < 3f ? Mathf.Lerp(tierAngles.Y, tierAngles.Z, (tier - 2.5f) * 2f) :
            tier < 4f ? Mathf.Lerp(tierAngles.Z, tierAngles.W, tier - 3f) :
            Mathf.Lerp(tierAngles.W, tierFiveAngle, Mathf.Min(1f, tier - 4f));
        float angle = Mathf.DegToRad(Mathf.Min(12f, degrees * strength) * Mathf.Min(1f, intensity));
        if (angle <= 0f) return;
        if (direction.LengthSquared() < .000001f) direction = Vector2.Right;
        Vector2 start = Vector2.Zero;
        if (reactions.TryGetValue(id, out var previous)) start = Evaluate(previous);
        else active.Add(id);
        reactions[id] = new Reaction(start, direction.Normalized() * angle, 0f);
        units.Dirty[id] = true;
    }

    internal bool Advance(UnitState units, float dt)
    {
        bool changed = false;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            int id = active[i];
            bool alive = units.Health[id] > 0f;
            var reaction = reactions[id];
            float age = reaction.Age + dt;
            if (!alive || age >= duration)
            {
                reactions.Remove(id);
                active[i] = active[^1];
                active.RemoveAt(active.Count - 1);
            }
            else reactions[id] = reaction with { Age = age };
            if (alive) changed = units.Dirty[id] = true;
        }
        return changed;
    }

    private Vector2 Evaluate(Reaction reaction)
    {
        float time = reaction.Age / duration;
        if (time <= .15f) return reaction.Start.Lerp(reaction.Peak, Mathf.SmoothStep(0f, 1f, time / .15f));
        float weight = time < .4f ? Blend(1f, .25f, .15f, .4f) :
            time < .575f ? Blend(.25f, -counter, .4f, .575f) :
            time < .8f ? Blend(-counter, .1f, .575f, .8f) : Blend(.1f, 0f, .8f, 1f);
        return reaction.Peak * weight;

        float Blend(float from, float to, float start, float end) =>
            Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, (time - start) / (end - start)));
    }

    internal bool TryTransform(int id, float radius, out Basis tilt, out float lift)
    {
        tilt = Basis.Identity;
        lift = 0f;
        if (active.Count == 0 || !reactions.TryGetValue(id, out var reaction)) return false;
        Vector2 rotation = Evaluate(reaction);
        float angle = rotation.Length();
        if (angle < .00001f) return false;
        tilt = new Basis(new Vector3(rotation.Y, 0f, -rotation.X) / angle, angle);
        lift = Mathf.Min(.45f, radius * Mathf.Sin(angle));
        return true;
    }
}
