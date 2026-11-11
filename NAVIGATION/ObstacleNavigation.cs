// Shares static corner graphs and budgeted sector routes across soldiers.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;

namespace MassiveRts;

public sealed class ObstacleNavigation
{
    private const float CornerClearance = 0.008f;
    private const float ArrivalDistance = 0.004f;
    private const int RetryFrames = 30;
    private readonly record struct RouteKey(Vector2I From, Vector2I To, byte Type, bool Fine);
    private readonly record struct Request(RouteKey Key, Vector2 Start, Vector2 Goal, int Frame);
    private sealed record Route(Vector2[] Points, Vector2 Start, Vector2 Goal, int Frame);
    private struct Follower
    {
        public Route Route;
        public int Next;
        public Vector2I GoalSector;
    }

    private readonly ObstacleField field;
    private readonly Graph[] graphs;
    private readonly Follower[] followers;
    private readonly Dictionary<RouteKey, Route> routes = new();
    private readonly Queue<RouteKey> cacheOrder = new();
    private readonly Queue<Request> requests = new();
    private readonly HashSet<RouteKey> pending = new();
    private readonly float sectorSize;
    private readonly int capacity, budget;
    private int frame;
    public int QueueDepth => requests.Count;
    public int CacheHits { get; private set; }
    public int CacheMisses { get; private set; }
    public int RoutesBuilt { get; private set; }
    public int MaxWaitFrames { get; private set; }
    public long SteeringTicks { get; private set; }

    public ObstacleNavigation(ObstacleField field, RuntimeUnitCatalog catalog, int soldiers,
        float sectorSize, int capacity, int budget)
    {
        this.field = field;
        this.sectorSize = sectorSize;
        this.capacity = capacity;
        this.budget = budget;
        graphs = new Graph[catalog.Count];
        for (int i = 0; i < graphs.Length; i++)
            graphs[i] = new Graph(field, catalog[i].Footprint);
        followers = new Follower[soldiers];
    }

    public void ResetCounters()
    {
        CacheHits = CacheMisses = RoutesBuilt = MaxWaitFrames = 0;
        SteeringTicks = 0;
    }

    public void AdvanceFrame() => frame++;

    public void BuildQueuedRoutes()
    {
        frame++;
        for (int i = 0; i < budget && requests.TryDequeue(out Request request); i++)
        {
            pending.Remove(request.Key);
            Vector2[] points = graphs[request.Key.Type].FindRoute(field, request.Start, request.Goal);
            if (!routes.ContainsKey(request.Key))
            {
                if (routes.Count == capacity) routes.Remove(cacheOrder.Dequeue());
                cacheOrder.Enqueue(request.Key);
            }
            routes[request.Key] = new Route(points, request.Start, request.Goal, frame);
            RoutesBuilt++;
            MaxWaitFrames = Math.Max(MaxWaitFrames, frame - request.Frame);
        }
    }

    // A direct corridor wins. While a route is pending, hold at the current legal position.
    public Vector2 Steer(int id, byte type, Vector2 start, Vector2 goal, out bool detouring)
    {
        long stamp = Stopwatch.GetTimestamp();
        Vector2 result = ChooseWaypoint(id, type, start, goal, out detouring);
        SteeringTicks += Stopwatch.GetTimestamp() - stamp;
        return result;
    }

    private Vector2 ChooseWaypoint(int id, byte type, Vector2 start, Vector2 goal, out bool detouring)
    {
        float radius = graphs[type].Radius;
        ref Follower follower = ref followers[id];
        if (!field.BlocksCorridor(start, goal, radius))
        {
            follower = default;
            detouring = false;
            return goal;
        }

        detouring = true;
        Vector2I goalSector = Sector(goal, sectorSize);
        if (follower.Route != null && (follower.GoalSector == goalSector ||
            !field.BlocksCorridor(follower.Route.Points[^1], goal, radius)))
        {
            follower.GoalSector = goalSector;
            Vector2[] points = follower.Route.Points;
            while (follower.Next < points.Length &&
                start.DistanceSquaredTo(points[follower.Next]) <= ArrivalDistance * ArrivalDistance)
                follower.Next++;
            if (follower.Next < points.Length && !field.BlocksCorridor(start, points[follower.Next], radius))
                return points[follower.Next];
        }
        follower = default;

        for (int detail = 0; detail < 2; detail++)
        {
            bool fine = detail != 0;
            float cell = fine ? 0.5f : sectorSize;
            var key = new RouteKey(Sector(start, cell), Sector(goal, cell), type, fine);
            if (routes.TryGetValue(key, out Route route))
            {
                Vector2[] points = route.Points;
                if (points != null && points.Length != 0 &&
                    !field.BlocksCorridor(start, points[0], radius) &&
                    !field.BlocksCorridor(points[^1], goal, radius))
                {
                    CacheHits++;
                    follower = new Follower { Route = route, GoalSector = goalSector };
                    return points[0];
                }
                if (!fine) continue;
                if (frame - route.Frame < RetryFrames ||
                    (start.DistanceSquaredTo(route.Start) < 0.0064f && goal.DistanceSquaredTo(route.Goal) < 0.0064f)) break;

                // Keep the existing key in the FIFO. Its representative endpoints can be refreshed.
                QueueRoute(key, start, goal);
                break;
            }
            QueueRoute(key, start, goal);
            break;
        }
        CacheMisses++;
        return start;
    }

