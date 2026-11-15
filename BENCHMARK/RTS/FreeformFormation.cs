// Stores a bounded freeform mouse stroke and caches soldier slots along its world-space arc.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed class FreeformFormation
{
    private const int MaxSamples = 512;
    private const float SampleSpacing = 0.35f;
    private readonly List<Vector3> points = new(MaxSamples);
    private readonly List<float> distances = new(MaxSamples);
    private readonly List<Vector3> slots = new();

    public IReadOnlyList<Vector3> Points => points;
    public List<Vector3> Slots => slots;
    public float Length => distances.Count == 0 ? 0f : distances[^1];

    // =========================================================
    public void Begin(Vector3 start)
    {
        Clear();
        points.Add(start);
        distances.Add(0f);
    }

    public void Clear()
    {
        points.Clear();
        distances.Clear();
        slots.Clear();
    }

    // Thin old samples when necessary so a very long mouse gesture stays bounded.
    public bool Add(Vector3 position, bool final = false)
    {
        if (points.Count == 0) { Begin(position); return true; }
        float distance = points[^1].DistanceTo(position);
        if (distance <= (final ? 0.00001f : SampleSpacing)) return false;
        if (points.Count >= MaxSamples) Thin();
        points.Add(position);
        distances.Add(distances[^1] + distance);
        return true;
    }

    private void Thin()
    {
        int write = 1;
        for (int read = 2; read < points.Count; read += 2)
            points[write++] = points[read];
        if (points[write - 1] != points[^1]) points[write++] = points[^1];
        points.RemoveRange(write, points.Count - write);
        distances.Clear();
        distances.Add(0f);
        for (int i = 1; i < points.Count; i++)
            distances.Add(distances[^1] + points[i - 1].DistanceTo(points[i]));
    }

    // =========================================================
    // The first rank traces the stroke. Further rows follow its local perpendicular.
    public void BuildSlots(int count, float spacing)
    {
        slots.Clear();
        if (count <= 0 || points.Count < 2 || spacing <= 0f) return;
        float radius = Mathf.Max(0f, (spacing - 0.3f) * 0.5f);
        int columns = Mathf.Clamp(1 + Mathf.FloorToInt((Length - 2f * radius) / spacing), 1, count);
        float step = columns > 1 ? Length / (columns - 1) : 0f;
        for (int i = 0; i < count; i++)
        {
            int row = i / columns;
            int rankSize = Mathf.Min(columns, count - row * columns);
            float along = Length * 0.5f + (i % columns - (rankSize - 1) * 0.5f) * step;
            Sample(Mathf.Clamp(along, 0f, Length), out Vector3 point, out Vector3 tangent);
            slots.Add(point + tangent.Cross(Vector3.Up) * (radius + row * spacing));
        }
    }

    // Locate each destination in logarithmic time within the cumulative arc-length samples.
    private void Sample(float length, out Vector3 point, out Vector3 tangent)
    {
        int low = 1, high = distances.Count - 1;
        while (low < high)
        {
            int mid = low + (high - low) / 2;
            if (distances[mid] < length) low = mid + 1;
            else high = mid;
        }
        float before = distances[low - 1], segment = distances[low] - before;
        Vector3 delta = points[low] - points[low - 1];
        tangent = delta / segment;
        point = points[low - 1] + delta * ((length - before) / segment);
    }
}
