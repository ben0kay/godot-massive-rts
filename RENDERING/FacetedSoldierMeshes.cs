// Creates shared beveled low-poly soldier meshes with baked top, chamfer and side tones.
// All shapes use one unshaded material and remain compatible with MultiMesh batching.
using Godot;
namespace MassiveRts;

public static class FacetedSoldierMeshes
{
    private const int CircleSides = 16;
    private const float TopInsetFraction = 0.11f;
    private const float DiamondLengthFootprintFraction = 0.96f;
    private static readonly Color TopTint = Colors.White;
    private static readonly Color BottomTint = new(0.48f, 0.48f, 0.48f);

    // =========================================================
    // Construct one reusable mesh per unit type. No per-soldier geometry or lighting work.
    public static Mesh[] Create(RuntimeUnitCatalog catalog)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = Colors.White,
            VertexColorUseAsAlbedo = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        var meshes = new Mesh[catalog.Count];
        for (int i = 0; i < meshes.Length; i++)
        {
            ref readonly UnitStats type = ref catalog[i];
            float radius = type.Width * 0.5f;
            meshes[i] = type.Shape switch
            {
                UnitShape.Circle => CreateBeveledPrism(CircleSides, radius, radius, type.Height, 0f, material),
                // A four-vertex square rotated 45 degrees in polar coordinates gives axis-aligned edges.
                UnitShape.Square => CreateBeveledPrism(4, type.Width / Mathf.Sqrt(2f),
                    type.Width / Mathf.Sqrt(2f), type.Height, Mathf.Pi * 0.25f, material),
                UnitShape.Triangle => CreateBeveledPrism(3, type.Width / Mathf.Sqrt(3f),
                    type.Width / Mathf.Sqrt(3f), type.Height, -Mathf.Pi * 0.5f, material),
                // A genuinely long, narrow rhombus, rather than the old equal-axis rotated square.
                // Longest point follows the default X-axis march, staying inside the existing collision footprint.
                UnitShape.Diamond => CreateBeveledPrism(4,
                    Mathf.Max(radius, type.Footprint * DiamondLengthFootprintFraction), radius, type.Height, 0f, material),
                UnitShape.Pentagon => CreateBeveledPrism(5,
                    type.Width / (1f + Mathf.Cos(Mathf.Pi / 5f)),
                    type.Width / (1f + Mathf.Cos(Mathf.Pi / 5f)),
                    type.Height, -Mathf.Pi * 0.5f, material),
                UnitShape.Hexagon => CreateBeveledPrism(6, radius, radius, type.Height, -Mathf.Pi * 0.5f, material),
                _ => throw new System.ArgumentOutOfRangeException()
            };
        }
        return meshes;
    }

    // =========================================================
    // A narrow inset top ring creates a single, lightly shaded bevel around every silhouette.
    // The shoulders retain the original outer footprint, and the bottoms remain at -height/2.
    private static ArrayMesh CreateBeveledPrism(int sides, float radiusX, float radiusZ,
        float height, float rotation, Material material)
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(material);

        AppendPrism(surface, sides, radiusX, radiusZ, height, rotation, Vector3.Zero, Colors.White);
        return surface.Commit();
    }

    public static void AppendBeveledRectangle(SurfaceTool surface, Vector3 center, Vector3 size, Color tint)
    {
        const float inverseSqrtTwo = 0.70710678f;
        AppendPrism(surface, 4, size.X * inverseSqrtTwo, size.Z * inverseSqrtTwo,
            size.Y, Mathf.Pi * 0.25f, center, tint, 0.16f);
    }

    private static void AppendPrism(SurfaceTool surface, int sides, float radiusX, float radiusZ,
        float height, float rotation, Vector3 center, Color tint, float rectangleInset = 0f)
    {
        float half = height * 0.5f;
        float bevelRise = Mathf.Min(height * 0.16f, Mathf.Min(radiusX, radiusZ) * 0.15f);
        float insetDistance = Mathf.Min(radiusX, radiusZ) * TopInsetFraction;
        float insetX = TopInsetFraction, insetZ = TopInsetFraction;
        if (rectangleInset > 0f)
        {
            insetDistance = rectangleInset;
            insetX = Mathf.Min(TopInsetFraction, rectangleInset / (radiusX * 0.70710678f));
            insetZ = Mathf.Min(TopInsetFraction, rectangleInset / (radiusZ * 0.70710678f));
        }
        var top = new Vector3[sides];
        var shoulder = new Vector3[sides];
        var bottom = new Vector3[sides];

        for (int i = 0; i < sides; i++)
        {
            float angle = rotation + i * Mathf.Tau / sides;
            float x = Mathf.Cos(angle) * radiusX, z = Mathf.Sin(angle) * radiusZ;
            top[i] = center + new Vector3(x * (1f - insetX), half, z * (1f - insetZ));
            shoulder[i] = center + new Vector3(x, half - bevelRise, z);
            bottom[i] = center + new Vector3(x, -half, z);
        }

        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            Vector3 edge = shoulder[next] - shoulder[i];
            Vector3 outward = new Vector3(edge.Z, 0f, -edge.X).Normalized();

            AddTriangle(surface, center + new Vector3(0, half, 0), top[next], top[i], Vector3.Up, TopTint * tint);
            AddTriangle(surface, center + new Vector3(0, -half, 0), bottom[i], bottom[next], Vector3.Down, BottomTint * tint);

            AddQuad(surface, bottom[i], shoulder[i], shoulder[next], bottom[next],
                outward, SideTint(outward) * tint);
            Vector3 bevelNormal = (outward * bevelRise + Vector3.Up * insetDistance).Normalized();
            AddQuad(surface, shoulder[i], top[i], top[next], shoulder[next],
                bevelNormal, BevelTint(outward) * tint);
        }
    }

    // =========================================================
    private static float SideBrightness(Vector3 outward)
    {
        // Fixed fake northwest sun, with no run-time lighting or per-instance material cost.
        return Mathf.Clamp(0.73f - 0.10f * outward.X - 0.12f * outward.Z, 0.58f, 0.86f);
    }

    private static Color SideTint(Vector3 outward)
    {
        float brightness = SideBrightness(outward);
        return new Color(brightness, brightness, brightness);
    }

    private static Color BevelTint(Vector3 outward)
    {
        // Extra intermediate tone makes the top chamfer read as a softened edge.
        float brightness = Mathf.Lerp(SideBrightness(outward), 1f, 0.58f);
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
        // Preserve the project's existing Godot clockwise front-face convention.
        surface.AddVertex(a);
        surface.AddVertex(c);
        surface.AddVertex(b);
    }
}
