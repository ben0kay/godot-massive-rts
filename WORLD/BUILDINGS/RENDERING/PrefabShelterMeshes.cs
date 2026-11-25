// Builds the low two-tone metal shelter in normalized footprint space.
using Godot;
using static MassiveRts.TowerArmorGeometry;

namespace MassiveRts;

internal static class PrefabShelterMeshes
{
    internal const string Id = "prefab_shelter";

    internal static Mesh Create()
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);

        Plate(surface, new Vector3(0, 0.06f, 0), new Vector3(1f, 0.12f, 1f), 0.22f, 0.91f, Steel(0.52f));
        Plate(surface, new Vector3(0, 0.32f, 0), new Vector3(0.95f, 0.42f, 0.92f), 0.18f, 0.94f, Steel(0.76f));
        Plate(surface, new Vector3(0, 0.56f, 0), new Vector3(0.96f, 0.18f, 0.94f), 0.46f, 0.84f, Steel(1f));
        for (int side = -1; side <= 1; side += 2)
        {
            Block(surface, new Vector3(side * 0.43f, 0.33f, 0), new Vector3(0.10f, 0.36f, 0.86f), Steel(0.48f));
            Block(surface, new Vector3(side * 0.48f, 0.42f, 0), new Vector3(0.025f, 0.07f, 0.62f), Paint());
            Block(surface, new Vector3(side * 0.24f, 0.65f, 0), new Vector3(0.035f, 0.025f, 0.65f), Steel(0.59f));
            Block(surface, new Vector3(side * 0.31f, 0.42f, -0.463f), new Vector3(0.085f, 0.055f, 0.016f), Trim(0.6f));
        }
        Block(surface, new Vector3(0, 0.28f, -0.463f), new Vector3(0.31f, 0.32f, 0.018f), Steel(0.29f));
        Block(surface, new Vector3(0, 0.29f, -0.475f), new Vector3(0.018f, 0.27f, 0.012f), Steel(0.53f));
        Block(surface, new Vector3(0, 0.46f, -0.47f), new Vector3(0.34f, 0.045f, 0.025f), Paint());
        return surface.Commit();
    }
}
