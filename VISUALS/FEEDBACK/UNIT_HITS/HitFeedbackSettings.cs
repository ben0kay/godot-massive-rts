// Shared presentation settings for soldier hits.
using Godot;

namespace MassiveRts;

[GlobalClass]
public partial class HitFeedbackSettings : Resource
{
    [Export(PropertyHint.Range, "0,2,0.01")] public float WhiteFlashDuration { get; set; } = 0.12f;
    [Export(PropertyHint.Range, "0.01,2,0.01")] public float PulseDuration { get; set; } = 0.18f;
    [Export(PropertyHint.Range, "1,1.25,0.01")] public float PulseMaxScale { get; set; } = 1.03f;
    [Export(PropertyHint.Range, "0.01,0.99,0.01")] public float PulseRiseFraction { get; set; } = 0.20f;
    [Export] public bool PulseEnabled { get; set; } = true;
    [ExportGroup("Heavy Impact")]
    [Export] public bool UseHeavyImpactReaction { get; set; } = true;
    [Export(PropertyHint.Range, "2,10,.5")] public float HeavyImpactMinimumTier { get; set; } = 2f;
    [Export(PropertyHint.Range, ".1,2,.01")] public float HeavyImpactDuration { get; set; } = .40f;
    [Export(PropertyHint.Range, "0,2,.05")] public float HeavyImpactStrength { get; set; } = 1f;
    [Export(PropertyHint.Range, "0,.5,.01")] public float HeavyImpactCounterRock { get; set; } = .25f;
    [Export(PropertyHint.Range, "0,1,.01")] public float HeavyImpactSplashStrength { get; set; } = .65f;
    // X/Y/Z/W are degrees at tiers 2, 2.5, 3 and 4 respectively.
    [Export] public Vector4 HeavyImpactTierAngles { get; set; } = new(8f, 7f, 6f, 4.5f);
    [Export(PropertyHint.Range, "0,12,.1")] public float HeavyImpactTierFiveAngle { get; set; } = 2.5f;
}
