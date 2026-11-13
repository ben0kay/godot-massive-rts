// Runs the painted defenders' local acquisition and movement around their original posts.
using Godot;

namespace MassiveRts;

public enum GuardOrder { AutoAttack, Hold, Return, Counterattack }

public sealed class GuardGroup
{
    private readonly int start;
    private readonly Vector3[] posts;
    private readonly float radius, radiusSquared;
    private readonly bool returnToPost, counterattack;
    private int lastContact = -1;

    public GuardGroup(int start, SoldierPlacement[] placements, float radius, GuardOrder order)
    {
        this.start = start;
        this.radius = radius;
        radiusSquared = radius * radius;
        returnToPost = order == GuardOrder.Return;
        counterattack = order == GuardOrder.Counterattack;
        posts = new Vector3[placements.Length - start];
        for (int i = start; i < placements.Length; i++) posts[i - start] = placements[i].Position();
    }

    public bool Acquire(UnitState units, SpatialGrid grid, BenchmarkConfig config, int tick,
        float acquisitionSeconds, ObstacleField obstacles)
    {
        int interval = Mathf.Max(1, Mathf.RoundToInt(config.TargetRefreshTicks * BenchmarkConfig.TargetingStepSeconds / acquisitionSeconds));
        int offset = (tick % interval - start % interval + interval) % interval;
        for (int id = start + offset; id < units.Count; id += interval)
        {
            if (units.Health[id] <= 0f) continue;
            Vector3 post = posts[id - start];
            // Include height in the search bound while keeping the guard area on the ground plane.
            units.Target[id] = grid.FindEnemy(units, id, float.PositiveInfinity, counterattack ? int.MaxValue : config.QueryCandidateLimit, tick,
                guardPost: post, guardRadius: radius);
            if (units.Target[id] >= 0) lastContact = tick;
        }

        int faction = units.Faction[start];
        return counterattack && lastContact >= 0 && tick - lastContact >= interval &&
            units.FactionAlive[faction] > 0 && units.Alive > units.FactionAlive[faction];
    }

    public void Move(UnitState units, float dt, float separationGap, ObstacleField obstacles,
        ObstacleNavigation navigation, byte[] movementFlags)
    {
        Vector2 turnStep = SoldierFacing.TurnStep(dt);
        for (int id = start; id < units.Count; id++)
        {
            if (movementFlags != null) movementFlags[id] = 0;
            if (units.Health[id] <= 0f) continue;
            Vector3 post = posts[id - start];
            Vector2 anchor = ObstacleField.Xz(post);
            Vector3 position = units.Position[id];
            Vector2 from = ObstacleField.Xz(position);
            int target = units.Target[id];
            bool engaging = target >= 0 && units.Health[target] > 0f &&
                anchor.DistanceSquaredTo(ObstacleField.Xz(units.Position[target])) <= radiusSquared;
            if (!engaging)
            {
                units.Target[id] = -1;
                if (!returnToPost) continue;
            }
            ref readonly UnitStats type = ref units.Catalog[units.Type[id]];
            Vector2 destination = engaging ? ObstacleField.Xz(units.Position[target]) : anchor;
            float stop = 0f;
            if (engaging)
            {
                float reach = type.Ranged ? type.Reach :
                    CombatSystem.MeleeContact(type.Footprint, units.Catalog[units.Type[target]].Footprint, separationGap);
                bool indirect = type.Ranged &&
                    units.Catalog.Projectiles.Flight(type.ProjectileType).ObstacleFire == ObstacleFireMode.IndirectFire;
                bool clear = indirect || obstacles?.BlocksSight(position, units.Position[target]) != true;
                float minimum = type.MinimumFireRange;
                if (minimum > 0f && from.DistanceSquaredTo(destination) < minimum * minimum)
                {
                    Vector2 away = from - destination;
                    if (away == Vector2.Zero) away = Vector2.Right;
                    destination = from + away.Normalized() * minimum;
                }
                else if (position.DistanceSquaredTo(units.Position[target]) <= reach * reach &&
                    clear)
                    continue;
                else if (clear) stop = Mathf.Max(0f, reach - 0.02f);
            }
            if (navigation != null)
            {
                Vector2 waypoint = navigation.Steer(id, units.Type[id], from, destination, stop, out bool detouring, out _);
                if (detouring) { destination = waypoint; stop = 0f; }
            }
            Vector2 offset = destination - from;
            float distance = offset.Length();
            if (distance <= Mathf.Max(stop, 0.00001f)) continue;
            Vector2 direction = offset / distance;
            Vector2 next = from + direction * Mathf.Min(type.Speed * dt, distance - stop);
            float currentRadiusSquared = anchor.DistanceSquaredTo(from);
            if (anchor.DistanceSquaredTo(next) > Mathf.Max(radiusSquared, currentRadiusSquared)) continue;
            if (obstacles != null) next = obstacles.ClipMove(from, next, type.Footprint);
            if (next == from) continue;
            if (movementFlags != null) movementFlags[id] = 1;
            if (type.Shape != UnitShape.Circle) SoldierFacing.Turn(units, id, direction, turnStep);
            units.Position[id] = new Vector3(next.X, position.Y, next.Y);
            units.Grounding?.MarkMoved(id);
            units.Dirty[id] = true;
        }
    }
}
