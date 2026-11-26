// Stores a closed cliff outline and compiles its plateau, rock faces, and blocked rim.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

[Tool]
public partial class TerrainCliff : Node3D
{
    internal const int MaxPoints = 64, MaxCliffs = 16;
    [Export] public Vector2[] Points { get; set; } = Array.Empty<Vector2>();
    [Export(PropertyHint.Range, "1,80,0.5")] public float Rise { get; set; } = 8f;

    internal bool TryCompile(float halfWidth, out Vector2[] outline, out ObstacleRectangle[] rim)
    {
        outline = Array.Empty<Vector2>();
        rim = Array.Empty<ObstacleRectangle>();
        if (Points == null || Points.Length is < 3 or > MaxPoints || !float.IsFinite(Rise) || Rise is < 1f or > 80f) return false;
        foreach (var point in Points)
            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) ||
                Mathf.Abs(point.X) > halfWidth || Mathf.Abs(point.Y) > halfWidth) return false;

        // Soften each corner locally while keeping the player's long edges intact.
        var corners = new List<Vector2>(Points.Length * 3);
        for (int i = 0; i < Points.Length; i++)
        {
            var point = Points[i];
            var toPrevious = Points[(i + Points.Length - 1) % Points.Length] - point;
            var toNext = Points[(i + 1) % Points.Length] - point;
            if (toPrevious.LengthSquared() < 4f || toNext.LengthSquared() < 4f) return false;
            var enter = point + toPrevious.Normalized() * Mathf.Min(1.2f, toPrevious.Length() * 0.2f);
            var exit = point + toNext.Normalized() * Mathf.Min(1.2f, toNext.Length() * 0.2f);
            corners.Add(enter);
            corners.Add((enter + 2f * point + exit) * 0.25f);
            corners.Add(exit);
        }

        outline = corners.ToArray();
        for (int i = 0; i < outline.Length; i++)
        for (int j = i + 2; j < outline.Length; j++)
        {
            if (i == 0 && j == outline.Length - 1) continue;
            if (Geometry2D.SegmentIntersectsSegment(outline[i], outline[(i + 1) % outline.Length],
                outline[j], outline[(j + 1) % outline.Length]).VariantType != Variant.Type.Nil) return false;
        }
        if (Geometry2D.TriangulatePolygon(outline).Length == 0) return false;

        // Rasterize the outline only. The flat interior stays available for deployment.
        const float grid = WorldPaint.CellSize;
        var cells = new HashSet<Vector2I>();
        for (int i = 0; i < outline.Length; i++)
        {
            var from = outline[i];
            var to = outline[(i + 1) % outline.Length];
            int steps = Mathf.Max(1, Mathf.CeilToInt(from.DistanceTo(to) / (grid * 0.5f)));
            for (int step = 0; step < steps; step++)
            {
                var a = from.Lerp(to, step / (float)steps);
                var b = from.Lerp(to, (step + 1f) / steps);
                var low = a.Min(b) / grid;
                var high = a.Max(b) / grid;
                for (int z = Mathf.FloorToInt(low.Y); z <= Mathf.FloorToInt(high.Y); z++)
                for (int x = Mathf.FloorToInt(low.X); x <= Mathf.FloorToInt(high.X); x++)
                    cells.Add(new Vector2I(x, z));
            }
        }
        rim = ObstacleCellCompiler.Merge(cells, grid, WorldPaint.MaxPaintedRectangles * 4);
        return rim != null;
    }

    internal static bool Overlaps(Vector2[] first, Vector2[] second)
    {
        if (Geometry2D.IsPointInPolygon(first[0], second) || Geometry2D.IsPointInPolygon(second[0], first)) return true;
        for (int i = 0; i < first.Length; i++)
        for (int j = 0; j < second.Length; j++)
            if (Geometry2D.SegmentIntersectsSegment(first[i], first[(i + 1) % first.Length],
                second[j], second[(j + 1) % second.Length]).VariantType != Variant.Type.Nil) return true;
        return false;
    }

    internal void AddMesh(Node3D parent, TerrainHeightField.Plateau plateau, TerrainHeightField ground, ShaderMaterial rock)
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(rock);
        var outline = plateau.Outline;
        int[] triangles = Geometry2D.TriangulatePolygon(outline);
        Vector3 Top(Vector2 p) => new(p.X, plateau.Height, p.Y);
        for (int i = 0; i < triangles.Length; i += 3)
        {
            surface.SetNormal(Vector3.Up);
            surface.AddVertex(Top(outline[triangles[i]]));
            surface.AddVertex(Top(outline[triangles[i + 1]]));
            surface.AddVertex(Top(outline[triangles[i + 2]]));
        }

        bool clockwise = Geometry2D.IsPolygonClockwise(outline);
        for (int i = 0; i < outline.Length; i++)
        {
            var a = outline[i];
            var b = outline[(i + 1) % outline.Length];
            var edge = b - a;
            var normal = new Vector3(edge.Y, 0f, -edge.X).Normalized() * (clockwise ? -1f : 1f);
            int sections = Mathf.Max(1, Mathf.CeilToInt(edge.Length() / TerrainHeightField.Spacing));
            for (int section = 0; section < sections; section++)
            {
                var left = a.Lerp(b, section / (float)sections);
                var right = a.Lerp(b, (section + 1f) / sections);
                var lowLeft = new Vector3(left.X, (ground?.At(left.X, left.Y) ?? 0f) - 0.08f, left.Y);
                var lowRight = new Vector3(right.X, (ground?.At(right.X, right.Y) ?? 0f) - 0.08f, right.Y);
                surface.SetNormal(normal);
                surface.AddVertex(Top(left));
                surface.AddVertex(lowLeft);
                surface.AddVertex(lowRight);
                surface.AddVertex(Top(left));
                surface.AddVertex(lowRight);
                surface.AddVertex(Top(right));
            }
        }
        parent.AddChild(new MeshInstance3D { Mesh = surface.Commit(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }
}
