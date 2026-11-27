// Builds a seeded battlefield using the editor's existing elevation and world-paint formats.
using System;
using System.Collections.Generic;
using Godot;
using Samples = Godot.Collections.Dictionary<Godot.Vector2I, float>;

namespace MassiveRts;

internal static class ProceduralBattlefield
{
    internal enum Layout { OpenPlains, RiverDivide, MountainPass }
    internal enum ForestPattern { Groves, LargeForest }
    internal const int MinSize = 192;
    internal const int MaxSize = 1024;
    private const float Cell = WorldPaint.CellSize;

    internal sealed record Settings(int Seed, int Size, Layout Preset, float Relief, float Forest, ForestPattern Trees, float Rock,
        int RiverWidth, float Meander, float FlatBand, bool Fortress);

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
        bool river = settings.Preset == Layout.RiverDivide;
        var random = new Random(settings.Seed);
        float phase = (float)random.NextDouble() * Mathf.Tau;
        var paint = new WorldPaintData { ResourceLocalToScene = true };

        float RiverCenterAt(float x) =>
            riverCenter + swing * Mathf.Sin((x / size + 0.5f) * 9.6f + phase);

        int RiverRow(int column)
        {
            int segment = (column + edge) * 12 / (2 * edge);
            return riverCenter + Mathf.RoundToInt(swing * Mathf.Sin(segment * 0.8f + phase));
        }

        if (river)
        {
            if (settings.Fortress && (riverCenter - riverHalf - swing) * Cell <= 18f)
                throw new InvalidOperationException("The river reaches the fortress build area. Narrow it or increase the map size.");

            for (int x = -edge; x < edge; x++)
            {
                int row = RiverRow(x);
                int previous = RiverRow(Math.Max(x - 1, -edge));
                for (int z = Math.Min(row, previous) - riverHalf; z < Math.Max(row, previous) + riverHalf; z++)
                    paint.WaterCells.Add(new Vector2I(x, z));
            }
        }

        bool Protected(Vector2 point)
        {
            float end = size * (0.5f - settings.FlatBand);
            return Mathf.Abs(point.Y) >= end ||
                (settings.Fortress && Mathf.Abs(point.X) <= 32f && point.Y >= -38f && point.Y <= 18f);
        }

        Vector2 Center(Vector2I cell) => ((Vector2)cell + Vector2.One * 0.5f) * Cell;
        var water = new HashSet<Vector2I>(paint.WaterCells);
        var rocks = new HashSet<Vector2I>();
        bool OpenForScenery(Vector2I cell)
        {
            if (water.Contains(cell)) return false;
            if (!settings.Fortress) return true;
            var point = Center(cell);
            return Mathf.Abs(point.X) > 32f || point.Y < -38f || point.Y > 18f;
        }

        bool CanPlaceRock(Vector2I cell) =>
            cell.X >= -edge && cell.X < edge && cell.Y >= -edge && cell.Y < edge &&
            !Protected(Center(cell)) && OpenForScenery(cell) && !rocks.Contains(cell);

        BuildRockOutcrops(settings, edge, water.Count, CanPlaceRock, rocks);
        foreach (var cell in rocks) paint.RockCells.Add(cell);

        var forest = BuildForest(settings, edge, cell => OpenForScenery(cell) && !rocks.Contains(cell) &&
            Mathf.Abs(Center(cell).Y) < size * (0.5f - settings.FlatBand * 0.5f));
        foreach (var cell in forest) paint.ForestCells.Add(cell);

        // Keep the default relief and give the low end finer control over nearly flat maps.
        float relief = settings.Relief < 9f ? settings.Relief * settings.Relief / 9f : settings.Relief;
        float detailScale = Mathf.Min(1f, settings.Relief / 9f);
        float sampleThreshold = 0.035f * Mathf.Clamp(relief / 9f, 0.01f, 1f);

