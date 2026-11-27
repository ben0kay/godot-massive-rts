// Shares static corner graphs and budgeted sector routes across soldiers.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

public sealed class ObstacleNavigation
{
    private const float CornerClearance = 0.35f;
    private const float ArrivalDistance = 0.004f;
    private const int RetryFrames = 30;
    private const float CorridorMargin = 0.5f;
    private const float ProofInset = CorridorMargin + 0.005f;

    private struct ApproachMemo
    {
        public Vector2 Start, Goal;
        public float Stop;
        public bool Valid, Clear, HasMargin;
    }
    private readonly record struct RouteKey(Vector2I From, Vector2I To, byte Type, int Detail, int Soldier);
    private readonly record struct Request(RouteKey Key, Vector2 Start, Vector2 Goal, float Stop, int Frame);
    private sealed record Route(Vector2[] Points, Vector2 Start, Vector2 Goal, float Stop, int Frame, bool EndsAtAttackPoint);
    private struct Follower
    {
        public Route Route;
        public int Next;
        public Vector2I GoalSector;
        public Vector2 VerifiedStart;
        public bool WaypointVerified, WaypointHasMargin;
        public Vector2? ClearanceExit;
    }

    private readonly ObstacleField[] fields;
    private readonly Graph[] graphs;
    private readonly RuntimeUnitCatalog catalog;
    private readonly Follower[] followers;
    private readonly ApproachMemo[] approachMemos;
    public bool ReuseCorridors { get; set; } = true;
    public bool ReuseWaypoints { get; set; }
    public int WaypointReuses { get; private set; }
    public int CorridorReuses { get; private set; }
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
    public int RouteWaits { get; private set; }
    public int CachedRouteStalls { get; private set; }
    public int QueueFullWaits { get; private set; }
    public string LastIndividualFailure { get; private set; } = "";

    public ObstacleNavigation(ObstacleField field, RuntimeUnitCatalog catalog, int soldiers,
        float sectorSize, int capacity, int budget, bool deferGraphs = false)
    {
        this.catalog = catalog;
        fields = new ObstacleField[catalog.Count];
        this.sectorSize = sectorSize;
        this.capacity = capacity;
        this.budget = budget;
        graphs = new Graph[catalog.Count];
        for (int i = 0; i < graphs.Length; i++)
        {
            fields[i] = field.ForTier(catalog[i].SizeTier);
            if (!deferGraphs) graphs[i] = new Graph(fields[i], catalog[i].Footprint);
        }
        followers = new Follower[soldiers];
        approachMemos = new ApproachMemo[soldiers];
    }

    public void ResetCounters()
    {
        CacheHits = CacheMisses = RoutesBuilt = MaxWaitFrames = CorridorReuses = WaypointReuses = 0;
        RouteWaits = CachedRouteStalls = QueueFullWaits = 0;
    }

    public void AdvanceFrame() => frame++;

    public void BuildQueuedRoutes()
    {
        frame++;
        bool builtGraph = false;
        for (int i = 0; i < budget && requests.TryPeek(out Request request); i++)
        {
            byte type = request.Key.Type;
            var field = fields[type];
            var graph = graphs[type];
            if (graph == null)
            {
                // After a wreck refresh, build at most one requested unit graph this update.
                if (builtGraph) break;
                graph = graphs[type] = new Graph(field, catalog[type].Footprint);
                builtGraph = true;
            }
            requests.Dequeue();
            pending.Remove(request.Key);
            Vector2[] points = graph.FindRoute(field, request.Start, request.Goal, request.Stop, out bool hasReachableCorner,
                out bool endsAtAttackPoint, out int validAttackPoints);
            if (points == null && request.Key.Soldier >= 0 && request.Key.Detail == 2)
            {
                bool overlaps = field.IntersectsCircle(request.Start, graph.Radius);
                bool contact = overlaps || field.HasClearanceContact(request.Start, graph.Radius);
                bool foundExit = false;
                if (contact && field.TryFindClearanceExit(request.Start, graph.Radius, out Vector2 exit))
                {
                    followers[request.Key.Soldier].ClearanceExit = exit;
                    foundExit = true;
                }
                LastIndividualFailure = $"Unit {request.Key.Soldier} {request.Start} → {request.Goal} · overlap {overlaps} · edge contact {contact} · exit found {foundExit} · reachable corner {hasReachableCorner} · valid attack points {validAttackPoints}";
            }
            if (!routes.ContainsKey(request.Key))
            {
                if (routes.Count == capacity) routes.Remove(cacheOrder.Dequeue());
                cacheOrder.Enqueue(request.Key);
            }
            routes[request.Key] = new Route(points, request.Start, request.Goal, request.Stop, frame, endsAtAttackPoint);
            RoutesBuilt++;
            MaxWaitFrames = Math.Max(MaxWaitFrames, frame - request.Frame);
        }
    }

