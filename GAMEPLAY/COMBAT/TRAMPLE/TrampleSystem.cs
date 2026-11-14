// Momentum-based charge shared by any opted-in melee unit.
using System;
using Godot;

namespace MassiveRts;

public readonly record struct TrampleStats(bool Enabled, float RunUp, float Distance, float Efficiency,
    float DamageMultiplier, float Resistance, int MaxHits);

public sealed class TrampleSystem
{
    // Instantaneous contact displacement, not a continuous correction rate.
    private const float MaxCombinedContactShove = 0.16f;
    private const float RecoverySeconds = 0.30f;
    private const float EntrySpeedFraction = 0.90f;
    private const int HitSlots = 6;

    private struct Charge
    {
        public Vector2 Start, Heading, Approach;
        public float RunUp, Distance, Momentum, Recovery;
        public int Hits;
        public bool Active, BypassThisTick;
    }

    private readonly int[] ids, slots, hits, shovedIds;
    private readonly Charge[] states;
    private readonly Vector2[] shoves;
    private readonly bool[] queued;
    private int shoveCount;

    private TrampleSystem(UnitState units, int count, int end, int[] activeIds)
    {
        ids = new int[count];
        states = new Charge[count];
        hits = new int[count * HitSlots];
        slots = new int[units.Count];
        Array.Fill(slots, -1);
        shoves = new Vector2[units.Count];
        shovedIds = new int[units.Count];
        queued = new bool[units.Count];
        int slot = 0;
        for (int index = 0; index < end; index++)
        {
            int id = activeIds == null ? index : activeIds[index];
            if (!units.Catalog[units.Type[id]].Trample.Enabled) continue;
            slots[id] = slot;
            ids[slot++] = id;
        }
    }

    public static TrampleSystem Create(UnitState units, int end = -1, int[] activeIds = null, TrampleSystem previous = null)
    {
        int count = 0;
        if (end < 0) end = units.Count;
        for (int index = 0; index < end; index++)
        {
            int id = activeIds == null ? index : activeIds[index];
            if (units.Catalog[units.Type[id]].Trample.Enabled) count++;
        }
        if (count == 0) return null;
        var result = new TrampleSystem(units, count, end, activeIds);
        if (previous != null)
        {
            // Orders change before the charge pass, with no queued shoves.
            for (int slot = 0; slot < previous.ids.Length; slot++)
            {
                int next = result.slots[previous.ids[slot]];
                result.states[next] = previous.states[slot];
                Array.Copy(previous.hits, slot * HitSlots, result.hits, next * HitSlots, HitSlots);
            }
        }
        return result;
    }

    public bool OverridesMovement(int id)
    {
        int slot = slots[id];
        return slot >= 0 && (states[slot].Active || states[slot].Recovery > 0f);
    }

    public bool OverridesAttack(int id)
    {
        int slot = slots[id];
        return slot >= 0 && (states[slot].Active || states[slot].BypassThisTick);
    }

    public bool IgnoreSeparation(int a, int b) => CanPass(a, b) || CanPass(b, a);

    private bool CanPass(int charger, int defender)
    {
        int slot = slots[charger];
        return slot >= 0 && states[slot].BypassThisTick && AlreadyHit(slot, defender, states[slot].Hits);
    }

    private bool AlreadyHit(int slot, int defender, int count)
    {
        int start = slot * HitSlots;
        for (int i = 0; i < count; i++)
            if (hits[start + i] == defender) return true;
        return false;
    }

