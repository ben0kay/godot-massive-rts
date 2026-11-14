// Editable starting placements on a grid independent of the combat index.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

public readonly record struct SoldierPlacement(int X, int Z, byte Faction, byte Type, float GridSize = DeploymentLayout.CellSize)
{
    public Vector3 Position() => new(
        (X / (float)DeploymentLayout.SoldierSubdivisions + 0.5f) * GridSize, 0,
        (Z / (float)DeploymentLayout.SoldierSubdivisions + 0.5f) * GridSize);
}

public sealed class DeploymentLayout
{
    public const float CellSize = 2f;
    public const float LegacyCellSize = 2.1f;
    public const float WorldHalfWidth = 2500f;
    public static int CellHalfWidth(float gridSize) => (int)(WorldHalfWidth / gridSize);
    public const int SoldierSubdivisions = 40;
    public float GridSize { get; }
    public float SoldierStep => GridSize / SoldierSubdivisions;
    public int HalfWidth => CellHalfWidth(GridSize);
    private int SoldierBoundary => HalfWidth * SoldierSubdivisions;
    private readonly List<SoldierPlacement> placements = new();
    private readonly Dictionary<Vector2I, int> slots = new();
    private readonly Dictionary<Vector2I, List<Vector2I>> soldierBuckets = new();
    private readonly RuntimeUnitCatalog catalog;

    private static Vector2I Bucket(Vector2I point) => new(
        Mathf.FloorToInt(point.X / (float)SoldierSubdivisions + 0.5f),
        Mathf.FloorToInt(point.Y / (float)SoldierSubdivisions + 0.5f));
    public ObstacleLayout Obstacles { get; private set; }

    public const int MaxBuildings = 64;
    private readonly RuntimeBuildingCatalog buildings;
    private readonly List<BuildingPlacement> defenses = new();
    public IReadOnlyList<BuildingPlacement> Defenses => defenses;
    public ObstacleField CollisionField { get; private set; }

    public DeploymentLayout(RuntimeUnitCatalog catalog, RuntimeBuildingCatalog buildings, float gridSize = CellSize)
    {
        GridSize = gridSize;
        Obstacles = new ObstacleLayout(gridSize);
        this.catalog = catalog;
        this.buildings = buildings;
    }

    // =========================================================
    // Walls and buildings keep separate authoring records and share the compiled navigation index.
    public void RebuildCollision()
    {
        if (defenses.Count == 0) { CollisionField = Obstacles.Field; return; }
        var walls = Obstacles.Field;
        int count = walls?.RectangleCount ?? 0;
        var rectangles = new ObstacleRectangle[count + defenses.Count];
        if (walls != null) walls.Rectangles.CopyTo(rectangles);
        for (int i = 0; i < defenses.Count; i++) rectangles[count + i] = defenses[i].Footprint(buildings);
        CollisionField = new ObstacleField(rectangles);
    }

    public bool CanPlaceDefense(BuildingPlacement placement)
    {
        if (placement.Faction >= Factions.Count || (uint)placement.Type >= (uint)buildings.Count ||
            defenses.Count >= MaxBuildings) return false;
        ObstacleRectangle area = placement.Footprint(buildings);
        if (!area.IsValid) return false;
        if (Obstacles.Field != null)
            foreach (var wall in Obstacles.Field.Rectangles)
                if (Overlaps(area, wall)) return false;
        foreach (var defense in defenses)
            if (Overlaps(area, defense.Footprint(buildings))) return false;

        int padding = Mathf.CeilToInt(catalog.MaxFootprint / GridSize);
        for (int z = area.Z - padding; z < area.Z + area.Depth + padding; z++)
        for (int x = area.X - padding; x < area.X + area.Width + padding; x++)
        {
            if (!soldierBuckets.TryGetValue(new Vector2I(x, z), out var bucket)) continue;
            foreach (var key in bucket)
            {
                SoldierPlacement soldier = placements[slots[key]];
                Vector2 point = ObstacleField.Xz(soldier.Position());
                Vector2 closest = point.Max(area.Min).Min(area.Max);
                float radius = catalog[soldier.Type].Footprint;
                if (point.DistanceSquaredTo(closest) <= radius * radius) return false;
            }
        }
        return true;
    }

    public bool TryPlaceDefense(BuildingPlacement placement)
    {
        if (!CanPlaceDefense(placement)) return false;
        defenses.Add(placement);
        RebuildCollision();
        return true;
    }

