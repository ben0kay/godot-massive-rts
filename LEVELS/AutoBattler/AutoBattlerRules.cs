// Stores level-authored deployment rules independently from the player's army choices.
using Godot;

namespace MassiveRts;

[GlobalClass]
public partial class AutoBattlerRules : Resource
{
    public enum ObjectiveType { EliminateEnemy }
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

    [Export] public ObjectiveType Objective { get; set; }
    [Export] public int BlueUnitLimit { get; set; } = 200;
    [Export] public string[] AllowedBlueUnitIds { get; set; } = System.Array.Empty<string>();
    [Export] public Godot.Collections.Array<Vector2I> BlueDeploymentCells { get; set; } = new();
}
