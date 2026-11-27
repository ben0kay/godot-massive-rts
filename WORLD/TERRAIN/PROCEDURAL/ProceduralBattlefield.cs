// Builds a seeded battlefield using the editor's existing elevation and world-paint formats.
using System;
using System.Collections.Generic;
using Godot;
using Samples = Godot.Collections.Dictionary<Godot.Vector2I, float>;

namespace MassiveRts;

internal static class ProceduralBattlefield
{
    internal enum Layout { OpenPlains, RiverDivide, MountainPass }
    internal const int MinSize = 192;
    internal const int MaxSize = 1024;
    private const float Cell = WorldPaint.CellSize;

    internal sealed record Settings(int Seed, int Size, Layout Preset, float Relief, float Forest, float Rock,
        int RiverWidth, int Crossings, int BridgeWidth, float Meander, float FlatBand, float Clearance, bool Fortress);

    internal sealed record Candidate(int Size, Samples Elevation, WorldPaintData Paint, Image Preview);

    // =========================================================
    internal static Candidate Generate(Settings settings)
    {
        int size = settings.Size;
        if (size < MinSize || size > MaxSize || size % 4 != 0)
            throw new ArgumentException("Choose a supported map size in steps of four.");

        int edge = Mathf.FloorToInt(size / (2f * Cell));
        int riverCenter = settings.Fortress ? Mathf.RoundToInt(edge * 0.48f) : 0;
        int riverHalf = Mathf.CeilToInt(settings.RiverWidth / (2f * Cell));
        int swing = Mathf.RoundToInt(riverHalf * settings.Meander);
        int clearance = Mathf.CeilToInt(settings.Clearance / Cell);
        int bridgeSpan = Mathf.Max(Mathf.CeilToInt(settings.BridgeWidth / Cell), 2 * clearance + 2);
        int leftLane = settings.Crossings == 1 ? 0 : -edge / 2;
        int rightLane = settings.Crossings == 1 ? 0 : edge / 2;
        bool river = settings.Preset == Layout.RiverDivide;
        var random = new Random(settings.Seed);
        float phase = (float)random.NextDouble() * Mathf.Tau;
        var paint = new WorldPaintData { ResourceLocalToScene = true };

        int RiverRow(int column)
        {
            int segment = (column + edge) * 12 / (2 * edge);
            return riverCenter + Mathf.RoundToInt(swing * Mathf.Sin(segment * 0.8f + phase));
        }

        if (river)
        {
            if (bridgeSpan + 2 >= edge)
                throw new InvalidOperationException("Increase the map size or reduce the bridge width.");
            if (settings.Fortress && (riverCenter - riverHalf - swing) * Cell <= 18f)
                throw new InvalidOperationException("The river reaches the fortress build area. Narrow it or increase the map size.");

            for (int x = -edge; x < edge; x++)
            {
                int row = RiverRow(x);
                int previous = RiverRow(Math.Max(x - 1, -edge));
                for (int z = Math.Min(row, previous) - riverHalf; z < Math.Max(row, previous) + riverHalf; z++)
                    paint.WaterCells.Add(new Vector2I(x, z));
            }

            AddBridge(leftLane);
            if (settings.Crossings == 2) AddBridge(rightLane);
        }

        void AddBridge(int center)
        {
            int left = center - bridgeSpan / 2;
            int low = riverCenter - swing - riverHalf - clearance - 1;
            int high = riverCenter + swing + riverHalf + clearance + 1;
            paint.Bridges.Add(new Rect2I(left, low, bridgeSpan, high - low));
        }

        bool Protected(Vector2 point)
        {
            float end = size * (0.5f - settings.FlatBand);
            return Mathf.Abs(point.Y) >= end ||
                (settings.Fortress && Mathf.Abs(point.X) <= 32f && point.Y >= -38f && point.Y <= 18f);
        }

        bool InLane(Vector2I cell)
        {
            if (!river) return Mathf.Abs(cell.X) <= clearance + edge / 7;
            int width = bridgeSpan / 2 + clearance + 1;
            return Mathf.Abs(cell.X - leftLane) <= width || Mathf.Abs(cell.X - rightLane) <= width;
        }

        Vector2 Center(Vector2I cell) => ((Vector2)cell + Vector2.One * 0.5f) * Cell;
        var water = new HashSet<Vector2I>(paint.WaterCells);
        var rocks = new HashSet<Vector2I>();
        var forest = new HashSet<Vector2I>();
        var scenery = new Random(unchecked(settings.Seed ^ 0x2D3A318B));
        bool LegalScenery(Vector2I cell) =>
            !Protected(Center(cell)) && !InLane(cell) &&
            (!settings.Fortress || Mathf.Abs(Center(cell).Y + 13f) > settings.Clearance + 4f) && !water.Contains(cell) &&
            !rocks.Contains(cell) && !forest.Contains(cell);

        // Place compact rock groups so the existing rectangle compiler can merge them.
        int rockGroups = Mathf.RoundToInt(settings.Rock * (settings.Preset == Layout.MountainPass ? 16 : 10));
        for (int i = 0; i < rockGroups; i++)
        {
            int width = scenery.Next(3, 7), depth = scenery.Next(2, 6);
            int sign = i % 2 == 0 ? -1 : 1;
            int x = settings.Preset == Layout.MountainPass
                ? sign * scenery.Next(edge / 3, edge * 4 / 5) : scenery.Next(-edge + 2, edge - width);
            int z = settings.Preset == Layout.MountainPass
                ? scenery.Next(-edge / 5, edge / 5) : scenery.Next(-edge + 2, edge - depth);
            var patch = new List<Vector2I>();
            for (int dz = 0; dz < depth; dz++)
            for (int dx = 0; dx < width; dx++)
                patch.Add(new Vector2I(x + dx, z + dz));
            if (patch.Exists(cell => cell.X >= edge || !LegalScenery(cell))) continue;
            foreach (var cell in patch) { rocks.Add(cell); paint.RockCells.Add(cell); }
        }

        scenery = new Random(unchecked(settings.Seed ^ 0x491A56B7));
        int clusters = Mathf.RoundToInt(settings.Forest * Mathf.Lerp(14f, 56f, size / (float)MaxSize));
        for (int i = 0; i < clusters; i++)
        {
            int x = scenery.Next(-edge + 10, edge - 10), z = scenery.Next(-edge + 10, edge - 10);
            int radius = scenery.Next(4, 10);
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx * dx + dz * dz > radius * radius || scenery.NextDouble() > 0.78) continue;
                var cell = new Vector2I(x + dx, z + dz);
                if (!LegalScenery(cell)) continue;
                forest.Add(cell);
                paint.ForestCells.Add(cell);
            }
        }

        // Check the reserved routes on the same cells the world-paint compiler opens.
        void CheckLane(int x, Rect2I? bridge)
        {
            for (int z = -edge; z < edge; z++)
            for (int dx = -clearance; dx <= clearance; dx++)
            {
                var cell = new Vector2I(x + dx, z);
                if (rocks.Contains(cell) || (water.Contains(cell) && !(bridge?.HasPoint(cell) ?? false)))
                    throw new InvalidOperationException("A route is blocked for the largest ground footprint.");
            }
        }

        if (river)
        {
            CheckLane(leftLane, paint.Bridges[0]);
            if (settings.Crossings == 2) CheckLane(rightLane, paint.Bridges[1]);
        }
        else CheckLane(0, null);

        // Broad slopes taper to zero at deployment ends, around the fortress and along the river valley.
        float Height(Vector2 point)
        {
            float end = size * (0.5f - settings.FlatBand) - TerrainHeightField.Spacing * 1.5f;
            float envelope = Mathf.SmoothStep(0f, size * 0.12f, end - Mathf.Abs(point.Y));
            if (settings.Fortress)
            {
                float distance = Mathf.Max(Mathf.Abs(point.X) - 36f,
                    Mathf.Max(-42f - point.Y, point.Y - 22f));
                envelope *= Mathf.SmoothStep(0f, 24f, distance);
            }
            if (river)
            {
                float bank = (riverHalf + swing + clearance + 2) * Cell;
                envelope *= Mathf.SmoothStep(0f, size * 0.12f, Mathf.Abs(point.Y - riverCenter * Cell) - bank);
            }
            float u = point.X / size, v = point.Y / size;
            float shape = Mathf.Sin(u * 10f + phase) * Mathf.Cos(v * 8f - phase)
                + 0.35f * Mathf.Sin((u + v) * 17f);
            if (settings.Preset == Layout.MountainPass)
                shape += 1.4f * Mathf.SmoothStep(0.16f, 0.43f, Mathf.Abs(u));
            return shape * settings.Relief * envelope;
        }

        const float spacing = TerrainHeightField.Spacing;
        int sampleEdge = Mathf.CeilToInt(size / (2f * spacing));
        var heights = new Samples();
        for (int z = -sampleEdge; z <= sampleEdge; z++)
        for (int x = -sampleEdge; x <= sampleEdge; x++)
        {
            var key = new Vector2I(x, z);
            float height = Height((Vector2)key * spacing);
            if (Mathf.Abs(height) >= 0.035f) heights.Add(key, height);
        }

        float Sample(Vector2 point)
        {
            var key = new Vector2I(Mathf.RoundToInt(point.X / spacing), Mathf.RoundToInt(point.Y / spacing));
            return heights.TryGetValue(key, out float height) ? height : 0f;
        }
        float half = size * 0.5f;
        var surface = new TerrainHeightField(new Vector2(-half, -half), new Vector2(half, half), Sample);
        var bridges = new List<Rect2I>(paint.Bridges);
        const int pixels = 256;
        var preview = Image.CreateEmpty(pixels, pixels, false, Image.Format.Rgb8);
        for (int z = 0; z < pixels; z++)
        for (int x = 0; x < pixels; x++)
        {
            Vector2 point = new Vector2((x + 0.5f) / pixels - 0.5f, (z + 0.5f) / pixels - 0.5f) * size;
            var cell = new Vector2I(Mathf.FloorToInt(point.X / Cell), Mathf.FloorToInt(point.Y / Cell));
            float height = surface.At(point.X, point.Y);
            var color = new Color(0.31f, 0.39f, 0.32f).Lightened(Mathf.Clamp((height + 9f) / 90f, 0f, 0.4f));
            if (Protected(point)) color = new Color("#647668");
            if (forest.Contains(cell)) color = new Color("#28543d");
            if (rocks.Contains(cell)) color = new Color("#626b75");
            if (water.Contains(cell)) color = new Color("#287692");
            foreach (var bridge in bridges)
                if (bridge.HasPoint(cell)) { color = new Color("#b7a785"); break; }
            preview.SetPixel(x, z, color);
        }
        return new Candidate(size, heights, paint, preview);
    }
}
