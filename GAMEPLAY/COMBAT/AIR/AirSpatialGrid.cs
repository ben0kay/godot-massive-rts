// Indexes living aircraft in sparse XZ cells for bounded, altitude-aware acquisition.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed class AirSpatialGrid
{
    private const float CellSize = 24f;
    private const int CandidateLimit = 64;
    private readonly Dictionary<Vector2I, int> heads;
    private readonly int[] next;

    internal AirSpatialGrid(int capacity)
    {
        heads = new Dictionary<Vector2I, int>(capacity);
        next = new int[capacity];
    }

    private static Vector2I Cell(Vector3 position) => new(
        Mathf.FloorToInt(position.X / CellSize), Mathf.FloorToInt(position.Z / CellSize));

    internal void Rebuild(AirSquadron.Fighter[] fighters)
    {
        heads.Clear();
        for (int id = fighters.Length - 1; id >= 0; id--)
        {
            if (fighters[id].Health <= 0f) continue;
            var cell = Cell(fighters[id].Position);
            next[id] = heads.GetValueOrDefault(cell, -1);
            heads[cell] = id;
        }
    }

    internal int FindEnemy(AirSquadron.Fighter[] fighters, int seeker, float radius)
    {
        ref var fighter = ref fighters[seeker];
        Vector2I center = Cell(fighter.Position);
        int extent = Mathf.CeilToInt(radius / CellSize);
        int remaining = CandidateLimit;
        int best = -1;
        float closest = radius * radius;
        for (int ring = 0; ring <= extent; ring++)
        for (int z = -ring; z <= ring; z++)
        for (int x = -ring; x <= ring; x++)
        {
            if (Math.Max(Math.Abs(x), Math.Abs(z)) != ring) continue;
            for (int id = heads.GetValueOrDefault(center + new Vector2I(x, z), -1); id >= 0; id = next[id])
            {
                ref var candidate = ref fighters[id];
                if (candidate.Faction == fighter.Faction || candidate.Health <= 0f) continue;
                float distance = fighter.Position.DistanceSquaredTo(candidate.Position);
                if (distance < closest)
                {
                    best = id;
                    closest = distance;
                }
                if (--remaining == 0) return best;
            }
        }
        return best;
    }
}
