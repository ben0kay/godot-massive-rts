// Shared angular plant, stone and branch geometry for decorative paint batches.
using Godot;

namespace MassiveRts;

internal static class UndergrowthMeshes
{
    internal static readonly string[] Presets = { "Grassland", "Rocky", "Marsh" };
    internal enum Shape { Grass, Fern, Scrub, Stone, Twig, Reeds, Cattail }
    internal const int ShapeCount = 7;
    private static Mesh[] meshes;

    internal static Mesh Get(Shape shape)
    {
        meshes ??= new Mesh[ShapeCount];
        return meshes[(int)shape] ??= Build(shape);
    }

    private static Mesh Build(Shape shape)
    {
        var material = new StandardMaterial3D { VertexColorUseAsAlbedo = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(material);

        void Triangle(Vector3 a, Vector3 b, Vector3 c, float shade)
        {
            surface.SetColor(new Color(shade, shade, shade));
            surface.AddVertex(a); surface.AddVertex(b); surface.AddVertex(c);
        }

        void Blade(float x, float z, float yaw, float width, float height, float lean, float shade)
        {
            var root = new Vector3(x, 0f, z);
            var side = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
            var middle = root + Vector3.Up * (height * .47f) + side * (lean * .35f);
            var tip = root + Vector3.Up * height + side * lean;
            var a = root - side * width * .5f;
            var b = root + side * width * .5f;
            Triangle(a, b, middle + side * width * .34f, shade * .85f);
            Triangle(a, middle + side * width * .34f, tip, shade);
        }

        void Facet(Vector3 center, float width, float depth, float height)
        {
            Vector3 a = center + new Vector3(-width, 0f, -depth * .5f);
            Vector3 b = center + new Vector3(width * .78f, 0f, -depth);
            Vector3 c = center + new Vector3(width, 0f, depth * .5f);
            Vector3 d = center + new Vector3(-width * .65f, 0f, depth);
            Vector3 peak = center + new Vector3(width * .18f, height, -depth * .12f);
            Triangle(a, b, peak, .83f); Triangle(b, c, peak, .71f);
            Triangle(c, d, peak, .97f); Triangle(d, a, peak, .88f);
        }

        switch (shape)
        {
            case Shape.Grass:
            case Shape.Reeds:
            case Shape.Cattail:
                int count = shape == Shape.Grass ? 6 : 4;
                for (int i = 0; i < count; i++)
                {
                    float yaw = i * 2.39996f;
                    float x = Mathf.Cos(yaw) * (.036f + .012f * i);
                    float z = Mathf.Sin(yaw) * (.036f + .012f * i);
                    float high = (shape == Shape.Grass ? .12f : .22f) + i * .039f;
                    float lean = shape == Shape.Cattail ? .018f : .052f;
                    Blade(x, z, yaw, shape == Shape.Grass ? .055f : .035f, high, lean, .8f + .035f * i);
                    if (shape == Shape.Cattail)
                        Facet(new Vector3(x + Mathf.Cos(yaw) * lean, high * .85f, z + Mathf.Sin(yaw) * lean),
                            .023f, .02f, .065f);
                }
                break;
            case Shape.Fern:
                for (int i = 0; i < 5; i++) Blade(0f, 0f, i * Mathf.Tau / 5f, .115f, .17f + i * .02f, .23f, .83f + i * .03f);
                break;
            case Shape.Scrub:
                Facet(new Vector3(-.066f, 0f, .02f), .17f, .14f, .24f);
                Facet(new Vector3(.11f, .035f, -.07f), .14f, .14f, .21f);
                break;
            case Shape.Stone:
                Facet(Vector3.Zero, .17f, .135f, .15f);
                Facet(new Vector3(.23f, 0f, -.06f), .075f, .072f, .065f);
                break;
            case Shape.Twig:
                Facet(Vector3.Zero, .23f, .035f, .052f);
                Facet(new Vector3(.105f, 0f, .046f), .035f, .11f, .055f);
                break;
        }
        return surface.Commit();
    }
}
