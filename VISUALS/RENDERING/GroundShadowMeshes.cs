// Shared unlit ground-shadow geometry for soldier batches and static obstacles.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal static class GroundShadowMeshes
{
    internal static StandardMaterial3D Material(float opacity = 0.52f) => new()
    {
        // About twice the previous opacity for stronger near-black contact shadows.
        AlbedoColor = new Color(0.005f, 0.008f, 0.012f, opacity),
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled
    };

    public static Mesh Soldier(float opacity = 0.52f)
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(Material(opacity));
        AppendDisc(surface, Vector3.Zero, Vector2.One);
        return surface.Commit();
    }

    internal static Vector2 SceneryOffset(float height) => new Vector2(0.45f, -0.18f) * height;

    internal static void AppendDisc(SurfaceTool surface, Vector3 center, Vector2 radius, TerrainHeightField terrain = null)
    {
        const int sides = 10;
        for (int i = 0; i < sides; i++)
        {
            float angle = Mathf.Tau * i / sides;
            float next = Mathf.Tau * (i + 1) / sides;
            surface.AddVertex(OnGround(center.X, center.Z));
            surface.AddVertex(Rim(angle));
            surface.AddVertex(Rim(next));
        }

        Vector3 Rim(float angle) => OnGround(center.X + Mathf.Cos(angle) * radius.X,
            center.Z + Mathf.Sin(angle) * radius.Y);
        Vector3 OnGround(float x, float z) => new(x, center.Y + (terrain?.At(x, z) ?? 0f), z);
    }

    // Project only the +X and -Z edges, the outgoing sides of the upper-left light.
    internal static Mesh ExposedWallEdges(IReadOnlyList<ObstacleRectangle> walls, TerrainHeightField terrain,
        Transform3D toLocal)
    {
        if (walls.Count == 0) return null;
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var spans = new List<(float Start, float End, float Gap)>();
        var cuts = new List<float>();
        bool added = false;
        foreach (var wall in walls)
        {
            var height = wall.AirborneHeightRange.Value;
            Vector2 offset = SceneryOffset(Mathf.Min(height.Y - height.X, 6f));
            Edge(wall.Max.X, wall.Min.Y, wall.Max.Y, offset, east: true);
            Edge(-wall.Min.Y, wall.Min.X, wall.Max.X, offset, east: false);
        }
        return added ? surface.Commit() : null;

        void Edge(float edge, float start, float end, Vector2 offset, bool east)
        {
            float reach = east ? offset.X : -offset.Y;
            if (reach <= 0f) return;
            spans.Clear();
            cuts.Clear();
            cuts.Add(start);
            cuts.Add(end);
            foreach (var other in walls)
            {
                float near = east ? other.Min.X : -other.Max.Y;
                float far = east ? other.Max.X : -other.Min.Y;
                if (far <= edge + 0.001f || near >= edge + reach) continue;
                float lo = Mathf.Max(start, east ? other.Min.Y : other.Min.X);
                float hi = Mathf.Min(end, east ? other.Max.Y : other.Max.X);
                if (hi <= lo) continue;
                spans.Add((lo, hi, Mathf.Max(0f, near - edge)));
                cuts.Add(lo);
                cuts.Add(hi);
            }
            cuts.Sort();
            for (int i = 1; i < cuts.Count; i++)
            {
                float lo = cuts[i - 1], hi = cuts[i];
                if (hi - lo <= 0.001f) continue;
                float midpoint = (lo + hi) * 0.5f;
                float clear = reach;
                foreach (var span in spans)
                    if (midpoint > span.Start && midpoint < span.End)
                        clear = Mathf.Min(clear, span.Gap);
                if (clear <= 0.001f) continue;
                Vector2 projection = offset * (clear / reach);
                int sections = terrain == null ? 1 : Mathf.CeilToInt((hi - lo) / 2f);
                for (int j = 0; j < sections; j++)
                {
                    float from = Mathf.Lerp(lo, hi, (float)j / sections);
                    float to = Mathf.Lerp(lo, hi, (float)(j + 1) / sections);
                    Vector2 a = east ? new(edge, from) : new(from, -edge);
                    Vector2 b = east ? new(edge, to) : new(to, -edge);
                    Add(a); Add(b); Add(a + projection);
                    Add(b); Add(b + projection); Add(a + projection);
                }
                added = true;
            }
        }

        void Add(Vector2 point) => surface.AddVertex(toLocal * new Vector3(point.X,
            (terrain?.At(point.X, point.Y) ?? 0f) + 0.035f, point.Y));
    }

    public static Mesh Obstacles(ObstacleField field, float height)
    {
        if (field == null || field.RectangleCount == 0) return null;
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(Material());
        // Toward screen-right/down at the default 45-degree camera, as if light
        // falls from the upper-left. Keep the obstacle/tower shadow compact.
        Vector2 offset = new Vector2(0.55f, -0.20f) * height;
        foreach (ObstacleRectangle rect in field.Rectangles)
        {
            Vector2 inset = (rect.Max - rect.Min) * 0.05f;
            Vector2 min = rect.Min + inset + offset, max = rect.Max - inset + offset;
            Vector3 a = new(min.X, 0.02f, min.Y), b = new(max.X, 0.02f, min.Y);
            Vector3 c = new(max.X, 0.02f, max.Y), d = new(min.X, 0.02f, max.Y);
            surface.AddVertex(a); surface.AddVertex(b); surface.AddVertex(c);
            surface.AddVertex(a); surface.AddVertex(c); surface.AddVertex(d);
        }
        return surface.Commit();
    }
}
