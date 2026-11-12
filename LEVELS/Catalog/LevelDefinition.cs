// Describes an authored level without loading its scene in the campaign menu.
using Godot;

namespace MassiveRts;

[GlobalClass]
public partial class LevelDefinition : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public int Order { get; set; }
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
    [Export(PropertyHint.File, "*.tscn,*.scn")] public string ScenePath { get; set; } = "";
    [Export] public bool Enabled { get; set; } = true;
}
