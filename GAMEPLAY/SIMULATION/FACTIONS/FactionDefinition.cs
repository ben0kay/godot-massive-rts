// Stores one stable faction ID and its shared presentation metadata.
using Godot;

namespace MassiveRts;

[GlobalClass]
public partial class FactionDefinition : Resource
{
    [Export(PropertyHint.Range, "0,31,1")] public int Id { get; set; }
    [Export] public string DisplayName { get; set; } = "";
    [Export] public Color Color { get; set; } = Colors.White;
    [Export] public int DisplayOrder { get; set; }
    [Export] public bool ShowWhenEmpty { get; set; }
}
