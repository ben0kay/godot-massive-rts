// Editable starting placements on a grid independent of the combat index.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

public readonly record struct SoldierPlacement(int X, int Z, byte Faction, byte Type)
{
    public Vector3 Position() => new((X + 0.5f) * DeploymentLayout.CellSize, 0, (Z + 0.5f) * DeploymentLayout.CellSize);
}

public sealed class DeploymentLayout
{
    public const float CellSize = 2.1f;
    public const int HalfWidth = 256;
    private readonly List<SoldierPlacement> placements = new();
    private readonly Dictionary<Vector2I, int> slots = new();
    private readonly RuntimeUnitCatalog catalog;

    public DeploymentLayout(RuntimeUnitCatalog catalog) => this.catalog = catalog;

    public bool TryGet(Vector2I cell, out SoldierPlacement placement)
    {
        if (slots.TryGetValue(cell, out int slot))
        {
            placement = placements[slot];
            return true;
        }
        placement = default;
        return false;
    }

    public IReadOnlyList<SoldierPlacement> Placements => placements;

    public bool TryPaint(SoldierPlacement placement, int limit, out int slot)
    {
        slot = -1;
        if (placement.X < -HalfWidth || placement.X >= HalfWidth ||
            placement.Z < -HalfWidth || placement.Z >= HalfWidth ||
            placement.Faction != (placement.X < 0 ? 0 : 1) || placement.Type >= catalog.Count) return false;

        var cell = new Vector2I(placement.X, placement.Z);
        float radius = catalog[placement.Type].Footprint;
        Vector3 position = placement.Position();
        float edge = HalfWidth * CellSize;
        if (Mathf.Abs(position.X) + radius > edge || Mathf.Abs(position.Z) + radius > edge) return false;

        int extent = Mathf.Min(HalfWidth * 2 - 1, Mathf.CeilToInt((radius + catalog.MaxFootprint) / CellSize));
        for (int z = System.Math.Max(-HalfWidth, cell.Y - extent); z <= System.Math.Min(HalfWidth - 1, cell.Y + extent); z++)
        for (int x = System.Math.Max(-HalfWidth, cell.X - extent); x <= System.Math.Min(HalfWidth - 1, cell.X + extent); x++)
        {
            if ((x == cell.X && z == cell.Y) || !slots.TryGetValue(new Vector2I(x, z), out int neighbor)) continue;
            float spacing = radius + catalog[placements[neighbor].Type].Footprint;
            float dx = (x - cell.X) * CellSize, dz = (z - cell.Y) * CellSize;
            if (dx * dx + dz * dz < spacing * spacing) return false;
        }

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
