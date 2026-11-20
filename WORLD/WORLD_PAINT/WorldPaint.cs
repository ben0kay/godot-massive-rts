// Owns level-local paint and builds matching visuals and gameplay footprints.
using System;
using System.Collections.Generic;
using Godot;
using Cells = Godot.Collections.Array<Godot.Vector2I>;

namespace MassiveRts;

[Tool]
public partial class WorldPaint : Node3D
{
    internal const float CellSize = DeploymentLayout.CellSize;
    internal const int MaxPaintedCells = 16384;
    internal const int MaxPaintedRectangles = 128;

    private WorldPaintData data;
    private WorldPaintData observedData;
    private CommandTerrain terrain;
    private MeshInstance3D rocksView, waterView, bridgeView;
    private Node3D forestView;
    internal ForestCover Forest { get; private set; }
    internal ObstacleRectangle[] MovementWater { get; private set; } = Array.Empty<ObstacleRectangle>();
    internal Rect2[] BridgeAreas { get; private set; } = Array.Empty<Rect2>();
    internal const int MaxBridges = 32;
    internal const int MaxBridgeWidthCells = 24;
    private bool visualDirty;
    private float displayedHalfWidth;

    [Export]
    public WorldPaintData Data
    {
        get => data;
        set
        {
            data = value;
            if (IsInsideTree()) ObserveData(data);
            Invalidate();
        }
    }

    internal float MapHalfWidth => GetParent() is CommandSandbox level ? level.MapSize * 0.5f : 0f;
    internal int GridEdge => Mathf.FloorToInt(MapHalfWidth / CellSize);
    internal TerrainHeightField Surface => terrain?.Field;

    // =========================================================
    public override void _EnterTree()
    {
        terrain = CommandTerrain.Find(this);
        if (terrain != null) terrain.SurfaceChanged += Invalidate;
        ObserveData(data);
        Invalidate();
    }

    public override void _Ready()
    {
        rocksView = AddView("RockPreview");
        waterView = AddView("WaterPreview");
        bridgeView = AddView("BridgePreview");
        if (Engine.IsEditorHint()) Invalidate();
        SetProcess(Engine.IsEditorHint());
    }

    public override void _ExitTree()
    {
        if (terrain != null) terrain.SurfaceChanged -= Invalidate;
        terrain = null;
        ObserveData(null);
    }

    private void ObserveData(WorldPaintData resource)
    {
        if (observedData == resource) return;
        if (IsInstanceValid(observedData)) observedData.Changed -= Invalidate;
        observedData = resource;
        if (IsInstanceValid(observedData)) observedData.Changed += Invalidate;
    }

    public override void _Process(double delta)
    {
        if (displayedHalfWidth != MapHalfWidth) Invalidate();
        if (!visualDirty) return;
        visualDirty = false;
        displayedHalfWidth = MapHalfWidth;
        RebuildViews(new HashSet<Vector2I>(data?.RockCells ?? new Cells()),
            new HashSet<Vector2I>(data?.WaterCells ?? new Cells()));
        RebuildForest();
        UpdateConfigurationWarnings();
    }

    public override string[] _GetConfigurationWarnings()
    {
        if (data == null) return Array.Empty<string>();
        foreach (var cell in data.RockCells)
            if (!Contains(cell)) return new[] { "Some World Paint cells lie beyond this map. They are kept in the level and skipped until they fit." };
        foreach (var cell in data.WaterCells)
            if (!Contains(cell)) return new[] { "Some World Paint cells lie beyond this map. They are kept in the level and skipped until they fit." };
        foreach (var cell in data.ForestCells)
            if (!Contains(cell)) return new[] { "Some forest cells lie beyond this map." };
        return Array.Empty<string>();
    }

    // =========================================================
    internal bool Contains(Vector2I cell)
    {
        int edge = GridEdge;
        return cell.X >= -edge && cell.X < edge && cell.Y >= -edge && cell.Y < edge;
    }

