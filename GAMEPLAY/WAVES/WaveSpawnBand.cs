// Caches open segments of one edge for a particular soldier footprint.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed class WaveSpawnBand
{
    private readonly record struct Segment(float Start, float End, float CumulativeWidth);
    private readonly List<Segment> segments = new();
    private readonly Vector3 origin, sideways;
    public bool IsEmpty => segments.Count == 0;

    public WaveSpawnBand(ObstacleField geometry, Vector3 inward, Vector3 sideways, float mapHalfWidth,
        float width, float centre, float radius)
    {
        this.sideways = sideways;
        float edge = mapHalfWidth - radius - 0.1f;
        origin = -inward * edge;
        if (edge <= 0f) return;

        float halfWidth = Mathf.Min(width * 0.5f, edge);
        centre = Mathf.Clamp(centre, -edge + halfWidth, edge - halfWidth);
        float start = centre - halfWidth, end = centre + halfWidth;
        var blocked = new List<Vector2>();
        float exitDepth = Mathf.Min(edge * 2f, Mathf.Max(4f, radius * 2f + 1f));
        float padding = radius + 0.025f;
        Vector2 normal = ObstacleField.Xz(inward), tangent = ObstacleField.Xz(sideways);
        if (geometry != null)
            foreach (var obstacle in geometry.Rectangles)
            {
                float a = obstacle.Min.Dot(normal), b = obstacle.Max.Dot(normal);
                float distance = Mathf.Max(0f, Mathf.Max(Mathf.Min(a, b) + edge - exitDepth, -edge - Mathf.Max(a, b)));
                if (distance > padding) continue;
                // Project the swept circle against the obstacle to get its blocked interval on the edge.
                float expansion = Mathf.Sqrt(Mathf.Max(0f, padding * padding - distance * distance));
                a = obstacle.Min.Dot(tangent);
                b = obstacle.Max.Dot(tangent);
                float from = Mathf.Max(start, Mathf.Min(a, b) - expansion);
                float to = Mathf.Min(end, Mathf.Max(a, b) + expansion);
                if (from <= to) blocked.Add(new Vector2(from, to));
            }

        blocked.Sort((left, right) => left.X.CompareTo(right.X));
        float openStart = start, total = 0f;
        foreach (var interval in blocked)
        {
            AddOpenSegment(openStart, interval.X);
            openStart = Mathf.Max(openStart, interval.Y);
        }
        AddOpenSegment(openStart, end);

        void AddOpenSegment(float from, float to)
        {
            if (to <= from) return;
            total += to - from;
            segments.Add(new Segment(from, to, total));
        }
    }

    public Vector3 Sample(RandomNumberGenerator random)
    {
        float draw = random.Randf() * segments[^1].CumulativeWidth;
        int low = 0, high = segments.Count - 1;
        while (low < high)
        {
            int middle = (low + high) / 2;
            if (draw < segments[middle].CumulativeWidth) high = middle;
            else low = middle + 1;
        }
        var segment = segments[low];
        float along = Mathf.Max(segment.Start, segment.End - (segment.CumulativeWidth - draw));
        return origin + sideways * along;
    }
}
