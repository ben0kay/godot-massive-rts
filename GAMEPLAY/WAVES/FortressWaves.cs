// Runs authored waves and applies Fortress Defense target priority using the shared soldier systems.
using Godot;
using System.Diagnostics;

namespace MassiveRts;

internal sealed class FortressWaves
{
    // Preserve the existing retry cadence when a spawn batch is blocked.
    private const float RowSpawnRetrySeconds = 0.16f;
    private const int AcquisitionInterval = 6;
    private const float DefenderMeleeDetectionMultiplier = 2f;
    private const float ContactTolerance = 0.02f;
    private readonly UnitState units;
    private readonly CommandBuildings buildings;
    private readonly CommandMovement movement;
    private readonly Vector3 inward, sideways;
    private readonly float citadelAlongEdge;
    private readonly WaveSpawnPattern spawnPattern;
    private readonly float spawnWidth, rowDelay, unitSpawnInterval;
    private readonly int rowSize;
    private readonly System.Collections.Generic.Dictionary<float, WaveSpawnBand> spawnBands = new();
    private ObstacleField cachedSpawnGeometry;
    public bool SpawnBandBlocked { get; private set; }
    private readonly RandomNumberGenerator spawnRandom = new();
    private readonly Vector2 citadelMin, citadelMax;
    private readonly byte[][] waves;
    private readonly float intermission;
    private readonly BenchmarkConfig combat = new();
    private readonly SpatialGrid grid;
    private readonly StructureCombat structures;
    private readonly ShockwaveRenderer impactRings;
    private bool structureVisualChanged;
    private readonly SeparationSystem separation;
    public bool NormalSeparation { get; set; } = true;
    public int SampleTicks { get; private set; }
    public double MovementMs { get; private set; }
    public double SeparationMs { get; private set; }
    public double GridMs { get; private set; }
    public int OverlapPairs => NormalSeparation ? separation.OverlapPairs : 0;
    public int NeighbourBudgetHits => NormalSeparation ? separation.BudgetHits : 0;
    private float SeparationGap => NormalSeparation ? combat.SeparationGap : 0f;
    private float SeparationPadding => NormalSeparation ? combat.SeparationMaxPushPerTick : 0f;
    private readonly int[] enemies;
    private int enemyCount, nextSpawn, tick;
    private float spawnClock;
    public readonly ProjectileSystem Projectiles;
    public WaveSpawnEdge SpawnEdge { get; }
    public int WaveNumber { get; private set; }
    public float Countdown { get; private set; }
    public bool Defeat => buildings.Citadel.Destroyed;
    public bool Victory { get; private set; }
    public bool WaitingToStart => WaveNumber == 0 && !Defeat;
    public int Remaining => WaveNumber == 0 ? 0 : units.FactionAlive[Factions.Red] + waves[WaveNumber - 1].Length - nextSpawn;

