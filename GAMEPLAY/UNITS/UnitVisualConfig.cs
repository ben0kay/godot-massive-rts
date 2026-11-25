// Controls which optional unit visuals are enabled when a scene loads.
using Godot;

namespace MassiveRts;

[GlobalClass]
public partial class UnitVisualConfig : Resource
{
    internal const string DefaultPath = "res://GAMEPLAY/UNITS/UnitVisualConfig.tres";

    [Export] public bool UseEnhancedMeshes { get; set; }
    [Export] public bool UseCircleRobotMesh { get; set; }
    [Export] public bool AnimateCircleSword { get; set; }
    [Export] public bool AnimateCircleWalk { get; set; }
    [Export] public bool UseSquareHeavyMesh { get; set; }
    [Export] public bool AnimateSquareAttack { get; set; }
    [Export] public bool AnimateSquareWalk { get; set; }
    [Export] public bool UseTriangleArcherMesh { get; set; }
    [Export] public bool AnimateTriangleFire { get; set; }
    [Export] public bool AnimateTriangleWalk { get; set; }
    [Export] public bool UsePentagonEliteMesh { get; set; }
    [Export] public bool AnimatePentagonAttack { get; set; }
    [Export] public bool AnimatePentagonWalk { get; set; }
    [Export] public bool UseEnhancedHexagonMesh { get; set; }
    [Export] public bool UseHexagonSiegeRobotMesh { get; set; }
    [Export] public bool AnimateHexagonAttack { get; set; }
    [Export] public bool AnimateHexagonWalk { get; set; }
    [Export] public bool UseProjectileLaunchEffects { get; set; }
    [Export] public bool UseMuzzleSmoke { get; set; }

    [Export(PropertyHint.Range, "0,512,1")] public int MaxPrismBeamEffects { get; set; } = 128;

    [ExportGroup("Shields")]
    [Export] public bool ShowShieldShells { get; set; } = true;
    [Export(PropertyHint.Range, "0,2048,1")] public int MaxShieldShells { get; set; } = 512;

    [ExportGroup("Unit Fracture (FX2)")]
    [Export] public bool UseUnitFracture { get; set; }
    [Export(PropertyHint.Range, "0,1,0.01")] public float FirstCrackHealth { get; set; } = 0.75f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float HeavyCrackHealth { get; set; } = 0.25f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float CrackDarkness { get; set; } = 0.84f;

    [ExportGroup("Crumble")]
    [Export(PropertyHint.Range, "4,6,1")] public int FragmentsPerDeath { get; set; } = 6;
    [Export(PropertyHint.Range, "0,2,0.01")] public float FragmentSpread { get; set; } = 0.86f;
    [Export(PropertyHint.Range, "0,4,0.01")] public float FragmentUpwardVelocity { get; set; } = 1.1f;
    [Export(PropertyHint.Range, "1,30,0.1")] public float FragmentGravity { get; set; } = 11f;
    [Export(PropertyHint.Range, "0,0.5,0.01")] public float FragmentBounce { get; set; } = 0.18f;
    [Export(PropertyHint.Range, "0.3,2,0.01")] public float FragmentLifetime { get; set; } = 0.9f;
    [Export(PropertyHint.Range, "0.05,1,0.01")] public float FragmentFadeDuration { get; set; } = 0.35f;

    [ExportGroup("Crumble Budget")]
    [Export(PropertyHint.Range, "0,512,1")] public int MaxDetailedDeaths { get; set; } = 256;
    [Export(PropertyHint.Range, "0,3000,1")] public int MaxActiveFragments { get; set; } = 1500;
    [Export(PropertyHint.Range, "0,128,1")] public int MaxDetailedDeathsPerTick { get; set; } = 48;

    internal static UnitVisualConfig Load() => GD.Load<UnitVisualConfig>(DefaultPath) ?? new();
}
