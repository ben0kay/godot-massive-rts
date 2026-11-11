// Resolves local XZ footprint overlaps with bounded queries and simultaneous corrections.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Godot;

namespace MassiveRts;

public enum SeparationMode { Off, Normal, Strong }

public sealed class SeparationSystem
{
    private const float Slop = 0.025f;
    private const int ForwardNeighbours = 4;
    private static readonly Vector2I[] ForwardCells =
    {
        new(1, 0), new(-1, 1), new(0, 1), new(1, 1)
    };
    private readonly Dictionary<Vector2I, int> buckets;
    private readonly Vector2I[] bucketCells;
    private readonly int[] bucketHeads, neighbourHeads, soldierBuckets, next;
    private readonly int[] contacts;
    private readonly Vector2[] corrections;

    public int OverlapPairs { get; private set; }
    public int BudgetHits { get; private set; }

    public SeparationSystem(int capacity)
    {
        buckets = new Dictionary<Vector2I, int>(capacity);
        bucketCells = new Vector2I[capacity];
        bucketHeads = new int[capacity];
        neighbourHeads = new int[capacity * ForwardNeighbours];
        soldierBuckets = new int[capacity];
        next = new int[capacity];
        contacts = new int[capacity];
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
            Build(units, inverseCellWidth, first, (tick + pass) & 3);
            Array.Clear(corrections, 0, units.Count);
            Array.Clear(contacts, 0, units.Count);
            for (int rank = 0; rank < units.Count; rank++)
            {
                int id = IdAtRank(rank, first, units.Count);
                if (units.Health[id] > 0)
                    Collect(units, id, gap, neighbourBudget, tick + pass);
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

    private void Build(UnitState units, float inverseWidth, int first, int rotation)
    {
        buckets.Clear();
        int bucketCount = 0;
        for (int rank = units.Count - 1; rank >= 0; rank--)
        {
            int id = IdAtRank(rank, first, units.Count);
            if (units.Health[id] <= 0) continue;
            Vector2I cell = Cell(units.Position[id], inverseWidth);
            ref int bucket = ref CollectionsMarshal.GetValueRefOrAddDefault(buckets, cell, out bool occupied);
            if (!occupied)
            {
                bucket = bucketCount++;
                bucketCells[bucket] = cell;
                bucketHeads[bucket] = -1;
            }
            soldierBuckets[id] = bucket;
            next[id] = bucketHeads[bucket];
            bucketHeads[bucket] = id;
        }

        // Cache four neighbouring heads per occupied bucket. A half stencil visits
        // each cross-cell pair once, with its direction rotated to share priority.
        for (int bucket = 0; bucket < bucketCount; bucket++)
        for (int i = 0; i < ForwardNeighbours; i++)
        {
            Vector2I offset = ForwardCells[i];
            offset = rotation switch
            {
                1 => new Vector2I(-offset.Y, offset.X),
                2 => -offset,
                3 => new Vector2I(offset.Y, -offset.X),
                _ => offset
            };
            neighbourHeads[bucket * ForwardNeighbours + i] =
                buckets.TryGetValue(bucketCells[bucket] + offset, out int neighbour) ? bucketHeads[neighbour] : -1;
        }
    }

    private void Collect(UnitState units, int id, float gap, int budget, int phase)
    {
        Vector3 position = units.Position[id];
        float ownRadius = units.Catalog[units.Type[id]].Footprint;
        int visited = 0;
        int firstBucket = (id + phase) % (ForwardNeighbours + 1);
        int neighbourStart = soldierBuckets[id] * ForwardNeighbours;
        for (int offset = 0; offset <= ForwardNeighbours; offset++)
        {
            int bucket = (firstBucket + offset) % (ForwardNeighbours + 1);
            int other = bucket == ForwardNeighbours ? next[id] : neighbourHeads[neighbourStart + bucket];
            for (; other >= 0; other = next[other])
            {
                if (visited == budget)
                {
                    BudgetHits++;
                    return;
                }
                visited++;
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
                contacts[id]++;
                contacts[other]++;
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
            // Several overlapping neighbours can add large opposing corrections.
            // Dampen packed contacts while keeping the full response for isolated pairs.
            if (contacts[id] > 2) push *= 2f / contacts[id];
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
