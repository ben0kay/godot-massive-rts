// Editable starting placements on a grid independent of the combat index.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

public readonly record struct SoldierPlacement(int X, int Z, byte Faction, byte Kind)
{
    public Vector3 Position() => new((X + 0.5f) * DeploymentLayout.CellSize, 0, (Z + 0.5f) * DeploymentLayout.CellSize);
}

public sealed class DeploymentLayout
{
    public const float CellSize = 2.1f;
    public const int HalfWidth = 256;
    private readonly List<SoldierPlacement> placements = new();
    private readonly Dictionary<Vector2I, int> slots = new();
    public IReadOnlyList<SoldierPlacement> Placements => placements;

    public bool TryPaint(SoldierPlacement placement, int limit, out int slot)
    {
        slot = -1;
        if (placement.X < -HalfWidth || placement.X >= HalfWidth ||
            placement.Z < -HalfWidth || placement.Z >= HalfWidth ||
            placement.Faction != (placement.X < 0 ? 0 : 1) || placement.Kind > 1) return false;

        var cell = new Vector2I(placement.X, placement.Z);
        if (slots.TryGetValue(cell, out slot))
        {
            if (placements[slot] == placement) return false;
            placements[slot] = placement;
            return true;
        }
        if (placements.Count >= limit) return false;

        slot = placements.Count;
        slots.Add(cell, slot);
        placements.Add(placement);
        return true;
    }

    public bool TryErase(Vector2I cell, out int slot)
    {
        if (!slots.Remove(cell, out slot)) return false;
        int last = placements.Count - 1;
        if (slot != last)
        {
            SoldierPlacement moved = placements[last];
            placements[slot] = moved;
            slots[new Vector2I(moved.X, moved.Z)] = slot;
        }
        placements.RemoveAt(last);
        return true;
    }

    public void Clear()
    {
        placements.Clear();
        slots.Clear();
    }
}
