// Static XZ obstacle index shared by ground movement, visibility and projectile sweeps.
using System;
using System.Diagnostics;
using Godot;

namespace MassiveRts;

public sealed class ObstacleField
{
    private readonly record struct Box(Vector2 Min, Vector2 Max);
    private readonly record struct Node(Box Bounds, int Left, int Right);
    private readonly Node[] nodes;
    private int nodeCount;
    public int RectangleCount => rectangles.Length;
    public ReadOnlySpan<ObstacleRectangle> Rectangles => rectangles;
    private readonly ObstacleRectangle[] rectangles;
    public int LosQueries { get; private set; }
    public int LosBlocks { get; private set; }
    public int ClippedMoves { get; private set; }
    public bool ProfileQueries { get; set; }

    public enum Query { Corridor, Clip, Sweep, Sight, Circle }
    public struct QuerySample
    {
        public int Calls, Timed;
        public long Ticks;
        public double Microseconds => Timed == 0 ? 0d : Ticks * 1_000_000d / Stopwatch.Frequency / Timed;
    }

    private readonly QuerySample[] samples = new QuerySample[5];
    public QuerySample Sample(Query query) => samples[(int)query];

    private long BeginSample(Query query)
    {
        if (!ProfileQueries) return 0;
        ref QuerySample sample = ref samples[(int)query];
        return (++sample.Calls & 63) == 1 ? Stopwatch.GetTimestamp() : 0;
    }

    private void EndSample(Query query, long start)
    {
        if (start == 0) return;
        ref QuerySample sample = ref samples[(int)query];
        sample.Ticks += Stopwatch.GetTimestamp() - start;
        sample.Timed++;
    }

    public ObstacleField(ObstacleRectangle[] rectangles)
    {
        this.rectangles = (ObstacleRectangle[])rectangles.Clone();
        var boxes = new Box[rectangles.Length];
        for (int i = 0; i < boxes.Length; i++) boxes[i] = new Box(rectangles[i].Min, rectangles[i].Max);
        nodes = new Node[boxes.Length * 2 - 1];
        Build(boxes, 0, boxes.Length);
    }

    private int Build(Box[] boxes, int start, int count)
    {
        int index = nodeCount++;
        Box bounds = boxes[start];
        for (int i = start + 1; i < start + count; i++)
            bounds = new Box(bounds.Min.Min(boxes[i].Min), bounds.Max.Max(boxes[i].Max));
        if (count == 1) nodes[index] = new Node(bounds, -1, -1);
        else
        {
            bool splitX = bounds.Max.X - bounds.Min.X >= bounds.Max.Y - bounds.Min.Y;
            Array.Sort(boxes, start, count, ComparerForAxis(splitX));
            int half = count / 2;
            nodes[index] = new Node(bounds, Build(boxes, start, half), Build(boxes, start + half, count - half));
        }
        return index;
    }

    private static System.Collections.Generic.IComparer<Box> ComparerForAxis(bool x) =>
        System.Collections.Generic.Comparer<Box>.Create((a, b) => x
            ? (a.Min.X + a.Max.X).CompareTo(b.Min.X + b.Max.X)
            : (a.Min.Y + a.Max.Y).CompareTo(b.Min.Y + b.Max.Y));

    public void ResetCounters()
    {
        LosQueries = LosBlocks = ClippedMoves = 0;
        Array.Clear(samples);
    }
    public static Vector2 Xz(Vector3 point) => new(point.X, point.Z);

    public bool IntersectsCircle(Vector2 point, float radius)
    {
        long start = BeginSample(Query.Circle);
        bool hit = IntersectsCircle(0, point, radius * radius);
        EndSample(Query.Circle, start);
        return hit;
    }

    private bool IntersectsCircle(int index, Vector2 point, float radiusSquared)
    {
        ref readonly Node node = ref nodes[index];
        Vector2 closest = point.Max(node.Bounds.Min).Min(node.Bounds.Max);
        if (point.DistanceSquaredTo(closest) > radiusSquared) return false;
        return node.Left < 0 || IntersectsCircle(node.Left, point, radiusSquared) ||
            IntersectsCircle(node.Right, point, radiusSquared);
    }

    public bool BlocksSight(Vector3 from, Vector3 to)
    {
        LosQueries++;
        long start = BeginSample(Query.Sight);
        bool blocked = Trace(Xz(from), Xz(to), 0f, true, out _);
        EndSample(Query.Sight, start);
        if (blocked) LosBlocks++;
        return blocked;
    }

    public Vector2 ClipMove(Vector2 from, Vector2 to, float radius)
    {
        long start = BeginSample(Query.Clip);
        Vector2 result = ClipMoveCore(from, to, radius);
        EndSample(Query.Clip, start);
        return result;
    }

    private Vector2 ClipMoveCore(Vector2 from, Vector2 to, float radius)
    {
        if (from == to) return to;
        float edge = DeploymentLayout.HalfWidth * DeploymentLayout.CellSize - radius;
        Vector2 delta = to - from;
        float limit = Mathf.Min(BoundaryFraction(from.X, to.X, delta.X, edge),
            BoundaryFraction(from.Y, to.Y, delta.Y, edge));
        bool hitWall = Trace(from, to, radius, false, out float hit) && hit <= limit;
        if (!hitWall && limit >= 1f) return to;

        ClippedMoves++;
        float fraction = Mathf.Max(0f, Mathf.Min(hit, limit) - 0.002f / delta.Length());
        return from + delta * fraction;
    }

