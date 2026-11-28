// Compiles an authored command-level terrain preview and runtime height surface.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

[Tool]
public partial class CommandTerrain : Node3D
{
    private readonly record struct Hill(Vector2 Center, float Radius, float Rise);
    private Node3D preview, cliffPreview;
    private ShaderMaterial cliffRock;
    internal ObstacleRectangle[] CliffObstacles { get; private set; } = Array.Empty<ObstacleRectangle>();
    internal ObstacleRectangle[] TrenchBlockers { get; private set; } = Array.Empty<ObstacleRectangle>();
    internal TrenchField Trenches { get; private set; }
    private int elevationRevision;
    private Godot.Collections.Dictionary<Vector2I, float> elevation = new();

    [Export]
    public Godot.Collections.Dictionary<Vector2I, float> Elevation
    {
        get => elevation;
        set { elevation = value ?? new(); elevationRevision++; }
    }

    [Export] public float[] ElevationTierHeights { get; set; } = new[] { 0f, 3f, 6f, 9f, 12f, 15f, 18f };

    internal float TierHeight(int level)
    {
        int index = Mathf.Clamp(level - 1, 0, 6);
        var heights = ElevationTierHeights;
        return heights != null && index < heights.Length && float.IsFinite(heights[index])
            ? Mathf.Clamp(heights[index], -80f, 80f) : index * 3f;
    }

    private MeshInstance3D Ground => GetParent()?.GetNodeOrNull<MeshInstance3D>("Ground");

    private int fingerprint;
    private double previewDelay;
    public event Action SurfaceChanged;
    public TerrainHeightField Field { get; private set; }

    public override void _Ready()
    {
        Rebuild(force: true);
        SetProcess(Engine.IsEditorHint());
    }

    public override void _Process(double delta)
    {
        previewDelay += delta;
        if (previewDelay < 0.25) return;
        previewDelay = 0;
        Rebuild(force: false);
    }

    public float SampleHeight(float x, float z) => Field?.At(x, z) ?? 0f;

    public float SampleFootprint(Vector2 min, Vector2 max) => Field?.FootprintHeight(min, max, out _) ?? 0f;

    internal static CommandTerrain Find(Node source)
    {
        for (Node parent = source.GetParent(); parent != null; parent = parent.GetParent())
            if (parent.GetNodeOrNull<CommandTerrain>("Terrain") is CommandTerrain terrain) return terrain;
        return null;
    }

