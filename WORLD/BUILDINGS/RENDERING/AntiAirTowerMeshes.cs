// Static vertical-launch air-defense silhouettes with shared baked faction armor.
using Godot;
using static MassiveRts.TowerArmorGeometry;

namespace MassiveRts;

internal static class AntiAirTowerMeshes
{
    internal const string BarrageId = "missile_barrage_tower", HeavyId = "long_range_aa_tower";
    internal static int Variant(string id) => id == BarrageId ? 1 : id == HeavyId ? 2 : 0;
    internal static int SocketCount(int variant) => variant == 1 ? 12 : 1;
    private static Vector2 Socket(int variant, int index) => variant == 1
        ? new Vector2((index % 4 - 1.5f) * 0.19f, (index / 4 - 1f) * 0.22f) : Vector2.Zero;
    private static float Top(int variant) => variant == 1 ? 1.34f : 1.85f;

    internal static Vector3 Muzzle(Vector3 pivot, ObstacleRectangle area, float height, int variant, int index)
    {
        Vector2 size = area.Max - area.Min, socket = Socket(variant, index);
        return pivot + new Vector3(socket.X * size.X, (Top(variant) - 1f) * height - 0.25f, socket.Y * size.Y);
    }

    // =========================================================
    // Barrage has twelve square launch mouths; heavy has one raised armored silo.
    internal static Mesh Create(int variant)
    {
        using var s = new SurfaceTool();
        s.Begin(Mesh.PrimitiveType.Triangles);
        Plate(s, new Vector3(0, 0.13f, 0), new Vector3(1f, 0.26f, 1f), 0.14f, 0.94f, Steel(0.60f));
        Plate(s, new Vector3(0, 0.46f, 0), new Vector3(0.92f, 0.40f, 0.92f), 0.32f, 0.86f, Steel(0.88f));
        Plate(s, new Vector3(0, 0.72f, 0), new Vector3(0.78f, 0.22f, 0.78f), 0.24f, 0.94f, Paint());
        for (int side = -1; side <= 1; side += 2)
        {
            Plate(s, new Vector3(side * 0.43f, 0.42f, 0), new Vector3(0.10f, 0.40f, 0.52f), 0.25f, 0.80f, Paint());
            Block(s, new Vector3(side * 0.43f, 0.65f, 0), new Vector3(0.06f, 0.045f, 0.34f), Trim());
        }
        float top = Top(variant), w = variant == 1 ? 0.155f : 0.40f;
        if (variant == 2)
        {
            Plate(s, new Vector3(0, 1.05f, 0), new Vector3(0.66f, 0.55f, 0.66f), 0.40f, 0.77f, Steel(0.72f));
            Plate(s, new Vector3(0, 1.37f, 0), new Vector3(0.53f, 0.22f, 0.53f), 0.40f, 0.92f, Paint());
        }
        for (int i = 0; i < SocketCount(variant); i++)
        {
            Vector2 p = Socket(variant, i);
            float tubeHeight = variant == 1 ? 0.49f : 0.38f;
            Block(s, new Vector3(p.X, top - tubeHeight * 0.5f, p.Y), new Vector3(w, tubeHeight, w), Steel(0.75f));
            float rim = w * 0.16f, inner = w - 2f * rim;
            Block(s, new Vector3(p.X, top - 0.035f, p.Y), new Vector3(inner, 0.035f, inner), Steel(0.14f));
            for (int side = -1; side <= 1; side += 2)
            {
                Block(s, new Vector3(p.X + side * (w-rim)*0.5f, top - 0.025f, p.Y), new Vector3(rim, 0.05f, w), Paint());
                Block(s, new Vector3(p.X, top - 0.025f, p.Y + side * (w-rim)*0.5f), new Vector3(inner, 0.05f, rim), Paint());
            }
        }
        return s.Commit();
    }
}
