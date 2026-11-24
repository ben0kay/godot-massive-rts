// Maps the authored staging grid to a battlefield edge and checks ingress clearance.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal static class RedReserveFormation
{
    internal const string ContainerName = "RedReserveUnits";
    internal const float HalfWidth = 128f;
    internal const float EntrySpeedFraction = 0.75f;
    internal enum EntryEdge { Automatic, North, East, South, West }
    internal readonly record struct Lane(Vector3 Start, Vector3 Goal, Vector2 Facing, float Distance, bool Clear);

    internal static IEnumerable<CommandUnitPlacement> Groups(CommandSandbox map)
    {
        var container = map.GetNodeOrNull<Node3D>(ContainerName);
        if (container == null) yield break;
        foreach (var child in container.GetChildren())
            if (child is CommandUnitPlacement group) yield return group;
    }

    internal static EntryEdge ResolveEdge(AutoBattlerRules rules)
    {
        if (rules.RedFormationEdge is >= 1 and <= 4) return (EntryEdge)rules.RedFormationEdge;
        return rules.LineAxis == AutoBattlerRules.DeploymentAxis.X
            ? rules.FlipBlueSide ? EntryEdge.West : EntryEdge.East
            : rules.FlipBlueSide ? EntryEdge.North : EntryEdge.South;
    }

    internal static Lane Project(Vector3 local, float radius, CommandSandbox map, ObstacleField field, float leadIn, float handoffRadius)
    {
        float half = map.MapSize * 0.5f;
        Vector2 outward = ResolveEdge(map.AutoBattlerRules) switch
        {
            EntryEdge.North => -Vector2.Down,
            EntryEdge.East => Vector2.Right,
            EntryEdge.West => -Vector2.Right,
            _ => Vector2.Down
        };
        Vector2 inward = -outward;
        // Staging +Z is forward. Preserve the formation's handedness when rotating.
        Vector2 across = new(inward.Y, -inward.X);
        float offset = map.AutoBattlerRules.RedReserveEdgeOffset;
        if (!float.IsFinite(offset)) offset = 0f;
        Vector2 edge = outward * half + across * (local.X + half * Mathf.Clamp(offset, -100f, 100f) * 0.01f);
        Vector2 start = edge + outward * (HalfWidth - local.Z + leadIn);
        // Use the largest footprint for the common handoff line so ranks do not stack.
        Vector2 goal = edge + inward * (4f * handoffRadius + 0.05f);
        var destination = new Vector3(goal.X, 0, goal.Y);
        bool clear = Mathf.Abs(goal.X) + radius < half && Mathf.Abs(goal.Y) + radius < half &&
            map.AutoBattlerRules.AllowsSide(destination, radius, blue: false) &&
            field?.BlocksCorridor(edge + outward * radius, goal, radius) != true;
        return new Lane(new Vector3(start.X, 0, start.Y), destination, inward, start.DistanceTo(goal), clear);
    }
}
