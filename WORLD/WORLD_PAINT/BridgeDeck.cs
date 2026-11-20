// Builds the shared terrain-following deck mesh for authored bridges and their editor previews.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal static class BridgeDeck
{
    internal static Mesh Build(IEnumerable<Rect2I> bridges, TerrainHeightField terrain, bool preview = false)
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        });
        bool hasDeck = false;
        foreach (var span in bridges)
        {
            hasDeck = true;
            bool alongX = span.Size.X >= span.Size.Y;
            for (int z = span.Position.Y; z < span.End.Y; z++)
            for (int x = span.Position.X; x < span.End.X; x++)
            {
                Vector3 a = Point(x, z), b = Point(x + 1, z), c = Point(x, z + 1), d = Point(x + 1, z + 1);
                bool trim = alongX ? z == span.Position.Y || z == span.End.Y - 1 : x == span.Position.X || x == span.End.X - 1;
                surface.SetColor(preview ? new Color("#c99955") : trim ? new Color("#74624d") :
                    ((alongX ? x : z) & 1) == 0 ? new Color("#9b8060") : new Color("#8e7456"));
                surface.AddVertex(a); surface.AddVertex(b); surface.AddVertex(c);
                surface.AddVertex(b); surface.AddVertex(d); surface.AddVertex(c);
            }
        }
        return hasDeck ? surface.Commit() : null;

        Vector3 Point(int x, int z)
        {
            float worldX = x * WorldPaint.CellSize, worldZ = z * WorldPaint.CellSize;
            return new Vector3(worldX, (terrain?.At(worldX, worldZ) ?? 0f) + (preview ? 0.30f : 0.035f), worldZ);
        }
    }
}
