// Runs authored waves and applies Fortress Defense target priority using the shared soldier systems.
using Godot;
using System.Diagnostics;

namespace MassiveRts;

internal sealed class FortressWaves
{
    private const float SpawnInterval = 0.16f;
    private const int AcquisitionInterval = 6;
    private const float DefenderMeleeDetectionMultiplier = 2f;
    private const float ContactTolerance = 0.02f;
    private readonly UnitState units;
    private readonly CommandBuildings buildings;
    private readonly CommandMovement movement;
    private readonly Vector3 spawn;
    private readonly Vector2 citadelMin, citadelMax;
    private readonly byte[][] waves;
    private readonly float intermission;
    private readonly BenchmarkConfig combat = new();
    private readonly SpatialGrid grid;
    private readonly StructureCombat structures;
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
    public int WaveNumber { get; private set; }
    public float Countdown { get; private set; }
    public bool Defeat => buildings.Citadel.Destroyed;
    public bool Victory { get; private set; }
    public bool WaitingToStart => WaveNumber == 0 && !Defeat;
    public int Remaining => WaveNumber == 0 ? 0 : units.FactionAlive[Factions.Red] + waves[WaveNumber - 1].Length - nextSpawn;

    public FortressWaves(UnitState units, CommandBuildings buildings, CommandMovement movement, Vector3 spawn,
        Godot.Collections.Array<WaveDefinition> definitions, float intermission)
    {
        this.units = units;
        this.buildings = buildings;
        this.movement = movement;
        structures = new StructureCombat(units, buildings.Structures);
        this.spawn = spawn;
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
        enemies = new int[maxWave];
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
        nextSpawn = enemyCount = 0;
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
        if (!NormalSeparation || units.Alive < 2) return moved;

        start = Stopwatch.GetTimestamp();
        separation.Step(units, 1, SeparationGap, combat.SeparationNeighbourBudget, SeparationPadding,
            tick, obstacles: buildings.Obstacles);
        SeparationMs += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        return moved || separation.MovedSoldiers;
    }

    public bool Step(float delta)
    {
        if (Defeat || Victory) return false;
        SampleTicks++;
        tick++;
        structureVisualChanged = false;
        if (WaitingToStart || Countdown > 0)
        {
            // Allow structure engagements and in-flight shots even between waves.
            var idleField = buildings.Obstacles;
            bool engageStructures = units.HasStructureAttackers;
            if (engageStructures) AcquireDefenderTargets(idleField, structuresOnly: true);
            bool idleMoved = MoveAndSeparate(delta);
            bool lungeChanged = false;
            int before = units.Alive;
            if (engageStructures)
            {
                lungeChanged = CombatSystem.Step(units, grid, Projectiles, 1f, delta, SeparationPadding, SeparationGap, obstacles: idleField);
                AttackStructures(delta, idleField, Factions.Blue);
            }
            if (Projectiles.Count > 0)
            {
                RebuildGrid();
                Projectiles.Step(units, grid, combat, delta, SeparationPadding, idleField);
            }
            if (Countdown > 0 && !Defeat)
            {
                Countdown = Mathf.Max(0, Countdown - delta);
                if (Countdown == 0) BeginNext();
            }
            return idleMoved || lungeChanged || structureVisualChanged || units.Alive != before;
        }

        int previousCount = units.Count, previousAlive = units.Alive;
        TryRelease(delta);
        var field = buildings.Obstacles;
        RebuildGrid();
        AcquireDefenderTargets(field);
        for (int i = 0; i < enemyCount; i++)
        {
            int id = enemies[i];
            if (units.Health[id] > 0) OrderEnemy(id, field);
        }
        bool moved = MoveAndSeparate(delta);
        if (moved) RebuildGrid();
        // Crossing into Citadel detection range suppresses defender attacks on this same tick.
        for (int i = 0; i < enemyCount; i++)
        {
            int id = enemies[i];
            if (units.Health[id] <= 0 || units.Target[id] < 0 ||
                units.Catalog[units.Type[id]].TargetsStructures) continue;
            Vector2 position = ObstacleField.Xz(units.Position[id]);
            float radius = Detection(units.Catalog[units.Type[id]]);
            int target = units.Target[id];
            if (position.DistanceSquaredTo(ClosestEdge(position)) <= radius * radius ||
                units.Health[target] <= 0 ||
                units.Position[id].DistanceSquaredTo(units.Position[target]) > radius * radius)
                units.Target[id] = -1;
        }
        CombatSystem.Step(units, grid, Projectiles, 1f, delta, SeparationPadding, SeparationGap, obstacles: field);

        if (units.HasStructureAttackers) AttackStructures(delta, field, Factions.Blue);
        for (int i = 0; i < enemyCount && !Defeat; i++)
        {
            int id = enemies[i];
            if (units.Health[id] <= 0 || units.Target[id] >= 0) continue;
            ref readonly var type = ref units.Catalog[units.Type[id]];
            if (type.TargetsStructures) structures.Attack(id, Projectiles, delta, Sight(type, field));
            else if (type.TargetsUnits) AttackCitadel(id, field);
        }
        Projectiles.Step(units, grid, combat, delta, SeparationPadding, field);
        if (!Defeat && Remaining == 0)
        {
            for (int id = 0; id < units.Count; id++)
                if (units.Faction[id] == Factions.Blue) movement.Disengage(id);
            if (WaveNumber == waves.Length) Victory = Projectiles.Count == 0;
            else Countdown = intermission;
        }

        // Shared combat can turn a soldier even when no one moves or dies.
        return moved || previousCount != units.Count || previousAlive != units.Alive || units.FactionAlive[Factions.Red] > 0;
    }

