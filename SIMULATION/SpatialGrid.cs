// Reused linked-cell index with candidate-bounded local enemy queries.
using System;
using System.Collections.Generic;
using BitOperations = System.Numerics.BitOperations;
using Godot;
namespace MassiveRts;

public sealed class SpatialGrid
{
    private readonly Dictionary<Vector3I, int> overflowHeads = new();
    private readonly int[] heads, occupied;
    private readonly int halfWidth, width;
    private int occupiedCount;
    private readonly int[] next;
    private readonly float cellSize;
    private readonly Vector2[] factionMin = new Vector2[Factions.Count];
    private readonly Vector2[] factionMax = new Vector2[Factions.Count];
    private readonly OccupiedCellTree[] pursuitTrees;
    private bool pursuitTreesReady;
    private int occupiedFactions;
    public int LimitedQueries { get; private set; }
    public SpatialGrid(int capacity, float size)
    {
        next = new int[capacity];
        occupied = new int[capacity];
        cellSize = size;
        pursuitTrees = new OccupiedCellTree[Factions.Count];
        for (int i = 0; i < pursuitTrees.Length; i++) pursuitTrees[i] = new OccupiedCellTree(size);
        halfWidth = Mathf.CeilToInt(640f / size);
        width = halfWidth * 2;
        heads = new int[width * width * Factions.Count];
    }

    // Zero denotes an empty cell. Stored IDs are offset by one.
    private int DenseIndex(int x, int z, int faction)
    {
        x += halfWidth;
        z += halfWidth;
        return (uint)x < (uint)width && (uint)z < (uint)width ? (z * width + x) * Factions.Count + faction : -1;
    }

    private int Head(int x, int z, int faction)
    {
        int index = DenseIndex(x, z, faction);
        if (index >= 0) return heads[index] - 1;
        return overflowHeads.TryGetValue(new Vector3I(x, z, faction), out int id) ? id : -1;
    }
    private Vector2I Cell(Vector3 p) => new(Mathf.FloorToInt(p.X / cellSize), Mathf.FloorToInt(p.Z / cellSize));
    // =========================================================
    public void Rebuild(UnitState units)
    {
        for (int i = 0; i < occupiedCount; i++) heads[occupied[i]] = 0;
        occupiedCount = 0;
        occupiedFactions = 0;
        overflowHeads.Clear();
        LimitedQueries = 0;
        pursuitTreesReady = false;
        for (int faction = 0; faction < Factions.Count; faction++)
        {
            factionMin[faction] = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            factionMax[faction] = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        }
        // Reverse insertion preserves low IDs at the head; rotating queries change their start cell.
        for (int i = units.Count - 1; i >= 0; i--)
        {
            if (units.Health[i] <= 0) continue;
            Vector3 position = units.Position[i];
            int faction = units.Faction[i];
            occupiedFactions |= 1 << faction;
            var point = new Vector2(position.X, position.Z);
            factionMin[faction] = factionMin[faction].Min(point);
            factionMax[faction] = factionMax[faction].Max(point);
            var cell = Cell(position);
            int index = DenseIndex(cell.X, cell.Y, faction);
            if (index >= 0)
            {
                int previous = heads[index];
                if (previous == 0) occupied[occupiedCount++] = index;
                next[i] = previous - 1;
                heads[index] = i + 1;
            }
            else
            {
                var key = new Vector3I(cell.X, cell.Y, faction);
                next[i] = overflowHeads.TryGetValue(key, out int first) ? first : -1;
                overflowHeads[key] = i;
            }
        }
    }
    // =========================================================
    public int FindEnemy(UnitState units, int id, float radius, int budget, int tick, ObstacleField clearSight = null)
    {
        Vector3 position = units.Position[id];
        int enemies = EnemiesOf(units.Faction[id]);
        // A disjoint XZ interval proves the spherical query cannot reach that faction.
        for (int mask = enemies; mask != 0; mask &= mask - 1)
        {
            int faction = BitOperations.TrailingZeroCount(mask);
            Vector2 min = factionMin[faction], max = factionMax[faction];
            if (position.X + radius < min.X || position.X - radius > max.X ||
                position.Z + radius < min.Y || position.Z - radius > max.Y)
                enemies &= ~(1 << faction);
        }
        if (enemies == 0) return -1;
        Vector2I center = Cell(position);
        int cells = Mathf.CeilToInt(radius / cellSize), examined = 0, best = -1;
        float bestDistance = radius * radius;
        // Search center outwards so dense distant cells cannot consume the entire query first.
        for (int ring = 0; ring <= cells; ring++)
        {
            int width = ring * 2 + 1, perimeter = ring == 0 ? 1 : ring * 8;
            int start = (id + tick) % perimeter;
            for (int k = 0; k < perimeter; k++)
            {
                int edge = (k + start) % perimeter;
                int dx, dz;
                if (ring == 0) { dx = 0; dz = 0; }
                else if (edge < width) { dx = edge - ring; dz = -ring; }
                else if (edge < width + width - 1) { dx = ring; dz = edge - width - ring + 1; }
                else if (edge < width * 3 - 2) { dx = ring - 1 - (edge - (width * 2 - 1)); dz = ring; }
                else { dx = -ring; dz = ring - 1 - (edge - (width * 3 - 2)); }
                for (int mask = enemies; mask != 0; mask &= mask - 1)
                {
                    int faction = BitOperations.TrailingZeroCount(mask);
                    for (int other = Head(center.X + dx, center.Y + dz, faction); other >= 0; other = next[other])
                    {
                        if (++examined > budget) { LimitedQueries++; return best; }
                        if (units.Health[other] <= 0f) continue;
                        float distance = position.DistanceSquaredTo(units.Position[other]);
                        if (distance < bestDistance && clearSight?.BlocksSight(position, units.Position[other]) != true)
                        { bestDistance = distance; best = other; }
                    }
                }
            }
        }
        return best;
    }

