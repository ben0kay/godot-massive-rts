// Holds the authored roster for painters, rendering, and battle HUDs.
using Godot;

namespace MassiveRts;

[GlobalClass]
public partial class FactionCatalog : Resource
{
    [Export] public Godot.Collections.Array<FactionDefinition> Definitions { get; set; } = new();
}