    private void TryRelease(float delta)
    {
        byte[] wave = waves[WaveNumber - 1];
        if (nextSpawn == wave.Length) return;
        spawnClock -= delta;
        if (spawnClock > 0) return;
        spawnClock = SpawnInterval;
        byte type = wave[nextSpawn];
        float radius = units.Catalog[type].Footprint;
        float spacing = radius * 2 + 0.5f;
        for (int i = 0; i < 17; i++)
        {
            int lane = (i + 1) / 2 * (i % 2 == 0 ? -1 : 1);
            Vector3 position = spawn + Vector3.Right * (lane * spacing);
            if (!buildings.IsSpawnClear(position, radius)) continue;
            int id = units.TrySpawn(type, Factions.Red, position);
            if (id < 0) return;
            buildings.ReservedWaveSlots--;
            enemies[enemyCount++] = id;
            nextSpawn++;
            return;
        }
    }

    private float Detection(in UnitStats type) =>
        type.Ranged ? type.Reach : Mathf.Max(combat.CellSize * 2f, type.Reach + units.MaxFootprint);

    private ObstacleField Sight(in UnitStats type, ObstacleField field) =>
        !type.Ranged || units.Catalog.Projectiles.Flight(type.ProjectileType).ObstacleFire ==
            ObstacleFireMode.RequiresDirectLineOfSight ? field : null;

    private void AttackStructures(float delta, ObstacleField field, byte faction)
    {
        for (int id = 0; id < units.Count; id++)
        {
            if (units.Faction[id] != faction || structures.Target(id) == null) continue;
            structureVisualChanged |= structures.Attack(id, Projectiles, delta, Sight(units.Catalog[units.Type[id]], field));
        }
    }

    private bool ChooseStructure(int id, in UnitStats type, int unitTarget, CommandStructure target, ObstacleField sight)
    {
        if (target == null) return false;
        if (unitTarget < 0 || units.Health[unitTarget] <= 0) return true;
        if (type.PreferredTarget == TargetPreference.Units) return false;
        // Keep fighting an immediate threat when the preferred structure still needs an approach.
        float reach = type.Ranged ? type.Reach :
            CombatSystem.MeleeContact(type.Footprint, units.Catalog[units.Type[unitTarget]].Footprint, SeparationGap) + 0.08f;
        return structures.CanHit(id, type, target, sight) ||
            units.Position[id].DistanceSquaredTo(units.Position[unitTarget]) > reach * reach;
    }

