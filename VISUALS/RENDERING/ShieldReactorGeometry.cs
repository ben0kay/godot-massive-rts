// Shared faceted animated reactor: baked into each generator's existing mesh.
using Godot;

namespace MassiveRts;

internal static class ShieldReactorGeometry
{
    // UV.x = 32 + circumferential coordinate; UV.y = normalized height.
    // This range is reserved for reactors, outside all existing armor/motion tags.
    // Dimensions X/Z are radii, Y is total height. No new mesh or instance state.
    internal static void Append(SurfaceTool surface, Vector3 center, Vector3 dimensions, Color mask)
    {
        const int sides = 12;
        Vector3 Point(int i, float v, float radius)
        {
            float angle = i * Mathf.Tau / sides;
            return center + new Vector3(Mathf.Cos(angle) * dimensions.X * radius,
                (v - 0.5f) * dimensions.Y, Mathf.Sin(angle) * dimensions.Z * radius);
        }
        void Vertex(Vector3 point, Vector3 normal, float u, float v)
        {
            surface.SetNormal(normal);
            surface.SetColor(mask);
            surface.SetUV(new Vector2(32f + u, v));
            surface.AddVertex(point);
        }
        void Band(float bottom, float top, float r0, float r1)
        {
            for (int i = 0; i < sides; i++)
            {
                float u0 = i / (float)sides, u1 = (i + 1) / (float)sides;
                Vector3 a = Point(i, bottom, r0), b = Point(i, top, r1);
                Vector3 c = Point(i + 1, top, r1), d = Point(i + 1, bottom, r0);
                Vector3 normal = (b - a).Cross(c - a).Normalized();
                Vertex(a, normal, u0, bottom); Vertex(c, normal, u1, top); Vertex(b, normal, u0, top);
                Vertex(a, normal, u0, bottom); Vertex(d, normal, u1, bottom); Vertex(c, normal, u1, top);
            }
        }
        Band(0f, 0.14f, 0.80f, 1f);
        Band(0.14f, 0.86f, 1f, 1f);
        Band(0.86f, 1f, 1f, 0.80f);
        for (int i = 0; i < sides; i++)
        {
            float u0 = i / (float)sides, u1 = (i + 1) / (float)sides;
            Vertex(center + Vector3.Up * (dimensions.Y * 0.5f), Vector3.Up, (u0 + u1) * 0.5f, 1f);
            Vertex(Point(i, 1f, 0.80f), Vector3.Up, u0, 1f);
            Vertex(Point(i + 1, 1f, 0.80f), Vector3.Up, u1, 1f);
            Vertex(center - Vector3.Up * (dimensions.Y * 0.5f), Vector3.Down, (u0 + u1) * 0.5f, 0f);
            Vertex(Point(i + 1, 0f, 0.80f), Vector3.Down, u1, 0f);
            Vertex(Point(i, 0f, 0.80f), Vector3.Down, u0, 0f);
        }
    }
}
