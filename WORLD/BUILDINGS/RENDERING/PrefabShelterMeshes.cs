// Three compact two-tone habitation modules, normalized XZ footprint and ground-based Y.
using Godot;
using static MassiveRts.TowerArmorGeometry;

namespace MassiveRts;

internal static class PrefabShelterMeshes
{
    internal const string Id = "prefab_shelter";
    internal const int VariantCount = 3;

    // Stable pseudo-random choice from the placement cell, not the batch slot
    // or process-randomized GetHashCode. Editor rebuilds and reloads agree.
    internal static int Variant(ObstacleRectangle area)
    {
        unchecked
        {
            uint h = (uint)area.X * 0x9E3779B9u ^ (uint)area.Z * 0x85EBCA6Bu;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return (int)(h % VariantCount);
        }
    }

    internal static Mesh Create(int variant)
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        void Box(float x, float y, float z, float w, float h, float d, Color mask) =>
            Block(surface, new Vector3(x, y, z), new Vector3(w, h, d), mask);
        void Slab(float x, float y, float z, float w, float h, float d, float corner, float top, Color mask) =>
            Plate(surface, new Vector3(x, y, z), new Vector3(w, h, d), corner, top, mask);

        // Almost square, full-footprint foundation makes blocked cells visible.
        Slab(0, 0.10f, 0, 1f, 0.20f, 1f, 0.06f, 0.98f, Steel(0.52f));
        // Tall, broadly vertical walls rather than a shallow bunker.
        float wallHeight = variant == 1 ? 1.78f : 1.62f;
        float wallTop = 0.20f + wallHeight;
        Slab(0, 0.20f + wallHeight * 0.5f, 0, 0.91f, wallHeight, 0.91f, 0.11f, 0.99f, Steel(0.91f));

        // Four small vertical frame posts; all remain within the foundation.
        for (int side = -1; side <= 1; side += 2)
        {
            for (int end = -1; end <= 1; end += 2)
                Box(side * 0.422f, 0.20f + wallHeight * 0.5f, end * 0.422f,
                    0.11f, wallHeight, 0.11f, Steel(0.62f));
            // Raised side panels and a dark window band with muted faction glass.
            Box(side * 0.462f, 1.30f, 0, 0.035f, 0.47f, 0.66f, Steel(0.27f));
            for (int pane = -1; pane <= 1; pane += 2)
                Box(side * 0.483f, 1.31f, pane * 0.16f,
                    0.012f, 0.30f, 0.25f, Paint(0.62f));
            Box(side * 0.463f, 0.69f, 0, 0.025f, 0.16f, 0.68f, Paint(0.92f));
        }

        // Front airlock, framed in steel, with a slim doorway and status light.
        float doorX = variant == 2 ? -0.19f : 0f;
        Box(doorX, 0.83f, -0.460f, 0.30f, 1.18f, 0.035f, Steel(0.26f));
        Box(doorX, 0.83f, -0.481f, 0.22f, 0.98f, 0.014f, Steel(0.57f));
        Box(doorX, 1.46f, -0.480f, 0.31f, 0.12f, 0.025f, Paint());
        Box(doorX + 0.086f, 0.90f, -0.490f, 0.024f, 0.14f, 0.012f, Trim(0.65f));
        // Rear ventilation and service panel.
        Box(0, 1.14f, 0.462f, 0.48f, 0.55f, 0.035f, Steel(0.31f));
        for (int i = 0; i < 3; i++)
            Box(0, 0.98f + 0.15f * i, 0.483f, 0.35f, 0.048f, 0.014f, Steel(0.75f));
        Box(0, 0.61f, 0.464f, 0.51f, 0.13f, 0.025f, Paint());

        switch (variant)
        {
            case 0:
                // Habitation pod: sloped armored roof with a central service ridge.
                Slab(0, wallTop + 0.18f, 0, 0.95f, 0.40f, 0.95f, 0.22f, 0.78f, Steel(1f));
                Box(0, wallTop + 0.415f, 0.02f, 0.23f, 0.13f, 0.56f, Steel(0.66f));
                Box(0, wallTop + 0.49f, 0.02f, 0.16f, 0.035f, 0.43f, Paint());
                break;
            case 1:
                // Barracks pod: taller squared cap, faction fascia and twin vents.
                Slab(0, wallTop + 0.09f, 0, 0.97f, 0.22f, 0.97f, 0.12f, 0.95f, Paint(0.82f));
                Slab(0, wallTop + 0.27f, 0, 0.91f, 0.19f, 0.91f, 0.20f, 0.92f, Steel(1f));
                for (int side = -1; side <= 1; side += 2)
                {
                    Box(side * 0.20f, wallTop + 0.43f, 0.08f, 0.24f, 0.18f, 0.36f, Steel(0.57f));
                    Box(side * 0.20f, wallTop + 0.525f, 0.08f, 0.17f, 0.025f, 0.24f, Steel(0.29f));
                }
                break;
            default:
                // Service habitat: asymmetric raised roof bay and front viewport.
                Slab(0, wallTop + 0.11f, 0, 0.96f, 0.25f, 0.96f, 0.18f, 0.92f, Steel(1f));
                Slab(0.14f, wallTop + 0.37f, 0.09f, 0.52f, 0.34f, 0.64f, 0.22f, 0.87f, Steel(0.83f));
                Box(0.14f, wallTop + 0.55f, 0.09f, 0.33f, 0.04f, 0.44f, Paint());
                Box(0.19f, 1.24f, -0.464f, 0.28f, 0.48f, 0.033f, Steel(0.25f));
                Box(0.19f, 1.24f, -0.486f, 0.22f, 0.30f, 0.012f, Paint(0.64f));
                break;
        }
        return surface.Commit();
    }
}
