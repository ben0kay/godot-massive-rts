// Builds deterministic undergrowth chunks and masks incompatible ground occupancy.
using System;
using System.Collections.Generic;
using Godot;
using Shape = MassiveRts.UndergrowthMeshes.Shape;

namespace MassiveRts;

public partial class WorldPaint
{
    private const int UndergrowthChunkCells = 16;
    private const float PlantClearance = .45f;
    private sealed record PlantChunk(Node3D Root, List<(Vector2I Cell, int Stamp)> Cells,
        List<Rect2> Blockers, TerrainHeightField Surface);
    private readonly Dictionary<Vector2I, PlantChunk> plantChunks = new();
    private Rect2[] runtimePlantBlockers = Array.Empty<Rect2>();

    private static readonly Shape[][] PlantMix =
    {
        new[] { Shape.Grass, Shape.Grass, Shape.Fern, Shape.Scrub, Shape.Stone, Shape.Twig },
        new[] { Shape.Stone, Shape.Stone, Shape.Grass, Shape.Scrub, Shape.Twig },
        new[] { Shape.Reeds, Shape.Cattail, Shape.Reeds, Shape.Grass, Shape.Fern, Shape.Scrub }
    };

    internal static bool ValidUndergrowth(Godot.Collections.Dictionary<Vector2I, int> cells)
    {
        if (cells.Count > MaxPaintedCells) return false;
        foreach (int stamp in cells.Values)
            if ((stamp & 255) >= PlantMix.Length || ((stamp >> 8) & 255) is < 1 or > 100 ||
                (stamp >> 16) is < 35 or > 160) return false;
        return true;
    }

    internal void SetUndergrowthBlockers(ObstacleField field)
    {
        if (data?.UndergrowthCells.Count is not > 0) return;
        runtimePlantBlockers = new Rect2[field?.RectangleCount ?? 0];
        for (int i = 0; i < runtimePlantBlockers.Length; i++)
        {
            var obstacle = field.Rectangles[i];
            runtimePlantBlockers[i] = new Rect2(obstacle.Min, obstacle.Max - obstacle.Min);
        }
        RebuildUndergrowth();
    }

    internal void RebuildUndergrowth()
    {
        if (!IsInsideTree()) return;
        var coverage = data?.UndergrowthCells;
        if (coverage == null || coverage.Count == 0 || !ValidUndergrowth(coverage))
        {
            foreach (var chunk in plantChunks.Values) Discard(chunk);
            plantChunks.Clear();
            return;
        }

        var blocked = new HashSet<Vector2I>(data.RockCells);
        blocked.UnionWith(data.WaterCells);
        blocked.UnionWith(data.RoadCells);
        blocked.UnionWith(data.IndustrialWallCells);
        blocked.UnionWith(data.BlueWallCells);
        blocked.UnionWith(data.RedWallCells);
        blocked.UnionWith(data.BlueBarricadeCells);
        blocked.UnionWith(data.RedBarricadeCells);
        foreach (var item in data.RubbleCells)
            if (item.Value == (int)RubbleKind.Ruin) blocked.Add(item.Key);
        var solids = new List<Rect2>(runtimePlantBlockers);
        foreach (var bridge in FittingBridges())
            solids.Add(new Rect2((Vector2)bridge.Position * CellSize, (Vector2)bridge.Size * CellSize));
        CollectAuthoredPlantBlockers(solids);

        var groups = new Dictionary<Vector2I, List<(Vector2I Cell, int Stamp)>>();
        foreach (var item in coverage)
        {
            if (!Contains(item.Key) || blocked.Contains(item.Key)) continue;
            Vector2I key = new(Mathf.FloorToInt(item.Key.X / (float)UndergrowthChunkCells),
                Mathf.FloorToInt(item.Key.Y / (float)UndergrowthChunkCells));
            if (!groups.TryGetValue(key, out var group)) groups.Add(key, group = new());
            group.Add((item.Key, item.Value));
        }
        var obsolete = new List<Vector2I>();
        foreach (var item in plantChunks)
            if (!groups.ContainsKey(item.Key)) obsolete.Add(item.Key);
        foreach (var key in obsolete) { Discard(plantChunks[key]); plantChunks.Remove(key); }

        var field = Surface;
        foreach (var item in groups)
        {
            const float span = CellSize * UndergrowthChunkCells;
            var region = new Rect2((Vector2)item.Key * span, Vector2.One * span).Grow(PlantClearance);
            var local = solids.FindAll(solid => solid.Intersects(region));
            Vector2I first = item.Key * UndergrowthChunkCells - Vector2I.One;
            for (int z = first.Y; z < first.Y + UndergrowthChunkCells + 2; z++)
            for (int x = first.X; x < first.X + UndergrowthChunkCells + 2; x++)
                if (blocked.Contains(new Vector2I(x, z)))
                    local.Add(new Rect2(x * CellSize, z * CellSize, CellSize, CellSize));
            if (plantChunks.TryGetValue(item.Key, out var old))
            {
                if (old.Surface == field && Same(old.Cells, item.Value) && Same(old.Blockers, local)) continue;
                Discard(old);
            }
            var root = BuildPlants(item.Value, local, field);
            AddChild(root, false, InternalMode.Back);
            root.GlobalTransform = Transform3D.Identity;
            plantChunks[item.Key] = new PlantChunk(root, item.Value, local, field);
        }
    }

