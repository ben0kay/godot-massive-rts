// Holds the display metadata and ordered levels for one authored campaign.
using Godot;

namespace MassiveRts;

[GlobalClass]
public partial class CampaignDefinition : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
    [Export] public Godot.Collections.Array<LevelDefinition> Levels { get; set; } = new();
}
