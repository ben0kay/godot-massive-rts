// Shared defaults applied when an arena compiles its soldier and projectile catalogues.
using Godot;

namespace MassiveRts;

[GlobalClass]
public partial class SimulationSettings : Resource
{
    public const string DefaultPath = "res://SIMULATION/CONFIG/DefaultSimulationSettings.tres";
    private const float DefaultMovementSpeedMultiplier = 0.8f;

    [Export(PropertyHint.Range, "0.1,4.0,0.05")]
    public float MovementSpeedMultiplier { get; set; } = DefaultMovementSpeedMultiplier;

    public float EffectiveMovementSpeedMultiplier =>
        float.IsFinite(MovementSpeedMultiplier) && MovementSpeedMultiplier > 0f
            ? Mathf.Clamp(MovementSpeedMultiplier, 0.1f, 4f) : DefaultMovementSpeedMultiplier;
}