    internal bool TryMerge(HashSet<Vector2I> rocks, HashSet<Vector2I> water,
        out ObstacleRectangle[] rockAreas, out ObstacleRectangle[] waterAreas,
        Godot.Collections.Array<Rect2I> bridges = null) =>
        TryMerge(rocks, water, out rockAreas, out waterAreas, out _, bridges ?? data?.Bridges);

    private bool TryMerge(HashSet<Vector2I> rocks, HashSet<Vector2I> water,
        out ObstacleRectangle[] rockAreas, out ObstacleRectangle[] waterAreas,
        out ObstacleRectangle[] movementWater, Godot.Collections.Array<Rect2I> bridges)
    {
        rockAreas = waterAreas = movementWater = null;
        rocks.RemoveWhere(cell => !Contains(cell));
        water.RemoveWhere(cell => !Contains(cell));
        water.ExceptWith(rocks);
        if (rocks.Count + water.Count > MaxPaintedCells) return false;
        rockAreas = ObstacleCellCompiler.Merge(rocks, CellSize, MaxPaintedRectangles);
        if (rockAreas == null) return false;
        waterAreas = ObstacleCellCompiler.Merge(water, CellSize, MaxPaintedRectangles - rockAreas.Length);
        if (waterAreas == null) return false;
        if (bridges == null || bridges.Count == 0)
        {
            movementWater = waterAreas;
            return true;
        }
        if (bridges.Count > MaxBridges) return false;
        long deckCells = 0;
        foreach (var bridge in bridges)
            if (BridgeFits(bridge)) deckCells += (long)bridge.Size.X * bridge.Size.Y;
        if (deckCells > MaxPaintedCells) return false;
        var openWater = new HashSet<Vector2I>(water);
        OpenBridgeCells(openWater, bridges);
        movementWater = ObstacleCellCompiler.Merge(openWater, CellSize, MaxPaintedRectangles - rockAreas.Length);
        return movementWater != null;
    }

    internal (ObstacleRectangle[] Rocks, ObstacleRectangle[] Water) Compile()
    {
        var rocks = new HashSet<Vector2I>(data?.RockCells ?? new Cells());
        var water = new HashSet<Vector2I>(data?.WaterCells ?? new Cells());
        if (!TryMerge(rocks, water, out var rockAreas, out var waterAreas, out var movementWater, data?.Bridges))
            throw new InvalidOperationException("World Paint exceeds the Fortress navigation budget. Reduce its painted area or simplify fragmented banks.");

        for (int i = 0; i < rockAreas.Length; i++)
        {
            var area = rockAreas[i];
            // Include cell corners and centres in the conservative terrain-relative cover bounds.
            float low = float.PositiveInfinity, high = float.NegativeInfinity;
            for (float z = area.Min.Y; z <= area.Max.Y; z += WorldPaintMeshBuilder.Step)
            for (float x = area.Min.X; x <= area.Max.X; x += WorldPaintMeshBuilder.Step)
            {
                float height = Surface?.At(x, z) ?? 0f;
                low = Mathf.Min(low, height);
                high = Mathf.Max(high, height);
            }
            rockAreas[i] = area with { AirborneHeightRange = new Vector2(low, high + data.RockHeight * WorldPaintMeshBuilder.MaxRockHeightScale) };
        }
        RebuildViews(rocks, water);
        if (visualDirty || displayedHalfWidth != MapHalfWidth)
        {
            RebuildForest();
            visualDirty = false;
            displayedHalfWidth = MapHalfWidth;
        }
        MovementWater = movementWater;
        var bridgeAreas = new List<Rect2>();
        if (data != null)
            foreach (var bridge in data.Bridges)
                if (BridgeFits(bridge))
                    bridgeAreas.Add(new Rect2((Vector2)bridge.Position * CellSize, (Vector2)bridge.Size * CellSize));
        BridgeAreas = bridgeAreas.ToArray();
        return (rockAreas, waterAreas);
    }

