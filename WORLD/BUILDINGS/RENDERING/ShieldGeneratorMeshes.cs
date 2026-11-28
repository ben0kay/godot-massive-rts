// Static shield generator: a shared two-tone armored reactor with a recognizable energy crown.
using Godot;
using static MassiveRts.TowerArmorGeometry;

namespace MassiveRts;

internal static class ShieldGeneratorMeshes
{
    // =========================================================
    // XZ normalized to footprint, Y starts at ground and scales by base height.
    // No rotating parts: the complete generator is baked into one surface.
    internal static Mesh Create()
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetUV(Vector2.Zero); // Ordinary tower armor has no reactor tag.

        // Heavy beveled foundation and chamfered upper deck.
        Plate(surface, new Vector3(0f, 0.10f, 0f), new Vector3(1f, 0.20f, 1f), 0.25f, 0.95f, Steel(0.50f));
        Plate(surface, new Vector3(0f, 0.32f, 0f), new Vector3(0.92f, 0.32f, 0.92f), 0.36f, 0.82f, Steel(0.91f));
        Plate(surface, new Vector3(0f, 0.51f, 0f), new Vector3(0.75f, 0.20f, 0.75f), 0.58f, 0.91f, Steel(1f));

        // Four corner armor caps and recessed status strips.
        for (int side = -1; side <= 1; side += 2)
        {
            for (int end = -1; end <= 1; end += 2)
            {
                Plate(surface, new Vector3(side * 0.33f, 0.28f, end * 0.33f),
                    new Vector3(0.24f, 0.37f, 0.24f), 0.31f, 0.79f, Paint());
                Block(surface, new Vector3(side * 0.33f, 0.475f, end * 0.33f),
                    new Vector3(0.077f, 0.025f, 0.077f), Trim(0.88f));
            }
            Block(surface, new Vector3(side * 0.451f, 0.29f, 0f),
                new Vector3(0.065f, 0.16f, 0.40f), Steel(0.27f));
            Block(surface, new Vector3(0f, 0.29f, side * 0.451f),
                new Vector3(0.40f, 0.16f, 0.065f), Steel(0.27f));
            Block(surface, new Vector3(side * 0.49f, 0.31f, 0f),
                new Vector3(0.014f, 0.065f, 0.29f), Trim());
            Block(surface, new Vector3(0f, 0.31f, side * 0.49f),
                new Vector3(0.29f, 0.065f, 0.014f), Trim());
        }

        // Nested reactor collar, illuminated inner core and dark cap.
        Plate(surface, new Vector3(0f, 0.65f, 0f), new Vector3(0.50f, 0.20f, 0.50f), 0.68f, 0.86f, Steel(0.60f));
        Plate(surface, new Vector3(0f, 0.77f, 0f), new Vector3(0.42f, 0.12f, 0.42f), 0.66f, 0.90f, Paint());
        Plate(surface, new Vector3(0f, 1.22f, 0f), new Vector3(0.37f, 0.15f, 0.37f), 0.65f, 0.77f, Steel(0.91f));
        Plate(surface, new Vector3(0f, 1.32f, 0f), new Vector3(0.22f, 0.09f, 0.22f), 0.58f, 0.78f, Trim());

        // Four radially symmetric emitter pylons surround the visible core.
        // Dark structural bodies, faction armor crowns and inward light strips.
        for (int side = -1; side <= 1; side += 2)
        {
            Plate(surface, new Vector3(side * 0.26f, 0.84f, 0f),
                new Vector3(0.15f, 0.52f, 0.22f), 0.32f, 0.86f, Steel(0.94f));
            Plate(surface, new Vector3(0f, 0.84f, side * 0.26f),
                new Vector3(0.22f, 0.52f, 0.15f), 0.32f, 0.86f, Steel(0.94f));
            Plate(surface, new Vector3(side * 0.25f, 1.10f, 0f),
                new Vector3(0.17f, 0.20f, 0.20f), 0.35f, 0.70f, Paint());
            Plate(surface, new Vector3(0f, 1.10f, side * 0.25f),
                new Vector3(0.20f, 0.20f, 0.17f), 0.35f, 0.70f, Paint());
            Block(surface, new Vector3(side * 0.176f, 0.94f, 0f),
                new Vector3(0.025f, 0.25f, 0.075f), Trim());
            Block(surface, new Vector3(0f, 0.94f, side * 0.176f),
                new Vector3(0.075f, 0.25f, 0.025f), Trim());
            Block(surface, new Vector3(side * 0.25f, 1.216f, 0f),
                new Vector3(0.066f, 0.035f, 0.095f), Trim());
            Block(surface, new Vector3(0f, 1.216f, side * 0.25f),
                new Vector3(0.095f, 0.035f, 0.066f), Trim());
        }
        ShieldReactorGeometry.Append(surface, new Vector3(0f, 1.00f, 0f),
            new Vector3(0.15f, 0.42f, 0.15f), Trim(1f));
        return surface.Commit();
    }
}
