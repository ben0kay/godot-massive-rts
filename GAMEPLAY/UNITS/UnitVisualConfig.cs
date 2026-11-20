// Controls which optional unit meshes are built when a scene loads.
using Godot;

namespace MassiveRts;

[GlobalClass]
public partial class UnitVisualConfig : Resource
{
    internal const string DefaultPath = "res://GAMEPLAY/UNITS/UnitVisualConfig.tres";

    [Export] public bool UseEnhancedMeshes { get; set; }
}
