// Tracks shield fields and temporary shield pools using stable soldier IDs.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;

namespace MassiveRts;

public readonly record struct ShieldEmitterStats(float Radius, float Capacity, float Recharge, float Decay)
{
    public bool Enabled => Capacity > 0f;

    internal static ShieldEmitterStats Compile(bool enabled, float radius, float capacity, float recharge, float decay) =>
        enabled ? new(Finite(radius, 1f, 64f, 14f), Finite(capacity, 1f, 500f, 20f),
            Finite(recharge, 0f, 100f, 4f), Finite(decay, 0.1f, 100f, 2f)) : default;

    private static float Finite(float value, float min, float max, float fallback) =>
        float.IsFinite(value) ? Mathf.Clamp(value, min, max) : fallback;
}

internal sealed class ShieldSystem
{
    internal const float DefaultInterval = 0.2f;
    internal const float FlashSeconds = 0.16f;
    private readonly record struct Emitter(int Soldier, StructureHealth Tower, ShieldEmitterStats Stats);
    private readonly List<Emitter> emitters = new();
    private readonly float[] amount, capacity, decay, flashUntil;
    private readonly int[] ids, slots, winners;
    private float elapsed, clock, lastFlashEnd;
    private bool enabled = true;
    internal int SourceCount => emitters.Count;
    internal int ActiveCount { get; private set; }
    internal int ShieldedCount { get; private set; }
    internal double CoverageMilliseconds { get; private set; }
    internal double UpdateMilliseconds { get; private set; }
    internal int IdAt(int slot) => ids[slot];
    internal float Amount(int id) => amount[id];
    internal float Strength(int id) => capacity[id] > 0f ? Mathf.Min(1f, amount[id] / capacity[id]) : 0f;
    internal float Flash(int id) => Mathf.Clamp((flashUntil[id] - clock) / FlashSeconds, 0f, 1f);
    internal bool HasFlashes => clock < lastFlashEnd;
    internal uint Revision { get; private set; }

    internal ShieldSystem(int size)
    {
        amount = new float[size]; capacity = new float[size]; decay = new float[size]; flashUntil = new float[size];
        ids = new int[size]; slots = new int[size]; winners = new int[size];
        Array.Fill(slots, -1);
    }

    internal void AddMobile(int id, ShieldEmitterStats stats) => emitters.Add(new Emitter(id, null, stats));
    internal void AddTower(StructureHealth tower, ShieldEmitterStats stats) => emitters.Add(new Emitter(-1, tower, stats));

    internal float Absorb(int id, float damage)
    {
        if (!enabled || damage <= 0f || amount[id] <= 0f) return damage;
        float absorbed = Mathf.Min(amount[id], damage);
        amount[id] -= absorbed;
        if (amount[id] == 0f) ShieldedCount--;
        flashUntil[id] = lastFlashEnd = clock + FlashSeconds;
        Revision++;
        return damage - absorbed;
    }

    internal void RemoveRecipient(int id)
    {
        int slot = slots[id];
        if (slot < 0) return;
        if (amount[id] > 0f) ShieldedCount--;
        int moved = ids[--ActiveCount];
        ids[slot] = moved;
        slots[moved] = slot;
        slots[id] = -1;
        amount[id] = capacity[id] = decay[id] = flashUntil[id] = 0f;
        Revision++;
    }

    internal bool AdvanceFlash(float dt)
    {
        bool animate = HasFlashes;
        if (animate) clock += dt;
        return animate;
    }

    internal bool TryGetSource(int index, UnitState units, out Vector3 position, out float radius,
        out int soldier, out StructureHealth tower)
    {
        var emitter = emitters[index];
        soldier = emitter.Soldier;
        tower = emitter.Tower;
        radius = emitter.Stats.Radius;
        if (soldier >= 0)
        {
            position = units.Position[soldier];
            return units.Health[soldier] > 0f;
        }

        Vector2 center = (tower.Area.Min + tower.Area.Max) * 0.5f;
        position = units.OnTerrain(new Vector3(center.X, 0f, center.Y));
        return !tower.Destroyed;
    }