    public FortressWaves(UnitState units, CommandBuildings buildings, CommandMovement movement, WaveSpawnEdge edge,
        Godot.Collections.Array<WaveDefinition> definitions, float intermission, WaveSpawnPattern pattern,
        float width, int enemiesPerRow, float secondsBetweenRows, float unitsPerSecond, ShockwaveRenderer impactRings)
    {
        this.units = units;
        this.buildings = buildings;
        this.movement = movement;
        this.impactRings = impactRings;
        structures = new StructureCombat(units, buildings.Structures, buildings.MapHalfWidth);
        SpawnEdge = edge;
        spawnPattern = pattern;
        spawnWidth = float.IsFinite(width) ? Mathf.Clamp(width, 1f, buildings.MapHalfWidth * 2f) : buildings.MapHalfWidth * 2f;
        rowSize = Mathf.Clamp(enemiesPerRow, 1, 24);
        rowDelay = Mathf.Clamp(secondsBetweenRows, 0.25f, 10f);
        float spawnRate = float.IsFinite(unitsPerSecond) ? Mathf.Clamp(unitsPerSecond, 1f, 60f) : 6.25f;
        unitSpawnInterval = 1f / spawnRate;
        inward = edge switch
        {
            WaveSpawnEdge.North => Vector3.Back,
            WaveSpawnEdge.East => Vector3.Left,
            WaveSpawnEdge.West => Vector3.Right,
            _ => Vector3.Forward
        };
        sideways = inward.Cross(Vector3.Up);
        citadelAlongEdge = buildings.Citadel.Center.Dot(sideways);
        this.intermission = intermission;
        citadelMin = buildings.Citadel.Area.Min;
        citadelMax = buildings.Citadel.Area.Max;
        waves = new byte[definitions.Count][];
        int reserved = 0, maxWave = 0;
        for (int i = 0; i < waves.Length; i++)
        {
            waves[i] = definitions[i].Compile(units.Catalog, units.Position.Length - units.Count - reserved);
            reserved += waves[i].Length;
            maxWave = Mathf.Max(maxWave, waves[i].Length);
        }
        if (waves.Length == 0) throw new System.InvalidOperationException("Author at least one Fortress Defense wave.");
        buildings.ReservedWaveSlots = reserved;
        enemies = new int[maxWave + units.FactionAlive[Factions.Red]];
        grid = new SpatialGrid(units.Position.Length, combat.CellSize);
        separation = new SeparationSystem(units.Position.Length);
        Projectiles = new ProjectileSystem(2048, units.Catalog.Projectiles, combat.ProjectileLifetime);
    }

    public void Start()
    {
        if (WaitingToStart) BeginNext();
    }

    private void BeginNext()
    {
        WaveNumber++;
        spawnRandom.Seed = (ulong)WaveNumber;
        spawnBands.Clear();
        SpawnBandBlocked = false;
        nextSpawn = enemyCount = 0;
        if (WaveNumber == 1)
            for (int id = 0; id < units.Count; id++)
                if (units.Faction[id] == Factions.Red && units.Health[id] > 0f) enemies[enemyCount++] = id;
        spawnClock = Countdown = 0;
    }

    public void ResetTimings()
    {
        SampleTicks = 0;
        MovementMs = SeparationMs = GridMs = 0;
    }

    private void RebuildGrid()
    {
        long start = Stopwatch.GetTimestamp();
        grid.Rebuild(units);
        GridMs += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }

    private bool MoveAndSeparate(float delta)
    {
        long start = Stopwatch.GetTimestamp();
        bool moved = movement.Step(delta);
        MovementMs += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        if (NormalSeparation && units.Alive >= 2)
        {
            start = Stopwatch.GetTimestamp();
            separation.Step(units, 1, SeparationGap, combat.SeparationNeighbourBudget, SeparationPadding,
                tick, obstacles: buildings.MovementField);
            SeparationMs += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            moved |= separation.MovedSoldiers;
        }
        units.Grounding?.Apply(units);
        return moved;
    }

