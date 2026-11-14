// Shared presentation settings for soldier hits.
using Godot;

namespace MassiveRts;

[GlobalClass]
public partial class HitFeedbackSettings : Resource
{
    [Export(PropertyHint.Range, "0,2,0.01")] public float WhiteFlashDuration { get; set; } = 0.12f;
    [Export(PropertyHint.Range, "0.01,2,0.01")] public float PulseDuration { get; set; } = 0.18f;
    [Export(PropertyHint.Range, "1,1.25,0.01")] public float PulseMaxScale { get; set; } = 1.06f;
    [Export(PropertyHint.Range, "0.01,0.99,0.01")] public float PulseRiseFraction { get; set; } = 0.20f;
    [Export] public bool PulseEnabled { get; set; } = true;
}