    public void BeginTick(UnitState units, float dt, ObstacleField obstacles = null)
    {
        for (int slot = 0; slot < ids.Length; slot++)
        {
            int id = ids[slot];
            ref Charge charge = ref states[slot];
            charge.BypassThisTick = false;
            if (units.Health[id] <= 0f) continue;
            charge.Recovery = Mathf.Max(0f, charge.Recovery - dt);
            if (charge.Active)
            {
                if (units.Alive == units.FactionAlive[units.Faction[id]]) End(units, id, ref charge);
                continue;
            }

            charge.Start = Xz(units.Position[id]);
            charge.Approach = Vector2.Zero;
            int target = units.Target[id];
            if (target < 0 || units.Health[target] <= 0f || charge.Recovery > 0f)
            {
                charge.RunUp = 0f;
                continue;
            }

            Vector2 offset = Xz(units.Position[target]) - charge.Start;
            float distance = offset.Length();
            if (distance <= 0.0001f) continue;
            Vector2 heading = offset / distance;
            ref readonly UnitStats type = ref units.Catalog[units.Type[id]];
            float launchRange = type.AttackReach(units.Catalog[units.Type[target]]) + 2.5f;
            if (charge.Heading.Dot(heading) < 0.85f) charge.RunUp = 0f;
            charge.Heading = heading;
            if (distance <= launchRange && charge.RunUp >= type.Trample.RunUp &&
                units.MoveSpeed[id] >= type.Speed * EntrySpeedFraction &&
                obstacles?.BlocksCorridor(charge.Start, Xz(units.Position[target]), 0f) != true)
            {
                charge.Active = true;
                charge.Distance = type.Trample.Distance;
                charge.Momentum = type.Trample.Efficiency;
                charge.Hits = 0;
                continue;
            }
            charge.Approach = heading;
        }
    }

    public float MoveCharges(UnitState units, SpatialGrid grid, float dt, ObstacleField obstacles = null)
    {
        float padding = units.Catalog.MaxSpeed * dt;
        Vector2 turnStep = SoldierFacing.TurnStep(dt);
        for (int slot = 0; slot < ids.Length; slot++)
        {
            int id = ids[slot];
            if (units.Health[id] <= 0f || !states[slot].Active) continue;
            MoveCharge(units, grid, id, slot, dt, padding, turnStep, obstacles);
        }
        bool appliedShove = false;
        for (int i = 0; i < shoveCount; i++)
        {
            int id = shovedIds[i];
            Vector2 shove = shoves[id].LimitLength(MaxCombinedContactShove);
            if (units.Health[id] > 0f && shove != Vector2.Zero)
            {
                Vector2 from = Xz(units.Position[id]);
                Vector2 to = from + shove;
                Place(units, id, obstacles?.ClipMove(from, to, units.Catalog[units.Type[id]].Footprint) ?? to);
                appliedShove = true;
            }
            shoves[id] = Vector2.Zero;
            queued[id] = false;
        }
        shoveCount = 0;
        return appliedShove ? MaxCombinedContactShove : 0f;
    }

    private void MoveCharge(UnitState units, SpatialGrid grid, int id, int slot, float dt, float padding, Vector2 turnStep, ObstacleField obstacles)
    {
        ref Charge charge = ref states[slot];
        ref readonly UnitStats type = ref units.Catalog[units.Type[id]];
        charge.BypassThisTick = true;
        float momentumFraction = charge.Momentum / type.Trample.Efficiency;
        float speed = type.Speed * (0.55f + 0.45f * momentumFraction);
        float travel = Mathf.Min(charge.Distance, speed * dt);
        Vector2 origin = Xz(units.Position[id]);
        bool blocked = false;
        if (obstacles != null)
        {
            Vector2 end = origin + charge.Heading * travel;
            Vector2 safe = obstacles.ClipMove(origin, end, type.Footprint);
            blocked = safe != end;
            if (blocked)
            {
                travel = Mathf.Clamp((safe - origin).Dot(charge.Heading), 0f, travel);
            }
        }

        // Each iteration either records a new contact or ends the charge.
        while (true)
        {
            int defender = FindContact(units, grid, id, slot, origin, charge.Heading, travel, padding, obstacles, out float contact);
            if (defender < 0) break;
            if (charge.Hits == type.Trample.MaxHits)
            {
                blocked = true;
                travel = Mathf.Max(0f, contact - 0.01f);
                break;
            }

            float resistance = units.Catalog[units.Type[defender]].Trample.Resistance;
            bool penetrates = charge.Momentum > resistance;
            float damage = type.Damage * type.Trample.DamageMultiplier * (penetrates ? 1f : 0.5f) / (1f + resistance);
            units.Damage(defender, damage);
            if (!penetrates)
            {
                blocked = true;
                travel = Mathf.Max(0f, contact - 0.01f);
                break;
            }

            hits[slot * HitSlots + charge.Hits++] = defender;
            charge.Momentum -= resistance;
            if (units.Health[defender] > 0f)
                QueueShove(defender, charge.Heading * (MaxCombinedContactShove * momentumFraction / (1f + resistance)));
        }

        if (travel > 0f)
        {
            SoldierFacing.Turn(units, id, charge.Heading, turnStep);
            Place(units, id, origin + charge.Heading * travel);
        }
        charge.Distance -= travel;
        if (blocked || charge.Distance <= 0.0001f ||
            (charge.Hits >= type.Trample.MaxHits && ClearedContacts(units, id, slot, charge.Hits)))
            End(units, id, ref charge);
    }

