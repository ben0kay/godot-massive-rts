// Builds the Broadside's three reusable, two-tone hard-surface meshes.
// All parts are baked at initialization; no extra per-tower nodes or animation.
using Godot;

namespace MassiveRts;

internal static class BroadsideTowerMeshes
{
    // RGB are material weights, not literal colors: metal, faction paint, bright trim.
    // Existing faceted helpers multiply these weights by their baked face lighting.
    private static Color Steel(float shade) => new(shade, 0f, 0f);
    private static Color Paint(float shade = 0.90f) => new(0f, shade, 0f);
    private static Color Trim(float shade = 1f) => new(0f, 0f, shade);

    // =========================================================
    // Normalized 3x3 footprint: clipped plinth, sloped armor, corner plates
    // and dark side recesses. The existing renderer scales this to the footprint.
    public static Mesh CreateBase()
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        Plate(surface, new Vector3(0f, -0.41f, 0f), new Vector3(1f, 0.18f, 1f), 0.11f, 0.96f, Steel(0.48f));
        Plate(surface, new Vector3(0f, -0.09f, 0f), new Vector3(0.96f, 0.58f, 0.96f), 0.12f, 0.84f, Steel(0.95f));
        Plate(surface, new Vector3(0f, 0.32f, 0f), new Vector3(0.84f, 0.28f, 0.84f), 0.14f, 0.94f, Steel(0.86f));
        for (int x = -1; x <= 1; x += 2)
        {
            for (int z = -1; z <= 1; z += 2)
            {
                Plate(surface, new Vector3(x * 0.35f, 0.15f, z * 0.35f),
                    new Vector3(0.21f, 0.48f, 0.21f), 0.22f, 0.79f, Paint());
                Block(surface, new Vector3(x * 0.35f, 0.399f, z * 0.35f),
                    new Vector3(0.075f, 0.025f, 0.075f), Steel(0.48f));
            }
            // Four armored side panels with dark insets and a faction light strip.
            Block(surface, new Vector3(x * 0.457f, -0.12f, 0f),
                new Vector3(0.06f, 0.30f, 0.45f), Steel(0.46f));
            Block(surface, new Vector3(0f, -0.12f, x * 0.457f),
                new Vector3(0.45f, 0.30f, 0.06f), Steel(0.46f));
            Block(surface, new Vector3(x * 0.49f, -0.08f, 0f),
                new Vector3(0.018f, 0.065f, 0.30f), Trim());
            Block(surface, new Vector3(0f, -0.08f, x * 0.49f),
                new Vector3(0.30f, 0.065f, 0.018f), Trim());
            for (int rib = -1; rib <= 1; rib++)
            {
                Block(surface, new Vector3(x * 0.49f, -0.21f, rib * 0.085f),
                    new Vector3(0.016f, 0.075f, 0.032f), Steel(0.95f));
                Block(surface, new Vector3(rib * 0.085f, -0.21f, x * 0.49f),
                    new Vector3(0.032f, 0.075f, 0.016f), Steel(0.95f));
            }
        }
        return surface.Commit();
    }

    // =========================================================
    // Rotating octagonal collar, central armor spine and twin launcher yokes.
    // The original pivot and barrel mounting offsets remain in DefenseRenderer.
    public static Mesh CreateHead()
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        Plate(surface, new Vector3(0f, -0.21f, 0.12f), new Vector3(3.65f, 0.24f, 3.45f), 0.46f, 0.94f, Steel(0.52f));
        Plate(surface, new Vector3(0f, -0.02f, 0.12f), new Vector3(3.40f, 0.24f, 3.15f), 0.46f, 0.87f, Steel(0.92f));
        Block(surface, new Vector3(0f, 0.32f, 0.25f), new Vector3(2.55f, 0.62f, 2.28f), Steel(0.92f));
        Plate(surface, new Vector3(0f, 0.73f, 0.46f), new Vector3(1.12f, 0.42f, 1.75f), 0.19f, 0.78f, Steel(1f));
        Block(surface, new Vector3(0f, 0.97f, 0.68f), new Vector3(0.68f, 0.07f, 0.34f), Trim());
        for (int side = -1; side <= 1; side += 2)
        {
            // Side armor has a substantial faction-colored shell over a dark pod.
            Plate(surface, new Vector3(side * 1.37f, 0.33f, 0.32f),
                new Vector3(0.91f, 0.68f, 2.45f), 0.19f, 0.91f, Steel(0.66f));
            Plate(surface, new Vector3(side * 1.37f, 0.71f, 0.28f),
                new Vector3(0.94f, 0.48f, 2.34f), 0.19f, 0.80f, Paint());
            for (int vent = 0; vent < 3; vent++)
                Block(surface, new Vector3(side * 1.37f, 0.959f, 0.25f + vent * 0.22f),
                    new Vector3(0.46f, 0.035f, 0.095f), Steel(0.40f));
            // Dark launcher support beneath each independently recoiling barrel.
            Block(surface, new Vector3(side * 0.70f, 0.82f, -0.79f),
                new Vector3(1.00f, 0.88f, 1.78f), Steel(0.78f));
            Block(surface, new Vector3(side * 0.70f, 1.29f, -0.66f),
                new Vector3(0.48f, 0.07f, 0.38f), Paint());
            Block(surface, new Vector3(side * 0.78f, -0.10f, 1.71f),
                new Vector3(0.60f, 0.09f, 0.07f), Trim());
        }
        return surface.Commit();
    }

    // =========================================================
    // Dark faceted tube, reinforced recoil sleeve, painted muzzle crown.
    // Nothing extends past the existing local muzzle endpoint at z = -4.7.
    public static Mesh CreateBarrel()
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        Block(surface, new Vector3(0f, 0f, -3.10f), new Vector3(0.48f, 0.42f, 3.12f), Steel(1f));
        Block(surface, new Vector3(0f, 0f, -1.86f), new Vector3(0.76f, 0.65f, 0.64f), Steel(0.76f));
        Block(surface, new Vector3(0f, 0.225f, -3.15f), new Vector3(0.17f, 0.035f, 1.45f), Paint());
        Block(surface, new Vector3(0f, 0f, -4.16f), new Vector3(0.58f, 0.51f, 0.15f), Paint());
        Block(surface, new Vector3(0f, 0f, -4.44f), new Vector3(0.70f, 0.60f, 0.50f), Paint());
        // Dark inset face suggests an open muzzle at battlefield scale.
        Block(surface, new Vector3(0f, 0f, -4.687f), new Vector3(0.38f, 0.30f, 0.026f), Steel(0.18f));
        return surface.Commit();
    }

    private static void Block(SurfaceTool surface, Vector3 center, Vector3 size, Color mask) =>
        FacetedSoldierMeshes.AppendBeveledRectangle(surface, center, size, mask);

    // =========================================================
    // Eight-sided clipped rectangle with a tapered upper shoulder. Uses the
    // same clockwise triangle convention and northwest faux light as soldiers.
    private static void Plate(SurfaceTool surface, Vector3 center, Vector3 size,
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
                bevelNormal, mask * Mathf.Lerp(shade, 1f, 0.58f));
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