    // Hold for a verified route when the direct corridor is blocked.
    public Vector2 Steer(int id, byte type, Vector2 start, Vector2 goal, float stop,
        out bool detouring, out bool routeCorner)
    {
        var field = fields[type];
        float radius = catalog[type].Footprint;
        ref Follower follower = ref followers[id];
        routeCorner = false;
        if (follower.ClearanceExit is Vector2 exit)
        {
            if (field.HasClearanceContact(start, radius) && field.CanExitClearance(start, exit, radius))
            {
                detouring = routeCorner = true;
                return exit;
            }
            follower = default;
        }
        if (CanApproachSoldier(field, id, start, goal, radius, stop))
        {
            follower = default;
            detouring = false;
            return goal;
        }

        detouring = true;
        Vector2I goalSector = Sector(goal, sectorSize);
        if (follower.Route != null && (follower.Route.EndsAtAttackPoint
            ? CanFinish(field, follower.Route, goal, radius, stop)
            : follower.GoalSector == goalSector || CanFinish(field, follower.Route, goal, radius, stop)))
        {
            follower.GoalSector = goalSector;
            Vector2[] points = follower.Route.Points;
            while (follower.Next < points.Length &&
                start.DistanceSquaredTo(points[follower.Next]) <= ArrivalDistance * ArrivalDistance)
            {
                follower.Next++;
                follower.WaypointVerified = false;
            }
            if (ReuseWaypoints && follower.WaypointVerified && follower.Next < points.Length &&
                CanReuseWaypoint(follower, start, points[follower.Next]))
            {
                WaypointReuses++;
                routeCorner = true;
                return points[follower.Next];
            }
            int last = Math.Min(points.Length - 1, follower.Next + 3);
            for (int next = last; next >= follower.Next; next--)
            {
                if (field.BlocksCorridor(start, points[next], radius)) continue;
                RememberWaypoint(field, ref follower, next, start, points[next], radius);
                routeCorner = true;
                return points[next];
            }
            // Separation may leave a soldier with only the previous corner reachable.
            if (follower.Next > 0 && follower.Next < points.Length &&
                !field.BlocksCorridor(start, points[follower.Next - 1], radius))
            {
                RememberWaypoint(field, ref follower, follower.Next - 1, start, points[follower.Next - 1], radius);
                routeCorner = true;
                return points[follower.Next];
            }
        }
        follower = default;

        for (int detail = 0; detail < 3; detail++)
        {
            bool individual = detail == 2;
            float cell = detail == 0 ? sectorSize : 0.5f;
            var key = new RouteKey(Sector(start, cell), Sector(goal, cell), type, detail, individual ? id : -1);
            if (routes.TryGetValue(key, out Route route))
            {
                Vector2[] points = route.Points;
                if (points != null && points.Length != 0 && CanFinish(field, route, goal, radius, stop))
                {
                    int next = 0;
                    while (next < points.Length &&
                        start.DistanceSquaredTo(points[next]) <= ArrivalDistance * ArrivalDistance)
                        next++;
                    if (next < points.Length && !field.BlocksCorridor(start, points[next], radius))
                    {
                        CacheHits++;
                        follower = new Follower { Route = route, Next = next, GoalSector = goalSector };
                        RememberWaypoint(field, ref follower, next, start, points[next], radius);
                        routeCorner = true;
                        return points[next];
                    }
                }
                // Even a half-unit sector can straddle a rock corner or shoreline.
                // An unusable shared result must not freeze another soldier in that sector.
                if (!individual) continue;
                if (frame - route.Frame < RetryFrames ||
                    (points == null && start.DistanceSquaredTo(route.Start) < 0.0064f && goal.DistanceSquaredTo(route.Goal) < 0.0064f &&
                    Mathf.Abs(stop - route.Stop) < 0.08f))
                {
                    CacheMisses++;
                    CachedRouteStalls++;
                    return start;
                }

                // Keep the existing key in the FIFO. Its representative endpoints can be refreshed.
                QueueRoute(key, start, goal, stop);
                break;
            }
            QueueRoute(key, start, goal, stop);
            break;
        }
        CacheMisses++;
        RouteWaits++;
        return start;
    }

    // A tower needs an exact attack position. A missing result means the shared queue
    // has not answered yet, including when the queue was full.
    internal bool? ProbeStructureRoute(int id, byte type, Vector2 start, Vector2 goal)
    {
        static Vector2I Exact(Vector2 point) =>
            new(BitConverter.SingleToInt32Bits(point.X), BitConverter.SingleToInt32Bits(point.Y));

        var key = new RouteKey(Exact(start), Exact(goal), type, 3, id);
        if (!routes.TryGetValue(key, out Route route))
        {
            QueueRoute(key, start, goal, 0f);
            return null;
        }
        return route.Points != null;
    }

