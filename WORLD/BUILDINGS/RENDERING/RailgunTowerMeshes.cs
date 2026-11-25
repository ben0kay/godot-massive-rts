// Builds the Railgun's compact armored base, rotating wedge carriage and single recoiling rail assembly.
using Godot;
using static MassiveRts.TowerArmorGeometry;

namespace MassiveRts;

internal static class RailgunTowerMeshes
{
    // =========================================================
    // A smaller 2x2 footprint than the Broadside, with a diamond-like inset deck,
    // clipped corners and slender faction panels. Base coordinates are normalized.
    public static Mesh CreateBase()
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        Plate(surface, new Vector3(0f, -0.41f, 0f), new Vector3(1f, 0.18f, 1f), 0.20f, 0.96f, Steel(0.46f));
        Plate(surface, new Vector3(0f, -0.08f, 0f), new Vector3(0.95f, 0.58f, 0.95f), 0.24f, 0.81f, Steel(0.88f));
        Plate(surface, new Vector3(0f, 0.31f, 0f), new Vector3(0.84f, 0.30f, 0.84f), 0.40f, 0.91f, Steel(1f));

        for (int side = -1; side <= 1; side += 2)
        {
            for (int end = -1; end <= 1; end += 2)
            {
                Plate(surface, new Vector3(side * 0.34f, -0.03f, end * 0.34f),
                    new Vector3(0.24f, 0.70f, 0.24f), 0.38f, 0.78f, Steel(0.96f));
                Plate(surface, new Vector3(side * 0.34f, 0.27f, end * 0.34f),
                    new Vector3(0.19f, 0.30f, 0.19f), 0.35f, 0.81f, Paint());
                Block(surface, new Vector3(side * 0.34f, 0.432f, end * 0.34f),
                    new Vector3(0.073f, 0.026f, 0.052f), Trim(0.84f));
            }
            // Recess-like dark side panel and illuminated transverse strip.
            Block(surface, new Vector3(side * 0.463f, -0.06f, 0f),
                new Vector3(0.048f, 0.28f, 0.44f), Steel(0.30f));
            Block(surface, new Vector3(0f, -0.06f, side * 0.463f),
                new Vector3(0.44f, 0.28f, 0.048f), Steel(0.30f));
            Block(surface, new Vector3(side * 0.493f, -0.025f, 0f),
                new Vector3(0.013f, 0.088f, 0.32f), Trim());
            Block(surface, new Vector3(0f, -0.025f, side * 0.493f),
                new Vector3(0.32f, 0.088f, 0.013f), Trim());
            // Long faction-colored deck rails contrast with the dark central mount.
            Plate(surface, new Vector3(side * 0.31f, 0.42f, 0f),
                new Vector3(0.10f, 0.12f, 0.41f), 0.25f, 0.83f, Paint());
        }
        return surface.Commit();
    }

    // =========================================================
    // Compact pointed turret with swept armor cheeks and a central saddle.
    // All offsets are in world units relative to the existing yaw pivot.
    public static Mesh CreateHead()
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        Plate(surface, new Vector3(0f, -0.25f, 0.05f), new Vector3(2.65f, 0.20f, 2.52f), 0.62f, 0.96f, Steel(0.52f));
        Plate(surface, new Vector3(0f, -0.08f, 0.08f), new Vector3(2.42f, 0.22f, 2.29f), 0.55f, 0.83f, Steel(1f));
        Plate(surface, new Vector3(0f, 0.21f, 0.29f), new Vector3(1.72f, 0.62f, 1.90f), 0.50f, 0.74f, Steel(0.86f));
        Plate(surface, new Vector3(0f, 0.55f, 0.57f), new Vector3(1.08f, 0.30f, 1.06f), 0.36f, 0.76f, Steel(1f));
        // Rear-facing illuminated power cell, visible above the single gun.
        Block(surface, new Vector3(0f, 0.72f, 0.68f), new Vector3(0.57f, 0.06f, 0.48f), Paint());
        Block(surface, new Vector3(0f, 0.76f, 0.68f), new Vector3(0.35f, 0.035f, 0.28f), Trim());

        for (int side = -1; side <= 1; side += 2)
        {
            // Separate side armor pods keep the central gun corridor legible.
            Plate(surface, new Vector3(side * 0.89f, 0.18f, 0.12f),
                new Vector3(0.72f, 0.60f, 1.95f), 0.48f, 0.78f, Steel(0.88f));
            Plate(surface, new Vector3(side * 0.91f, 0.49f, 0.12f),
                new Vector3(0.64f, 0.32f, 1.78f), 0.43f, 0.76f, Paint());
            for (int vent = 0; vent < 3; vent++)
                Block(surface, new Vector3(side * 0.91f, 0.66f, 0.18f + vent * 0.18f),
                    new Vector3(0.32f, 0.028f, 0.09f), Steel(0.22f));
            Block(surface, new Vector3(side * 1.26f, 0.18f, 0.15f),
                new Vector3(0.07f, 0.17f, 0.84f), Steel(0.30f));
            Block(surface, new Vector3(side * 1.31f, 0.18f, 0.15f),
                new Vector3(0.033f, 0.07f, 0.66f), Trim());
            Block(surface, new Vector3(side * 0.52f, -0.09f, 1.25f),
                new Vector3(0.42f, 0.10f, 0.06f), Trim());
        }
        return surface.Commit();
    }

    // =========================================================
    // One weapon, two visible conductor rails. All pieces share the existing
    // barrel pitch and recoil transform, and the beam exits the clear central gap.
    // The authored barrel is centered at zero: the muzzle is z = -length / 2.
    public static Mesh CreateBarrel(float length)
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        float half = length * 0.5f;
        // Long central receiver, ending before the split muzzle.
        Block(surface, new Vector3(0f, 0f, 0.29f), new Vector3(0.47f, 0.36f, length - 0.78f), Steel(0.88f));
        Plate(surface, new Vector3(0f, 0f, 1.23f), new Vector3(0.86f, 0.65f, 1.18f), 0.28f, 0.79f, Steel(0.98f));
        Block(surface, new Vector3(0f, 0.342f, 1.22f), new Vector3(0.49f, 0.055f, 0.49f), Paint());
        Plate(surface, new Vector3(0f, 0f, 0.33f), new Vector3(0.85f, 0.61f, 0.43f), 0.24f, 0.85f, Steel(0.61f));

        for (int side = -1; side <= 1; side += 2)
        {
            // A single broad linear accelerator silhouette, not two separate barrels.
            Block(surface, new Vector3(side * 0.255f, 0f, -0.59f),
                new Vector3(0.29f, 0.40f, 2.58f), Steel(1f));
            Block(surface, new Vector3(side * 0.255f, 0.216f, -0.57f),
                new Vector3(0.21f, 0.046f, 1.94f), Steel(0.27f));
            Block(surface, new Vector3(side * 0.255f, 0.247f, -0.57f),
                new Vector3(0.105f, 0.030f, 1.78f), Trim());
            // Faceted painted end caps stop short of the exact beam endpoint.
            Plate(surface, new Vector3(side * 0.265f, 0f, -half + 0.25f),
                new Vector3(0.41f, 0.53f, 0.46f), 0.24f, 0.85f, Paint());
            Block(surface, new Vector3(side * 0.265f, 0f, -half + 0.01f),
                new Vector3(0.30f, 0.33f, 0.02f), Steel(0.23f));
            Block(surface, new Vector3(side * 0.265f, 0.285f, -half + 0.25f),
                new Vector3(0.17f, 0.032f, 0.22f), Trim());
        }
        return surface.Commit();
    }
}
