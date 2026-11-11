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
    [Export] public float Speed { get; set; } = 5f;
    [Export] public float CellSize { get; set; } = 6f;
    [Export] public int QueryCandidateLimit { get; set; } = 96;
    [Export] public int TargetRefreshTicks { get; set; } = 6;
    [Export] public float MeleeReach { get; set; } = 1.7f;
    [Export] public float RangedReach { get; set; } = 35f;
    [ExportGroup("Combat")]
    [Export] public float Health { get; set; } = 100f;
    [Export] public float Damage { get; set; } = 25f;
    [Export] public float MeleeInterval { get; set; } = 0.8f;
    [Export] public float FlashSeconds { get; set; } = 0.12f;
    [ExportGroup("Projectiles")]
    [Export] public int MaxProjectiles { get; set; } = 30000;
    [Export] public float ProjectileSpeed { get; set; } = 45f;
    [Export] public float ProjectileLifetime { get; set; } = 4f;
    [Export] public float ProjectileHitRadius { get; set; } = 0.8f;
}
public enum BenchmarkMode { RenderingOnly, Movement, Melee, Ranged }
public enum Deployment { OpposingLines, DenseClusters, DistributedBattlefield }