    internal bool NeedsRefresh(float dt, float interval) =>
        (emitters.Count != 0 || ActiveCount != 0) && elapsed + dt >= interval;

    // One completed coverage pass is published at a time. No partial winner sets reach recharge.
    internal bool Step(UnitState units, SpatialGrid grid, float dt, float interval, float movementPadding, bool active)
    {
        if (!active)
        {
            bool cleared = ActiveCount != 0;
            if (enabled)
            {
                while (ActiveCount > 0) RemoveRecipient(ids[ActiveCount - 1]);
                elapsed = lastFlashEnd = 0f;
                enabled = false;
            }
            return cleared;
        }
        enabled = true;
        bool animate = AdvanceFlash(dt);
        if (emitters.Count == 0 && ActiveCount == 0) return animate;
        elapsed += dt;
        if (elapsed < interval) return animate;

        long start = Stopwatch.GetTimestamp();
        for (int i = emitters.Count - 1; i >= 0; i--)
        {
            var emitter = emitters[i];
            bool dead = emitter.Soldier >= 0 ? units.Health[emitter.Soldier] <= 0f : emitter.Tower.Destroyed;
            if (dead) emitters.RemoveAt(i);
        }
        for (int i = 0; i < ActiveCount; i++) winners[ids[i]] = -1;
        for (int index = 0; index < emitters.Count; index++)
        {
            var emitter = emitters[index];
            byte faction = emitter.Soldier >= 0 ? units.Faction[emitter.Soldier] : emitter.Tower.Faction;
            Vector2 center = emitter.Soldier >= 0 ? ObstacleField.Xz(units.Position[emitter.Soldier])
                : (emitter.Tower.Area.Min + emitter.Tower.Area.Max) * 0.5f;
            float radius = emitter.Stats.Radius;
            var candidates = grid.GetNearbyAllies(center, radius + movementPadding, faction);
            while (candidates.MoveNext())
            {
                int id = candidates.Current;
                if (id == emitter.Soldier || units.Health[id] <= 0f ||
                    center.DistanceSquaredTo(ObstacleField.Xz(units.Position[id])) > radius * radius) continue;
                if (slots[id] < 0)
                {
                    slots[id] = ActiveCount;
                    ids[ActiveCount++] = id;
                    winners[id] = -1;
                }
                int previous = winners[id];
                if (previous < 0 || Stronger(emitter.Stats, emitters[previous].Stats)) winners[id] = index;
            }
        }
        CoverageMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        start = Stopwatch.GetTimestamp();
        bool changed = false;
        for (int i = ActiveCount - 1; i >= 0; i--)
        {
            int id = ids[i];
            int winner = winners[id];
            float old = amount[id];
            if (winner >= 0)
            {
                var stats = emitters[winner].Stats;
                capacity[id] = stats.Capacity;
                decay[id] = stats.Decay;
                amount[id] = Mathf.MoveToward(old, stats.Capacity,
                    elapsed * (old > stats.Capacity ? stats.Decay : stats.Recharge));
            }
            else amount[id] = Mathf.Max(0f, old - elapsed * decay[id]);

            if (old == 0f && amount[id] > 0f) ShieldedCount++;
            else if (old > 0f && amount[id] == 0f) ShieldedCount--;
            changed |= old != amount[id];
            if (winner < 0 && amount[id] == 0f && Flash(id) == 0f) RemoveRecipient(id);
        }
        elapsed = 0f;
        UpdateMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        if (changed) Revision++;
        return changed || animate;
    }

    private static bool Stronger(ShieldEmitterStats a, ShieldEmitterStats b) =>
        a.Capacity > b.Capacity || (a.Capacity == b.Capacity && a.Recharge > b.Recharge);
}