    public bool TryEraseDefense(Vector2I cell)
    {
        int index = defenses.FindIndex(defense => Overlaps(defense.Footprint(buildings),
            new ObstacleRectangle(cell.X, cell.Y, 1, 1) { GridSize = GridSize }));
        if (index < 0) return false;
        defenses.RemoveAt(index);
        RebuildCollision();
        return true;
    }

    private static bool Overlaps(ObstacleRectangle a, ObstacleRectangle b) =>
        a.X < b.X + b.Width && b.X < a.X + a.Width && a.Z < b.Z + b.Depth && b.Z < a.Z + a.Depth;

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

    public bool TryPaint(SoldierPlacement placement, out int slot)
    {
        slot = -1;
        if (placement.X < -SoldierBoundary - SoldierSubdivisions / 2 || placement.X >= SoldierBoundary - SoldierSubdivisions / 2 ||
            placement.Z < -SoldierBoundary - SoldierSubdivisions / 2 || placement.Z >= SoldierBoundary - SoldierSubdivisions / 2 ||
            placement.Faction >= Factions.Count || placement.Type >= catalog.Count) return false;

        var cell = new Vector2I(placement.X, placement.Z);
        float radius = catalog[placement.Type].Footprint;
        Vector3 position = placement.Position();
        float edge = HalfWidth * GridSize;
        if (Mathf.Abs(position.X) + radius > edge || Mathf.Abs(position.Z) + radius > edge) return false;

        if (CollisionField?.IntersectsCircle(ObstacleField.Xz(position), radius) == true) return false;

        Vector2I center = Bucket(cell);
        int extent = Mathf.CeilToInt((radius + catalog.MaxFootprint) / GridSize);
        for (int z = center.Y - extent; z <= center.Y + extent; z++)
        for (int x = center.X - extent; x <= center.X + extent; x++)
        {
            if (!soldierBuckets.TryGetValue(new Vector2I(x, z), out var neighbors)) continue;
            foreach (var neighbor in neighbors)
            {
                if (neighbor == cell) continue;
                float spacing = radius + catalog[placements[slots[neighbor]].Type].Footprint;
                float dx = (neighbor.X - cell.X) * SoldierStep, dz = (neighbor.Y - cell.Y) * SoldierStep;
                if (dx * dx + dz * dz < spacing * spacing) return false;
            }
        }

        if (slots.TryGetValue(cell, out slot))
        {
            if (placements[slot] == placement) return false;
            placements[slot] = placement;
            return true;
        }

        slot = placements.Count;
        slots.Add(cell, slot);
        if (!soldierBuckets.TryGetValue(center, out var bucket))
            soldierBuckets.Add(center, bucket = new List<Vector2I>());
        bucket.Add(cell);
        placements.Add(placement);
        return true;
    }

    public bool TryErase(Vector2I cell, out int slot)
    {
        if (!slots.Remove(cell, out slot)) return false;
        var bucketKey = Bucket(cell);
        var bucket = soldierBuckets[bucketKey];
        bucket.Remove(cell);
        if (bucket.Count == 0) soldierBuckets.Remove(bucketKey);
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

    public bool TryEditObstacles(ObstacleRectangle area, bool erase, out string message)
    {
        if (!Obstacles.TryEdit(area, erase, out ObstacleLayout draft))
        {
            message = $"Obstacle edit exceeds battlefield bounds or the {ObstacleLayout.MaxCells:N0}-cell / {ObstacleLayout.MaxRectangles}-rectangle limit.";
            return false;
        }
        message = null;
        if (ReferenceEquals(draft, Obstacles)) return false;
        if (!erase && draft.Field != null)
        {
            foreach (var defense in defenses)
            {
                if (!Overlaps(area, defense.Footprint(buildings))) continue;
                message = "Obstacle overlaps a defense footprint.";
                return false;
            }
            foreach (SoldierPlacement placement in placements)
            {
                if (!draft.Field.IntersectsCircle(ObstacleField.Xz(placement.Position()), catalog[placement.Type].Footprint)) continue;
                message = "Obstacle overlaps a soldier’s footprint.";
                return false;
            }
        }
        Obstacles = draft;
        RebuildCollision();
        return true;
    }

    public void SetLoadedObstacles(ObstacleLayout obstacles)
    {
        Obstacles = obstacles;
        RebuildCollision();
    }

    public void Clear()
    {
        Obstacles = new ObstacleLayout(GridSize);
        CollisionField = null;
        defenses.Clear();
        placements.Clear();
        slots.Clear();
        soldierBuckets.Clear();
    }
}
