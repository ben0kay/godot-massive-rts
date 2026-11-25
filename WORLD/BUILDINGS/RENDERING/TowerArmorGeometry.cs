// Shared baked material masks and faceted armor primitives for defense tower meshes.
using Godot;

namespace MassiveRts;

internal static class TowerArmorGeometry
{
    // RGB are material weights, not literal colors: metal, faction paint, bright trim.
    // Existing faceted helpers multiply these weights by their baked face lighting.
    internal static Color Steel(float shade) => new(shade, 0f, 0f);
    internal static Color Paint(float shade = 0.90f) => new(0f, shade, 0f);
    internal static Color Trim(float shade = 1f) => new(0f, 0f, shade);

    internal static void Block(SurfaceTool surface, Vector3 center, Vector3 size, Color mask) =>
        FacetedSoldierMeshes.AppendBeveledRectangle(surface, center, size, mask);

    // =========================================================
    // Eight-sided clipped rectangle with a tapered upper shoulder. Uses the
    // same clockwise triangle convention and northwest faux light as soldiers.
    internal static void Plate(SurfaceTool surface, Vector3 center, Vector3 size,
        float cornerFraction, float topScale, Color mask)
    {
        float x = size.X * 0.5f, z = size.Z * 0.5f, y = size.Y * 0.5f;
        float cut = Mathf.Min(x, z) * cornerFraction;
        Vector2[] outline =
        {
            new(x, -z + cut), new(x, z - cut), new(x - cut, z), new(-x + cut, z),
            new(-x, z - cut), new(-x, -z + cut), new(-x + cut, -z), new(x - cut, -z)
        };
        Vector3 Bottom(Vector2 p) => center + new Vector3(p.X, -y, p.Y);
        Vector3 Shoulder(Vector2 p) => center + new Vector3(p.X, y * 0.10f, p.Y);
        Vector3 Top(Vector2 p) => center + new Vector3(p.X * topScale, y, p.Y * topScale);
        for (int i = 0; i < outline.Length; i++)
        {
            Vector2 a = outline[i], b = outline[(i + 1) % outline.Length];
            Vector3 edge = Shoulder(b) - Shoulder(a);
            Vector3 outward = new Vector3(edge.Z, 0f, -edge.X).Normalized();
            float shade = Mathf.Clamp(0.73f - 0.10f * outward.X - 0.12f * outward.Z, 0.58f, 0.86f);
            Triangle(surface, center + Vector3.Up * y, Top(b), Top(a), Vector3.Up, mask);
            Triangle(surface, center - Vector3.Up * y, Bottom(a), Bottom(b), Vector3.Down, mask * 0.48f);
            Quad(surface, Bottom(a), Shoulder(a), Shoulder(b), Bottom(b), outward, mask * shade);
            Vector3 bevelNormal = (outward * (y * 0.9f) + Vector3.Up * (Mathf.Min(x, z) * (1f - topScale))).Normalized();
            Quad(surface, Shoulder(a), Top(a), Top(b), Shoulder(b),
                bevelNormal, mask * Mathf.Lerp(shade, 1f, 0.78f));
        }
    }

    private static void Quad(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
        Vector3 normal, Color mask)
    {
        Triangle(surface, a, b, c, normal, mask);
        Triangle(surface, a, c, d, normal, mask);
    }

    private static void Triangle(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c,
        Vector3 normal, Color mask)
    {
        surface.SetNormal(normal);
        surface.SetColor(mask);
        surface.AddVertex(a);
        surface.AddVertex(c);
        surface.AddVertex(b);
    }
}
