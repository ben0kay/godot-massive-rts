// Builds one indexed surface with terrain-following banks inside the painted water cells.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal static class PaintedWaterMesh
{
    private const float SurfaceOffset = 0.09f;
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
                // Use the inward diagonal on convex corners to keep the bank slope continuous.
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
        return surface.Commit();

        float Distance(Vector2I point) => shoreline.TryGetValue(point, out float squared)
            ? Mathf.Sqrt(squared) * Step : DepthFadeDistance;

        int Vertex(Vector2I point)
        {
            if (vertices.TryGetValue(point, out int index)) return index;
            float distance = Distance(point);
            float bank = 1f - Mathf.Clamp(distance / data.ShorelineWidth, 0f, 1f);
            float x = point.X * Step, z = point.Y * Step;
            float y = (terrain?.At(x, z) ?? 0f) + SurfaceOffset + bank * data.BankHeight;
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
