// Deterministic, incremental benchmark deployments: legacy presets plus six-shape tactical formations.
using System;
using System.Collections.Generic;
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
    private readonly int[] formationStarts, formationCounts, formationColumns, formationRows;
    private int[] ringStarts, ringCounts;
    private float[] ringRadii;
    private int ringCursor;

    public DeploymentFactory(int count, ulong seed, Deployment scenario, BenchmarkMode benchmarkMode, float rangedFraction)
    {
        total = count; sideCount = (count + 1) / 2; columns = Mathf.CeilToInt(Mathf.Sqrt(sideCount));
        layout = scenario; mode = benchmarkMode; ranged = Mathf.Clamp(rangedFraction, 0f, 1f); random.Seed = seed;
        if (scenario < Deployment.LayeredRanks) return;
        int factions = scenario == Deployment.ThreeWayClash ? 3 : 2;
        formationStarts = new int[factions]; formationCounts = new int[factions];
        formationColumns = new int[factions]; formationRows = new int[factions];
        int start = 0;
        for (int faction = 0; faction < factions; faction++)
        {
            int factionCount = count / factions + (faction < count % factions ? 1 : 0);
            int width = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(factionCount)));
            formationStarts[faction] = start;
            formationCounts[faction] = factionCount;
            formationColumns[faction] = width;
            formationRows[faction] = (factionCount + width - 1) / width;
            start += factionCount;
        }
    }
    public DeploymentFactory(SoldierPlacement[] placements, ulong seed)
    {
        painted = placements; total = placements.Length; random.Seed = seed;
    }

    // =========================================================
    // One-time concentric ring layout with space for the largest unit footprint.
    private void PrepareEncirclement(float spacing)
    {
        float half = (formationColumns[0] - 1) * spacing * 0.5f;
        float innerRadius = Mathf.Sqrt(2f) * half + spacing * 9f;
        var starts = new List<int>(); var counts = new List<int>(); var radii = new List<float>();
        int assigned = 0;
        while (assigned < formationCounts[1])
        {
            float radius = innerRadius + starts.Count * spacing;
            int slots = Mathf.Max(8, Mathf.FloorToInt(Mathf.Tau * radius / (spacing * 1.08f)));
            starts.Add(assigned); counts.Add(slots); radii.Add(radius);
            assigned += slots;
        }
        ringStarts = starts.ToArray(); ringCounts = counts.ToArray(); ringRadii = radii.ToArray();
    }

    // =========================================================
    public void Append(UnitState units, int budget)
    {
        int end = Mathf.Min(total, units.Count + budget);
        float spacing = 0f;
        if (painted == null)
        {
            // The three historical layouts retain their original spacing/composition.
            int limit = layout < Deployment.LayeredRanks ? RuntimeUnitCatalog.Hexagon : RuntimeUnitCatalog.Chevron;
            float maxRadius = 0f;
            for (int type = 0; type < limit; type++)
                maxRadius = Mathf.Max(maxRadius, units.Catalog[type].Footprint);
            spacing = maxRadius * 2f + (layout == Deployment.DenseClusters ? 0f : 0.5f);
            if (layout == Deployment.Encirclement && ringStarts == null) PrepareEncirclement(spacing);
        }

        for (int i = units.Count; i < end; i++)
        {
            if (painted != null)
            {
                SoldierPlacement placement = painted[i];
                Vector3 p = placement.Position();
                p.Y = units.Grounding?.Field.At(p.X, p.Z) ?? 0f;
                units.Position[i] = p;
                units.Faction[i] = placement.Faction; units.Type[i] = placement.Type;
                InitializeSoldier(units, i);
                continue;
            }
            if (layout >= Deployment.LayeredRanks)
            {
                AppendFormation(units, i, spacing);
                continue;
            }
            // Historical preset behavior is deliberately unchanged for benchmark comparisons.
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

    // =========================================================
    // Arithmetic formations: O(1) placement per soldier and no scene-tree units.
    private void AppendFormation(UnitState units, int id, float spacing)
    {
        byte faction = 0;
        while (faction + 1 < formationStarts.Length && id >= formationStarts[faction + 1]) faction++;
        int local = id - formationStarts[faction];
        int count = formationCounts[faction];
        int width = formationColumns[faction], height = formationRows[faction];
        int row = local / width, column = local % width;
        float lateral = (column - (width - 1) * 0.5f) * spacing;
        float side = Mathf.Abs(lateral) / Mathf.Max(spacing, width * spacing * 0.5f);
        float x, z;
        if (layout == Deployment.Encirclement)
        {
            if (faction == 0)
            {
                x = (row - (height - 1) * 0.5f) * spacing;
                z = lateral;
            }
            else
            {
                while (ringCursor + 1 < ringStarts.Length &&
                    local >= ringStarts[ringCursor] + ringCounts[ringCursor]) ringCursor++;
                int slot = local - ringStarts[ringCursor];
                float angle = Mathf.Tau * (slot + (ringCursor % 2) * 0.5f) / ringCounts[ringCursor];
                x = ringRadii[ringCursor] * Mathf.Cos(angle);
                z = ringRadii[ringCursor] * Mathf.Sin(angle);
            }
        }
        else if (layout == Deployment.ThreeWayClash)
        {
            float angle = faction * Mathf.Tau / 3f;
            float forward = Mathf.Max(60f, width * spacing * 0.75f) + row * spacing;
            float cosine = Mathf.Cos(angle), sine = Mathf.Sin(angle);
            x = forward * cosine - lateral * sine;
            z = forward * sine + lateral * cosine;
        }
        else
        {
            float sign = faction == 0 ? -1f : 1f;
            float front = 40f;
            if (layout == Deployment.CavalryPincer) front = side > 0.70f ? 14f : 48f;
            else if (layout == Deployment.WedgeVsShield)
                front = 38f + (faction == 0 ? Mathf.Abs(lateral) * 0.70f : 0f);
            x = sign * (front + row * spacing);
            z = lateral;
        }

        units.Faction[id] = faction;
        units.Type[id] = PickFormationType(local, count, faction, side);
        units.Position[id] = new Vector3(x, units.Grounding?.Field.At(x, z) ?? 0f, z);
        if (mode == BenchmarkMode.Movement) units.Goal[id] = new Vector3(-x, 0f, -z);
        InitializeSoldier(units, id);
    }

    // =========================================================
    // Rear rows contain the chosen ranged quota; all melee shapes are available
    // in the other ranks. Hexagons are sparse heavyweight anchors.
    private byte PickFormationType(int local, int count, byte faction, float side)
    {
        float rangedFraction = mode == BenchmarkMode.Ranged ? ranged
            : mode == BenchmarkMode.Melee ? 0f : 0.20f;
        int archers = Mathf.Clamp(Mathf.RoundToInt(count * rangedFraction), 0, count);
        if (archers > 0 && local >= count - archers) return RuntimeUnitCatalog.Triangle;
        float meleeDepth = (float)local / Mathf.Max(1, count - archers);
        if (layout == Deployment.CavalryPincer && side > 0.70f) return RuntimeUnitCatalog.Diamond;
        if (layout == Deployment.WedgeVsShield && faction == 0 &&
            side < 0.16f && meleeDepth < 0.35f)
            return local % 4 == 0 ? RuntimeUnitCatalog.Hexagon : RuntimeUnitCatalog.Pentagon;
        if (side > (layout == Deployment.LayeredRanks ? 0.82f : 0.86f) && meleeDepth < 0.82f)
            return RuntimeUnitCatalog.Diamond;
        if ((local + faction * 11) % 37 == 0) return RuntimeUnitCatalog.Hexagon;
        if ((local + faction * 7) % 13 == 0) return RuntimeUnitCatalog.Pentagon;
        if ((local + faction * 3) % 17 == 0) return RuntimeUnitCatalog.Diamond;
        if (layout == Deployment.LayeredRanks && meleeDepth < 0.27f)
            return local % 5 == 0 ? RuntimeUnitCatalog.Circle : RuntimeUnitCatalog.Square;
        if (layout == Deployment.WedgeVsShield && faction == 1 && meleeDepth < 0.30f)
            return local % 4 == 0 ? RuntimeUnitCatalog.Circle : RuntimeUnitCatalog.Square;
        if (layout == Deployment.Encirclement && faction == 0 && meleeDepth < 0.22f)
            return RuntimeUnitCatalog.Square;
        return local % 3 == 0 ? RuntimeUnitCatalog.Square : RuntimeUnitCatalog.Circle;
    }

    // =========================================================
    // Original four-melee-type weighted selection for the first three layouts.
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
            throw new InvalidOperationException("Melee benchmarks need a melee unit definition.");
        }
        int roll = random.RandiRange(1, totalWeight);
        for (byte i = 0; i < RuntimeUnitCatalog.Hexagon; i++)
        {
            if (catalog[i].Ranged != rangedPick) continue;
            roll -= TypeWeight(i);
            if (roll <= 0) return i;
        }
        throw new InvalidOperationException("Invalid preset unit weights.");
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