    private void QueueRoute(RouteKey key, Vector2 start, Vector2 goal)
    {
        if (requests.Count >= capacity || !pending.Add(key)) return;
        requests.Enqueue(new Request(key, start, goal, frame));
    }

    private static Vector2I Sector(Vector2 point, float size) =>
        new(Mathf.FloorToInt(point.X / size), Mathf.FloorToInt(point.Y / size));

    private sealed class Graph
    {
        public float Radius { get; }
        private readonly Vector2[] points;
        private readonly float[,] edges;
        private readonly float[] distances;
        private readonly int[] previous;
        private readonly bool[] visited;

        public Graph(ObstacleField field, float radius)
        {
            Radius = radius;
            var corners = new List<Vector2>(field.Rectangles.Length * 4);
            var pad = new Vector2(radius + CornerClearance, radius + CornerClearance);
            foreach (ObstacleRectangle rectangle in field.Rectangles)
            {
                Vector2 min = rectangle.Min - pad, max = rectangle.Max + pad;
                AddCorner(corners, field, min);
                AddCorner(corners, field, max);
                AddCorner(corners, field, new Vector2(min.X, max.Y));
                AddCorner(corners, field, new Vector2(max.X, min.Y));
            }

            points = corners.ToArray();
            int count = points.Length;
            edges = new float[count, count];
            for (int i = 0; i < count; i++)
            for (int j = i + 1; j < count; j++)
            {
                float distance = field.BlocksCorridor(points[i], points[j], radius)
                    ? float.PositiveInfinity : points[i].DistanceTo(points[j]);
                edges[i, j] = edges[j, i] = distance;
            }
            distances = new float[count];
            previous = new int[count];
            visited = new bool[count];
        }

        private void AddCorner(List<Vector2> corners, ObstacleField field, Vector2 point)
        {
            float edge = DeploymentLayout.HalfWidth * DeploymentLayout.CellSize - Radius;
            if (Mathf.Abs(point.X) <= edge && Mathf.Abs(point.Y) <= edge && !field.IntersectsCircle(point, Radius))
                corners.Add(point);
        }

        // Solve once for a shared request. Distances are seeded by all corners reachable from the start.
        public Vector2[] FindRoute(ObstacleField field, Vector2 start, Vector2 goal)
        {
            Array.Clear(visited);
            Array.Fill(previous, -1);
            for (int i = 0; i < points.Length; i++)
                distances[i] = field.BlocksCorridor(start, points[i], Radius)
                    ? float.PositiveInfinity : start.DistanceTo(points[i]);

            float bestDistance = float.PositiveInfinity;
            int end = -1;
            for (int step = 0; step < points.Length; step++)
            {
                int current = -1;
                float distance = bestDistance;
                for (int i = 0; i < points.Length; i++)
                {
                    if (visited[i] || distances[i] >= distance) continue;
                    current = i;
                    distance = distances[i];
                }
                if (current < 0) break;
                visited[current] = true;
                float total = distance + points[current].DistanceTo(goal);
                if (total < bestDistance && !field.BlocksCorridor(points[current], goal, Radius))
                {
                    bestDistance = total;
                    end = current;
                }
                for (int i = 0; i < points.Length; i++)
                {
                    float through = distance + edges[current, i];
                    if (visited[i] || through >= distances[i]) continue;
                    distances[i] = through;
                    previous[i] = current;
                }
            }
            if (end < 0) return null;
            int length = 0;
            for (int i = end; i >= 0; i = previous[i]) length++;
            var path = new Vector2[length];
            for (int i = end; i >= 0; i = previous[i]) path[--length] = points[i];
            return path;
        }
    }
}