    internal void Rebuild(bool force)
    {
        var hills = new List<Hill>();
        var hash = new HashCode();
        var cliffs = new List<TerrainCliff>();
        var trenches = new List<TerrainTrench>();
        float half = GetParent() is CommandSandbox map ? map.MapSize * 0.5f : DeploymentLayout.WorldHalfWidth;
        hash.Add(half);
        foreach (Node child in GetChildren())
        {
            if (child is TerrainTrench trench && trenches.Count < TerrainTrench.MaxPerMap && trench.Fits(half))
            {
                trenches.Add(trench);
                hash.Add(trench.Start); hash.Add(trench.End);
                hash.Add(trench.Width); hash.Add(trench.Depth); hash.Add(trench.Ramp);
            }
            if (child is TerrainCliff cliff)
            {
                cliffs.Add(cliff);
                hash.Add(cliff.Rise);
                hash.Add(cliff.Points?.Length ?? 0);
                if (cliff.Points != null) foreach (var point in cliff.Points) hash.Add(point);
            }
            if (child is not TerrainHill marker || marker.Rise <= 0f) continue;
            var hill = new Hill(ObstacleField.Xz(marker.GlobalPosition),
                Mathf.Clamp(marker.Radius, 8f, 256f), Mathf.Clamp(marker.Rise, 0f, 80f));
            hills.Add(hill);
            hash.Add(hill);
        }

        var marsh = GetParent()?.GetNodeOrNull<WorldPaint>("WorldPaint")?.CollectMarshCells();
        if (marsh != null)
        {
            hash.Add(marsh.Count);
            foreach (var cell in marsh) hash.Add(cell);
        }
        hash.Add(elevationRevision);
        int signature = hash.ToHashCode();
        if (!force && signature == fingerprint) return;
        fingerprint = signature;
        if (preview != null)
        {
            RemoveChild(preview);
            preview.QueueFree();
            preview = null;
        }
        if (cliffPreview != null)
        {
            RemoveChild(cliffPreview);
            cliffPreview.QueueFree();
            cliffPreview = null;
        }
        CliffObstacles = Array.Empty<ObstacleRectangle>();
        Field = null;
        Trenches = null;
        TrenchBlockers = Array.Empty<ObstacleRectangle>();
        if (hills.Count == 0 && elevation.Count == 0 && marsh == null && trenches.Count == 0)
        {
            Ground?.SetInstanceShaderParameter("terrain_replaces_ground", false);
            BuildCliffs(cliffs, half);
            SurfaceChanged?.Invoke();
            return;
        }

        // Compile only chunks that a hill can touch, even if hills sit far apart.
        var regions = new List<Rect2>(hills.Count);
        float highest = 1f;
        foreach (var hill in hills)
        {
            var size = Vector2.One * hill.Radius;
            regions.Add(new Rect2(hill.Center - size, size * 2f));
            highest = Mathf.Max(highest, hill.Rise);
        }
        var editedChunks = new HashSet<Vector2I>();
        int chunkSize = TerrainHeightField.ChunkCells;
        void IncludeSample(Vector2I sample)
        {
            // A changed sample on a chunk edge affects both adjoining meshes.
            int left = Mathf.FloorToInt((sample.X - 0.5f) / chunkSize);
            int right = Mathf.FloorToInt((sample.X + 0.5f) / chunkSize);
            int near = Mathf.FloorToInt((sample.Y - 0.5f) / chunkSize);
            int far = Mathf.FloorToInt((sample.Y + 0.5f) / chunkSize);
            for (int z = near; z <= far; z++)
            for (int x = left; x <= right; x++)
                editedChunks.Add(new Vector2I(x, z));
        }
        foreach (var sample in elevation.Keys) IncludeSample(sample);
        const float spacing = TerrainHeightField.Spacing;
        if (marsh != null)
        {
            foreach (var cell in marsh)
            {
                var min = (Vector2)cell * WorldPaint.CellSize;
                var max = min + Vector2.One * WorldPaint.CellSize;
                for (int z = Mathf.FloorToInt(min.Y / spacing) - 1; z <= Mathf.CeilToInt(max.Y / spacing) + 1; z++)
                for (int x = Mathf.FloorToInt(min.X / spacing) - 1; x <= Mathf.CeilToInt(max.X / spacing) + 1; x++)
                    IncludeSample(new Vector2I(x, z));
            }
        }
        foreach (var chunk in editedChunks)
            regions.Add(new Rect2(((Vector2)chunk + Vector2.One * 0.5f) * (chunkSize * TerrainHeightField.Spacing), Vector2.Zero));

        float BaseHeight(Vector2 point)
        {
            float height = 0f;
            foreach (var hill in hills)
            {
                float distanceSquared = hill.Center.DistanceSquaredTo(point);
                if (distanceSquared >= hill.Radius * hill.Radius) continue;
                float factor = 1f - Mathf.Sqrt(distanceSquared) / hill.Radius;
                height = Mathf.Max(height, hill.Rise * factor * factor * (3f - 2f * factor));
            }
            var key = (Vector2I)(point / TerrainHeightField.Spacing).Round();
            return height + (elevation.TryGetValue(key, out float change) ? change : 0f) - MarshDepth(point, marsh);
        }
        var blockers = new List<ObstacleRectangle>(trenches.Count * 3);
        foreach (var trench in trenches)
        {
            regions.Add(trench.Bounds.Grow(spacing));
            trench.FloorHeight = Mathf.Min(BaseHeight(trench.Start),
                Mathf.Min(BaseHeight(trench.End), BaseHeight((trench.Start + trench.End) * 0.5f))) - trench.Depth;
            trench.AddMovementBlockers(blockers);
        }
        TrenchBlockers = blockers.ToArray();
        Field = new TerrainHeightField(regions, point =>
        {
            float height = BaseHeight(point);
            foreach (var trench in trenches)
            {
                if (!trench.Bounds.HasPoint(point)) continue;
                height = Mathf.Lerp(height, Mathf.Min(height, trench.FloorHeight), trench.Blend(point));
            }
            return height;
        });
        if (trenches.Count > 0) Trenches = new TrenchField(trenches, Field);
        var ground = Ground;
        if (ground?.GetActiveMaterial(0) is ShaderMaterial material)
        {
            var bounds = ground.GlobalTransform * ground.GetAabb();
            var flat = new Rect2(ObstacleField.Xz(bounds.Position), ObstacleField.Xz(bounds.Size));
            preview = TerrainRenderer.BuildCommandGround(Field, material, flat);
            ground.SetInstanceShaderParameter("terrain_replaces_ground", true);
        }
        else preview = TerrainRenderer.Build(Field, highest, DeploymentLayout.WorldHalfWidth, includeSurround: false);
        AddChild(preview, false, InternalMode.Back);
        // Chunks are stored in world coordinates, independent of the authoring node.
        preview.TopLevel = true;
        preview.GlobalTransform = Transform3D.Identity;
        if (trenches.Count > 0)
        {
            // Ground grid and material updates apply only to the terrain meshes.
            var linings = new Node3D { Name = "TrenchLinings" };
            preview.AddChild(linings);
            foreach (var trench in trenches) linings.AddChild(TrenchLining.Build(trench, Field));
        }
        BuildCliffs(cliffs, half);
        SurfaceChanged?.Invoke();
    }