    private bool CanFinish(ObstacleField field, Route route, Vector2 goal, float radius, float stop)
    {
        Vector2 last = route.Points[^1];
        if (!route.EndsAtAttackPoint) return CanApproach(field, last, goal, radius, stop);
        if (stop <= 0f || last.DistanceSquaredTo(goal) > stop * stop) return false;
        return goal == route.Goal || !field.BlocksCorridor(last, goal, 0f);
    }

    private static bool CanReuseWaypoint(Follower follower, Vector2 start, Vector2 waypoint)
    {
        if (start == follower.VerifiedStart) return true;
        if (!follower.WaypointHasMargin) return false;
        Vector2 segment = waypoint - follower.VerifiedStart;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0f) return false;
        Vector2 offset = start - follower.VerifiedStart;
        float progress = Mathf.Clamp(offset.Dot(segment) / lengthSquared, 0f, 1f);
        return (offset - segment * progress).LengthSquared() <= CorridorMargin * CorridorMargin;
    }

    private void RememberWaypoint(ObstacleField field, ref Follower follower, int index, Vector2 start, Vector2 waypoint, float radius)
    {
        follower.Next = index;
        follower.WaypointVerified = ReuseWaypoints;
        if (!ReuseWaypoints) return;
        follower.VerifiedStart = start;
        follower.WaypointHasMargin = !field.BlocksCorridor(start, waypoint, radius + ProofInset);
    }

    private bool CanApproachSoldier(ObstacleField field, int id, Vector2 start, Vector2 goal, float radius, float stop)
    {
        if (!ReuseCorridors) return CanApproach(field, start, goal, radius, stop);
        ref ApproachMemo memo = ref approachMemos[id];
        if (memo.Valid &&
            ((memo.Start == start && memo.Goal == goal && memo.Stop == stop) ||
                (memo.HasMargin && memo.Start.DistanceSquaredTo(start) <= CorridorMargin * CorridorMargin &&
                    memo.Goal.DistanceSquaredTo(goal) <= CorridorMargin * CorridorMargin)))
        {
            CorridorReuses++;
            return memo.Clear;
        }

        bool clear = CanApproach(field, start, goal, radius, stop);
        // Enlarged clearance proves the whole segment is clear. An inset wall hit
        // proves that even the shorter approach plus its sightline cannot pass.
        bool hasMargin = clear ? !field.BlocksCorridor(start, goal, radius + ProofInset)
            : field.CrossesWallInterior(start, goal, ProofInset);
        memo = new ApproachMemo { Start = start, Goal = goal, Stop = stop, Valid = true, Clear = clear, HasMargin = hasMargin };
        return clear;
    }

    // An enemy can stand closer to a wall than a larger attacker can. Test the firing
    // position before the enemy center, and require the remaining line to be unobstructed.
    private static bool CanApproach(ObstacleField field, Vector2 from, Vector2 goal, float radius, float stop)
    {
        if (!field.BlocksCorridor(from, goal, radius)) return true;
        if (stop <= 0f) return false;
        float distance = from.DistanceTo(goal);
        Vector2 end = distance > stop ? goal.Lerp(from, stop / distance) : from;
        return !field.BlocksCorridor(from, end, radius) && !field.BlocksCorridor(end, goal, 0f);
    }

    private void QueueRoute(RouteKey key, Vector2 start, Vector2 goal, float stop)
    {
        if (pending.Contains(key)) return;
        if (requests.Count >= capacity)
        {
            QueueFullWaits++;
            return;
        }
        pending.Add(key);
        requests.Enqueue(new Request(key, start, goal, stop, frame));
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
            foreach (ObstacleRectangle rectangle in field.Rectangles)
            {
                Vector2 min = rectangle.Min, max = rectangle.Max;
                AddCorner(corners, field, min, new Vector2(-1, -1));
                AddCorner(corners, field, max, new Vector2(1, 1));
                AddCorner(corners, field, new Vector2(min.X, max.Y), new Vector2(-1, 1));
                AddCorner(corners, field, new Vector2(max.X, min.Y), new Vector2(1, -1));
            }

            foreach (Rect2 bridge in field.Bridges)
            {
                Vector2 center = bridge.GetCenter();
                // Cache only approaches whose cross-section fits this catalogue footprint.
                if (bridge.Size.X >= bridge.Size.Y)
                    AddBridgeApproaches(bridge.Size.Y, center, new Vector2(bridge.Size.X * 0.5f, 0f));
                if (bridge.Size.Y >= bridge.Size.X)
                    AddBridgeApproaches(bridge.Size.X, center, new Vector2(0f, bridge.Size.Y * 0.5f));
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

            void AddBridgeApproaches(float width, Vector2 center, Vector2 halfSpan)
            {
                if (width <= radius * 2f) return;
                Vector2 a = center - halfSpan, b = center + halfSpan;
                float edge = field.Boundary - radius;
                if (Mathf.Abs(a.X) >= edge || Mathf.Abs(a.Y) >= edge || Mathf.Abs(b.X) >= edge ||
                    Mathf.Abs(b.Y) >= edge || field.BlocksCorridor(a, b, radius)) return;
                corners.Add(a);
                corners.Add(b);
            }
        }

        private void AddCorner(List<Vector2> corners, ObstacleField field, Vector2 corner, Vector2 direction)
        {
            float edge = field.Boundary - Radius - 0.005f;
            if (edge <= 0f) return;
            var bounds = Vector2.One * edge;
            Vector2 point = (corner + direction * (Radius + CornerClearance)).Clamp(-bounds, bounds);
            if (field.IntersectsCircle(point, Radius))
            {
                // A stepped bank or map edge can consume the usual waypoint margin.
                point = (corner + direction * (Radius + 0.025f)).Clamp(-bounds, bounds);
                if (field.IntersectsCircle(point, Radius)) return;
            }
            corners.Add(point);
        }

        // Solve once for a shared request. Distances are seeded by all corners reachable from the start.
        public Vector2[] FindRoute(ObstacleField field, Vector2 start, Vector2 goal, float stop,
            out bool hasReachableCorner, out bool endsAtAttackPoint, out int validAttackPoints)
        {
            endsAtAttackPoint = false;
            validAttackPoints = 0;
            Array.Clear(visited);
            Array.Fill(previous, -1);
            hasReachableCorner = false;
            for (int i = 0; i < points.Length; i++)
            {
                bool blocked = field.BlocksCorridor(start, points[i], Radius);
                distances[i] = blocked ? float.PositiveInfinity : start.DistanceTo(points[i]);
                hasReachableCorner |= !blocked;
            }
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
                float total = distance + Mathf.Max(0f, points[current].DistanceTo(goal) - stop);
                if (total < bestDistance && CanApproach(field, points[current], goal, Radius, stop))
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
            if (end < 0)
            {
                if (stop <= 0.01f) return null;
                if (!TryFindAttackPoint(field, start, goal, stop, out end, out Vector2 attackPoint,
                    out validAttackPoints)) return null;
                endsAtAttackPoint = true;
                return BuildPath(end, attackPoint);
            }
            return BuildPath(end);
        }

        // The ordinary search already settled the reachable corners when it found no finish.
        // Only those failed requests pay for the eight alternate bearings.
        private bool TryFindAttackPoint(ObstacleField field, Vector2 start, Vector2 goal, float stop,
            out int end, out Vector2 attackPoint, out int validAttackPoints)
        {
            const int bearings = 8;
            float angle = (start - goal).Angle();
            float edge = field.Boundary - Radius;
            float best = float.PositiveInfinity;
            end = -1;
            attackPoint = default;
            validAttackPoints = 0;
            for (int a = 0; a < bearings; a++)
            {
                Vector2 point = goal + Vector2.FromAngle(angle + a * (Mathf.Tau / bearings)) * (stop - 0.01f);
                if (Mathf.Abs(point.X) > edge || Mathf.Abs(point.Y) > edge ||
                    field.IntersectsCircle(point, Radius) || field.BlocksCorridor(point, goal, 0f)) continue;
                validAttackPoints++;

                float direct = start.DistanceTo(point);
                if (direct >= best) continue;
                if (!field.BlocksCorridor(start, point, Radius))
                {
                    best = direct;
                    end = -1;
                    attackPoint = point;
                    continue;
                }
                for (int i = 0; i < points.Length; i++)
                {
                    if (!visited[i] || distances[i] >= best) continue;
                    float cost = distances[i] + points[i].DistanceTo(point);
                    if (cost >= best || field.BlocksCorridor(points[i], point, Radius)) continue;
                    best = cost;
                    end = i;
                    attackPoint = point;
                }
            }
            return float.IsFinite(best);
        }

        private Vector2[] BuildPath(int end, Vector2? attackPoint = null)
        {
            int length = attackPoint.HasValue ? 1 : 0;
            for (int i = end; i >= 0; i = previous[i]) length++;
            var path = new Vector2[length];
            if (attackPoint.HasValue) path[--length] = attackPoint.Value;
            for (int i = end; i >= 0; i = previous[i]) path[--length] = points[i];
            return path;
        }
    }
}
