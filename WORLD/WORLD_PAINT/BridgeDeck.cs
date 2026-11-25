// Bakes industrial bridge decks and side rails into one terrain-following mesh.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal static class BridgeDeck
{
    internal static Mesh Build(IEnumerable<Rect2I> bridges, TerrainHeightField terrain)
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://WORLD/WORLD_PAINT/BridgeDeck.gdshader")
        });
        bool hasDeck = false;
        const float step = WorldPaint.CellSize;
        const float deckHeight = 0.085f;

        foreach (var span in bridges)
        {
            hasDeck = true;
            bool alongX = span.Size.X >= span.Size.Y;
            Vector2 origin = (Vector2)span.Position * step;
            int length = alongX ? span.Size.X : span.Size.Y;
            int width = alongX ? span.Size.Y : span.Size.X;
            float lengthWorld = length * step;
            float widthWorld = width * step;

            for (int row = 0; row < length; row++)
            {
                float start = row * step, end = start + step;
                for (int col = 0; col < width; col++)
                    Band(start, end, col * step, (col + 1) * step, deckHeight, deckHeight, 0f, Vector3.Up);

                // Leave both ends open. The rails and markers stay inside the navigation footprint.
                Rail(start, end, 0f, 1f);
                Rail(start, end, widthWorld, -1f);
            }

            void Rail(float start, float end, float edge, float inward)
            {
                const float shoulder = 0.07f, railWidth = 0.23f;
                const float railHeight = 0.48f, bevel = 0.08f;
                Vector3 inwardNormal = alongX ? Vector3.Back * inward : Vector3.Right * inward;
                Band(start, end, edge, edge, deckHeight, railHeight - bevel, 1f, -inwardNormal);
                Band(start, end, edge, edge + inward * shoulder, railHeight - bevel, railHeight, 1f,
                    (Vector3.Up - inwardNormal).Normalized());
                Band(start, end, edge + inward * shoulder, edge + inward * railWidth, railHeight, railHeight, 2f, Vector3.Up);
                Band(start, end, edge + inward * railWidth, edge + inward * railWidth, railHeight, deckHeight, 1f, inwardNormal);
                if (start == 0f) Cap(start, alongX ? Vector3.Left : Vector3.Forward);
                if (end == lengthWorld) Cap(end, alongX ? Vector3.Right : Vector3.Back);

                void Cap(float at, Vector3 normal)
                {
                    var outside = new Vector2(at, edge);
                    var corner = new Vector2(at, edge + inward * shoulder);
                    var inside = new Vector2(at, edge + inward * railWidth);
                    Triangle(outside, inside, corner, deckHeight, deckHeight, railHeight, 1f, normal);
                    Triangle(inside, corner, inside, deckHeight, railHeight, railHeight, 1f, normal);
                    Triangle(outside, corner, outside, deckHeight, railHeight, railHeight - bevel, 1f, normal);
                }
            }

            void Band(float start, float end, float acrossA, float acrossB, float heightA, float heightB,
                float part, Vector3 normal)
            {
                var a = new Vector2(start, acrossA);
                var b = new Vector2(end, acrossA);
                var c = new Vector2(start, acrossB);
                var d = new Vector2(end, acrossB);
                Triangle(a, b, c, heightA, heightA, heightB, part, normal);
                Triangle(b, d, c, heightA, heightB, heightB, part, normal);
            }

            void Triangle(Vector2 a, Vector2 b, Vector2 c, float ha, float hb, float hc, float part, Vector3 facing)
            {
                Vector3 va = Point(a, ha), vb = Point(b, hb), vc = Point(c, hc);
                Vector3 normal = (vc - va).Cross(vb - va).Normalized();
                if (normal.Dot(facing) < 0f)
                {
                    (b, c) = (c, b);
                    (vb, vc) = (vc, vb);
                    normal = -normal;
                }
                Add(a, va); Add(b, vb); Add(c, vc);

                void Add(Vector2 uv, Vector3 vertex)
                {
                    surface.SetNormal(normal);
                    surface.SetUV(uv);
                    surface.SetUV2(new Vector2(lengthWorld, part));
                    surface.AddVertex(vertex);
                }
            }

            Vector3 Point(Vector2 uv, float height)
            {
                Vector2 point = origin + (alongX ? uv : new Vector2(uv.Y, uv.X));
                return new Vector3(point.X, (terrain?.At(point.X, point.Y) ?? 0f) + height, point.Y);
            }
        }
        return hasDeck ? surface.Commit() : null;
    }
}
