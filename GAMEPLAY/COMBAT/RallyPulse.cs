// Applies a timed, non-stacking rally to nearby allied ground units.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed class RallyPulse
{
    internal const float Radius = 14.4f;
    private const float Duration = 5f;
    private const float Cooldown = 8f;
    private const float Boost = 1.15f;

    private readonly record struct Commander(int Id, float ReadyAt);
    private readonly List<Commander> commanders = new();
    private readonly List<int> recipients = new();
    private readonly float[] expires;
    private float clock;

    internal RallyPulse(int capacity) => expires = new float[capacity];

    internal IReadOnlyList<int> Recipients => recipients;
    internal float RemainingFraction(int id) => Mathf.Clamp((expires[id] - clock) / Duration, 0f, 1f);
    internal float Multiplier(int id) => expires[id] > 0f ? Boost : 1f;

    internal void AddCommander(int id) => commanders.Add(new Commander(id, clock + 3f));

    internal void Step(UnitState units, SpatialGrid grid, float delta, ShockwaveRenderer effects)
    {
        clock += delta;
        for (int i = recipients.Count - 1; i >= 0; i--)
        {
            int id = recipients[i];
            if (units.Health[id] > 0f && expires[id] > clock) continue;
            if (units.Health[id] > 0f) units.Health[id] /= Boost;
            units.Dirty[id] = true;
            expires[id] = 0f;
            recipients[i] = recipients[^1];
            recipients.RemoveAt(recipients.Count - 1);
        }

        for (int i = commanders.Count - 1; i >= 0; i--)
        {
            var commander = commanders[i];
            int id = commander.Id;
            if (units.Health[id] <= 0f)
            {
                commanders[i] = commanders[^1];
                commanders.RemoveAt(commanders.Count - 1);
                continue;
            }
            if (clock < commander.ReadyAt) continue;

            Vector3 center = units.Position[id];
            Vector2 origin = ObstacleField.Xz(center);
            var allies = grid.GetNearbyAllies(origin, Radius, units.Faction[id]);
            bool rallied = false;
            while (allies.MoveNext())
            {
                int ally = allies.Current;
                if (ally == id || units.Health[ally] <= 0f ||
                    origin.DistanceSquaredTo(ObstacleField.Xz(units.Position[ally])) > Radius * Radius) continue;
                if (expires[ally] <= 0f)
                {
                    units.Health[ally] *= Boost;
                    units.Dirty[ally] = true;
                    recipients.Add(ally);
                }
                expires[ally] = clock + Duration;
                rallied = true;
            }
            commanders[i] = commander with { ReadyAt = clock + (rallied ? Cooldown : 1f) };
            if (rallied) effects?.EmitRally(center, Radius, units.Faction[id]);
        }
    }
}