    private void AcquireDefenderTargets(ObstacleField field, bool structuresOnly = false)
    {
        for (int id = 0; id < units.Count; id++)
        {
            if (units.Health[id] <= 0 || units.Faction[id] != Factions.Blue) continue;
            ref readonly var type = ref units.Catalog[units.Type[id]];
            if (structuresOnly && !type.TargetsStructures) continue;
            bool attackMove = movement.IsAttackMove(id);
            bool canPursue = !type.Ranged && (!movement.HasCommand(id) || attackMove);
            float range = type.Ranged ? type.Reach : canPursue ? Detection(type) * DefenderMeleeDetectionMultiplier :
                type.Footprint + units.MaxFootprint + SeparationGap + 0.08f;
            int target = structuresOnly ? -1 : units.Target[id];
            Vector3 position = units.Position[id];
            var sight = Sight(type, field);
            if (attackMove && target >= 0 &&
                (units.Health[target] <= 0 || position.DistanceSquaredTo(units.Position[target]) > range * range ||
                 position.DistanceSquaredTo(units.Position[target]) < type.MinimumFireRange * type.MinimumFireRange ||
                 sight?.BlocksSight(position, units.Position[target]) == true))
                target = -1;

            bool refresh = id % AcquisitionInterval == tick % AcquisitionInterval;
            if (!structuresOnly && (!attackMove || target < 0) && refresh)
                target = grid.FindEnemy(units, id, range, combat.QueryCandidateLimit, tick, sight,
                    minimumRange: type.MinimumFireRange,
                    meleeReachPadding: type.Ranged || canPursue ? -1f : SeparationGap + 0.08f);
            if (type.TargetsStructures)
            {
                bool canApproach = !movement.HasCommand(id) || attackMove;
                float structureRange = canApproach ? type.StructureDetectionRange :
                    Mathf.Min(type.StructureDetectionRange, StructureCombat.Reach(type));
                CommandStructure structure = structures.Acquire(id, type, structureRange, refresh, canApproach, field, sight);
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
            float contact = CombatSystem.MeleeContact(type.Footprint, units.Catalog[units.Type[target]].Footprint, SeparationGap);
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
        float detect = Detection(type);
        int target = units.Target[id];
        bool refresh = id % AcquisitionInterval == tick % AcquisitionInterval;
        if (!type.TargetsStructures && p.DistanceSquaredTo(edge) <= detect * detect) target = -1;
        else
        {
            if (target >= 0 && (units.Health[target] <= 0 ||
                units.Position[id].DistanceSquaredTo(units.Position[target]) > detect * detect)) target = -1;
            if (target < 0 && refresh)
                target = grid.FindEnemy(units, id, detect, combat.QueryCandidateLimit, tick, Sight(type, field));
        }

        if (type.TargetsStructures)
        {
            var sight = Sight(type, field);
            CommandStructure structure = structures.Acquire(id, type, type.StructureDetectionRange, refresh, true, field, sight);
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
            float range = type.Ranged ? type.Reach * 0.94f :
                CombatSystem.MeleeContact(type.Footprint, units.Catalog[units.Type[target]].Footprint, SeparationGap);
            Vector2 offset = p - defender;
            float distance = offset.Length();
            if (distance <= range && distance >= type.MinimumFireRange)
                movement.SetGoal(id, units.Position[id]);
            else
                movement.SetGoal(id, Ground(defender + (offset == Vector2.Zero ? Vector2.Down : offset / distance) *
                    Mathf.Max(range, type.MinimumFireRange + 0.05f)));
            return;
        }

        float standOff = type.Ranged ? Mathf.Max(type.MinimumFireRange + 0.05f, type.Reach * 0.94f) :
            type.Footprint + ContactTolerance * 0.5f;
        float edgeDistance = p.DistanceTo(edge);
        bool clearShot = type.Ranged && edgeDistance <= type.Reach &&
            Sight(type, field)?.BlocksSightExcept(Ground(p), Ground(edge), buildings.Citadel.Area) != true;
        if (clearShot && edgeDistance <= type.Reach && edgeDistance >= type.MinimumFireRange)
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
        if (type.Ranged && !clearShot && edgeDistance <= type.Reach)
        {
            // Close along the original approach when cover blocks the firing position.
            standOff = Mathf.Max(type.Footprint + 0.05f, type.MinimumFireRange + 0.05f);
        }
        movement.SetGoal(id, Ground(edge + normal * standOff));
    }

    private void AttackCitadel(int id, ObstacleField field)
    {
        ref readonly var type = ref units.Catalog[units.Type[id]];
        Vector2 from = ObstacleField.Xz(units.Position[id]);
        Vector2 edge = ClosestEdge(from);
        float distance = from.DistanceTo(edge);
        if (distance > Detection(type)) return;
        if (!type.Ranged)
        {
            if (distance > type.Footprint + ContactTolerance) return;
            buildings.Citadel.Damage(type.Damage, type.DamageType);
            units.Kill(id);
            return;
        }

        if (units.Cooldown[id] > 0 || distance > type.Reach || distance < type.MinimumFireRange ||
            Sight(type, field)?.BlocksSightExcept(Ground(from), Ground(edge), buildings.Citadel.Area) == true) return;
        var heading = (edge - from).Normalized();
        units.Facing[id] = heading;
        units.Dirty[id] = true;
        Vector3 aim = Ground(edge) + Vector3.Up * 1.5f;
        if (Projectiles.FireAtStructure(id, units, aim, buildings.Citadel))
            units.Cooldown[id] = type.Interval;
    }

    private static Vector3 Ground(Vector2 point) => new(point.X, 0, point.Y);
}
