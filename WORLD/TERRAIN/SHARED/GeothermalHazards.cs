// Runs vent cycles and only steps soldiers currently launched by a vent.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed class GeothermalHazards
{
    private sealed class Cycle
    {
        internal GeothermalVent Vent;
        internal float Clock, LastActivity = -1f;
        internal bool InitialBurst;
    }

    private record struct Flight(int Id, Vector2 Start, Vector2 End, float Time, float Duration, float Height);
    private readonly List<Cycle> vents = new();
    private readonly List<Flight> flights = new();
    private readonly bool[] airborne;

    private GeothermalHazards(int capacity) => airborne = new bool[capacity];

    internal static GeothermalHazards Create(Node3D map, UnitState units)
    {
        if (map?.GetNodeOrNull<CommandTerrain>("Terrain") is not { } terrain) return null;
        GeothermalHazards hazards = null;
        foreach (Node item in terrain.GetChildren())
        {
            if (item is not GeothermalVent vent) continue;
            hazards ??= new GeothermalHazards(units.Position.Length);
            if (hazards.vents.Count == GeothermalVent.MaxPerMap) break;
            vent.Seat(terrain.Field);
            vent.ShowRadius(false);
            float period = vent.Rest + vent.Warning + vent.Burst;
            float offset = GeothermalVent.Setting(vent.InitialOffset, 0f, -120f, 120f);
            float phase = Mathf.PosMod(offset, period);
            hazards.vents.Add(new Cycle { Vent = vent, Clock = phase, InitialBurst = phase >= vent.Rest + vent.Warning });
        }
        return hazards;
    }

    internal bool IsAirborne(int id) => airborne[id];

    // =========================================================
    internal bool Step(UnitState units, SpatialGrid grid, ObstacleField obstacles, float halfWidth, float dt, bool gridCurrent = false)
    {
        bool moved = flights.Count > 0;
        for (int i = flights.Count - 1; i >= 0; i--)
        {
            var flight = flights[i];
            float time = flight.Time + dt;
            float progress = Mathf.Min(1f, time / flight.Duration);
            if (units.Health[flight.Id] <= 0f || progress >= 1f)
            {
                if (units.Health[flight.Id] > 0f)
                {
                    var stats = units.Catalog[units.Type[flight.Id]];
                    Vector2 landing = SafeEnd(obstacles, halfWidth, stats, flight.Start, flight.End);
                    SetPosition(units, flight.Id, landing, 0f);
                }
                airborne[flight.Id] = false;
                flights[i] = flights[^1];
                flights.RemoveAt(flights.Count - 1);
            }
            else
            {
                SetPosition(units, flight.Id, flight.Start.Lerp(flight.End, progress),
                    4f * flight.Height * progress * (1f - progress));
                flights[i] = flight with { Time = time };
            }
        }

        bool gridReady = gridCurrent && !moved;
        foreach (var cycle in vents)
        {
            var vent = cycle.Vent;
            float startBurst = vent.Rest + vent.Warning;
            float period = startBurst + vent.Burst;
            float next = cycle.Clock + dt;
            if (cycle.InitialBurst || (cycle.Clock < startBurst && next >= startBurst))
            {
                if (!gridReady) { grid.Rebuild(units); gridReady = true; }
                cycle.InitialBurst = false;
                moved |= Launch(vent, units, grid, obstacles, halfWidth);
            }
            cycle.Clock = Mathf.PosMod(next, period);
            float activity = cycle.Clock >= startBurst ? 1f
                : cycle.Clock <= vent.Rest ? 0f
                : Mathf.Floor((cycle.Clock - vent.Rest) / Mathf.Max(0.001f, vent.Warning) * 8f) / 16f;
            if (activity == cycle.LastActivity) continue;
            cycle.LastActivity = activity;
            vent.SetActivity(activity);
        }
        return moved;
    }

    private bool Launch(GeothermalVent vent, UnitState units, SpatialGrid grid, ObstacleField obstacles, float halfWidth)
    {
        Vector3 center = vent.GlobalPosition;
        Vector2 centerXZ = ObstacleField.Xz(center);
        float radius = vent.EffectiveRadius;
        var nearby = grid.GetNearbySoldiers(center, radius);
        bool changed = false;
        while (nearby.MoveNext())
        {
            int id = nearby.Current;
            if (units.Health[id] <= 0f || airborne[id]) continue;
            Vector2 start = ObstacleField.Xz(units.Position[id]);
            Vector2 away = start - centerXZ;
            if (away.LengthSquared() > radius * radius) continue;
            var stats = units.Catalog[units.Type[id]];
            float tier = stats.SizeTier;
            if (tier >= 4f) continue;

            if (vent.Damage > 0f)
            {
                units.Damage(id, vent.Damage, DamageRules.NeutralType);
                changed = true;
                if (units.Health[id] <= 0f) continue;
            }
            float lift = tier <= 1f ? 3f : tier <= 1.5f ? 1.85f : tier <= 2f ? 0.95f : 0.30f;
            lift *= vent.Power;
            Vector2 direction = away.LengthSquared() > 0.0001f ? away.Normalized()
                : Vector2.Right.Rotated((id % 16) * Mathf.Tau / 16f);
            Vector2 end = SafeEnd(obstacles, halfWidth, stats, start, start + direction * (lift * 0.75f));
            airborne[id] = true;
            flights.Add(new Flight(id, start, end, 0f, Mathf.Clamp(Mathf.Sqrt(8f * lift / 24f), 0.25f, 1.8f), lift));
            changed = true;
        }
        return changed;
    }

    private static Vector2 SafeEnd(ObstacleField obstacles, float halfWidth, in UnitStats stats, Vector2 start, Vector2 end)
    {
        float limit = Mathf.Max(0f, halfWidth - stats.Footprint);
        end = new Vector2(Mathf.Clamp(end.X, -limit, limit), Mathf.Clamp(end.Y, -limit, limit));
        var field = obstacles?.ForTier(stats.SizeTier);
        if (field == null) return end;
        end = field.ClipMove(start, end, stats.Footprint);
        if (!field.IntersectsCircle(end, stats.Footprint)) return end;
        if (field.TryFindClearanceExit(end, stats.Footprint, out Vector2 exit) &&
            Mathf.Abs(exit.X) <= limit && Mathf.Abs(exit.Y) <= limit) return exit;
        return start;
    }

    private static void SetPosition(UnitState units, int id, Vector2 point, float lift)
    {
        float ground = units.Grounding?.Field.At(point.X, point.Y) ?? 0f;
        units.Position[id] = new Vector3(point.X, ground + lift, point.Y);
        units.Dirty[id] = true;
    }
}
