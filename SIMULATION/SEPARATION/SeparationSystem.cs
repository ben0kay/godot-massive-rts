// Resolves local XZ footprint overlaps with bounded queries and simultaneous corrections.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Godot;

namespace MassiveRts;

public enum SeparationMode { Off, Normal, Strong }

public sealed class SeparationSystem
{
    private const float Slop = 0.015f;
    private readonly Dictionary<Vector2I, int> heads;
    private readonly int[] next;
    private readonly Vector2[] corrections;

    public int OverlapPairs { get; private set; }
    public int BudgetHits { get; private set; }

    public SeparationSystem(int capacity)
    {
        heads = new Dictionary<Vector2I, int>(capacity);
        next = new int[capacity];
        corrections = new Vector2[capacity];
    }

    // Each pass gets a share of the tick-wide cap. Their combined displacement stays within the cap.
    public void Step(UnitState units, int passes, float gap, int neighbourBudget, float tickPushCap, int tick)
    {
        OverlapPairs = BudgetHits = 0;
        float cellWidth = Mathf.Max(0.1f, units.Catalog.MaxFootprint * 2f + gap);
        float inverseCellWidth = 1f / cellWidth;
        float passCap = tickPushCap / passes;
        for (int pass = 0; pass < passes; pass++)
        {
            int first = (int)(((long)tick * 97 + pass * 53) % units.Count);
            Build(units, inverseCellWidth, first);
            Array.Clear(corrections, 0, units.Count);
            for (int rank = 0; rank < units.Count; rank++)
            {
                int id = IdAtRank(rank, first, units.Count);
                if (units.Health[id] > 0)
                    Collect(units, id, rank, first, inverseCellWidth, gap, neighbourBudget, tick + pass);
            }
            Apply(units, passCap);
        }
    }

    private static int IdAtRank(int rank, int first, int count)
    {
        int id = rank + first;
        return id < count ? id : id - count;
    }

    private static Vector2I Cell(Vector3 position, float inverseWidth) =>
        new(Mathf.FloorToInt(position.X * inverseWidth), Mathf.FloorToInt(position.Z * inverseWidth));

    private void Build(UnitState units, float inverseWidth, int first)
    {
        heads.Clear();
        // Link entries in rotating order so dense buckets do not always favor low IDs.
        for (int rank = units.Count - 1; rank >= 0; rank--)
        {
            int id = IdAtRank(rank, first, units.Count);
            if (units.Health[id] <= 0) continue;
            ref int head = ref CollectionsMarshal.GetValueRefOrAddDefault(heads, Cell(units.Position[id], inverseWidth), out bool occupied);
            next[id] = occupied ? head : -1;
            head = id;
        }
    }

    private void Collect(UnitState units, int id, int rank, int first, float inverseWidth, float gap, int budget, int phase)
    {
        Vector3 position = units.Position[id];
        Vector2I center = Cell(position, inverseWidth);
        float ownRadius = units.Catalog[units.Type[id]].Footprint;
        int visited = 0;
        int firstCell = (id + phase) % 9;
        // Cell width covers the widest possible pair, so adjacent cells suffice.
        for (int offset = 0; offset < 9; offset++)
        {
            int cell = (firstCell + offset) % 9;
            var key = center + new Vector2I(cell % 3 - 1, cell / 3 - 1);
            if (!heads.TryGetValue(key, out int other)) continue;
            for (; other >= 0; other = next[other])
            {
                // Count every bucket entry before the pair-ownership filter.
                if (visited == budget)
                {
                    BudgetHits++;
                    return;
                }
                visited++;

                int otherRank = other >= first ? other - first : other + units.Count - first;
                if (otherRank <= rank) continue;
                Vector3 otherPosition = units.Position[other];
                var away = new Vector2(position.X - otherPosition.X, position.Z - otherPosition.Z);
                float limit = ownRadius + units.Catalog[units.Type[other]].Footprint + gap - Slop;
                float distanceSquared = away.LengthSquared();
                if (limit <= 0f || distanceSquared >= limit * limit) continue;

                float distance = Mathf.Sqrt(distanceSquared);
                Vector2 direction = distance > 0.00001f ? away / distance : CoincidentDirection(id, other);
                Vector2 push = direction * ((limit - distance) * 0.5f);
                corrections[id] += push;
                corrections[other] -= push;
                OverlapPairs++;
            }
        }
    }

    private static Vector2 CoincidentDirection(int id, int other)
    {
        uint low = (uint)Math.Min(id, other), high = (uint)Math.Max(id, other);
        uint hash = unchecked((low * 73856093u) ^ (high * 19349663u));
        float angle = (hash & 255u) * (Mathf.Tau / 256f);
        var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        return id < other ? direction : -direction;
    }

    private void Apply(UnitState units, float cap)
    {
        float capSquared = cap * cap;
        for (int id = 0; id < units.Count; id++)
        {
            Vector2 push = corrections[id];
            float lengthSquared = push.LengthSquared();
            if (lengthSquared == 0f) continue;
            if (lengthSquared > capSquared) push *= cap / Mathf.Sqrt(lengthSquared);
            Vector3 position = units.Position[id];
            position.X += push.X;
            position.Z += push.Y;
            position.Y = BattlefieldHeight.At(position.X, position.Z);
            units.Position[id] = position;
            units.Dirty[id] = true;
        }
    }
}
