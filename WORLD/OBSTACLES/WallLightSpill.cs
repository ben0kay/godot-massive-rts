// Bakes a small ground decal beside exposed amber wall lamps.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal static class WallLightSpill
{
    internal static Mesh Build(IReadOnlyList<ObstacleRectangle> walls, TerrainHeightField terrain, Transform3D toLocal, IReadOnlyList<ObstacleRectangle> occluders = null)
    {
        if (walls.Count == 0) return null;
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        bool emitted = false;
        foreach (var wall in walls)
        {
            var height = wall.AirborneHeightRange.Value;
            if (height.Y - height.X < 0.55f) continue;
            Face(wall.Min.X, wall.Min.Y, wall.Max.Y, Vector2.Left, Vector2.Down);
            Face(wall.Max.X, wall.Min.Y, wall.Max.Y, Vector2.Right, Vector2.Down);
            Face(wall.Min.Y, wall.Min.X, wall.Max.X, Vector2.Up, Vector2.Right);
            Face(wall.Max.Y, wall.Min.X, wall.Max.X, Vector2.Down, Vector2.Right);
        }
        return emitted ? surface.Commit() : null;

        void Face(float plane, float start, float end, Vector2 outward, Vector2 along)
        {
            const float pitch = WorldPaint.CellSize;
            int first = Mathf.CeilToInt(start / pitch - 0.5f);
            for (float run = (first + 0.5f) * pitch; run < end; run += pitch)
            {
                Vector2 origin = along * run + new Vector2(Mathf.Abs(outward.X), Mathf.Abs(outward.Y)) * plane;
                Vector2 tile = (origin - outward * 0.05f) / pitch;
                if (!HasLamp(Mathf.FloorToInt(tile.X), Mathf.FloorToInt(tile.Y))) continue;
                Vector2 probe = origin + outward * 0.06f;
                bool occluded = false;
                foreach (var other in occluders ?? walls)
                {
                    if (probe.X >= other.Min.X && probe.X < other.Max.X &&
                        probe.Y >= other.Min.Y && probe.Y < other.Max.Y)
                    {
                        occluded = true;
                        break;
                    }
                }
                if (occluded) continue;
                emitted = true;
                Add(0f, 0f); Add(1f, 0f); Add(0f, 1f);
                Add(1f, 0f); Add(1f, 1f); Add(0f, 1f);

                void Add(float u, float v)
                {
                    Vector2 point = origin + along * ((u - 0.5f) * 2.3f) + outward * (0.03f + v * 1.6f);
                    float y = (terrain?.At(point.X, point.Y) ?? 0f) + 0.045f;
                    surface.SetUV(new Vector2(u, v));
                    surface.AddVertex(toLocal * new Vector3(point.X, y, point.Y));
                }
            }
        }
    }

    // Matches the panel selection in IndustrialWall.gdshader.
    private static bool HasLamp(int x, int z)
    {
        uint hash = unchecked((uint)x * 1597334677u ^ (uint)z * 3812015801u ^ 7u * 2798796415u);
        hash = unchecked((hash ^ (hash >> 16)) * 2246822519u);
        return (hash & 65535u) / 65536f < 0.10f;
    }
}