    public bool Step(float delta)
    {
        if (Defeat || Victory) return false;
        units.StructureDomes.Step(delta);
        units.AdvanceTrenchClock(delta);
        bool ventMoved = units.Geothermal?.Step(units, grid, buildings.MovementField, buildings.MapHalfWidth, delta) == true;
        SampleTicks++;
        tick++;
        structureVisualChanged = false;
        var shields = units.Shields;
        uint shieldRevision = shields?.Revision ?? 0;
        if (WaitingToStart || Countdown > 0)
        {
            // Allow structure engagements and in-flight shots even between waves.
            var idleField = buildings.CoverField;
            bool engageStructures = units.HasStructureAttackers;
            bool engagePrism = units.HasPrism && units.FactionAlive[Factions.Red] > 0;
            if (engagePrism || units.Rally != null) RebuildGrid();
            units.Rally?.Step(units, grid, delta, impactRings);
            if (engageStructures || engagePrism) AcquireDefenderTargets(idleField, structuresOnly: true);
            bool idleMoved = MoveAndSeparate(delta);
            if (shields?.NeedsRefresh(delta, ShieldSystem.DefaultInterval) == true) RebuildGrid();
            bool shieldChanged = shields?.Step(units, grid, delta, ShieldSystem.DefaultInterval, 0f, true) == true;
            bool attackVisualsChanged = false;
            int before = units.Alive;
            if (engageStructures || units.HasPrism)
            {
                if (engagePrism && idleMoved) RebuildGrid();
                attackVisualsChanged = CombatSystem.Step(units, grid, Projectiles, 1f, delta, SeparationPadding, SeparationGap,
                    obstacles: idleField, shockwaves: impactRings, structureCombat: structures, commands: movement);
                if (engageStructures) AttackStructures(delta, idleField, Factions.Blue);
            }
            bool towerTargets = buildings.HasDefenses && units.FactionAlive[Factions.Red] > 0;
            if (Projectiles.Count > 0 || towerTargets) RebuildGrid();
            if (towerTargets) buildings.StepDefenses(grid, Projectiles, delta, tick);
            else buildings.FinishDefenseEffects(delta);
            if (Projectiles.Count > 0) Projectiles.Step(units, grid, combat, delta, SeparationPadding, idleField, impactRings);
            if (Countdown > 0 && !Defeat)
            {
                Countdown = Mathf.Max(0, Countdown - delta);
                if (Countdown == 0) BeginNext();
            }
            return ventMoved || shieldChanged || (shields?.Revision ?? 0) != shieldRevision || idleMoved || attackVisualsChanged || structureVisualChanged || units.Alive != before;
        }

        int previousCount = units.Count, previousAlive = units.Alive;
        RebuildGrid();
        units.Rally?.Step(units, grid, delta, impactRings);
        bool activeShieldChanged = shields?.Step(units, grid, delta, ShieldSystem.DefaultInterval, 0f, true) == true;
        TryRelease(delta);
        var field = buildings.CoverField;
        if (buildings.HasDefenses) buildings.StepDefenses(grid, Projectiles, delta, tick);
        AcquireDefenderTargets(field);
        for (int i = 0; i < enemyCount; i++)
        {
            int id = enemies[i];
            if (units.Health[id] > 0 && !units.IsAirborne(id)) OrderEnemy(id, field);
        }
        bool moved = MoveAndSeparate(delta);
        if (moved) RebuildGrid();
        // Crossing into Citadel detection range suppresses defender attacks on this same tick.
        for (int i = 0; i < enemyCount; i++)
        {
            int id = enemies[i];
            if (units.Health[id] <= 0 || units.IsAirborne(id) || units.Target[id] < 0 ||
                (units.Catalog[units.Type[id]].TargetsStructures || units.Catalog[units.Type[id]].PrismBeam.Enabled)) continue;
            Vector2 position = ObstacleField.Xz(units.Position[id]);
            float radius = Detection(id, units.Catalog[units.Type[id]]);
            int target = units.Target[id];
            if (position.DistanceSquaredTo(ClosestEdge(position)) <= radius * radius ||
                units.Health[target] <= 0 ||
                units.Position[id].DistanceSquaredTo(units.Position[target]) > radius * radius)
                units.Target[id] = -1;
        }
        bool weaponChanged = CombatSystem.Step(units, grid, Projectiles, 1f, delta, SeparationPadding, SeparationGap,
            obstacles: field, shockwaves: impactRings, structureCombat: structures, commands: movement);

        if (units.HasStructureAttackers) AttackStructures(delta, field, Factions.Blue);
        for (int i = 0; i < enemyCount && !Defeat; i++)
        {
            int id = enemies[i];
            if (units.Health[id] <= 0 || units.Target[id] >= 0) continue;
            ref readonly var type = ref units.Catalog[units.Type[id]];
            if (type.TargetsStructures) structures.Attack(id, Projectiles, delta, Sight(type, field), impactRings);
            else if (type.TargetsUnits) AttackCitadel(id, field, delta);
        }
        Projectiles.Step(units, grid, combat, delta, SeparationPadding, field, impactRings);
        if (!Defeat && Remaining == 0)
        {
            for (int id = 0; id < units.Count; id++)
                if (units.Faction[id] == Factions.Blue) movement.Disengage(id);
            if (WaveNumber == waves.Length) Victory = Projectiles.Count == 0;
            else Countdown = intermission;
        }

        // Shared combat can turn a soldier even when no one moves or dies.
        return ventMoved || activeShieldChanged || (shields?.Revision ?? 0) != shieldRevision || moved || weaponChanged || previousCount != units.Count || previousAlive != units.Alive || units.FactionAlive[Factions.Red] > 0;
    }

