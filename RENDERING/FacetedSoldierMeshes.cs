// Creates three shared low-poly soldier meshes with baked top/side face tones.
// These vertex tints multiply the existing per-instance blue/red/white colors.
using Godot;
namespace MassiveRts;

public static class FacetedSoldierMeshes
{
    private static readonly Color TopTint = Colors.White;
    private static readonly Color BottomTint = new(0.48f, 0.48f, 0.48f);

    // =========================================================
    // All materials stay unshaded. Shading is authored once per mesh, not per frame.
    public static Mesh[] Create(Material material) =>
    [
        CreatePrism(3, 0.50f, 1.10f, -Mathf.Pi * 0.5f, material), // Melee wedge
        CreateBox(0.80f, 1.10f, 0.80f, material),                 // Ranged cube
        CreatePrism(6, 0.65f, 1.30f, Mathf.Pi / 6f, material)    // Heavy hex column
    ];

    // =========================================================
    private static ArrayMesh CreatePrism(int sides, float radius, float height, float rotation, Material material)
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(material);
        float half = height * 0.5f;
        var upper = new Vector3[sides];
        var lower = new Vector3[sides];

        for (int i = 0; i < sides; i++)
        {
            float angle = rotation + i * 2f * Mathf.Pi / sides;
            float x = Mathf.Cos(angle) * radius, z = Mathf.Sin(angle) * radius;
            upper[i] = new Vector3(x, half, z);
            lower[i] = new Vector3(x, -half, z);
        }

        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            AddTriangle(surface, new Vector3(0, half, 0), upper[next], upper[i], Vector3.Up, TopTint);
            AddTriangle(surface, new Vector3(0, -half, 0), lower[i], lower[next], Vector3.Down, BottomTint);

            Vector3 outward = new Vector3(upper[i].X + upper[next].X, 0, upper[i].Z + upper[next].Z).Normalized();
            AddQuad(surface, lower[i], upper[i], upper[next], lower[next], outward, SideTint(outward));
        }

        return surface.Commit();
    }

    // =========================================================
    private static ArrayMesh CreateBox(float width, float height, float depth, Material material)
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(material);
        float x = width * 0.5f, y = height * 0.5f, z = depth * 0.5f;

        AddQuad(surface, new(-x, y, -z), new(-x, y, z), new(x, y, z), new(x, y, -z),
            Vector3.Up, TopTint);
        AddQuad(surface, new(-x, -y, z), new(-x, -y, -z), new(x, -y, -z), new(x, -y, z),
            Vector3.Down, BottomTint);
        AddQuad(surface, new(-x, -y, z), new(x, -y, z), new(x, y, z), new(-x, y, z),
            new Vector3(0, 0, 1), SideTint(new Vector3(0, 0, 1)));
        AddQuad(surface, new(x, -y, -z), new(-x, -y, -z), new(-x, y, -z), new(x, y, -z),
            new Vector3(0, 0, -1), SideTint(new Vector3(0, 0, -1)));
        AddQuad(surface, new(x, -y, z), new(x, -y, -z), new(x, y, -z), new(x, y, z),
            Vector3.Right, SideTint(Vector3.Right));
        AddQuad(surface, new(-x, -y, -z), new(-x, -y, z), new(-x, y, z), new(-x, y, -z),
            Vector3.Left, SideTint(Vector3.Left));
        return surface.Commit();
    }

    // =========================================================
    private static Color SideTint(Vector3 outward)
    {
        // A fixed fake sun from northwest. Slightly different faces stay readable
        // even when no DirectionalLight3D or shadow rendering exists.
        float brightness = Mathf.Clamp(0.73f - 0.10f * outward.X - 0.12f * outward.Z, 0.58f, 0.86f);
        return new Color(brightness, brightness, brightness);
    }

    private static void AddQuad(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
        Vector3 normal, Color tint)
    {
        AddTriangle(surface, a, b, c, normal, tint);
        AddTriangle(surface, a, c, d, normal, tint);
    }

    private static void AddTriangle(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c,
        Vector3 normal, Color tint)
    {
        surface.SetNormal(normal);
        surface.SetColor(tint);
        // Arguments are ordered outward using the cross product; Godot's
        // front-face convention is clockwise, so reverse them when submitted.
        surface.AddVertex(a);
        surface.AddVertex(c);
        surface.AddVertex(b);
    }
}
