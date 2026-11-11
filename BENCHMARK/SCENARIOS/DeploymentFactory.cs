// Deterministic, incremental deployment with mirrored marching lanes.
using Godot;
namespace MassiveRts;

public sealed class DeploymentFactory
{
    private readonly RandomNumberGenerator random = new();
    private readonly int total, sideCount, columns;
    private readonly Deployment layout;
    private readonly float ranged;
    private readonly BenchmarkMode mode;
    public DeploymentFactory(int count, ulong seed, Deployment scenario, BenchmarkMode benchmarkMode, float rangedFraction)
    {
        total = count; sideCount = (count + 1) / 2; columns = Mathf.CeilToInt(Mathf.Sqrt(sideCount));
        layout = scenario; mode = benchmarkMode; ranged = rangedFraction; random.Seed = seed;
    }
    // =========================================================
    public void Append(UnitState units, BenchmarkConfig config, int budget)
    {
        int end = Mathf.Min(total, units.Count + budget);
        for (int i = units.Count; i < end; i++)
        {
            byte faction = (byte)(i < sideCount ? 0 : 1);
            int local = faction == 0 ? i : i - sideCount;
            float sign = faction == 0 ? -1 : 1;
            float spacing = layout == Deployment.DenseClusters ? 1.25f : 2.1f;
            float x = 14 + local / columns * spacing;
            float z = (local % columns - columns * 0.5f) * spacing;
            if (layout == Deployment.DistributedBattlefield)
            { x += random.RandfRange(0, 150); z += random.RandfRange(-150, 150); }
            units.Position[i] = new Vector3(sign * x, BattlefieldHeight.At(sign * x, z), z);
            units.Goal[i] = new Vector3(-sign * x, 0, z);
            units.Faction[i] = faction;
            units.Kind[i] = mode == BenchmarkMode.Ranged && random.Randf() < ranged ? (byte)1 : (byte)(local % 20 == 0 ? 2 : 0);
            units.Health[i] = config.Health; units.Target[i] = -1;
            units.Cooldown[i] = random.RandfRange(0, config.MeleeInterval);
            units.Dirty[i] = true;
            if (faction == 0) units.Blue++; else units.Red++;
        }
        units.Count = end;
    }
}
