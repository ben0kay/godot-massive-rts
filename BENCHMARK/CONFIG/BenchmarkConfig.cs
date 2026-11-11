// Central tuning resource for the benchmark, including explicit workload limits.
using Godot;
namespace MassiveRts;

[GlobalClass]
public partial class BenchmarkConfig : Resource
{
    [ExportGroup("Scheduling")]
    [Export] public int MaxSoldiers { get; set; } = 100000;
    [Export] public int SpawnPerFrame { get; set; } = 4000;
    [Export] public float TickSeconds { get; set; } = 1f / 30f;
    [Export] public int MaxStepsPerFrame { get; set; } = 3;
    [ExportGroup("Movement and targeting")]
    [Export] public float CellSize { get; set; } = 6f;
    [Export] public int QueryCandidateLimit { get; set; } = 96;
    [Export] public int TargetRefreshTicks { get; set; } = 6;
    [Export(PropertyHint.Range, "0,0.6,0.01")] public float MeleeAccelerationSeconds { get; set; } = 0.15f;
    [Export(PropertyHint.Range, "0,0.6,0.01")] public float RangedAccelerationSeconds { get; set; } = 0.32f;
    [ExportGroup("Separation")]
    [Export(PropertyHint.Range, "0,0.25,0.01")] public float SeparationGap { get; set; } = 0.05f;
    [Export(PropertyHint.Range, "1,128,1")] public int SeparationNeighbourBudget { get; set; } = 32;
    [Export(PropertyHint.Range, "0.01,1,0.01")] public float SeparationMaxPushPerTick { get; set; } = 0.35f;
    [ExportGroup("Allied crowd yielding")]
    [Export(PropertyHint.Range, "0,1,0.05")] public float AlliedRangedClearanceFraction { get; set; } = 0.5f;
    [Export(PropertyHint.Range, "0,0.25,0.01")] public float AlliedRangedYieldPairCap { get; set; } = 0.06f;
    [Export(PropertyHint.Range, "0,1,0.05")] public float AlliedRangedYieldMeleeShare { get; set; } = 0.7f;
    [Export(PropertyHint.Range, "0,0.25,0.01")] public float AlliedRangedExitPairCap { get; set; } = 0.06f;
    [ExportGroup("Feedback")]
    [Export] public HitFeedbackSettings HitFeedback { get; set; } = new();
    [ExportGroup("Projectiles")]
    [Export] public int MaxProjectiles { get; set; } = 30000;
    [Export] public float ProjectileLifetime { get; set; } = 4f;
    [Export] public float ProjectileHitRadius { get; set; } = 0.8f;
}
public enum BenchmarkMode { RenderingOnly, Movement, Melee, Ranged }
public enum Deployment { OpposingLines, DenseClusters, DistributedBattlefield }
