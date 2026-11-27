// Shared, rechargeable projectile domes attached to destructible structures.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

public enum ProjectileWeight { Light, Standard, Heavy }

internal sealed class StructureShieldDomes
{
    internal const float HeightRatio = 0.55f;

    internal sealed class Dome
    {
        internal readonly StructureHealth Owner;
        internal readonly Vector3 Center;
        internal readonly float Radius;
        internal readonly ShieldEmitterStats Stats;
        internal readonly Action<Dome> Render;
        internal float Energy, RippleTime;
        internal bool Active;
        internal Vector3 RippleDirection;
        internal ProjectileWeight LastHit;

        internal Dome(StructureHealth owner, Vector3 center, ShieldEmitterStats stats, Action<Dome> render)
        {
            Owner = owner;
            Center = center;
            Radius = stats.Radius;
            Stats = stats;
            Render = render;
        }

        internal Vector3 Local(Vector3 offset) => new(offset.X, offset.Y / HeightRatio, offset.Z);
        internal bool Contains(Vector3 point)
        {
            var local = Local(point - Center);
            return local.Y >= 0f && local.LengthSquared() <= Radius * Radius;
        }

        internal void Absorb(Vector3 point, ProjectileWeight weight)
        {
            Energy = Mathf.Max(0f, Energy - (weight switch
            {
                ProjectileWeight.Heavy => 25f,
                ProjectileWeight.Standard => 8f,
                _ => 1f
            }));
            RippleDirection = Local(point - Center).Normalized();
            LastHit = weight;
            RippleTime = 0.45f;
            Active = Energy > 0f;
            Render(this);
        }

        internal Vector3 Normal(Vector3 point)
        {
            var local = Local(point - Center);
            return new Vector3(local.X, local.Y / HeightRatio, local.Z).Normalized();
        }
    }

    private readonly List<Dome> domes = new();
    private bool enabled = true;

    internal void Add(StructureHealth owner, Vector3 center, ShieldEmitterStats settings, Action<Dome> render)
    {
        var dome = new Dome(owner, center, settings, render);
        domes.Add(dome);
        render(dome);
    }

    internal void Step(float dt, bool enabled = true)
    {
        this.enabled = enabled;
        for (int i = domes.Count - 1; i >= 0; i--)
        {
            var dome = domes[i];
            if (dome.Owner.Destroyed)
            {
                // The structure's renderer owns teardown of its slot.
                domes.RemoveAt(i);
                continue;
            }
            bool wasActive = dome.Active;
            bool rippling = dome.RippleTime > 0f;
            dome.RippleTime = Mathf.Max(0f, dome.RippleTime - dt);
            if (enabled) dome.Energy = Mathf.Min(dome.Stats.Capacity, dome.Energy + dome.Stats.Recharge * dt);
            dome.Active = enabled && dome.Energy > 0f;
            if (wasActive != dome.Active || rippling) dome.Render(dome);
        }
    }

    internal Dome FirstHit(byte faction, Vector3 from, Vector3 segment, out float fraction)
    {
        fraction = float.PositiveInfinity;
        Dome hit = null;
        if (!enabled || domes.Count == 0) return null;
        foreach (var dome in domes)
        {
            if (!dome.Active || dome.Owner.Destroyed || dome.Owner.Faction == faction) continue;
            Vector3 start = dome.Local(from - dome.Center);
            Vector3 delta = dome.Local(segment);
            float a = delta.LengthSquared(), b = start.Dot(delta);
            float c = start.LengthSquared() - dome.Radius * dome.Radius;
            // Only intercept entry through the curved surface. Shots fired inside can leave.
            if (a <= 0.0000001f || b >= 0f || c < 0f) continue;
            float discriminant = b * b - a * c;
            if (discriminant <= 0f) continue;
            float t = (-b - Mathf.Sqrt(discriminant)) / a;
            if (t < 0f || t > 1f || t >= fraction || start.Y + delta.Y * t < 0f) continue;
            fraction = t;
            hit = dome;
        }
        return hit;
    }
}
