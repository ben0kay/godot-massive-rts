// Stores level-authored deployment rules independently from the player's army choices.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

[GlobalClass]
public partial class AutoBattlerRules : Resource
{
    public enum ObjectiveType { EliminateEnemy, DestroyCitadel }
    [Export] public Godot.Collections.Array<Vector2I> RedDisruptorCells { get; set; } = new();
    public enum DeploymentAxis { Disabled, X, Z }
    [Export] public DeploymentAxis LineAxis { get; set; }
    [Export] public float LineOffset { get; set; }
    [Export] public bool FlipBlueSide { get; set; }

    public bool AllowsSide(Vector3 position, float clearance, bool blue)
    {
        if (LineAxis == DeploymentAxis.Disabled) return true;
        float coordinate = LineAxis == DeploymentAxis.X ? position.X : position.Z;
        return blue != FlipBlueSide ? coordinate + clearance <= LineOffset : coordinate - clearance >= LineOffset;
    }

    public bool AllowsBlueDeployment(Vector3 position, float clearance)
    {
        if (!AllowsSide(position, clearance, blue: true)) return false;
        if (LineAxis != DeploymentAxis.Disabled && BlueDeploymentCells.Count == 0) return true;
        const float grid = DeploymentLayout.CellSize;
        return BlueDeploymentCells.Contains(new Vector2I(Mathf.FloorToInt(position.X / grid), Mathf.FloorToInt(position.Z / grid)));
    }

    [Export] public string RedReserveUnitId { get; set; } = "";
    [Export] public int RedFormationEdge { get; set; }
    [Export] public int RedReserveCount { get; set; }
    [Export] public float RedReserveSeconds { get; set; } = 30f;
    [Export(PropertyHint.Range, "-100,100,1")] public float RedReserveEdgeOffset { get; set; }

    [Export] public ObjectiveType Objective { get; set; }
    [Export] public int BlueUnitLimit { get; set; } = 200;
    [Export] public string[] AllowedBlueUnitIds { get; set; } = System.Array.Empty<string>();
    [Export] public Godot.Collections.Array<Vector2I> BlueDeploymentCells { get; set; } = new();

    // Each group stores its start, end, bend and (hold seconds, bent flag, member count),
    // followed by the authored ground positions of its members.
    [Export] public Vector3[] RedOpeningOrders { get; set; } = System.Array.Empty<Vector3>();

    internal static Vector2 RedOrderKey(Vector3 point) => ObstacleField.Xz(point).Snapped(Vector2.One * 0.001f);

    internal Dictionary<Vector2, DeploymentOrderOverlay.Order> ReadRedOrders()
    {
        var orders = new Dictionary<Vector2, DeploymentOrderOverlay.Order>();
        for (int i = 0; i + 4 <= RedOpeningOrders.Length;)
        {
            var start = RedOpeningOrders[i++];
            var end = RedOpeningOrders[i++];
            var bend = RedOpeningOrders[i++];
            var settings = RedOpeningOrders[i++];
            if (settings.Z < 1 || settings.Z > RedOpeningOrders.Length - i || !float.IsFinite(settings.Z)) break;
            var order = new DeploymentOrderOverlay.Order
                { Start = start, End = end, Bend = bend, Hold = settings.X, Bent = settings.Y != 0 };
            for (int n = 0; n < (int)settings.Z; n++)
                orders[RedOrderKey(RedOpeningOrders[i++])] = order;
        }
        return orders;
    }

    internal static Vector3[] WriteRedOrders(Dictionary<SoldierPlacement, DeploymentOrderOverlay.Order> orders)
    {
        var groups = new Dictionary<DeploymentOrderOverlay.Order, List<Vector3>>();
        foreach (var (soldier, order) in orders)
        {
            if (!groups.TryGetValue(order, out var members)) groups.Add(order, members = new());
            members.Add(soldier.Position());
        }

        var data = new List<Vector3>();
        foreach (var (order, members) in groups)
        {
            data.Add(order.Start);
            data.Add(order.End);
            data.Add(order.Bend);
            data.Add(new Vector3(order.Hold, order.Bent ? 1 : 0, members.Count));
            data.AddRange(members);
        }
        return data.ToArray();
    }
}