    // =========================================================
    private void Invalidate() => visualDirty = true;

    private MeshInstance3D AddView(string name)
    {
        var view = new MeshInstance3D { Name = name, TopLevel = true };
        AddChild(view, false, InternalMode.Back);
        view.GlobalTransform = Transform3D.Identity;
        return view;
    }

    private void RebuildForest()
    {
        if (forestView != null)
        {
            RemoveChild(forestView);
            forestView.QueueFree();
            forestView = null;
        }
        Forest = null;
        var cells = new HashSet<Vector2I>(data?.ForestCells ?? new Cells());
        cells.RemoveWhere(cell => !Contains(cell));
        cells.ExceptWith(data?.RockCells ?? new Cells());
        cells.ExceptWith(data?.WaterCells ?? new Cells());
        if (cells.Count == 0 || cells.Count > MaxPaintedCells) return;
        forestView = new Node3D { Name = "ForestPreview", TopLevel = true };
        AddChild(forestView, false, InternalMode.Back);
        forestView.GlobalTransform = Transform3D.Identity;
        Forest = new ForestCover(forestView, cells, Surface);
    }

    private void RebuildViews(HashSet<Vector2I> rocks, HashSet<Vector2I> water)
    {
        if (data == null)
        {
            rocksView.Mesh = waterView.Mesh = bridgeView.Mesh = null;
            return;
        }
        if (Engine.IsEditorHint())
        {
            rocks = new HashSet<Vector2I>(rocks);
            water = new HashSet<Vector2I>(water);
            rocks.RemoveWhere(cell => !Contains(cell));
            water.RemoveWhere(cell => !Contains(cell));
            water.ExceptWith(rocks);
        }
        rocksView.Mesh = WorldPaintMeshBuilder.BuildRocks(rocks, Surface, data.RockHeight, data.RockColor);
        if (data.Bridges.Count == 0)
        {
            bridgeView.Mesh = null;
        }
        else
        {
            // The deck replaces the water surface in exactly the same cells opened for movement.
            water = new HashSet<Vector2I>(water);
            OpenBridgeCells(water, data.Bridges);
            bridgeView.Mesh = BridgeDeck.Build(FittingBridges(), Surface);
        }
        waterView.Mesh = PaintedWaterMesh.Build(water, Surface, data);
    }

    internal bool BridgeFits(Rect2I bridge)
    {
        int edge = GridEdge;
        return bridge.Size.X > 0 && bridge.Size.Y > 0 &&
            Mathf.Min(bridge.Size.X, bridge.Size.Y) <= MaxBridgeWidthCells &&
            (long)bridge.Size.X * bridge.Size.Y <= MaxPaintedCells &&
            bridge.Position.X >= -edge && bridge.Position.Y >= -edge &&
            (long)bridge.Position.X + bridge.Size.X <= edge &&
            (long)bridge.Position.Y + bridge.Size.Y <= edge;
    }

    private IEnumerable<Rect2I> FittingBridges()
    {
        int count = Mathf.Min(data.Bridges.Count, MaxBridges);
        for (int i = 0; i < count; i++)
            if (BridgeFits(data.Bridges[i])) yield return data.Bridges[i];
    }

    private void OpenBridgeCells(HashSet<Vector2I> water, Godot.Collections.Array<Rect2I> bridges)
    {
        if (bridges.Count == 0 || water.Count == 0) return;
        int count = Mathf.Min(bridges.Count, MaxBridges);
        for (int i = 0; i < count; i++)
        {
            Rect2I bridge = bridges[i];
            if (!BridgeFits(bridge)) continue;
            for (int z = bridge.Position.Y; z < bridge.End.Y; z++)
            for (int x = bridge.Position.X; x < bridge.End.X; x++)
                water.Remove(new Vector2I(x, z));
        }
    }
}