    private static bool Same<T>(List<T> left, List<T> right)
    {
        if (left.Count != right.Count) return false;
        for (int i = 0; i < left.Count; i++)
            if (!EqualityComparer<T>.Default.Equals(left[i], right[i])) return false;
        return true;
    }

    private static void Discard(PlantChunk chunk)
    {
        chunk.Root.Hide();
        chunk.Root.QueueFree();
    }

    private void CollectAuthoredPlantBlockers(List<Rect2> solids)
    {
        if (GetParent() is not CommandSandbox map) return;
        void Add(ObstacleRectangle area) => solids.Add(new Rect2(area.Min, area.Max - area.Min));
        if (map.GetNodeOrNull<Node3D>("StartingDefenses") is Node3D defenses)
            foreach (Node node in defenses.GetChildren())
                if (node is CommandLaserPlacement tower) Add(tower.Footprint(tower.GlobalPosition));
        foreach (var ruin in BuildingRuin.AuthoredOn(map)) solids.Add(ruin.BoundsAt(ruin.GlobalPosition));
        if (map.GetNodeOrNull<Node3D>("Citadel") is Node3D citadel && map.AutoBattlerRules == null)
            BlockMarker(citadel, 5f);
        if (map.GetNodeOrNull<Node3D>("HostileFabricator") is Node3D hostile) BlockMarker(hostile, 3f);
        if (map.GetNodeOrNull<Node3D>("StartingFabricators") is Node3D factories)
            foreach (Node node in factories.GetChildren())
                if (node is Node3D factory) BlockMarker(factory, 3f);
        if (map.GetNodeOrNull<Node3D>("Resources") is Node3D resources)
            foreach (Node node in resources.GetChildren())
                if (node is CarbonDeposit deposit) Add(deposit.Area);
        if (map.GetNodeOrNull<Node3D>("Obstacles") is Node3D obstacles)
            foreach (Node node in obstacles.GetChildren())
                if (node is MeshInstance3D mesh && mesh.Mesh != null)
                {
                    var bounds = mesh.GlobalTransform * mesh.GetAabb();
                    solids.Add(new Rect2(ObstacleField.Xz(bounds.Position), ObstacleField.Xz(bounds.Size)));
                }

        void BlockMarker(Node3D marker, float half) => solids.Add(new Rect2(
            ObstacleField.Xz(marker.GlobalPosition) - Vector2.One * half, Vector2.One * (2f * half)));
    }

