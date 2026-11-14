// Runs the painted defenders' local acquisition and movement around their original posts.
using Godot;

namespace MassiveRts;

public enum GuardOrder { AutoAttack, Hold, Return, Counterattack }

public sealed class GuardGroup
{
    public int Start { get; }
    public int End { get; }
    private readonly Vector3[] posts;
    private readonly float radius, radiusSquared;
    private readonly bool returnToPost, counterattack;
    private int lastContact = -1;

    public GuardGroup(int start, int end, SoldierPlacement[] placements, float radius, GuardOrder order)
    {
        Start = start;
        End = end;
        this.radius = radius;
        radiusSquared = radius * radius;
        returnToPost = order == GuardOrder.Return;
        counterattack = order == GuardOrder.Counterattack;
        posts = new Vector3[end - start];
        for (int i = start; i < end; i++) posts[i - start] = placements[i].Position();
    }

    public bool Acquire(UnitState units, SpatialGrid grid, BenchmarkConfig config, int tick,
        float acquisitionSeconds, ObstacleField obstacles)
    {
        int interval = Mathf.Max(1, Mathf.RoundToInt(config.TargetRefreshTicks * BenchmarkConfig.TargetingStepSeconds / acquisitionSeconds));
        int offset = (tick % interval - Start % interval + interval) % interval;
        for (int id = Start + offset; id < End; id += interval)
        {
            if (units.Health[id] <= 0f) continue;
            Vector3 post = posts[id - Start];
            ref readonly UnitStats type = ref units.Catalog[units.Type[id]];
            float engagementRadius = type.Ranged ? type.Reach : radius;
            int target = units.Target[id];
            if (!IsEngaging(units, target, ObstacleField.Xz(post), engagementRadius * engagementRadius))
            {
                units.Target[id] = grid.FindEnemy(units, id, float.PositiveInfinity,
                    counterattack ? int.MaxValue : config.QueryCandidateLimit, tick,
                    guardPost: post, guardRadius: type.Ranged ? engagementRadius * 0.5f : engagementRadius);
            }
            if (units.Target[id] >= 0) lastContact = tick;
        }

        int faction = units.Faction[Start];
        return counterattack && lastContact >= 0 && tick - lastContact >= interval &&
            units.FactionAlive[faction] > 0 && units.Alive > units.FactionAlive[faction];
    }

    private static bool IsEngaging(UnitState units, int target, Vector2 post, float rangeSquared) =>
        target >= 0 && units.Health[target] > 0f &&
        post.DistanceSquaredTo(ObstacleField.Xz(units.Position[target])) <= rangeSquared;

    public void Move(UnitState units, float dt, float separationGap, ObstacleField obstacles,
        ObstacleNavigation navigation, byte[] movementFlags, CrowdMovement crowd = default)
    {
        Vector2 turnStep = SoldierFacing.TurnStep(dt);
        for (int id = Start; id < End; id++)
        {
            if (movementFlags != null) movementFlags[id] = 0;
            if (units.Health[id] <= 0f) continue;
            Vector3 post = posts[id - Start];
            Vector2 anchor = ObstacleField.Xz(post);
            Vector3 position = units.Position[id];
            Vector2 from = ObstacleField.Xz(position);
            int target = units.Target[id];
            ref readonly UnitStats type = ref units.Catalog[units.Type[id]];
            float engagementRadiusSquared = type.Ranged ? type.Reach * type.Reach : radiusSquared;
            bool engaging = IsEngaging(units, target, anchor, engagementRadiusSquared);
            if (!engaging)
            {
                units.Target[id] = -1;
                if (!returnToPost) continue;
            }
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
            if (crowd.Enabled) next = crowd.Clip(units, id, from, next, type.Footprint);
            if (next == from) continue;
            if (movementFlags != null) movementFlags[id] = 1;
            if (type.Shape != UnitShape.Circle) SoldierFacing.Turn(units, id, direction, turnStep);
            units.Position[id] = new Vector3(next.X, position.Y, next.Y);
            units.Grounding?.MarkMoved(id);
            units.Dirty[id] = true;
        }
    }
}
