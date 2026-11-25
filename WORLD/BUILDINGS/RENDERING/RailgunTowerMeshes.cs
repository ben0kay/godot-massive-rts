// Railgun directed-energy turret: compact pedestal, capacitor carriage and single coil accelerator.
using Godot;
using static MassiveRts.TowerArmorGeometry;

namespace MassiveRts;

internal static class RailgunTowerMeshes
{
    // =========================================================
    // Keep the original normalized footprint and grounding; taper the
    // visible upper pedestal instead of using the Broadside's corner armor.
    public static Mesh CreateBase()
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        Plate(surface, new Vector3(0f, -0.41f, 0f), new Vector3(1f, 0.18f, 1f), 0.80f, 0.92f, Steel(0.48f));
        Plate(surface, new Vector3(0f, -0.17f, 0f), new Vector3(0.83f, 0.30f, 0.83f), 0.85f, 0.78f, Steel(0.92f));
        Plate(surface, new Vector3(0f, 0.04f, 0f), new Vector3(0.64f, 0.16f, 0.64f), 0.85f, 0.98f, Paint(0.82f));
        Plate(surface, new Vector3(0f, 0.27f, 0f), new Vector3(0.60f, 0.30f, 0.60f), 0.85f, 0.92f, Steel(1f));
        Plate(surface, new Vector3(0f, 0.44f, 0f), new Vector3(0.66f, 0.12f, 0.66f), 0.85f, 0.95f, Steel(0.63f));

