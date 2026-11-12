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
    private readonly SoldierPlacement[] painted;
    public DeploymentFactory(int count, ulong seed, Deployment scenario, BenchmarkMode benchmarkMode, float rangedFraction)
    {
        total = count; sideCount = (count + 1) / 2; columns = Mathf.CeilToInt(Mathf.Sqrt(sideCount));
        layout = scenario; mode = benchmarkMode; ranged = rangedFraction; random.Seed = seed;
    }
    public DeploymentFactory(SoldierPlacement[] placements, ulong seed)
    {
        painted = placements;
        total = placements.Length;
        random.Seed = seed;
    }
    // =========================================================
    public void Append(UnitState units, int budget)
    {
        int end = Mathf.Min(total, units.Count + budget);
        float spacing = 0f;
        if (painted == null)
        {
            float maxFootprint = 0f;
            for (int type = 0; type < RuntimeUnitCatalog.Hexagon; type++)
                maxFootprint = Mathf.Max(maxFootprint, units.Catalog[type].Footprint);
            spacing = maxFootprint * 2f + (layout == Deployment.DenseClusters ? 0f : 0.5f);
        }
        for (int i = units.Count; i < end; i++)
        {
            if (painted != null)
            {
                SoldierPlacement placement = painted[i];
                Vector3 position = placement.Position();
                position.Y = units.Grounding?.Field.At(position.X, position.Z) ?? 0f;
                units.Position[i] = position;
                units.Faction[i] = placement.Faction;
                units.Type[i] = placement.Type;
                InitializeSoldier(units, i);
                continue;
            }
            byte faction = (byte)(i < sideCount ? 0 : 1);
            int local = faction == 0 ? i : i - sideCount;
            float sign = faction == 0 ? -1 : 1;
            float x = 14 + local / columns * spacing;
            float z = (local % columns - columns * 0.5f) * spacing;
            if (layout == Deployment.DistributedBattlefield)
            { x += random.RandfRange(0, 150); z += random.RandfRange(-150, 150); }
            units.Position[i] = new Vector3(sign * x, units.Grounding?.Field.At(sign * x, z) ?? 0f, z);
            if (mode == BenchmarkMode.Movement) units.Goal[i] = new Vector3(-sign * x, 0, z);
            units.Faction[i] = faction;
            units.Type[i] = PickType(units.Catalog);
            InitializeSoldier(units, i);
        }
        units.Count = end;
    }

    private byte PickType(RuntimeUnitCatalog catalog)
    {
        bool allowRanged = mode != BenchmarkMode.Melee;
        float rangedFraction = mode == BenchmarkMode.Ranged ? ranged : 0.2f;
        bool rangedPick = allowRanged && random.Randf() < rangedFraction;
        int totalWeight = 0;
        for (byte i = 0; i < RuntimeUnitCatalog.Hexagon; i++)
            if (catalog[i].Ranged == rangedPick) totalWeight += TypeWeight(i);
        if (totalWeight == 0)
        {
            for (byte i = 0; i < RuntimeUnitCatalog.Hexagon; i++)
                if (!catalog[i].Ranged || allowRanged) return i;
            throw new System.InvalidOperationException("Melee benchmarks need a melee unit definition.");
        }
        int roll = random.RandiRange(1, totalWeight);
        for (byte i = 0; i < RuntimeUnitCatalog.Hexagon; i++)
        {
            if (catalog[i].Ranged != rangedPick) continue;
            roll -= TypeWeight(i);
            if (roll <= 0) return i;
        }
        throw new System.InvalidOperationException("Invalid preset unit weights.");
    }

    private static int TypeWeight(byte type) => type switch
    {
        RuntimeUnitCatalog.Circle => 60,
        RuntimeUnitCatalog.Square => 25,
        RuntimeUnitCatalog.Diamond => 10,
        RuntimeUnitCatalog.Pentagon => 5,
        _ => 20
    };

    private void InitializeSoldier(UnitState units, int i)
    {
        ref readonly UnitStats type = ref units.Catalog[units.Type[i]];
        units.Health[i] = type.Health;
        units.Target[i] = -1;
        units.Cooldown[i] = random.RandfRange(0, type.Interval);
        units.Dirty[i] = true;
        units.RegisterSpawn(units.Faction[i], units.Type[i]);
    }
}
