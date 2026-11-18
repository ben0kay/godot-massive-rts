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
    private MeshInstance3D rocksView, waterView;
    private HashSet<Vector2I> draftRocks, draftWater;
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
        RebuildViews(draftRocks ?? new HashSet<Vector2I>(data?.RockCells ?? new Cells()),
            draftWater ?? new HashSet<Vector2I>(data?.WaterCells ?? new Cells()));
        UpdateConfigurationWarnings();
    }

    public override string[] _GetConfigurationWarnings()
    {
        if (data == null) return Array.Empty<string>();
        foreach (var cell in data.RockCells)
            if (!Contains(cell)) return new[] { "Some World Paint cells lie beyond this map. They are kept in the level and skipped until they fit." };
        foreach (var cell in data.WaterCells)
            if (!Contains(cell)) return new[] { "Some World Paint cells lie beyond this map. They are kept in the level and skipped until they fit." };
        return Array.Empty<string>();
    }

    // =========================================================
    internal bool Contains(Vector2I cell)
    {
        int edge = GridEdge;
        return cell.X >= -edge && cell.X < edge && cell.Y >= -edge && cell.Y < edge;
    }

    internal void Preview(HashSet<Vector2I> rocks, HashSet<Vector2I> water)
    {
        draftRocks = rocks;
        draftWater = water;
        Invalidate();
    }

    internal bool TryMerge(HashSet<Vector2I> rocks, HashSet<Vector2I> water,
        out ObstacleRectangle[] rockAreas, out ObstacleRectangle[] waterAreas)
    {
        rockAreas = waterAreas = null;
        rocks.RemoveWhere(cell => !Contains(cell));
        water.RemoveWhere(cell => !Contains(cell));
        water.ExceptWith(rocks);
        if (rocks.Count + water.Count > MaxPaintedCells) return false;
        rockAreas = ObstacleCellCompiler.Merge(rocks, CellSize, MaxPaintedRectangles);
        if (rockAreas == null) return false;
        waterAreas = ObstacleCellCompiler.Merge(water, CellSize, MaxPaintedRectangles - rockAreas.Length);
        return waterAreas != null;
    }

    internal (ObstacleRectangle[] Rocks, ObstacleRectangle[] Water) Compile()
    {
        var rocks = new HashSet<Vector2I>(data?.RockCells ?? new Cells());
        var water = new HashSet<Vector2I>(data?.WaterCells ?? new Cells());
        if (!TryMerge(rocks, water, out var rockAreas, out var waterAreas))
            throw new InvalidOperationException("World Paint exceeds the Fortress navigation budget. Reduce its painted area or simplify fragmented banks.");

        for (int i = 0; i < rockAreas.Length; i++)
        {
            var area = rockAreas[i];
            // Use the same sample lattice as the generated rock top.
            float low = float.PositiveInfinity, high = float.NegativeInfinity;
            for (float z = area.Min.Y; z <= area.Max.Y; z += WorldPaintMeshBuilder.Step)
            for (float x = area.Min.X; x <= area.Max.X; x += WorldPaintMeshBuilder.Step)
            {
                float height = Surface?.At(x, z) ?? 0f;
                low = Mathf.Min(low, height);
                high = Mathf.Max(high, height);
            }
            rockAreas[i] = area with { AirborneHeightRange = new Vector2(low, high + data.RockHeight) };
        }
        RebuildViews(rocks, water);
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

    private void RebuildViews(HashSet<Vector2I> rocks, HashSet<Vector2I> water)
    {
        if (data == null)
        {
            rocksView.Mesh = waterView.Mesh = null;
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
        waterView.Mesh = PaintedWaterMesh.Build(water, Surface, data);
    }
}
