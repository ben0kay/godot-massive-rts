// Stores level-authored deployment rules independently from the player's army choices.
using Godot;

namespace MassiveRts;

[GlobalClass]
public partial class PresetBattleRules : Resource
{
    public enum ObjectiveType { EliminateEnemy }
    [Export] public ObjectiveType Objective { get; set; }
    [Export] public int BlueUnitLimit { get; set; } = 200;
    [Export] public string[] AllowedBlueUnitIds { get; set; } = System.Array.Empty<string>();
    [Export] public Godot.Collections.Array<Vector2I> BlueDeploymentCells { get; set; } = new();
}