    private static Node3D BuildPlants(List<(Vector2I Cell, int Stamp)> cells, List<Rect2> blockers, TerrainHeightField terrain)
    {
        var root = new Node3D { Name = "UndergrowthChunk", TopLevel = true };
        var batches = new List<(Transform3D Pose, Color Tint)>[UndergrowthMeshes.ShapeCount];
        foreach (var (cell, stamp) in cells)
        {
            int preset = stamp & 255;
            float density = ((stamp >> 8) & 255) * .01f;
            float scale = (stamp >> 16) * .01f;
            uint seed = unchecked((uint)cell.X * 2654435761u ^ (uint)cell.Y * 2246822519u);
            var mix = PlantMix[preset];
            int count = 1 + (int)(Random(seed, 0) * 4f);
            Vector2 cluster = new(Random(seed, 1), Random(seed, 2));
            for (int i = 0; i < count; i++)
            {
                uint index = (uint)i * 8 + 3;
                if (Random(seed, index) >= density) continue;
                var jitter = new Vector2(Random(seed, index + 1), Random(seed, index + 2)) - Vector2.One * .5f;
                Vector2 point = ((Vector2)cell + (cluster + jitter * .62f).Clamp(Vector2.One * .09f, Vector2.One * .91f)) * CellSize;
                if (IntersectsSolid(point, blockers)) continue;
                float width = scale * (.72f + Random(seed, index + 4) * .53f);
                float height = scale * (.75f + Random(seed, index + 5) * .46f);
                var ground = terrain?.SampleGroundCover(point) ?? (0f, -1);
                if (terrain != null && !OnSameSurface(terrain, point, ground, width * .43f)) continue;
                var basis = new Basis(Vector3.Up, Random(seed, index + 6) * Mathf.Tau)
                    .Scaled(new Vector3(width, height, width));
                Shape shape = mix[(int)(Random(seed, index + 3) * mix.Length)];
                var pose = new Transform3D(basis, new Vector3(point.X, ground.Item1 - .009f, point.Y));
                Color tint = Tint(preset, shape).Lightened(Random(seed, index + 7) * .13f - .05f);
                (batches[(int)shape] ??= new()).Add((pose, tint));
            }
        }
        for (int i = 0; i < batches.Length; i++)
        {
            var entries = batches[i];
            if (entries == null) continue;
            var multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = true, Mesh = UndergrowthMeshes.Get((Shape)i), InstanceCount = entries.Count };
            for (int slot = 0; slot < entries.Count; slot++)
            {
                multimesh.SetInstanceTransform(slot, entries[slot].Pose);
                multimesh.SetInstanceColor(slot, entries[slot].Tint);
            }
            root.AddChild(new MultiMeshInstance3D { Multimesh = multimesh,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }
        return root;
    }

    private static bool IntersectsSolid(Vector2 point, List<Rect2> blockers)
    {
        foreach (var area in blockers)
            if (area.Grow(PlantClearance).HasPoint(point)) return true;
        return false;
    }

    private static bool OnSameSurface(TerrainHeightField field, Vector2 point, (float Height, int Plateau) ground, float radius)
    {
        for (int z = -1; z <= 1; z += 2)
        for (int x = -1; x <= 1; x += 2)
        {
            var sample = field.SampleGroundCover(point + new Vector2(x, z) * radius);
            if (sample.Plateau != ground.Plateau || Mathf.Abs(sample.Height - ground.Height) > .22f) return false;
        }
        return true;
    }

    private static Color Tint(int preset, Shape shape) => shape switch
    {
        Shape.Stone => preset == 0 ? new("#636a54") : new("#77736a"),
        Shape.Twig => new("#645145"),
        Shape.Cattail => new("#656044"),
        _ => preset == 1 ? new("#81744c") : preset == 2 ? new("#4b6248") : new("#536444")
    };

    private static float Random(uint seed, uint channel)
    {
        uint hash = unchecked(seed + channel * 0x9E3779B9u);
        hash = unchecked((hash ^ (hash >> 16)) * 0x7FEB352Du);
        hash = unchecked((hash ^ (hash >> 15)) * 0x846CA68Bu);
        return ((hash ^ (hash >> 16)) & 0xFFFFFFu) / 16777216f;
    }
}