        for (int side = -1; side <= 1; side += 2)
        {
            // Small power conduits climb the pedestal, leaving its octagonal
            // shoulders exposed and avoiding a broad square turret base.
            Block(surface, new Vector3(side * 0.302f, 0.23f, 0f),
                new Vector3(0.055f, 0.25f, 0.12f), Steel(0.33f));
            Block(surface, new Vector3(side * 0.334f, 0.23f, 0f),
                new Vector3(0.016f, 0.18f, 0.065f), Trim(0.90f));
            Block(surface, new Vector3(0f, 0.23f, side * 0.302f),
                new Vector3(0.12f, 0.25f, 0.055f), Steel(0.33f));
            Block(surface, new Vector3(0f, 0.23f, side * 0.334f),
                new Vector3(0.065f, 0.18f, 0.016f), Trim(0.90f));
        }
        return surface.Commit();
    }

    // =========================================================
    // Compact trunnion cradle with an isolated aft capacitor bank.
    // Head coordinates are relative to the existing yaw pivot in world units.
    public static Mesh CreateHead()
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        Plate(surface, new Vector3(0f, -0.24f, 0f), new Vector3(1.95f, 0.22f, 1.95f), 0.85f, 0.94f, Steel(0.65f));
        Plate(surface, new Vector3(0f, -0.08f, 0.18f), new Vector3(1.55f, 0.20f, 1.73f), 0.72f, 0.84f, Steel(1f));

        // Short slender side supports, not full-length artillery armor pods.
        // Leave space for the existing pitching/recoiling barrel between them.
        for (int side = -1; side <= 1; side += 2)
        {
            Plate(surface, new Vector3(side * 0.56f, 0.11f, 0.02f),
                new Vector3(0.30f, 0.58f, 0.68f), 0.42f, 0.76f, Steel(0.92f));
            Plate(surface, new Vector3(side * 0.56f, 0.39f, 0.02f),
                new Vector3(0.30f, 0.14f, 0.44f), 0.48f, 0.76f, Paint());
            Block(surface, new Vector3(side * 0.718f, 0.12f, 0.02f),
                new Vector3(0.035f, 0.23f, 0.22f), Trim(0.86f));
        }

        // Rear power assembly is behind the barrel's 0.64-unit recoil travel.
        Plate(surface, new Vector3(0f, 0.12f, 0.96f),
            new Vector3(0.90f, 0.55f, 0.46f), 0.34f, 0.88f, Steel(0.85f));
        Plate(surface, new Vector3(0f, 0.41f, 0.96f),
            new Vector3(0.77f, 0.18f, 0.43f), 0.40f, 0.80f, Paint());
        for (int side = -1; side <= 1; side += 2)
            Block(surface, new Vector3(side * 0.23f, 0.512f, 0.96f),
                new Vector3(0.12f, 0.035f, 0.26f), Trim());
        Block(surface, new Vector3(0f, 0.13f, 1.205f),
            new Vector3(0.48f, 0.15f, 0.04f), Trim(0.85f));
        return surface.Commit();
    }

    // =========================================================
    // One long, faceted circular accelerator with four luminous induction
    // coils and a recessed focusing aperture. Every section is part of the
    // existing barrel mesh and therefore pitches and recoils with the beam.
    // Local z = -length / 2 is exactly the existing beam origin.
    public static Mesh CreateBarrel(float length)
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        float half = length * 0.5f;
        float Z(float progress) => half - length * progress;
        float lastZ = Z(0f), lastRadius = 0f;

        // A continuous lathed profile avoids overlapping rings and hidden caps.
        void Profile(float progress, float radius, Color mask)
        {
            float z = Z(progress);
            AxialBand(surface, lastZ, lastRadius, z, radius, mask);
            lastZ = z;
            lastRadius = radius;
        }

        Profile(0f, 0.28f, Steel(0.50f));
        Profile(0.035f, 0.34f, Steel(0.95f));
        Profile(0.125f, 0.34f, Paint(0.88f));
        Profile(0.16f, 0.22f, Steel(0.83f));

        // Four widely spaced rings read as an electromagnetic accelerator,
        // not a pair of shell-firing tubes. Dark flanges frame each light band.
        for (int i = 0; i < 4; i++)
        {
            float t = 0.20f + i * 0.155f;
            Profile(t, 0.22f, Steel(0.94f));
            Profile(t + 0.015f, 0.36f, Steel(0.88f));
            Profile(t + 0.032f, 0.36f, Steel(0.67f));
            Profile(t + 0.038f, 0.31f, Steel(0.52f));
            Profile(t + 0.069f, 0.31f, Trim(0.93f));
            Profile(t + 0.075f, 0.36f, Steel(0.52f));
            Profile(t + 0.092f, 0.36f, Steel(0.67f));
            Profile(t + 0.107f, 0.22f, Steel(0.88f));
        }
        Profile(0.83f, 0.22f, Steel(0.94f));

        // Tapered focusing head and a genuine open circular muzzle recess.
        // Return inward at the exact endpoint, then back into the aperture.
        Profile(0.87f, 0.32f, Steel(0.86f));
        Profile(0.94f, 0.32f, Paint(0.88f));
        Profile(0.985f, 0.25f, Steel(1f));
        Profile(1f, 0.23f, Steel(0.90f));
        Profile(1f, 0.15f, Trim(0.85f));
        Profile(0.95f, 0.15f, Steel(0.35f));
        Profile(0.95f, 0f, Trim(0.75f));
        return surface.Commit();
    }

    // =========================================================
    // Twelve-sided axial surface in XY, along local -Z. The normal derives
    // from the surface profile and is valid for outer walls, end faces and
    // reversed inner walls. Emit Godot's clockwise front-face winding.
    private static void AxialBand(SurfaceTool surface, float zA, float rA, float zB, float rB, Color mask)
    {
        const int sides = 12;
        Vector3 Point(float radius, float z, int i)
        {
            float angle = Mathf.Tau * i / sides;
            return new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), z);
        }
        for (int i = 0; i < sides; i++)
        {
            Vector3 a = Point(rA, zA, i), b = Point(rA, zA, i + 1);
            Vector3 c = Point(rB, zB, i + 1), d = Point(rB, zB, i);
            Vector3 normal = rA == 0f
                ? (d - a).Cross(c - a).Normalized()
                : (d - a).Cross(b - a).Normalized();
            float shade = Mathf.Clamp(0.82f + 0.15f * normal.Y - 0.08f * normal.X, 0.58f, 1f);
            void Triangle(Vector3 first, Vector3 second, Vector3 third)
            {
                surface.SetNormal(normal);
                surface.SetColor(mask * shade);
                surface.AddVertex(first);
                surface.AddVertex(third);
                surface.AddVertex(second);
            }
            if (rB > 0f) Triangle(a, d, c);
            if (rA > 0f) Triangle(a, c, b);
        }
    }
}
