// Builds one indexed surface with a separately shaded retaining rim along exposed edges.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal static class PaintedWaterMesh
{
    private const float SurfaceOffset = 0.09f;
    private const string RimShaderPath = "res://WORLD/OBSTACLES/IndustrialWall.gdshader";
    private const float DepthFadeDistance = 3.5f;
    private const float Step = WorldPaintMeshBuilder.Step;
    private const string ShaderPath = "res://WORLD/WORLD_PAINT/PaintedWater.gdshader";

    public static Mesh Build(HashSet<Vector2I> cells, TerrainHeightField terrain, WorldPaintData data)
    {
        if (cells.Count == 0) return null;

        var shoreline = SampleShoreline(cells);
        var material = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
        material.SetShaderParameter("water_color", data.WaterColor);
        material.SetShaderParameter("deep_water_color", data.DeepWaterColor);
        material.SetShaderParameter("shoreline_color", data.ShorelineColor);
        material.SetShaderParameter("shoreline_width", data.ShorelineWidth);
        material.SetShaderParameter("depth_fade_distance", DepthFadeDistance);
        material.SetShaderParameter("ripple_strength", data.RippleStrength);
        material.SetShaderParameter("ripple_speed", data.RippleSpeed);

        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(material);
        var vertices = new Dictionary<Vector2I, int>();
        foreach (var cell in cells)
        {
            Vector2I corner = cell * 2;
            for (int row = 0; row < 2; row++)
            for (int col = 0; col < 2; col++)
            {
                var a = corner + new Vector2I(col, row);
                var b = a + Vector2I.Right;
                var c = a + Vector2I.Down;
                var d = c + Vector2I.Right;
                int ai = Vertex(a), bi = Vertex(b), ci = Vertex(c), di = Vertex(d);
                // Use the inward diagonal on convex corners to keep shore-distance shading continuous.
                if (Distance(a) + Distance(d) > Distance(b) + Distance(c))
                {
                    Triangle(ai, bi, di);
                    Triangle(ai, di, ci);
                }
                else
                {
                    Triangle(ai, bi, ci);
                    Triangle(bi, di, ci);
                }
            }
        }
        var mesh = surface.Commit();
        surface.Clear();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var rimMaterial = new ShaderMaterial { Shader = GD.Load<Shader>(RimShaderPath) };
        rimMaterial.SetShaderParameter("basin_trim", true);
        surface.SetMaterial(rimMaterial);
        foreach (var cell in cells)
        {
            Vector2 corner = (Vector2)cell * WorldPaint.CellSize;
            float size = WorldPaint.CellSize;
            if (!cells.Contains(cell + Vector2I.Up))
                Rim(cell, corner, Vector2.Right, Vector2.Down);
            if (!cells.Contains(cell + Vector2I.Right))
                Rim(cell, corner + Vector2.Right * size, Vector2.Down, Vector2.Left);
            if (!cells.Contains(cell + Vector2I.Down))
                Rim(cell, corner + Vector2.One * size, Vector2.Left, Vector2.Up);
            if (!cells.Contains(cell + Vector2I.Left))
                Rim(cell, corner + Vector2.Down * size, Vector2.Up, Vector2.Right);
        }
        return surface.Commit(mesh);

        void Rim(Vector2I cell, Vector2 start, Vector2 run, Vector2 inward)
        {
            float width = Mathf.Clamp(data.ShorelineWidth * 0.26f, 0.14f, 0.36f);
            float height = 0.52f + data.BankHeight;
            int startMiter = Miter((Vector2I)(-run));
            int endMiter = Miter((Vector2I)run);
            for (int i = 0; i < 2; i++)
            {
                Vector2 a = start + run * (i * Step), b = a + run * Step;
                var outerA = Point(a, height - 0.075f);
                var outerB = Point(b, height - 0.075f);
                Vector2 insetA = inward + (i == 0 ? run * startMiter : Vector2.Zero);
                Vector2 insetB = inward - (i == 1 ? run * endMiter : Vector2.Zero);
                var capA = Point(a + insetA * 0.075f, height);
                var capB = Point(b + insetB * 0.075f, height);
                var innerA = Point(a + insetA * width, height);
                var innerB = Point(b + insetB * width, height);
                var footA = Point(a + insetA * width, SurfaceOffset - 0.02f);
                var footB = Point(b + insetB * width, SurfaceOffset - 0.02f);
                Vector3 normal = new(inward.X, 0f, inward.Y);
                Quad(Point(a, 0f), Point(b, 0f), outerA, outerB, -normal);
                Quad(outerA, outerB, capA, capB, (Vector3.Up - normal).Normalized());
                Quad(capA, capB, innerA, innerB, Vector3.Up);
                Quad(innerA, innerB, footA, footB, normal, light: true);
            }

            // Adjacent bands meet on a diagonal at convex and concave corners.
            int Miter(Vector2I along)
            {
                if (!cells.Contains(cell + along)) return 1;
                return cells.Contains(cell + along - (Vector2I)inward) ? -1 : 0;
            }
        }

        Vector3 Point(Vector2 point, float height) =>
            new(point.X, (terrain?.At(point.X, point.Y) ?? 0f) + height, point.Y);

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, bool light = false)
        {
            RimVertex(a, new Vector2(0f, 1f));
            RimVertex(b, new Vector2(0f, 1f));
            RimVertex(c, Vector2.Zero);
            RimVertex(b, new Vector2(0f, 1f));
            RimVertex(d, Vector2.Zero);
            RimVertex(c, Vector2.Zero);

            void RimVertex(Vector3 point, Vector2 uv)
            {
                surface.SetNormal(normal);
                surface.SetUV(new Vector2(light ? 1f : 0f, uv.Y));
                surface.AddVertex(point);
            }
        }

        float Distance(Vector2I point) => shoreline.TryGetValue(point, out float squared)
            ? Mathf.Sqrt(squared) * Step : DepthFadeDistance;

        int Vertex(Vector2I point)
        {
            if (vertices.TryGetValue(point, out int index)) return index;
            float distance = Distance(point);
            float x = point.X * Step, z = point.Y * Step;
            float y = (terrain?.At(x, z) ?? 0f) + SurfaceOffset;
            surface.SetUV(new Vector2(distance, 0f));
            surface.AddVertex(new Vector3(x, y, z));
            index = vertices.Count;
            vertices.Add(point, index);
            return index;
        }

        void Triangle(int a, int b, int c)
        {
            surface.AddIndex(a);
            surface.AddIndex(b);
            surface.AddIndex(c);
        }
    }

    private static Dictionary<Vector2I, float> SampleShoreline(HashSet<Vector2I> cells)
    {
        var distances = new Dictionary<Vector2I, float>();
        float fade = DepthFadeDistance / Step;
        int reach = Mathf.CeilToInt(fade);
        foreach (var cell in cells)
        {
            Vector2I corner = cell * 2;
            Vector2I east = corner + Vector2I.Right * 2, south = corner + Vector2I.Down * 2;
            Vector2I opposite = east + Vector2I.Down * 2;
            if (!cells.Contains(cell + Vector2I.Up)) SampleEdge(corner, east);
            if (!cells.Contains(cell + Vector2I.Right)) SampleEdge(east, opposite);
            if (!cells.Contains(cell + Vector2I.Down)) SampleEdge(south, opposite);
            if (!cells.Contains(cell + Vector2I.Left)) SampleEdge(corner, south);
        }
        return distances;

        void SampleEdge(Vector2I start, Vector2I end)
        {
            for (int z = start.Y - reach; z <= end.Y + reach; z++)
            for (int x = start.X - reach; x <= end.X + reach; x++)
            {
                int dx = x - Mathf.Clamp(x, start.X, end.X);
                int dz = z - Mathf.Clamp(z, start.Y, end.Y);
                float squared = dx * dx + dz * dz;
                if (squared >= fade * fade) continue;
                var point = new Vector2I(x, z);
                if (!distances.TryGetValue(point, out float previous) || squared < previous)
                    distances[point] = squared;
            }
        }
    }
}
