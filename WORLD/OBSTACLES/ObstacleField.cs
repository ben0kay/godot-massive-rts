// Static XZ obstacle index shared by ground movement, visibility and projectile sweeps.
using System;
using System.Diagnostics;
using Godot;

namespace MassiveRts;

public sealed class ObstacleField
{
    private readonly record struct Box(Vector2 Min, Vector2 Max);
    private readonly record struct Leaf(Box Bounds, Vector2? AirborneHeightRange, bool AllowInteriorEscape);
    private readonly record struct Node(Box Bounds, int Left, int Right, bool AllowInteriorEscape = false);
    private readonly Node[] nodes;
    private int nodeCount;
    private readonly float lowestWreckTier;
    private readonly float highestMinimumBlockingTier;
    private System.Collections.Generic.Dictionary<float, ObstacleField> tierFields;
    private readonly bool hasInteriorEscape;
    private readonly Vector2?[] airborneHeights;
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

    public float Boundary { get; }
    internal Rect2[] Bridges { get; }

    public ObstacleField(ObstacleRectangle[] rectangles, float? boundary = null, Rect2[] bridges = null)
    {
        Bridges = bridges ?? Array.Empty<Rect2>();
        float gridSize = rectangles.Length > 0 ? rectangles[0].GridSize : DeploymentLayout.CellSize;
        Boundary = boundary ?? DeploymentLayout.CellHalfWidth(gridSize) * gridSize;
        this.rectangles = (ObstacleRectangle[])rectangles.Clone();
        lowestWreckTier = float.PositiveInfinity;
        var leaves = new Leaf[rectangles.Length];
        bool hasCustomAirborneHeight = false;
        for (int i = 0; i < leaves.Length; i++)
        {
            var area = rectangles[i];
            highestMinimumBlockingTier = Mathf.Max(highestMinimumBlockingTier, area.MinimumBlockingTier);
            if (area.WreckTier > 0f) lowestWreckTier = Mathf.Min(lowestWreckTier, area.WreckTier);
            leaves[i] = new Leaf(new Box(area.Min, area.Max), area.AirborneHeightRange, area.AllowInteriorEscape);
            hasInteriorEscape |= area.AllowInteriorEscape;
            hasCustomAirborneHeight |= area.AirborneHeightRange.HasValue;
        }
        nodes = new Node[Math.Max(0, leaves.Length * 2 - 1)];
        if (hasCustomAirborneHeight) airborneHeights = new Vector2?[nodes.Length];
        if (leaves.Length > 0) Build(leaves, 0, leaves.Length);
    }

    internal ObstacleField ForTier(float tier)
    {
        if (tier <= lowestWreckTier && tier >= highestMinimumBlockingTier) return this;
        tierFields ??= new();
        if (tierFields.TryGetValue(tier, out var cached)) return cached;
        var remaining = Array.FindAll(rectangles, area => tier >= area.MinimumBlockingTier && (area.WreckTier == 0f || area.WreckTier >= tier));
        return tierFields[tier] = new ObstacleField(remaining, Boundary, Bridges);
    }

    internal static bool TouchesSweep(ObstacleRectangle area, Vector2 from, Vector2 to, float radius)
    {
        Vector2 delta = to - from;
        var box = new Box(area.Min, area.Max);
        var padding = new Vector2(radius, radius);
        if (!EnterBox(from, delta, box.Min - padding, box.Max + padding, out _)) return false;
        float hit = float.PositiveInfinity;
        TraceRoundedBox(box, from, delta, radius, ref hit);
        return hit <= 1f;
    }

    private int Build(Leaf[] leaves, int start, int count)
    {
        int index = nodeCount++;
        Box bounds = leaves[start].Bounds;
        for (int i = start + 1; i < start + count; i++)
            bounds = new Box(bounds.Min.Min(leaves[i].Bounds.Min), bounds.Max.Max(leaves[i].Bounds.Max));
        if (count == 1)
        {
            nodes[index] = new Node(bounds, -1, -1, leaves[start].AllowInteriorEscape);
            if (airborneHeights != null) airborneHeights[index] = leaves[start].AirborneHeightRange;
        }
        else
        {
            bool splitX = bounds.Max.X - bounds.Min.X >= bounds.Max.Y - bounds.Min.Y;
            Array.Sort(leaves, start, count, ComparerForAxis(splitX));
            int half = count / 2;
            nodes[index] = new Node(bounds, Build(leaves, start, half), Build(leaves, start + half, count - half));
        }
        return index;
    }