    private bool ClearedContacts(UnitState units, int id, int slot, int count)
    {
        Vector2 position = Xz(units.Position[id]);
        float radius = units.Catalog[units.Type[id]].Footprint;
        for (int i = 0; i < count; i++)
        {
            int other = hits[slot * HitSlots + i];
            if (units.Health[other] <= 0f) continue;
            float combined = radius + units.Catalog[units.Type[other]].Footprint;
            if (position.DistanceSquaredTo(Xz(units.Position[other])) < combined * combined) return false;
        }
        return true;
    }

    private int FindContact(UnitState units, SpatialGrid grid, int id, int slot, Vector2 from,
        Vector2 heading, float length, float padding, ObstacleField obstacles, out float earliest)
    {
        float radius = units.Catalog[units.Type[id]].Footprint;
        Vector2 middle = from + heading * (length * 0.5f);
        var candidates = grid.GetCandidates(new Vector3(middle.X, 0f, middle.Y),
            length * 0.5f + radius + units.MaxFootprint + padding, units.Faction[id]);
        earliest = length;
        int result = -1;
        while (candidates.MoveNext())
        {
            int other = candidates.Current;
            if (units.Health[other] <= 0f || AlreadyHit(slot, other, states[slot].Hits)) continue;
            float combined = radius + units.Catalog[units.Type[other]].Footprint;
            Vector2 relative = Xz(units.Position[other]) - from;
            float outside = relative.LengthSquared() - combined * combined;
            float projection = relative.Dot(heading);
            float entry = 0f;
            if (outside > 0f)
            {
                float discriminant = projection * projection - outside;
                if (projection <= 0f || discriminant < 0f) continue;
                entry = projection - Mathf.Sqrt(discriminant);
            }
            else if (projection < 0f) continue; // An existing contact behind a departing charge.
            if (entry > earliest || (entry == earliest && result >= 0 && other > result)) continue;
            if (obstacles?.BlocksCorridor(from + heading * entry, Xz(units.Position[other]), 0f) == true) continue;
            earliest = entry;
            result = other;
        }
        return result;
    }

    public void MeasureRunUp(UnitState units, float dt)
    {
        for (int slot = 0; slot < ids.Length; slot++)
        {
            ref Charge charge = ref states[slot];
            int id = ids[slot];
            if (charge.Active || charge.Recovery > 0f || units.Health[id] <= 0f) continue;
            ref readonly UnitStats type = ref units.Catalog[units.Type[id]];
            float progress = (Xz(units.Position[id]) - charge.Start).Dot(charge.Approach);
            if (units.MoveSpeed[id] >= type.Speed * 0.6f && progress >= units.MoveSpeed[id] * dt * 0.7f)
                charge.RunUp = Mathf.Min(type.Trample.RunUp, charge.RunUp + progress);
            else charge.RunUp = 0f;
        }
    }

    private void QueueShove(int id, Vector2 shove)
    {
        if (!queued[id])
        {
            queued[id] = true;
            shovedIds[shoveCount++] = id;
        }
        shoves[id] += shove;
    }

    private static void End(UnitState units, int id, ref Charge charge)
    {
        charge.Active = false;
        charge.Recovery = RecoverySeconds;
        charge.RunUp = 0f;
        units.MoveSpeed[id] = 0f;
    }

    private static Vector2 Xz(Vector3 p) => new(p.X, p.Z);

    private static void Place(UnitState units, int id, Vector2 p)
    {
        units.Position[id] = new Vector3(p.X, units.Position[id].Y, p.Y);
        units.Grounding?.MarkMoved(id);
        units.Dirty[id] = true;
    }
}
