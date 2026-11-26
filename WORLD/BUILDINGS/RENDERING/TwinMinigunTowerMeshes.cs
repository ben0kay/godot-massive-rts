// Compact anti-infantry turret. Two short rotary-looking barrels share one pitched mesh.
using Godot;
using static MassiveRts.TowerArmorGeometry;

namespace MassiveRts;

internal static class TwinMinigunTowerMeshes
{
    internal const string Id = "twin_minigun_tower";
    internal const float BarrelOffset = 0.48f;
    internal const float BarrelLength = 1.65f;

    internal static Mesh CreateBase()
    {
        using var s = new SurfaceTool();
        s.Begin(Mesh.PrimitiveType.Triangles);
        // Full square footprint with very small clipped corners.
        Plate(s, new Vector3(0f, -0.40f, 0f), new Vector3(1f, 0.20f, 1f), 0.05f, 0.97f, Steel(0.55f));
        Plate(s, new Vector3(0f, -0.08f, 0f), new Vector3(0.90f, 0.44f, 0.90f), 0.24f, 0.85f, Steel(0.95f));
        Plate(s, new Vector3(0f, 0.28f, 0f), new Vector3(0.70f, 0.28f, 0.70f), 0.40f, 0.88f, Steel(0.87f));
        Plate(s, new Vector3(0f, 0.45f, 0f), new Vector3(0.62f, 0.10f, 0.62f), 0.58f, 0.96f, Steel(0.65f));
        for (int side = -1; side <= 1; side += 2)
        {
            for (int end = -1; end <= 1; end += 2)
            {
                Plate(s, new Vector3(side * 0.33f, 0.02f, end * 0.33f),
                    new Vector3(0.22f, 0.45f, 0.22f), 0.28f, 0.77f, Paint());
                Block(s, new Vector3(side * 0.33f, 0.255f, end * 0.33f),
                    new Vector3(0.085f, 0.025f, 0.085f), Trim(0.78f));
            }
        }
        return s.Commit();
    }

    internal static Mesh CreateHead()
    {
        using var s = new SurfaceTool();
        s.Begin(Mesh.PrimitiveType.Triangles);
        Plate(s, new Vector3(0f, -0.24f, 0.12f), new Vector3(2.24f, 0.25f, 2.05f), 0.55f, 0.92f, Steel(0.63f));
        Plate(s, new Vector3(0f, 0.14f, 0.42f), new Vector3(1.82f, 0.60f, 1.37f), 0.43f, 0.75f, Steel(0.96f));
        Plate(s, new Vector3(0f, 0.48f, 0.50f), new Vector3(1.12f, 0.16f, 0.88f), 0.25f, 0.81f, Paint());
        Block(s, new Vector3(0f, 0.57f, 0.19f), new Vector3(0.34f, 0.04f, 0.11f), Trim());
        for (int side = -1; side <= 1; side += 2)
        {
            // Side ammunition magazines and short cooling vents.
            Plate(s, new Vector3(side * 0.87f, 0.07f, 0.48f), new Vector3(0.48f, 0.47f, 1.15f), 0.30f, 0.82f, Steel(0.73f));
            Plate(s, new Vector3(side * 0.87f, 0.34f, 0.48f), new Vector3(0.40f, 0.16f, 0.90f), 0.25f, 0.84f, Paint());
            for (int i = 0; i < 3; i++)
                Block(s, new Vector3(side * 0.87f, 0.43f, 0.23f + i * 0.20f),
                    new Vector3(0.24f, 0.026f, 0.08f), Steel(0.25f));
        }
        return s.Commit();
    }

    internal static Mesh CreateBarrels()
    {
        using var s = new SurfaceTool();
        s.Begin(Mesh.PrimitiveType.Triangles);
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * BarrelOffset;
            // One short rotary cluster per side: dark core, six slender
            // longitudinal tubes and a front retaining collar, not artillery tubes.
            Block(s, new Vector3(x, 0f, -0.28f), new Vector3(0.64f, 0.57f, 0.56f), Steel(0.93f));
            Block(s, new Vector3(x, 0.30f, -0.28f), new Vector3(0.43f, 0.06f, 0.40f), Paint());
            Block(s, new Vector3(x, 0f, -0.96f), new Vector3(0.22f, 0.22f, 1.24f), Steel(0.32f));
            for (int i = 0; i < 6; i++)
            {
                float angle = Mathf.Tau * i / 6;
                Block(s, new Vector3(x + 0.18f * Mathf.Cos(angle), 0.18f * Mathf.Sin(angle), -1.04f),
                    new Vector3(0.10f, 0.10f, 1.22f), Steel(0.96f));
            }
            // The central dark muzzle face ends exactly at the shot origin.
            Block(s, new Vector3(x, 0f, -1.43f), new Vector3(0.48f, 0.48f, 0.22f), Steel(0.80f));
            Block(s, new Vector3(x, 0f, -BarrelLength + 0.025f), new Vector3(0.20f, 0.20f, 0.05f), Steel(0.22f));
        }
        return s.Commit();
    }
}
