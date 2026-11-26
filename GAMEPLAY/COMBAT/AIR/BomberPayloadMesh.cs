// Cached visual-only payload; local -Z follows the shared projectile tangent.
using Godot;

namespace MassiveRts;

internal static class BomberPayloadMesh
{
    internal const string DefinitionPath = "res://GAMEPLAY/COMBAT/AIR/BomberPayload.tres";
    private static Mesh cached;
    internal static Mesh Get() => cached ??= Build();

    private static Mesh Build()
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        });
        Color steel = new("#56616b"), dark = new("#303840"), collar = new("#b49a55");
        const int sides = 10;
        void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            Vector3 normal = (c - a).Cross(b - a).Normalized();
            float light = 0.68f + 0.32f * Mathf.Abs(normal.Dot(new Vector3(0.38f,0.84f,-0.38f).Normalized()));
            Color shade = color * new Color(light,light,light);
            surface.SetNormal(normal); surface.SetColor(shade); surface.AddVertex(a);
            surface.SetNormal(normal); surface.SetColor(shade); surface.AddVertex(b);
            surface.SetNormal(normal); surface.SetColor(shade); surface.AddVertex(c);
        }
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            Triangle(a,b,c,color); Triangle(a,c,d,color);
        }
        Vector3 Ring(float radius, float z, int i)
        {
            float angle = i * Mathf.Tau / sides;
            return new Vector3(Mathf.Cos(angle)*radius, Mathf.Sin(angle)*radius,z);
        }
        void Band(float z1, float r1, float z2, float r2, Color color)
        {
            for (int i=0; i<sides; i++)
                Quad(Ring(r1,z1,i),Ring(r1,z1,i+1),Ring(r2,z2,i+1),Ring(r2,z2,i),color);
        }
        Band(-0.52f,0.015f,-0.43f,0.095f,dark);
        Band(-0.43f,0.095f,-0.25f,0.165f,steel);
        Band(-0.25f,0.165f,-0.18f,0.172f,collar);
        Band(-0.18f,0.172f,0.15f,0.172f,steel);
        Band(0.15f,0.172f,0.35f,0.10f,dark);
        Band(0.35f,0.10f,0.52f,0.075f,steel);
        for (int i=0; i<sides; i++)
        {
            Triangle(new Vector3(0,0,-0.52f),Ring(0.015f,-0.52f,i),Ring(0.015f,-0.52f,i+1),dark);
            Triangle(new Vector3(0,0,0.52f),Ring(0.075f,0.52f,i+1),Ring(0.075f,0.52f,i),dark);
        }
        // Thick swept fins, with a contrasting metal face on each side.
        for (int fin=0; fin<4; fin++)
        {
            float angle = fin*Mathf.Pi*0.5f;
            Vector3 radial = new(Mathf.Cos(angle),Mathf.Sin(angle),0);
            Vector3 tangent = new(-radial.Y,radial.X,0);
            Vector3 At(float r,float z,float offset) => radial*r+tangent*offset+new Vector3(0,0,z);
            Vector3 a=At(0.09f,0.19f,-0.017f), b=At(0.24f,0.36f,-0.017f);
            Vector3 c=At(0.24f,0.50f,-0.017f), d=At(0.07f,0.50f,-0.017f);
            Vector3 shift=tangent*0.034f;
            Quad(a,b,c,d,dark); Quad(a+shift,d+shift,c+shift,b+shift,steel);
            Quad(a,a+shift,b+shift,b,steel); Quad(b,b+shift,c+shift,c,steel);
            Quad(c,c+shift,d+shift,d,dark); Quad(d,d+shift,a+shift,a,dark);
        }
        return surface.Commit();
    }
}
