// Defines editable Preset Battles recruitment prices and initial availability.
using Godot;

namespace MassiveRts;

[GlobalClass]
public partial class PresetRecruitmentConfig : Resource
{
    [Export] public Godot.Collections.Dictionary<string, int> Prices { get; set; } = new();
    [Export] public string[] InitiallyAvailable { get; set; } = System.Array.Empty<string>();
}