        // Broad slopes taper to zero at deployment ends, around the fortress and along the river valley.
        var hills = new FastNoiseLite { Seed = settings.Seed, Frequency = 3.1f };
        var foothills = new FastNoiseLite { Seed = unchecked(settings.Seed + 1), Frequency = 7f };
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
                // Keep both banks level for an editor-placed bridge anywhere along the river.
                float bankDistance = Mathf.Abs(point.Y - RiverCenterAt(point.X) * Cell);
                float landingWidth = (riverHalf + swing * 0.8f + 2f) * Cell + TerrainHeightField.Spacing * 2f;
                envelope *= Mathf.SmoothStep(0f, size * 0.18f, bankDistance - landingWidth);
            }
            float u = point.X / size, v = point.Y / size;
            float shape = hills.GetNoise2D(u * 0.9f, v) * 1.45f +
                foothills.GetNoise2D(u, v * 1.15f) * 0.35f * detailScale;
            if (settings.Preset == Layout.MountainPass)
                shape += 1.4f * Mathf.SmoothStep(0.16f, 0.43f, Mathf.Abs(u));
            return shape * relief * envelope;
        }

        const float spacing = TerrainHeightField.Spacing;
        int sampleEdge = Mathf.CeilToInt(size / (2f * spacing));
        var heights = new Samples();
        for (int z = -sampleEdge; z <= sampleEdge; z++)
        for (int x = -sampleEdge; x <= sampleEdge; x++)
        {
            var key = new Vector2I(x, z);
            float height = Height((Vector2)key * spacing);
            if (Mathf.Abs(height) >= sampleThreshold) heights.Add(key, height);
        }

        float Sample(Vector2 point)
        {
            var key = new Vector2I(Mathf.RoundToInt(point.X / spacing), Mathf.RoundToInt(point.Y / spacing));
            return heights.TryGetValue(key, out float height) ? height : 0f;
        }
        float half = size * 0.5f;
        var surface = new TerrainHeightField(new Vector2(-half, -half), new Vector2(half, half), Sample);
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
            if (forest.Contains(cell)) color = new Color("#28543d").Lightened(Mathf.Clamp((height + 9f) / 110f, 0f, 0.3f));
            if (rocks.Contains(cell)) color = new Color("#626b75");
            if (water.Contains(cell)) color = new Color("#287692");
            preview.SetPixel(x, z, color);
        }
        return new Candidate(size, heights, paint, preview);
    }

    // =========================================================
    // A few broad outcrops plus smaller satellites. Stepped banks keep the obstacle data compact.
    private static void BuildRockOutcrops(Settings settings, int edge, int waterCount,
        Func<Vector2I, bool> canPlace, HashSet<Vector2I> rocks)
    {
        bool mountain = settings.Preset == Layout.MountainPass;
        var random = new Random(unchecked(settings.Seed ^ 0x2D3A318B));
        int target = Mathf.RoundToInt(settings.Rock * (mountain ? 24 : 18));
        int accepted = 0;
        for (int attempt = 0; attempt < target * 8 && accepted < target; attempt++)
        {
            bool small = accepted % 3 == 2;
            int scale = Mathf.Clamp(edge / 6, 7, 28);
            int length = small ? random.Next(3, scale) : random.Next(scale, scale * 3);
            int depth = small ? random.Next(2, 5) : random.Next(5, Mathf.Max(7, scale + 1));
            int x = mountain ? (attempt % 2 == 0 ? -1 : 1) * random.Next(edge / 3, edge * 4 / 5)
                : random.Next(-edge, edge);
            int z = random.Next(-edge, edge);
            int drift = random.Next(-depth, depth + 1);
            bool turn = random.Next(2) == 0;
            var patch = new List<Vector2I>();
            for (int row = 0; row < depth; row++)
            {
                // Quantize the profile to a few terraces so long outcrops still merge efficiently.
                float t = (row * 5 / depth + 0.5f) / 5f;
                int width = Mathf.Max(2, Mathf.RoundToInt(length * (0.25f + 0.75f * Mathf.Sin(t * Mathf.Pi))));
                int offset = Mathf.RoundToInt(drift * (t - 0.5f));
                for (int column = -width / 2; column < (width + 1) / 2; column++)
                {
                    int along = column + offset, across = row - depth / 2;
                    var cell = new Vector2I(x + (turn ? across : along), z + (turn ? along : across));
                    if (canPlace(cell)) patch.Add(cell);
                }
            }
            if (patch.Count < length * depth * 0.4f || rocks.Count + patch.Count + waterCount > WorldPaint.MaxPaintedCells)
                continue;

            rocks.UnionWith(patch);
            int rectangleBudget = settings.Preset == Layout.RiverDivide
                ? WorldPaint.MaxPaintedRectangles / 2 : WorldPaint.MaxPaintedRectangles;
            if (ObstacleCellCompiler.Merge(rocks, Cell, rectangleBudget) == null)
            {
                rocks.ExceptWith(patch);
                continue;
            }
            accepted++;
        }
    }

    // =========================================================
    // Rank eligible cells by a continuous canopy field, then fill to a whole-map area target.
    private static HashSet<Vector2I> BuildForest(Settings settings, int edge, Func<Vector2I, bool> canPlace)
    {
        var forest = new HashSet<Vector2I>();
        int target = Math.Min(WorldPaint.MaxPaintedCells,
            Mathf.RoundToInt(4 * edge * edge * settings.Forest));
        if (target == 0) return forest;

        int seed = unchecked(settings.Seed ^ 0x491A56B7);
        var broad = new FastNoiseLite { Seed = seed, Frequency = 4f };
        var detail = new FastNoiseLite { Seed = unchecked(seed + 1), Frequency = 21f };
        var warp = new FastNoiseLite { Seed = unchecked(seed + 2), Frequency = 3f };
        var groves = new FastNoiseLite { Seed = unchecked(seed + 3), Frequency = 11f };
        var fringe = new FastNoiseLite { Seed = unchecked(seed + 4), Frequency = 43f };
        var random = new Random(seed);
        Vector2 along = Vector2.Right.Rotated((float)random.NextDouble() * Mathf.Pi);
        Vector2 across = new(-along.Y, along.X);
        float bend = (float)random.NextDouble() * Mathf.Tau;
        bool sweeping = settings.Trees == ForestPattern.LargeForest;
        var ranked = new List<(Vector2I Cell, float Score)>();

        for (int z = -edge; z < edge; z++)
        for (int x = -edge; x < edge; x++)
        {
            var cell = new Vector2I(x, z);
            if (!canPlace(cell)) continue;
            var point = ((Vector2)cell + Vector2.One * 0.5f) / (2f * edge);
            float u = point.Dot(along), v = point.Dot(across);
            float curved = v + 0.13f * Mathf.Sin(u * 6f + bend) + 0.12f * warp.GetNoise2D(u, v);
            float patches = groves.GetNoise2D(u * 0.8f, curved * 1.15f);
            float boundary = detail.GetNoise2D(u, v) * 0.15f + fringe.GetNoise2D(u, v) * 0.09f;
            float score = sweeping
                ? broad.GetNoise2D(u * 0.55f, curved * 1.4f) * 0.33f - Mathf.Abs(curved) * 1.6f + patches * 0.25f
                : patches * 0.78f + broad.GetNoise2D(u, v) * 0.22f;
            // Small per-cell variation breaks up the edge and leaves occasional outlying trees.
            score += boundary + ((float)random.NextDouble() - 0.5f) * 0.12f;
            ranked.Add((cell, score));
        }

        ranked.Sort((a, b) =>
        {
            int score = b.Score.CompareTo(a.Score);
            if (score != 0) return score;
            int row = a.Cell.Y.CompareTo(b.Cell.Y);
            return row != 0 ? row : a.Cell.X.CompareTo(b.Cell.X);
        });
        int count = Math.Min(target, ranked.Count);
        for (int i = 0; i < count; i++) forest.Add(ranked[i].Cell);
        return forest;
    }
}
