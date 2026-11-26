// Cached, single-surface faceted aircraft; +Z is the existing flight-forward axis.
using Godot;

namespace MassiveRts;

internal static class AirframeMeshes
{
    private static Mesh interceptor, bomber;
    private static ShaderMaterial armor;
    internal static ShaderMaterial Armor => armor ??= new ShaderMaterial
    {
        Shader = GD.Load<Shader>("res://GAMEPLAY/COMBAT/AIR/AirframeArmor.gdshader")
    };

    internal static Mesh Get(bool heavy) =>
        heavy ? (bomber ??= Build(true)) : (interceptor ??= Build(false));

    private static Mesh Build(bool heavy)
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        const int Steel = 1, Paint = 2, Trim = 3;

        // Convex XZ outlines; shallow bevel ring, closed underside and flat caps.
        // Baked face shades keep the current inexpensive unshaded look.
        void Plate(Vector2[] outline, float bottomY, float topY, int kind, float shade)
        {
            Vector2 center = Vector2.Zero;
            foreach (var p in outline) center += p;
            center /= outline.Length;
            float shoulderY = Mathf.Lerp(bottomY, topY, 0.68f);
            Vector3 At(Vector2 p, float y) => new(p.X, y, p.Y);
            void Triangle(Vector3 a, Vector3 b, Vector3 c, float light)
            {
                Vector3 normal = (c - a).Cross(b - a).Normalized();
                foreach (var v in new[] { a, b, c })
                {
                    surface.SetNormal(normal);
                    surface.SetColor(Colors.White);
                    surface.SetUV(new Vector2(kind, shade * light));
                    surface.AddVertex(v);
                }
            }
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float light)
            {
                Triangle(a, b, c, light);
                Triangle(a, c, d, light);
            }
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 p = outline[i], q = outline[(i + 1) % outline.Length];
                Vector3 a = At(p, bottomY), b = At(q, bottomY);
                Vector3 c = At(q, shoulderY), d = At(p, shoulderY);
                Vector3 e = At(p.Lerp(center, 0.13f), topY), f = At(q.Lerp(center, 0.13f), topY);
                Triangle(At(center, bottomY), b, a, 0.63f);
                Quad(a, b, c, d, 0.67f);
                Quad(d, c, f, e, 0.84f);
                Triangle(At(center, topY), e, f, 1f);
            }
        }

        // Centered rectangular hull parts, bevels produced by the same builder.
        void Block(float x, float z, float width, float length, float bottom, float top, int kind, float shade = 1f)
        {
            float w = width * 0.5f, l = length * 0.5f;
            Plate(new[] { new Vector2(x-w,z-l), new Vector2(x+w,z-l),
                new Vector2(x+w,z+l), new Vector2(x-w,z+l) }, bottom, top, kind, shade);
        }

        void SidePlate(int side, Vector2[] points, float bottom, float top, int kind, float shade = 1f)
        {
            // Mirror both outline and winding so all caps/sides remain consistently oriented.
            if (side < 0)
            {
                System.Array.Reverse(points);
                for (int i = 0; i < points.Length; i++) points[i].X = -points[i].X;
            }
            Plate(points, bottom, top, kind, shade);
        }

        if (!heavy)
        {
            // Long narrow spine, pointed +Z nose, raised faceted cockpit.
            Plate(new[] { new Vector2(-0.27f,-0.75f), new Vector2(0.27f,-0.75f),
                new Vector2(0.36f,0.40f), new Vector2(0f,1.48f), new Vector2(-0.36f,0.40f) },
                -0.12f, 0.25f, Paint, 0.96f);
            Plate(new[] { new Vector2(-0.15f,-0.20f), new Vector2(0.15f,-0.20f),
                new Vector2(0.19f,0.43f), new Vector2(0f,0.94f), new Vector2(-0.19f,0.43f) },
                0.19f, 0.38f, Steel, 1f);
            Block(0f, 0.41f, 0.18f, 0.30f, 0.35f, 0.41f, Trim, 0.92f);

            for (int side = -1; side <= 1; side += 2)
            {
                // Tip exactly at the authored XZ trail anchor (1.72, -0.97).
                SidePlate(side, new[] { new Vector2(0.20f,-0.29f), new Vector2(0.50f,-0.31f),
                    new Vector2(1.72f,-0.97f), new Vector2(0.37f,0.63f) },
                    -0.07f, 0.033f, Paint, 0.93f);
                SidePlate(side, new[] { new Vector2(0.45f,-0.20f), new Vector2(0.77f,-0.36f),
                    new Vector2(0.48f,0.36f) }, 0.026f, 0.10f, Steel, 0.96f);
                SidePlate(side, new[] { new Vector2(1.28f,-0.72f), new Vector2(1.72f,-0.97f),
                    new Vector2(1.40f,-0.59f) }, 0.029f, 0.055f, Trim);
                // Gun front is at the existing tracer origin (+/-0.42, 0, +0.72).
                Block(side*0.42f, 0.44f, 0.20f, 0.53f, -0.09f, 0.14f, Steel);
                Block(side*0.42f, 0.665f, 0.10f, 0.11f, -0.045f, 0.045f, Trim);
                Block(side*0.27f, -0.45f, 0.23f, 0.52f, -0.15f, 0.20f, Paint, 0.87f);
                Block(side*0.27f, -0.705f, 0.15f, 0.055f, -0.085f, 0.11f, Trim);
            }
        }
        else
        {
            // Broad, thicker armored flying wing. Forward nose stays at +1.70.
            Plate(new[] { new Vector2(-0.67f,-0.75f), new Vector2(0.67f,-0.75f),
                new Vector2(0.79f,0.45f), new Vector2(0f,1.70f), new Vector2(-0.79f,0.45f) },
                -0.23f, 0.43f, Paint, 0.94f);
            Plate(new[] { new Vector2(-0.35f,-0.34f), new Vector2(0.35f,-0.34f),
                new Vector2(0.39f,0.62f), new Vector2(0f,1.14f), new Vector2(-0.39f,0.62f) },
                0.34f, 0.57f, Steel, 0.96f);
            Block(0f, 0.66f, 0.30f, 0.24f, 0.52f, 0.59f, Trim, 0.90f);

            for (int side = -1; side <= 1; side += 2)
            {
                // Broad wing keeps the original 3.1-unit half-span and rear notch.
                SidePlate(side, new[] { new Vector2(0.44f,-0.58f), new Vector2(1.03f,-1.16f),
                    new Vector2(3.10f,-0.62f), new Vector2(0.55f,0.87f) },
                    -0.12f, 0.057f, Paint, 0.94f);
                SidePlate(side, new[] { new Vector2(0.65f,-0.51f), new Vector2(1.08f,-0.94f),
                    new Vector2(2.22f,-0.54f), new Vector2(0.70f,0.56f) },
                    0.042f, 0.22f, Paint, 0.88f);
                SidePlate(side, new[] { new Vector2(2.52f,-0.70f), new Vector2(3.10f,-0.62f),
                    new Vector2(2.58f,-0.32f) }, 0.052f, 0.078f, Trim);
                // Large twin nacelles, dark exhaust collars and glowing aft panels.
                Block(side*0.94f, -0.23f, 0.48f, 1.28f, -0.17f, 0.37f, Steel, 0.98f);
                Block(side*0.94f, -0.16f, 0.42f, 1.08f, 0.20f, 0.43f, Paint);
                Block(side*0.94f, -0.82f, 0.38f, 0.12f, -0.10f, 0.27f, Steel);
                Block(side*0.94f, -0.875f, 0.28f, 0.045f, -0.045f, 0.20f, Trim);
                // Raised underside rails frame a dark bay without changing payload origin.
                Block(side*0.33f, -0.07f, 0.14f, 1.10f, -0.32f, -0.18f, Steel, 0.90f);
                Block(side*0.26f, -0.43f, 0.055f, 0.24f, -0.34f, -0.31f, Trim, 0.84f);
            }
            Block(0f, -0.08f, 0.48f, 0.92f, -0.27f, -0.21f, Steel, 0.60f);
        }
        return surface.Commit();
    }
}
