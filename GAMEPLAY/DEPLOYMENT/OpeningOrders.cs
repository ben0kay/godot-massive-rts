// Opening orders restrict acquisition to local enemies until a route or hold finishes.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed class OpeningOrders
{
    private sealed class Progress
    {
        internal Vector3[] Route;
        internal int Waypoint;
        internal float HoldUntil;
    }

    private readonly Dictionary<int, Progress> active = new();
    private readonly List<int> finished = new();
    private float elapsed;
    private float halfWidth;

    internal static OpeningOrders Create(SoldierPlacement[] placements, int count, DeploymentOrderOverlay overlay, float half)
    {
        var result = new OpeningOrders { halfWidth = half };
        var routes = new Dictionary<DeploymentOrderOverlay.Order, Vector3[]>();
        for (int id = 0; id < count; id++)
        {
            var order = overlay.For(placements[id]);
            if (order == null) continue;
            if (order.Hold > 0f)
            {
                result.active.Add(id, new Progress { HoldUntil = order.Hold });
                continue;
            }
            if (!routes.TryGetValue(order, out var route))
            {
                route = new Vector3[order.Bent ? 8 : 1];
                for (int i = 0; i < route.Length; i++)
                {
                    Vector3 point = order.Point((i + 1f) / route.Length);
                    route[i] = new Vector3(Mathf.Clamp(point.X, -half, half), 0f, Mathf.Clamp(point.Z, -half, half));
                }
                routes.Add(order, route);
            }
            result.active.Add(id, new Progress { Route = route });
        }
        return result.active.Count == 0 ? null : result;
    }

    internal bool Active(int id) => active.ContainsKey(id);
    internal bool Holding(int id) => active[id].Route == null;
    internal Vector3 Goal(int id, float footprint)
    {
        Vector3 point = active[id].Route[active[id].Waypoint];
        float bound = Mathf.Max(0f, halfWidth - footprint);
        return new Vector3(Mathf.Clamp(point.X, -bound, bound), 0f, Mathf.Clamp(point.Z, -bound, bound));
    }

    internal void Arrive(int id)
    {
        var progress = active[id];
        if (++progress.Waypoint == progress.Route.Length) active.Remove(id);
    }

    internal bool Advance(UnitState units, float delta)
    {
        elapsed += delta;
        finished.Clear();
        foreach (var (id, progress) in active)
            if (units.Health[id] <= 0f || (progress.Route == null && elapsed >= progress.HoldUntil)) finished.Add(id);
        foreach (int id in finished) active.Remove(id);
        return active.Count > 0;
    }

    internal void Acquire(UnitState units, SpatialGrid grid, BenchmarkConfig config, ObstacleField cover, int id, int tick)
    {
        ref readonly var stats = ref units.Catalog[units.Type[id]];
        float radius = stats.Ranged ? units.RangedReach(id, stats) :
            Mathf.Max(config.CellSize * 2f, stats.MeleeDetectionReach + units.MaxFootprint);
        units.Target[id] = stats.TargetsUnits
            ? grid.FindEnemy(units, id, radius, config.QueryCandidateLimit, tick,
                stats.DirectFire(units.Catalog.Projectiles) ? cover : null, minimumRange: stats.MinimumFireRange)
            : -1;
    }
}