    private static System.Collections.Generic.IComparer<Leaf> ComparerForAxis(bool x) =>
        System.Collections.Generic.Comparer<Leaf>.Create((a, b) => x
            ? (a.Bounds.Min.X + a.Bounds.Max.X).CompareTo(b.Bounds.Min.X + b.Bounds.Max.X)
            : (a.Bounds.Min.Y + a.Bounds.Max.Y).CompareTo(b.Bounds.Min.Y + b.Bounds.Max.Y));

    public void ResetCounters()
    {
        LosQueries = LosBlocks = ClippedMoves = 0;
        Array.Clear(samples);
    }
    public static Vector2 Xz(Vector3 point) => new(point.X, point.Z);

    public bool IntersectsCircle(Vector2 point, float radius)
    {
        long start = BeginSample(Query.Circle);
        bool hit = nodeCount > 0 && IntersectsCircle(0, point, radius * radius);
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

    // =========================================================
    // A static weapon's own obstacle is ignored; every other rectangle still blocks sight.
    public bool BlocksSightExcept(Vector3 from, Vector3 to, ObstacleRectangle own)
    {
        LosQueries++;
        long start = BeginSample(Query.Sight);
        bool blocked = nodeCount > 0 && BlocksSightExcept(0, Xz(from), Xz(to - from), new Box(own.Min, own.Max));
        EndSample(Query.Sight, start);
        if (blocked) LosBlocks++;
        return blocked;
    }

    private bool BlocksSightExcept(int index, Vector2 from, Vector2 delta, Box own)
    {
        ref readonly Node node = ref nodes[index];
        if (!EnterBox(from, delta, node.Bounds.Min, node.Bounds.Max, out _)) return false;
        if (node.Left < 0) return node.Bounds != own;
        return BlocksSightExcept(node.Left, from, delta, own) || BlocksSightExcept(node.Right, from, delta, own);
    }

    internal float FirstSightHitExcept(Vector3 from, Vector3 segment, ObstacleRectangle own)
    {
        float nearest = 1f;
        if (nodeCount > 0) FindSightHit(0, Xz(from), Xz(segment), new Box(own.Min, own.Max), ref nearest);
        return nearest;
    }

    private void FindSightHit(int index, Vector2 from, Vector2 delta, Box own, ref float nearest)
    {
        ref readonly Node node = ref nodes[index];
        if (!EnterBox(from, delta, node.Bounds.Min, node.Bounds.Max, out float entry) || entry >= nearest) return;
        if (node.Left < 0)
        {
            if (node.Bounds != own) nearest = entry;
            return;
        }
        FindSightHit(node.Left, from, delta, own, ref nearest);
        FindSightHit(node.Right, from, delta, own, ref nearest);
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
        float edge = Boundary - radius;
        Vector2 delta = to - from;
        float limit = Mathf.Min(BoundaryFraction(from.X, to.X, delta.X, edge),
            BoundaryFraction(from.Y, to.Y, delta.Y, edge));
        bool hitWall = Trace(from, to, radius, false, out float hit) && hit <= limit;
        if (!hitWall && limit >= 1f) return to;
        if (hitWall && hit == 0f && limit >= 1f && CanExitClearance(from, to, radius)) return to;

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

    // =========================================================
    // Reuse the XZ tree while clipping the sweep to the walls' shared vertical span.
    public float AirborneHit(Vector3 from, Vector3 delta, float wallHeight)
    {
        float enter = 0f, exit = 1f, first = float.PositiveInfinity;
        if (nodeCount > 0 && (airborneHeights != null || Axis(from.Y, delta.Y, 0f, wallHeight, ref enter, ref exit)))
            SearchAirborne(0, from, delta, wallHeight, enter, exit, ref first);
        return first;
    }

    private void SearchAirborne(int index, Vector3 from, Vector3 delta, float wallHeight, float enter, float exit, ref float first)
    {
        ref readonly Node node = ref nodes[index];
        Box box = node.Bounds;
        if (!Axis(from.X, delta.X, box.Min.X, box.Max.X, ref enter, ref exit) || enter >= first ||
            !Axis(from.Z, delta.Z, box.Min.Y, box.Max.Y, ref enter, ref exit) || enter >= first) return;
        if (node.Left < 0)
        {
            if (airborneHeights == null)
            {
                first = enter;
                return;
            }
            Vector2 height = airborneHeights[index] ?? new Vector2(0f, wallHeight);
            if (Axis(from.Y, delta.Y, height.X, height.Y, ref enter, ref exit) && enter < first) first = enter;
            return;
        }
        SearchAirborne(node.Left, from, delta, wallHeight, enter, exit, ref first);
        SearchAirborne(node.Right, from, delta, wallHeight, enter, exit, ref first);
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
        bool blocked = nodeCount > 0 && CrossesWallInterior(0, from, to - from, margin);
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

    // Rectangle expansion and closest-point distance round differently in float XZ.
    private const float ClearanceContactSkin = 0.0001f;

    internal bool HasClearanceContact(Vector2 point, float radius) =>
        IntersectsCircle(point, radius + ClearanceContactSkin);

    internal bool TryFindClearanceExit(Vector2 from, float radius, out Vector2 exit)
    {
        exit = from;
        for (int ring = 0; ring < 2; ring++)
        for (int direction = 0; direction < 8; direction++)
        {
            float step = ring == 0 ? 0.05f : radius + 0.05f;
            Vector2 to = from + Vector2.FromAngle(direction * (Mathf.Tau / 8f)) * step;
            if (HasClearanceContact(to, radius) || !CanExitClearance(from, to, radius)) continue;
            exit = to;
            return true;
        }
        if (!hasInteriorEscape) return false;
        // Newly settled wrecks can enclose a survivor's center. Probe beyond
        // their nearest face when the normal short clearance probes fail.
        foreach (var area in rectangles)
        {
            if (!area.AllowInteriorEscape || !new Rect2(area.Min, area.Max - area.Min).HasPoint(from)) continue;
            Vector2 direction = NearestFace(from, new Box(area.Min, area.Max), out float depth);
            Vector2 to = from + direction * (depth + radius + ClearanceContactSkin + 0.05f);
            if (HasClearanceContact(to, radius) || !CanExitClearance(from, to, radius)) continue;
            exit = to;
            return true;
        }
        return false;
    }

    private static Vector2 NearestFace(Vector2 point, Box box, out float depth)
    {
        float left = point.X - box.Min.X, right = box.Max.X - point.X;
        float near = point.Y - box.Min.Y, far = box.Max.Y - point.Y;
        float x = Mathf.Min(left, right), z = Mathf.Min(near, far);
        depth = Mathf.Min(x, z);
        return x <= z ? (left <= right ? Vector2.Left : Vector2.Right) :
            (near <= far ? Vector2.Up : Vector2.Down);
    }

    internal bool CanExitClearance(Vector2 from, Vector2 to, float radius)
    {
        float edge = Boundary - radius;
        return Mathf.Abs(to.X) <= edge && Mathf.Abs(to.Y) <= edge &&
            !Trace(from, to, radius, true, out _, allowExit: true);
    }

    private bool Trace(Vector2 from, Vector2 to, float radius, bool anyHit, out float fraction, bool allowExit = false)
    {
        fraction = float.PositiveInfinity;
        if (nodeCount > 0) Search(0, from, to - from, radius, anyHit, ref fraction, allowExit);
        return fraction <= 1f;
    }

    private void Search(int index, Vector2 from, Vector2 delta, float radius, bool anyHit, ref float first, bool allowExit)
    {
        ref readonly Node node = ref nodes[index];
        Box box = node.Bounds;
        var padding = new Vector2(radius, radius);
        if (!EnterBox(from, delta, box.Min - padding, box.Max + padding, out float entry) || entry >= first) return;
        if (node.Left >= 0)
        {
            Search(node.Left, from, delta, radius, anyHit, ref first, allowExit);
            if (!anyHit || first > 1f)
                Search(node.Right, from, delta, radius, anyHit, ref first, allowExit);
            return;
        }
        if (radius == 0f)
        {
            first = entry;
            return;
        }

        if (allowExit)
        {
            Vector2 away = from - from.Max(box.Min).Min(box.Max);
            float distanceSquared = away.LengthSquared();
            if (entry == 0f && distanceSquared == 0f && node.AllowInteriorEscape &&
                NearestFace(from, box, out _).Dot(delta) > 0f) return;
            // Ignore only the initial clearance overlap while moving away from its solid.
            float contactRadius = radius + ClearanceContactSkin;
            if (entry == 0f && distanceSquared > 0f && distanceSquared <= contactRadius * contactRadius &&
                away.Dot(delta) > 0f) return;
        }

        TraceRoundedBox(box, from, delta, radius, ref first);
    }

    private static void TraceRoundedBox(Box box, Vector2 from, Vector2 delta, float radius, ref float first)
    {
        // Two rectangular strips and four circles form the exact rounded Minkowski expansion.
        var xPad = new Vector2(radius, 0f);
        var zPad = new Vector2(0f, radius);
        if (EnterBox(from, delta, box.Min - xPad, box.Max + xPad, out float entry)) first = Mathf.Min(first, entry);
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
