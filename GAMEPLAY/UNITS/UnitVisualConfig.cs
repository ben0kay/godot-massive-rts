// Controls which optional unit visuals are enabled when a scene loads.
using Godot;

namespace MassiveRts;

[GlobalClass]
public partial class UnitVisualConfig : Resource
{
    internal const string DefaultPath = "res://GAMEPLAY/UNITS/UnitVisualConfig.tres";

    [Export] public bool UseEnhancedMeshes { get; set; }
    [Export] public bool UseProjectileLaunchEffects { get; set; }
    [Export] public bool UseMuzzleSmoke { get; set; }
}
