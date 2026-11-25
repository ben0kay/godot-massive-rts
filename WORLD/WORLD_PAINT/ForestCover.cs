// Compiles passable forest cells into batched trees and a shared outer boundary.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed class ForestCover
{
    private static readonly Vector2I[] Neighbors = { Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left };
    private const float CrownRadius = 0.56f;
    private readonly record struct Tree(int Index, Vector2 Center, float RadiusSquared);
    private readonly Dictionary<Vector2I, Tree> trees = new();
    private Rect2I visibilityBounds;
    private HashSet<int> faded = new(), nearby = new();
    private readonly MultiMesh trunks, crowns, shadows;
    private readonly ImmediateMesh outline = new();
    private readonly StandardMaterial3D outlineMaterial = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color("#668d4d")
    };
    private float timeUntilRefresh;
    private static Color Tint(float opacity) => new(1f, 1f, 1f, opacity);

    internal ForestCover(Node3D parent)
    {
        var shader = GD.Load<Shader>("res://WORLD/WORLD_PAINT/ForestTree.gdshader");
        ShaderMaterial Material(Color tint, bool foliage = false)
        {
            var material = new ShaderMaterial { Shader = shader };
            material.SetShaderParameter("tree_color", tint);
            material.SetShaderParameter("foliage", foliage);
            return material;
        }

        trunks = Batch(parent, new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.18f, Height = 1f,
            RadialSegments = 6, Rings = 0, Material = Material(new Color("#523322")) });
        crowns = Batch(parent, PineCrown(Material(new Color("#3b683d"), foliage: true)));
        shadows = Batch(parent, GroundShadowMeshes.Pine());

        parent.AddChild(new MeshInstance3D { Mesh = outline,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    // Keep the native render objects attached for the map's lifetime.
    internal void Rebuild(HashSet<Vector2I> cells, TerrainHeightField surface)
    {
        trees.Clear();
        faded.Clear();
        nearby.Clear();
        timeUntilRefresh = 0f;
        outline.ClearSurfaces();
        if (cells.Count > trunks.InstanceCount)
        {
            int capacity = System.Math.Max(cells.Count, trunks.InstanceCount * 2);
            trunks.InstanceCount = crowns.InstanceCount = shadows.InstanceCount = capacity;
        }
        trunks.VisibleInstanceCount = crowns.VisibleInstanceCount = shadows.VisibleInstanceCount = cells.Count;
        if (cells.Count == 0)
        {
            visibilityBounds = default;
            return;
        }
        outline.SurfaceBegin(Mesh.PrimitiveType.Lines, outlineMaterial);
        float cellSize = WorldPaint.CellSize;
        float Ground(Vector2 point) => surface?.At(point.X, point.Y) ?? 0f;
        Vector3 OnGround(Vector2 point) => new(point.X, Ground(point) + 0.06f, point.Y);
        Vector2I min = new(int.MaxValue, int.MaxValue), max = new(int.MinValue, int.MinValue);
        foreach (var cell in cells)
        {
            min = new Vector2I(System.Math.Min(min.X, cell.X), System.Math.Min(min.Y, cell.Y));
            max = new Vector2I(System.Math.Max(max.X, cell.X), System.Math.Max(max.Y, cell.Y));
            int index = trees.Count;
            uint seed = unchecked(((uint)cell.X * 0x9E3779B9u) ^ ((uint)cell.Y * 0x85EBCA6Bu));
            seed ^= seed >> 16;
            float height = 0.65f + (seed & 255) / 255f * 0.95f;
            float width = 0.72f + ((seed >> 8) & 255) / 255f * 0.51f;
            Vector2 low = (Vector2)cell * cellSize;
            // Jitter only the tree visuals, not the forest cell or its gameplay effects.
            // Stable per-cell hashing keeps trees in the same place after save/reload.
            uint jitter = unchecked(seed * 0x7FEB352Du + 0xA511E9B3u);
            jitter ^= jitter >> 16;
            jitter = unchecked(jitter * 0x846CA68Bu);
            jitter ^= jitter >> 15;
            Vector2 offset = new((jitter & 0xFFFFu) / 65535f * 2f - 1f,
                (jitter >> 16) / 65535f * 2f - 1f);
            Vector2 center = low + Vector2.One * (cellSize * 0.5f) + offset * (cellSize * 0.15f);
            float radius = CrownRadius * width;
            trees.Add(cell, new Tree(index, center, radius * radius));
            float ground = Ground(center);
            trunks.SetInstanceTransform(index, new Transform3D(Basis.Identity.Scaled(new Vector3(width, height, width)),
                new Vector3(center.X, ground + height * 0.5f, center.Y)));
            crowns.SetInstanceTransform(index, new Transform3D(Basis.Identity.Scaled(new Vector3(width, 0.6f + height * 0.65f, width)),
                new Vector3(center.X, ground + height + 0.25f, center.Y)));
            Vector2 cast = GroundShadowMeshes.SceneryOffset(height + 0.35f);
            Vector2 along = cast.Normalized();
            var shadowBasis = new Basis(new Vector3(along.X, 0f, along.Y) * (cast.Length() + radius * 0.55f),
                Vector3.Up, new Vector3(-along.Y, 0f, along.X) * radius);
            shadows.SetInstanceTransform(index, new Transform3D(shadowBasis,
                OnGround(center) - Vector3.Up * 0.025f));
            shadows.SetInstanceColor(index, Colors.White);
            trunks.SetInstanceColor(index, Colors.White);
            crowns.SetInstanceColor(index, Colors.White);

            for (int edge = 0; edge < 4; edge++)
            {
                if (cells.Contains(cell + Neighbors[edge])) continue;
                Vector2 a = low + (edge is 1 or 2 ? Vector2.Right * cellSize : Vector2.Zero) +
                    (edge == 2 ? Vector2.Down * cellSize : Vector2.Zero);
                Vector2 b = edge switch
                {
                    0 => low + Vector2.Right * cellSize,
                    1 => low + Vector2.One * cellSize,
                    2 => low + Vector2.Down * cellSize,
                    _ => low + Vector2.Down * cellSize
                };
                outline.SurfaceAddVertex(OnGround(a));
                Vector3 midpoint = OnGround((a + b) * 0.5f);
                outline.SurfaceAddVertex(midpoint);
                outline.SurfaceAddVertex(midpoint);
                outline.SurfaceAddVertex(OnGround(b));
            }
        }
        visibilityBounds = new Rect2I(min, max - min + Vector2I.One);
        outline.SurfaceEnd();
    }

    private static Mesh PineCrown(Material material)
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(material);
        const int sides = 7;
        for (int tier = 0; tier < 3; tier++)
        {
            float radius = CrownRadius * (1f - 0.23f * tier);
            float bottom = -0.4f + 0.29f * tier;
            var tip = Vector3.Up * (bottom + 0.63f);
            for (int side = 0; side < sides; side++)
            {
                float angle = Mathf.Tau * side / sides;
                float next = Mathf.Tau * (side + 1) / sides;
                var a = new Vector3(Mathf.Cos(angle) * radius, bottom, Mathf.Sin(angle) * radius);
                var b = new Vector3(Mathf.Cos(next) * radius, bottom, Mathf.Sin(next) * radius);
                surface.SetNormal((tip - a).Cross(b - a).Normalized());
                surface.SetColor(Colors.White.Darkened(0.14f - 0.05f * tier));
                surface.AddVertex(a);
                surface.AddVertex(b);
                surface.AddVertex(tip);
            }
        }
        return surface.Commit();
    }

    private static MultiMesh Batch(Node3D parent, Mesh mesh)
    {
        var batch = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true, Mesh = mesh };
        parent.AddChild(new MultiMeshInstance3D { Multimesh = batch,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        return batch;
    }

    internal bool Contains(Vector3 position) => trees.ContainsKey(Cell(position));

    private static Vector2I Cell(Vector3 position) => new(Mathf.FloorToInt(position.X / WorldPaint.CellSize),
        Mathf.FloorToInt(position.Z / WorldPaint.CellSize));

    internal void ResetVisibility()
    {
        foreach (int index in faded)
        {
            trunks.SetInstanceColor(index, Colors.White);
            crowns.SetInstanceColor(index, Colors.White);
        }
        faded.Clear();
        timeUntilRefresh = 0f;
    }

    // =========================================================
    // Sample occupancy only at display cadence and upload changed tree colors.
    internal void UpdateVisibility(UnitState units, float delta)
    {
        timeUntilRefresh -= delta;
        if (timeUntilRefresh > 0f) return;
        timeUntilRefresh = 0.1f;
        nearby.Clear();
        for (int id = 0; id < units.Count; id++)
        {
            if (units.Health[id] <= 0f) continue;
            Vector3 position = units.Position[id];
            Vector2I cell = Cell(position);
            // The crown fits inside its cell even at maximum scale and jitter.
            if (!visibilityBounds.HasPoint(cell) || !trees.TryGetValue(cell, out var tree) ||
                nearby.Contains(tree.Index)) continue;
            if (tree.Center.DistanceSquaredTo(new Vector2(position.X, position.Z)) <= tree.RadiusSquared)
                nearby.Add(tree.Index);
        }
        foreach (int index in faded)
            if (!nearby.Contains(index))
            {
                trunks.SetInstanceColor(index, Colors.White);
                crowns.SetInstanceColor(index, Colors.White);
            }
        foreach (int index in nearby)
            if (!faded.Contains(index))
            {
                trunks.SetInstanceColor(index, Tint(0.48f));
                crowns.SetInstanceColor(index, Tint(0.35f));
            }
        (faded, nearby) = (nearby, faded);
    }
}