    private static float MarshDepth(Vector2 point, HashSet<Vector2I> marsh)
    {
        if (marsh == null) return 0f;
        const float width = TerrainHeightField.Spacing * 0.5f;
        int covered = 0;
        // Average the painted coverage around each height sample to feather the banks.
        for (int z = -1; z <= 1; z += 2)
        for (int x = -1; x <= 1; x += 2)
        {
            var offset = point + new Vector2(x, z) * width;
            var cell = new Vector2I(Mathf.FloorToInt(offset.X / WorldPaint.CellSize),
                Mathf.FloorToInt(offset.Y / WorldPaint.CellSize));
            if (marsh.Contains(cell)) covered++;
        }
        return covered * 0.08f;
    }

    private void BuildCliffs(List<TerrainCliff> cliffs, float half)
    {
        if (cliffs.Count == 0) return;
        var plateaus = new List<TerrainHeightField.Plateau>();
        var obstacles = new List<ObstacleRectangle>();
        foreach (var cliff in cliffs)
        {
            if (plateaus.Count >= TerrainCliff.MaxCliffs || !cliff.TryCompile(half, out var outline, out var rim)) continue;
            var bounds = new Rect2(outline[0], Vector2.Zero);
            foreach (var point in outline) bounds = bounds.Expand(point);
            float top = (Field?.FootprintHeight(bounds.Position, bounds.End, out _) ?? 0f) + cliff.Rise;
            var plateau = new TerrainHeightField.Plateau(outline, bounds, top);
            if (cliffPreview == null)
            {
                cliffPreview = new Node3D { TopLevel = true };
                AddChild(cliffPreview, false, InternalMode.Back);
                cliffPreview.GlobalTransform = Transform3D.Identity;
            }
            cliffRock ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://WORLD/TERRAIN/RENDERING/CliffRock.gdshader") };
            cliff.AddMesh(cliffPreview, plateau, Field, cliffRock);
            plateaus.Add(plateau);
            foreach (var edge in rim)
            {
                float bottom = Field?.At(edge.Min.X, edge.Min.Y) ?? 0f;
                obstacles.Add(edge with { AirborneHeightRange = new Vector2(bottom - 1f, top) });
            }
        }
        if (plateaus.Count == 0) return;
        Field ??= new TerrainHeightField(Vector2.Zero, Vector2.Zero, _ => 0f);
        Field.SetPlateaus(plateaus.ToArray());
        CliffObstacles = obstacles.ToArray();
    }

    internal void UpdateGroundMaterial(ShaderMaterial material)
    {
        if (preview == null) return;
        foreach (Node child in preview.GetChildren())
            if (child is MeshInstance3D mesh) mesh.MaterialOverride = material;
    }
}