    // Long-range queries share a tree built once per tick, only when needed.
    public int FindPursuitEnemy(UnitState units, int id, int previous)
    {
        int enemies = EnemiesOf(units.Faction[id]);
        if (enemies == 0) return -1;
        if (!pursuitTreesReady)
        {
            foreach (OccupiedCellTree tree in pursuitTrees) tree.Clear();
            for (int i = 0; i < occupiedCount; i++)
            {
                int index = occupied[i];
                int cell = index / Factions.Count;
                pursuitTrees[index % Factions.Count].Add(cell % width - halfWidth, cell / width - halfWidth, heads[index] - 1);
            }
            foreach (var entry in overflowHeads)
                pursuitTrees[entry.Key.Z].Add(entry.Key.X, entry.Key.Y, entry.Value);
            foreach (OccupiedCellTree tree in pursuitTrees) tree.Build();
            pursuitTreesReady = true;
        }
        int best = previous;
        for (int mask = enemies; mask != 0; mask &= mask - 1)
        {
            int faction = BitOperations.TrailingZeroCount(mask);
            int found = pursuitTrees[faction].FindNearest(units, next, id, best);
            if (found >= 0) best = found;
        }
        return best;
    }

    private int EnemiesOf(int faction) => occupiedFactions & ~(1 << faction);

    // Enumerate opposing pre-movement cells without allocating a candidate list.
    public CandidateCursor GetCandidates(Vector3 center, float radius, int sourceFaction)
    {
        Vector3 extent = new(radius, 0f, radius);
        return new CandidateCursor(this, Cell(center - extent), Cell(center + extent), EnemiesOf(sourceFaction));
    }

    public struct CandidateCursor
    {
        private readonly SpatialGrid grid;
        private readonly Vector2I min, max;
        private readonly int enemies;
        private int x, z, nextId, remainingFactions;
        public int Current { get; private set; }

        internal CandidateCursor(SpatialGrid grid, Vector2I min, Vector2I max, int enemies)
        {
            this.grid = grid;
            this.min = min;
            this.max = max;
            this.enemies = enemies;
            x = min.X;
            z = min.Y;
            remainingFactions = enemies;
            nextId = Current = -1;
        }

        public bool MoveNext()
        {
            if (enemies == 0) return false;
            while (nextId < 0)
            {
                if (remainingFactions == 0)
                {
                    if (x == max.X) { x = min.X; z++; }
                    else x++;
                    remainingFactions = enemies;
                }
                if (z > max.Y) return false;
                int faction = BitOperations.TrailingZeroCount(remainingFactions);
                remainingFactions &= remainingFactions - 1;
                nextId = grid.Head(x, z, faction);
            }
            Current = nextId;
            nextId = grid.next[Current];
            return true;
        }
    }

    // Search pre-movement cells padded by the maximum distance a soldier moved this tick.
    public int FirstProjectileHit(UnitState units, int sourceFaction, Vector3 from, Vector3 segment, float radius, float movementPadding, float before = float.PositiveInfinity)
    {
        float padding = Mathf.Max(radius, units.Catalog.MaxFootprint) + movementPadding;
        Vector3 end = from + segment;
        var min = Cell(new Vector3(Mathf.Min(from.X, end.X) - padding, 0, Mathf.Min(from.Z, end.Z) - padding));
        var max = Cell(new Vector3(Mathf.Max(from.X, end.X) + padding, 0, Mathf.Max(from.Z, end.Z) + padding));
        float lengthSquared = segment.LengthSquared();
        float earliest = before;
        int hit = -1;
        var candidates = new CandidateCursor(this, min, max, EnemiesOf(sourceFaction));
        while (candidates.MoveNext())
        {
            int id = candidates.Current;
            if (units.Health[id] <= 0) continue;
            ref readonly UnitStats type = ref units.Catalog[units.Type[id]];
            float hitRadius = Mathf.Max(radius, type.Footprint);
            Vector3 relative = units.Position[id] + Vector3.Up * (type.Height * 0.5f) - from;
            float outside = relative.LengthSquared() - hitRadius * hitRadius;
            float entry = 0;
            if (outside > 0)
            {
                float projection = relative.Dot(segment);
                float discriminant = projection * projection - lengthSquared * outside;
                if (lengthSquared <= 0 || projection <= 0 || discriminant < 0) continue;
                entry = (projection - Mathf.Sqrt(discriminant)) / lengthSquared;
                if (entry > 1) continue;
            }
            if (entry >= earliest) continue;
            earliest = entry;
            hit = id;
            if (entry == 0) return hit;
        }
        return hit;
    }
}