    private static float BoundaryFraction(float start, float end, float delta, float edge)
    {
        if (end > edge && delta > 0f) return (edge - start) / delta;
        if (end < -edge && delta < 0f) return (-edge - start) / delta;
        return 1f;
    }

    public bool FirstHit(Vector2 from, Vector2 to, float radius, out float fraction)
    {
        long start = BeginSample(Query.Sweep);
        bool hit = Trace(from, to, radius, false, out fraction);
        EndSample(Query.Sweep, start);
        return hit;
    }

    public bool BlocksCorridor(Vector2 from, Vector2 to, float radius)
    {
        long start = BeginSample(Query.Corridor);
        bool hit = Trace(from, to, radius, true, out _);
        EndSample(Query.Corridor, start);
        return hit;
    }

    // A hit against an inset wall proves that small endpoint shifts still cross that wall.
    public bool CrossesWallInterior(Vector2 from, Vector2 to, float inset)
    {
        long start = BeginSample(Query.Corridor);
        var margin = new Vector2(inset, inset);
        bool blocked = CrossesWallInterior(0, from, to - from, margin);
        EndSample(Query.Corridor, start);
        return blocked;
    }

    private bool CrossesWallInterior(int index, Vector2 from, Vector2 delta, Vector2 margin)
    {
        ref readonly Node node = ref nodes[index];
        Vector2 min = node.Bounds.Min + margin, max = node.Bounds.Max - margin;
        if (min.X > max.X || min.Y > max.Y || !EnterBox(from, delta, min, max, out _)) return false;
        return node.Left < 0 || CrossesWallInterior(node.Left, from, delta, margin) ||
            CrossesWallInterior(node.Right, from, delta, margin);
    }

    private bool Trace(Vector2 from, Vector2 to, float radius, bool anyHit, out float fraction)
    {
        fraction = float.PositiveInfinity;
        Search(0, from, to - from, radius, anyHit, ref fraction);
        return fraction <= 1f;
    }

    private void Search(int index, Vector2 from, Vector2 delta, float radius, bool anyHit, ref float first)
    {
        ref readonly Node node = ref nodes[index];
        Box box = node.Bounds;
        var padding = new Vector2(radius, radius);
        if (!EnterBox(from, delta, box.Min - padding, box.Max + padding, out float entry) || entry >= first) return;
        if (node.Left >= 0)
        {
            Search(node.Left, from, delta, radius, anyHit, ref first);
            if (!anyHit || first > 1f)
                Search(node.Right, from, delta, radius, anyHit, ref first);
            return;
        }
        if (radius == 0f)
        {
            first = entry;
            return;
        }

        // Two rectangular strips and four circles form the exact rounded Minkowski expansion.
        var xPad = new Vector2(radius, 0f);
        var zPad = new Vector2(0f, radius);
        if (EnterBox(from, delta, box.Min - xPad, box.Max + xPad, out entry)) first = Mathf.Min(first, entry);
        if (EnterBox(from, delta, box.Min - zPad, box.Max + zPad, out entry)) first = Mathf.Min(first, entry);
        float lengthSquared = delta.LengthSquared();
        TestCorner(box.Min, from, delta, lengthSquared, radius, ref first);
        TestCorner(box.Max, from, delta, lengthSquared, radius, ref first);
        TestCorner(new Vector2(box.Min.X, box.Max.Y), from, delta, lengthSquared, radius, ref first);
        TestCorner(new Vector2(box.Max.X, box.Min.Y), from, delta, lengthSquared, radius, ref first);
    }

    private static void TestCorner(Vector2 center, Vector2 from, Vector2 delta, float lengthSquared, float radius, ref float first)
    {
        Vector2 offset = center - from;
        float outside = offset.LengthSquared() - radius * radius;
        if (outside <= 0f) { first = 0f; return; }
        float projection = offset.Dot(delta);
        float discriminant = projection * projection - lengthSquared * outside;
        if (projection <= 0f || lengthSquared == 0f || discriminant < 0f) return;
        float entry = outside / (projection + Mathf.Sqrt(discriminant));
        if (entry <= 1f) first = Mathf.Min(first, entry);
    }

    private static bool EnterBox(Vector2 from, Vector2 delta, Vector2 min, Vector2 max, out float entry)
    {
        entry = 0f;
        float exit = 1f;
        return Axis(from.X, delta.X, min.X, max.X, ref entry, ref exit) &&
            Axis(from.Y, delta.Y, min.Y, max.Y, ref entry, ref exit);
    }

    private static bool Axis(float from, float delta, float min, float max, ref float enter, ref float exit)
    {
        if (delta == 0f) return from >= min && from <= max;
        float a = (min - from) / delta, b = (max - from) / delta;
        enter = Mathf.Max(enter, Mathf.Min(a, b));
        exit = Mathf.Min(exit, Mathf.Max(a, b));
        return enter <= exit;
    }
}