    private void TryRelease(float delta)
    {
        byte[] wave = waves[WaveNumber - 1];
        if (nextSpawn == wave.Length) return;
        spawnClock -= delta;
        if (spawnClock > 0) return;
        if (spawnPattern == WaveSpawnPattern.Rows)
        {
            spawnClock = RowSpawnRetrySeconds;
            int remainingInRow = Mathf.Min(wave.Length - nextSpawn, rowSize - nextSpawn % rowSize);
            for (int i = 0; i < remainingInRow; i++)
                if (!TryReleaseSoldier(wave[nextSpawn])) return;
            spawnClock = rowDelay;
        }
        else
        {
            // Preserve fractional physics-step time for accurate high spawn rates,
            // but cap catch-up to one attempt per tick and keep blocked-entry retries paced.
            spawnClock = Mathf.Max(0f, spawnClock + unitSpawnInterval);
            TryReleaseSoldier(wave[nextSpawn]);
        }
    }

    private bool TryReleaseSoldier(byte type)
    {
        var geometry = buildings.MovementField;
        if (cachedSpawnGeometry != geometry)
        {
            spawnBands.Clear();
            cachedSpawnGeometry = geometry;
        }
        float radius = units.Catalog[type].Footprint;
        if (!spawnBands.TryGetValue(radius, out var band))
        {
            band = new WaveSpawnBand(geometry, inward, sideways, buildings.MapHalfWidth,
                spawnWidth, citadelAlongEdge, radius);
            spawnBands.Add(radius, band);
        }

        SpawnBandBlocked = band.IsEmpty;
        if (SpawnBandBlocked) return false;

        const int maxAttempts = 24;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector3 entry = band.Sample(spawnRandom);
            if (SpawnOccupied(entry, radius)) continue;
            int id = units.TrySpawn(type, Factions.Red, entry);
            if (id < 0) return false;
            grid.AddSoldier(units, id);
            buildings.ReservedWaveSlots--;
            enemies[enemyCount++] = id;
            nextSpawn++;
            return true;
        }
        return false;
    }

    private bool SpawnOccupied(Vector3 entry, float radius)
    {
        const float gap = 0.08f;
        var candidates = grid.GetNearbySoldiers(entry, radius + units.MaxFootprint + gap);
        while (candidates.MoveNext())
        {
            int id = candidates.Current;
            if (units.Health[id] <= 0f) continue;
            float clearance = radius + units.Catalog[units.Type[id]].Footprint + gap;
            if (ObstacleField.Xz(units.Position[id] - entry).LengthSquared() < clearance * clearance)
                return true;
        }
        return false;
    }

    private float Detection(int id, in UnitStats type) =>
        type.Ranged ? units.RangedReach(id, type) : Mathf.Max(combat.CellSize * 2f, type.MeleeDetectionReach + units.MaxFootprint);

    private ObstacleField Sight(in UnitStats type, ObstacleField field) =>
        type.DirectFire(units.Catalog.Projectiles) ? field : null;

    private void AttackStructures(float delta, ObstacleField field, byte faction)
    {
        for (int id = 0; id < units.Count; id++)
        {
            if (units.Faction[id] != faction || structures.Target(id) == null) continue;
            structureVisualChanged |= structures.Attack(id, Projectiles, delta, Sight(units.Catalog[units.Type[id]], field), impactRings);
        }
    }

    private bool ChooseStructure(int id, in UnitStats type, int unitTarget, StructureHealth target, ObstacleField sight)
    {
        if (target == null) return false;
        if (unitTarget < 0 || units.Health[unitTarget] <= 0) return true;
        if (type.PreferredTarget == TargetPreference.Units) return false;
        // Keep fighting an immediate threat when the preferred structure still needs an approach.
        float reach = type.Ranged ? units.RangedReach(id, type) :
            CombatSystem.MeleeContact(type.MeleeAttackRadius, units.Catalog[units.Type[unitTarget]].Footprint, SeparationGap) + 0.08f;
        return structures.CanHit(id, type, target, sight) ||
            units.Position[id].DistanceSquaredTo(units.Position[unitTarget]) > reach * reach;
    }

    private void AcquireDefenderTargets(ObstacleField field, bool structuresOnly = false)
    {
        for (int id = 0; id < units.Count; id++)
        {
            if (units.Health[id] <= 0 || units.IsAirborne(id) || units.Faction[id] != Factions.Blue) continue;
            ref readonly var type = ref units.Catalog[units.Type[id]];
            bool onlyStructures = structuresOnly && !type.PrismBeam.Enabled;
            if (onlyStructures && !type.TargetsStructures) continue;
            bool attackMove = movement.IsAttackMove(id);
            bool canPursue = !type.Ranged && (!movement.HasCommand(id) || attackMove);
            float range = type.Ranged ? units.RangedReach(id, type) : canPursue ? Detection(id, type) * DefenderMeleeDetectionMultiplier :
                type.MeleeAttackRadius + units.MaxFootprint + SeparationGap + CombatSystem.MeleeContactTolerance;
            int target = onlyStructures ? -1 : units.Target[id];
            Vector3 position = units.Position[id];
            var sight = Sight(type, field);
            if (attackMove && target >= 0 &&
                (units.Health[target] <= 0 || !units.CanDetectGroundTarget(units.Position[id], target) || position.DistanceSquaredTo(units.Position[target]) > range * range ||
                 position.DistanceSquaredTo(units.Position[target]) < type.MinimumFireRange * type.MinimumFireRange ||
                 sight?.BlocksSight(position, units.Position[target]) == true))
                target = -1;

            bool refresh = id % AcquisitionInterval == tick % AcquisitionInterval;
            if (!onlyStructures && (!attackMove || target < 0 || type.PrismBeam.Enabled) && refresh)
                target = grid.FindEnemy(units, id, range, combat.QueryCandidateLimit, tick, sight,
                    minimumRange: type.MinimumFireRange,
                    meleeReachPadding: type.Ranged || canPursue ? -1f : SeparationGap + CombatSystem.MeleeContactTolerance);
            if (type.TargetsStructures)
            {
                bool canApproach = !movement.HasCommand(id) || attackMove;
                float structureRange = canApproach ? type.StructureDetectionRange :
                    Mathf.Min(type.StructureDetectionRange, type.Ranged ? units.RangedReach(id, type) : StructureCombat.Reach(type));
                StructureHealth structure = structures.Acquire(id, type, structureRange, refresh, canApproach, buildings.MovementField, sight, tick, commands: movement);
                if (ChooseStructure(id, type, target, structure, sight))
                {
                    units.Target[id] = -1;
                    if (canApproach)
                        movement.Engage(id, structures.CanHit(id, type, structure, sight) ? position : structures.ApproachGoal(id));
                    continue;
                }
                structures.SetTarget(id, null);
            }
            units.Target[id] = target;
            if (!canPursue && !attackMove) continue;
            if (target < 0 || units.Health[target] <= 0 ||
                position.DistanceSquaredTo(units.Position[target]) > range * range)
            {
                movement.Disengage(id);
                continue;
            }

            if (type.Ranged)
            {
                movement.Engage(id, position);
                continue;
            }

            Vector3 enemy = units.Position[target];
            float contact = CombatSystem.MeleeContact(type.MeleeAttackRadius, units.Catalog[units.Type[target]].Footprint, SeparationGap);
            Vector3 offset = position - enemy;
            movement.Engage(id, offset.LengthSquared() <= contact * contact
                ? position : enemy + offset.Normalized() * contact);
        }
    }

    private Vector2 ClosestEdge(Vector2 position) => position.Max(citadelMin).Min(citadelMax);

    private void OrderEnemy(int id, ObstacleField field)
    {
        ref readonly var type = ref units.Catalog[units.Type[id]];
        Vector2 p = ObstacleField.Xz(units.Position[id]);
        Vector2 edge = ClosestEdge(p);
        float detect = Detection(id, type);
        int target = units.Target[id];
        bool refresh = id % AcquisitionInterval == tick % AcquisitionInterval;
        if (!type.TargetsStructures && !type.PrismBeam.Enabled && p.DistanceSquaredTo(edge) <= detect * detect) target = -1;
        else
        {
            if (target >= 0 && (units.Health[target] <= 0 ||
                units.Position[id].DistanceSquaredTo(units.Position[target]) > detect * detect)) target = -1;
            if ((target < 0 || type.PrismBeam.Enabled) && refresh)
                target = grid.FindEnemy(units, id, detect, combat.QueryCandidateLimit, tick, Sight(type, field), minimumRange: type.PrismBeam.Enabled ? type.MinimumFireRange : 0f);
        }

        if (!type.TargetsUnits) target = -1;
        if (type.TargetsStructures)
        {
            var sight = Sight(type, field);
            StructureHealth structure = structures.Acquire(id, type, type.StructureDetectionRange, refresh, true, buildings.MovementField, sight, tick, commands: movement);
            if (ChooseStructure(id, type, target, structure, sight))
            {
                units.Target[id] = -1;
                movement.SetGoal(id, structures.CanHit(id, type, structure, sight) ? units.Position[id] : structures.ApproachGoal(id));
                return;
            }
            structures.SetTarget(id, null);
        }
        units.Target[id] = target;
        if (target >= 0)
        {
            Vector2 defender = ObstacleField.Xz(units.Position[target]);
            float range = type.Ranged ? units.RangedReach(id, type) * 0.94f :
                CombatSystem.MeleeContact(type.MeleeAttackRadius, units.Catalog[units.Type[target]].Footprint, SeparationGap);
            Vector2 offset = p - defender;
            float distance = offset.Length();
            if (distance <= (type.PrismBeam.Enabled ? units.RangedReach(id, type) : range) && distance >= type.MinimumFireRange)
            {
                movement.SetGoal(id, units.Position[id]);
                return;
            }

            Vector2 direction = offset == Vector2.Zero ? Vector2.Down : offset / distance;
            float approachRange = Mathf.Max(range, type.MinimumFireRange + 0.05f);
            for (int sample = 0; sample < 8; sample++)
            {
                int turn = (sample + 1) / 2 * (sample % 2 == 0 ? -1 : 1);
                Vector2 candidate = defender + direction.Rotated(turn * Mathf.Pi / 4f) * approachRange;
                if (!ClearApproach(candidate, type.Footprint, buildings.MovementField?.ForTier(type.SizeTier))) continue;
                movement.SetGoal(id, Ground(candidate));
                return;
            }
            units.Target[id] = -1;
        }

        float rangedReach = type.Ranged ? units.RangedReach(id, type) : 0f;
        float standOff = type.Ranged ? Mathf.Max(type.MinimumFireRange + 0.05f, rangedReach * 0.94f) :
            type.Footprint + ContactTolerance * 0.5f;
        float edgeDistance = p.DistanceTo(edge);
        bool clearShot = type.Ranged && edgeDistance <= rangedReach &&
            Sight(type, field)?.BlocksSightExcept(Ground(p), Ground(edge), buildings.Citadel.Area) != true;
        if (clearShot && edgeDistance >= type.MinimumFireRange)
        {
            movement.SetGoal(id, units.Position[id]);
            return;
        }
        // Use a point on an exposed face so large footprints can reach contact at corners.
        Vector2 normal = p - edge;
        if (Mathf.Abs(normal.X) >= Mathf.Abs(normal.Y))
        {
            float sign = normal.X != 0 ? Mathf.Sign(normal.X) : 1f;
            edge = new Vector2(sign > 0 ? citadelMax.X : citadelMin.X,
                Mathf.Clamp(edge.Y, citadelMin.Y + 0.05f, citadelMax.Y - 0.05f));
            normal = Vector2.Right * sign;
        }
        else
        {
            edge = new Vector2(Mathf.Clamp(edge.X, citadelMin.X + 0.05f, citadelMax.X - 0.05f),
                normal.Y > 0 ? citadelMax.Y : citadelMin.Y);
            normal = Vector2.Down * Mathf.Sign(normal.Y);
        }
        if (type.Ranged && !clearShot && edgeDistance <= rangedReach)
        {
            // Close along the original approach when cover blocks the firing position.
            standOff = Mathf.Max(type.Footprint + 0.05f, type.MinimumFireRange + 0.05f);
        }
        Vector2 approach = edge + normal * standOff;
        var movementField = buildings.MovementField?.ForTier(type.SizeTier);
        if (!ClearApproach(approach, type.Footprint, movementField))
            approach = FindCitadelApproach(p, standOff, type.Footprint, movementField);
        movement.SetGoal(id, Ground(approach));
    }

    private bool ClearApproach(Vector2 point, float radius, ObstacleField field) =>
        buildings.OnMap(new Vector3(point.X, 0f, point.Y), radius) &&
        field?.IntersectsCircle(point, radius) != true;

    private Vector2 FindCitadelApproach(Vector2 from, float standOff, float radius, ObstacleField field)
    {
        Vector2 best = from;
        float bestDistance = float.PositiveInfinity;
        for (int face = 0; face < 4; face++)
        {
            bool alongZ = face < 2;
            float low = (alongZ ? citadelMin.Y : citadelMin.X) + 0.05f;
            float high = (alongZ ? citadelMax.Y : citadelMax.X) - 0.05f;
            float fixedCoordinate = (face & 1) == 0
                ? (alongZ ? citadelMin.X : citadelMin.Y) - standOff
                : (alongZ ? citadelMax.X : citadelMax.Y) + standOff;
            for (int sample = 0; sample <= 4; sample++)
            {
                float along = Mathf.Lerp(low, high, sample * 0.25f);
                Vector2 candidate = alongZ ? new Vector2(fixedCoordinate, along) : new Vector2(along, fixedCoordinate);
                float distance = from.DistanceSquaredTo(candidate);
                if (distance >= bestDistance || !ClearApproach(candidate, radius, field)) continue;
                best = candidate;
                bestDistance = distance;
            }
        }
        return best;
    }

    private void AttackCitadel(int id, ObstacleField field, float dt)
    {
        ref readonly var type = ref units.Catalog[units.Type[id]];
        Vector2 from = ObstacleField.Xz(units.Position[id]);
        Vector2 edge = ClosestEdge(from);
        float distance = from.DistanceTo(edge);
        if (distance > Detection(id, type)) return;
        if (!type.Ranged)
        {
            if (distance > type.Footprint + ContactTolerance ||
                !SoldierFacing.Aim(units, id, edge - from, dt, 0.97f)) return;
            buildings.Citadel.Damage(type.AgainstStructures(type.Damage) * units.AttackMultiplier(id), type.StructureDamageType);
            units.Kill(id);
            return;
        }

        if (type.PrismBeam.Enabled || units.Cooldown[id] > 0 || distance > units.RangedReach(id, type) || distance < type.MinimumFireRange ||
            Sight(type, field)?.BlocksSightExcept(Ground(from), Ground(edge), buildings.Citadel.Area) == true) return;
        if (!SoldierFacing.Aim(units, id, edge - from, dt, type.Outline.IsChevron ? 0.995f : 0.97f)) return;
        Vector3 aim = Ground(edge) + Vector3.Up * 1.5f;
        if (Projectiles.FireAtStructure(id, units, aim, buildings.Citadel))
        {
            units.HitFeedback?.OnAttack(units, id);
            units.Cooldown[id] = type.Interval;
        }
    }

    private Vector3 Ground(Vector2 point) => units.OnTerrain(new Vector3(point.X, 0f, point.Y));
}
