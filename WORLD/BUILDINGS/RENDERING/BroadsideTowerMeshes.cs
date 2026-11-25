// Builds the Broadside's three reusable, two-tone hard-surface meshes.
// All parts are baked at initialization; no extra per-tower nodes or animation.
using Godot;
using static MassiveRts.TowerArmorGeometry;

namespace MassiveRts;

internal static class BroadsideTowerMeshes
{
    // =========================================================
    // Normalized 3x3 footprint: clipped plinth, sloped armor, corner plates
    // and dark side recesses. The existing renderer scales this to the footprint.
    public static Mesh CreateBase()
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        Plate(surface, new Vector3(0f, -0.41f, 0f), new Vector3(1f, 0.18f, 1f), 0.11f, 0.96f, Steel(0.48f));
        Plate(surface, new Vector3(0f, -0.09f, 0f), new Vector3(0.96f, 0.58f, 0.96f), 0.18f, 0.84f, Steel(0.84f));
        Plate(surface, new Vector3(0f, 0.32f, 0f), new Vector3(0.84f, 0.28f, 0.84f), 0.23f, 0.91f, Steel(1f));
        for (int x = -1; x <= 1; x += 2)
        {
            for (int z = -1; z <= 1; z += 2)
            {
                // Dark structural corner buttress with a separate colored armor cap.
                Plate(surface, new Vector3(x * 0.35f, -0.04f, z * 0.35f),
                    new Vector3(0.27f, 0.74f, 0.27f), 0.34f, 0.78f, Steel(1f));
                Plate(surface, new Vector3(x * 0.35f, 0.21f, z * 0.35f),
                    new Vector3(0.23f, 0.48f, 0.23f), 0.30f, 0.83f, Paint());
                // High-contrast status strip on the top and two exposed faces.
                Block(surface, new Vector3(x * 0.35f, 0.463f, z * 0.35f),
                    new Vector3(0.09f, 0.025f, 0.055f), Steel(0.30f));
                Block(surface, new Vector3(x * 0.35f, 0.480f, z * 0.35f),
                    new Vector3(0.062f, 0.014f, 0.028f), Trim());
                Block(surface, new Vector3(x * 0.49f, 0.14f, z * 0.35f),
                    new Vector3(0.018f, 0.20f, 0.025f), Trim());
                Block(surface, new Vector3(x * 0.35f, 0.14f, z * 0.49f),
                    new Vector3(0.025f, 0.20f, 0.018f), Trim());
            }
            // Four armored side panels with dark insets and a faction light strip.
            Block(surface, new Vector3(x * 0.457f, -0.12f, 0f),
                new Vector3(0.06f, 0.30f, 0.45f), Steel(0.46f));
            Block(surface, new Vector3(0f, -0.12f, x * 0.457f),
                new Vector3(0.45f, 0.30f, 0.06f), Steel(0.46f));
            Block(surface, new Vector3(x * 0.49f, -0.08f, 0f),
                new Vector3(0.018f, 0.095f, 0.32f), Trim());
            Block(surface, new Vector3(0f, -0.08f, x * 0.49f),
                new Vector3(0.32f, 0.095f, 0.018f), Trim());
            // Two thick armor rails frame each side recess without crossing the footprint.
            for (int end = -1; end <= 1; end += 2)
            {
                Plate(surface, new Vector3(x * 0.453f, -0.06f, end * 0.24f),
                    new Vector3(0.09f, 0.50f, 0.09f), 0.25f, 0.76f, Steel(1f));
                Plate(surface, new Vector3(end * 0.24f, -0.06f, x * 0.453f),
                    new Vector3(0.09f, 0.50f, 0.09f), 0.25f, 0.76f, Steel(1f));
            }
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
        Plate(surface, new Vector3(0f, 0.32f, 0.25f), new Vector3(2.70f, 0.62f, 2.40f), 0.24f, 0.87f, Steel(0.92f));
        Plate(surface, new Vector3(0f, 0.73f, 0.53f), new Vector3(1.20f, 0.48f, 1.87f), 0.26f, 0.73f, Steel(1f));
        // Raised command core: dark rim, faction border, emissive center.
        Plate(surface, new Vector3(0f, 1.02f, 0.57f), new Vector3(0.86f, 0.14f, 0.80f), 0.24f, 0.82f, Steel(0.55f));
        Block(surface, new Vector3(0f, 1.105f, 0.57f), new Vector3(0.60f, 0.055f, 0.53f), Paint());
        Block(surface, new Vector3(0f, 1.143f, 0.57f), new Vector3(0.40f, 0.04f, 0.34f), Trim());
        Block(surface, new Vector3(0f, 0.38f, 1.47f), new Vector3(0.95f, 0.20f, 0.10f), Steel(0.32f));
        Block(surface, new Vector3(0f, 0.38f, 1.535f), new Vector3(0.73f, 0.09f, 0.035f), Trim());
        for (int side = -1; side <= 1; side += 2)
        {
            // Side armor has a substantial faction-colored shell over a dark pod.
            Plate(surface, new Vector3(side * 1.37f, 0.33f, 0.32f),
                new Vector3(0.91f, 0.68f, 2.45f), 0.19f, 0.91f, Steel(0.66f));
            Plate(surface, new Vector3(side * 1.37f, 0.71f, 0.28f),
                new Vector3(1.02f, 0.55f, 2.34f), 0.26f, 0.79f, Paint());
            // Larger vent slots and twin side indicators remain readable at RTS zoom.
            for (int vent = 0; vent < 4; vent++)
                Block(surface, new Vector3(side * 1.37f, 0.995f, -0.20f + vent * 0.24f),
                    new Vector3(0.53f, 0.036f, 0.12f), Steel(0.23f));
            Block(surface, new Vector3(side * 1.86f, 0.49f, 0.48f),
                new Vector3(0.12f, 0.39f, 0.96f), Steel(0.48f));
            for (int lamp = 0; lamp < 2; lamp++)
                Block(surface, new Vector3(side * 1.93f, 0.42f + lamp * 0.15f, 0.48f),
                    new Vector3(0.035f, 0.07f, 0.73f), Trim());
            // Heavy faceted launcher yoke below each independently recoiling tube.
            Plate(surface, new Vector3(side * 0.70f, 0.83f, -0.83f),
                new Vector3(1.17f, 1.02f, 1.92f), 0.32f, 0.76f, Steel(0.93f));
            Plate(surface, new Vector3(side * 0.70f, 1.38f, -0.69f),
                new Vector3(0.69f, 0.18f, 0.66f), 0.27f, 0.82f, Steel(1f));
            Block(surface, new Vector3(side * 0.70f, 1.49f, -0.61f),
                new Vector3(0.36f, 0.06f, 0.32f), Trim());
            // Rear rim lights rotate with the head, not the stationary base.
            Block(surface, new Vector3(side * 0.78f, -0.10f, 1.71f),
                new Vector3(0.65f, 0.13f, 0.07f), Trim());
            Block(surface, new Vector3(side * 1.72f, -0.10f, 0.25f),
                new Vector3(0.07f, 0.13f, 1.16f), Trim());
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
        Block(surface, new Vector3(0f, 0f, -3.10f), new Vector3(0.53f, 0.48f, 3.12f), Steel(1f));
        Plate(surface, new Vector3(0f, 0f, -1.88f), new Vector3(0.94f, 0.83f, 0.94f), 0.28f, 0.77f, Steel(0.92f));
        Block(surface, new Vector3(0f, 0.438f, -1.86f), new Vector3(0.39f, 0.07f, 0.35f), Paint());
        // A dark channel with a wide bright power rail, moving with the barrel.
        Block(surface, new Vector3(0f, 0.257f, -3.15f), new Vector3(0.27f, 0.055f, 1.69f), Steel(0.30f));
        Block(surface, new Vector3(0f, 0.294f, -3.15f), new Vector3(0.16f, 0.035f, 1.47f), Trim());
        Block(surface, new Vector3(0f, 0f, -4.03f), new Vector3(0.65f, 0.57f, 0.23f), Steel(0.55f));
        Plate(surface, new Vector3(0f, 0f, -4.37f), new Vector3(0.84f, 0.75f, 0.61f), 0.24f, 0.87f, Paint());
        for (int side = -1; side <= 1; side += 2)
        {
            Block(surface, new Vector3(side * 0.417f, 0f, -4.35f),
                new Vector3(0.018f, 0.25f, 0.33f), Steel(0.29f));
            for (int lamp = 0; lamp < 2; lamp++)
                Block(surface, new Vector3(side * 0.43f, -0.06f + lamp * 0.12f, -4.35f),
                    new Vector3(0.018f, 0.052f, 0.23f), Trim());
        }
        // Dark inset face suggests an open muzzle at battlefield scale.
        Block(surface, new Vector3(0f, 0f, -4.687f), new Vector3(0.43f, 0.36f, 0.026f), Steel(0.18f));
        return surface.Commit();
    }

}
