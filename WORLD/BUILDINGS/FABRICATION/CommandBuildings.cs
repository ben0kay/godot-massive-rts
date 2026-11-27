// Reserves building footprints and releases soldiers into checked exit and destination positions.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MassiveRts;

internal enum CommandBuildType { Fabricator, LaserTower, Extractor, RocketTower, ShieldGenerator, TwinMinigunTower, MissileBarrageTower, LongRangeAATower }

internal sealed class CommandBuildings
{
    private const int FabricatorLimit = 32;
    private const int LaserTowerLimit = CommandSandbox.MaxStartingTowers;
    internal const int LaserTowerCost = 750;
    internal const int RocketTowerCost = 1450;
    internal int TwinMinigunTowerCost => Mathf.Max(0, minigunDefinition.BuildCost);
    internal int MissileBarrageTowerCost => Mathf.Max(0, barrageDefinition.BuildCost);
    internal int LongRangeAATowerCost => Mathf.Max(0, heavyAADefinition.BuildCost);
    internal int ShieldGeneratorCost => Mathf.Max(0, shieldDefinition.BuildCost);
    private const float TowerBaseHeight = CommandLaserPlacement.BaseHeight;
    private readonly BuildingDefinition laserDefinition, rocketDefinition, shieldDefinition, minigunDefinition, barrageDefinition, heavyAADefinition;
    private readonly BuildingStats laserStats, rocketStats, shieldStats, minigunStats, barrageStats, heavyAAStats;
    internal string RocketTowerName => rocketStats.Name;
    private readonly RuntimeBuildingCatalog defenseCatalog;
    private DefenseRenderer defenseRenderer;
    private DefenseSystem defenses;
    private readonly Node3D root;
    private readonly UnitState units;
    private readonly CommandMovement movement;
    private readonly List<CommandStructure> structures = new();
    private readonly BatchRecipe[] recipes;
    private readonly ObstacleRectangle[] authoredWalls, paintedRocks, paintedWater, movementWater;
    private readonly Rect2[] bridges;
    private readonly List<CarbonDeposit> deposits = new();
    private readonly List<ExtractorJob> extractorJobs = new();
    private readonly CarbonExtractorSettings extractorSettings;
    private readonly byte workerType;
    private readonly Func<byte, bool> releaseWorker;

    private readonly record struct ExtractorJob(int Worker, CarbonDeposit Deposit, Vector3 Goal);
    public CitadelWorkerProduction Workers { get; }
    private readonly BuildingDefinition citadelDefinition, fabricatorDefinition;
    private ObstacleField field, baseMovementField;
    public readonly CarbonBalance Carbon;
    public CommandStructure Citadel { get; }
    public ObstacleField MovementField => field;
    public ObstacleField CoverField { get; private set; }
    public string ArmourName(CommandStructure building) => units.Catalog.DamageRules.ArmourName(building.ArmourClass);
    public int ReservedWaveSlots { get; set; }
    public IReadOnlyList<CommandStructure> Structures => structures;
    public BatchRecipe Recipe(int id) => recipes[id];
    public bool WorkerProductionOpen { get; set; } = true;
    public bool CanOperateCitadel => WorkerProductionOpen && !Citadel.Destroyed && Citadel.Ready;
    public bool QueueWorker() => Workers.Enqueue(CanOperateCitadel);

    public CommandBuildings(Node3D root, UnitState units, CommandMovement movement, float citadelHealth, ObstacleRectangle[] authoredWalls, Node3D citadelView, CitadelWorkerSettings workerSettings,
        ObstacleRectangle[] paintedRocks, ObstacleRectangle[] paintedWater, ObstacleRectangle[] movementWater,
        Rect2[] bridges, int startingCarbon)
    {
        Carbon = new CarbonBalance(startingCarbon);
        this.root = root;
        this.units = units;
        this.movement = movement;
        this.authoredWalls = authoredWalls;
        this.paintedRocks = paintedRocks;
        this.paintedWater = paintedWater;
        this.movementWater = movementWater;
        this.bridges = bridges;
        extractorSettings = GD.Load<CarbonExtractorSettings>(
            "res://WORLD/RESOURCES/CARBON/DefaultCarbonExtractorSettings.tres")
            ?? new CarbonExtractorSettings();
        if (!units.Catalog.TryResolve("worker", out workerType))
            throw new InvalidOperationException("Extractor construction requires the registered Worker unit.");
        recipes = GD.Load<ProductionRecipes>("res://GAMEPLAY/PRODUCTION/FabricatorRecipes.tres").Compile(units.Catalog);
        citadelDefinition = GD.Load<BuildingDefinition>("res://WORLD/BUILDINGS/DEFINITIONS/Citadel.tres");
        fabricatorDefinition = GD.Load<BuildingDefinition>("res://WORLD/BUILDINGS/DEFINITIONS/Fabricator001.tres");
        var defenseDefinitions = GD.Load<BuildingCatalog>("res://WORLD/BUILDINGS/CATALOG/DefaultBuildingCatalog.tres");
        laserDefinition = GD.Load<BuildingDefinition>(CommandLaserPlacement.DefinitionPath);
        rocketDefinition = GD.Load<BuildingDefinition>("res://WORLD/BUILDINGS/DEFINITIONS/RocketTower001.tres");
        shieldDefinition = GD.Load<BuildingDefinition>("res://WORLD/BUILDINGS/DEFINITIONS/ShieldGeneratorTower.tres");
        minigunDefinition = GD.Load<BuildingDefinition>("res://WORLD/BUILDINGS/DEFINITIONS/TwinMinigunTower.tres");
        barrageDefinition = GD.Load<BuildingDefinition>("res://WORLD/BUILDINGS/DEFINITIONS/MissileBarrageTower.tres");
        heavyAADefinition = GD.Load<BuildingDefinition>("res://WORLD/BUILDINGS/DEFINITIONS/LongRangeAATower.tres");
        defenseCatalog = new RuntimeBuildingCatalog(defenseDefinitions, units.Catalog.DamageRules, units.Catalog.Projectiles);
        if (!defenseCatalog.TryResolve(laserDefinition.Id, out int laserType))
            throw new InvalidOperationException("The Railgun Tower is missing from the building catalogue.");
        if (!defenseCatalog.TryResolve(rocketDefinition.Id, out int rocketType))
            throw new InvalidOperationException("The Broadside Artillery Tower is missing from the building catalogue.");
        laserStats = defenseCatalog[laserType];
        rocketStats = defenseCatalog[rocketType];
        if (!defenseCatalog.TryResolve(shieldDefinition.Id, out int shieldType))
            throw new InvalidOperationException("The Shield Generator is missing from the building catalogue.");
        shieldStats = defenseCatalog[shieldType];
        if (!defenseCatalog.TryResolve(minigunDefinition.Id, out int minigunType))
            throw new InvalidOperationException("The Twin Minigun Tower is missing from the building catalogue.");
        minigunStats = defenseCatalog[minigunType];
        if (!defenseCatalog.TryResolve(barrageDefinition.Id, out int barrageType) || !defenseCatalog.TryResolve(heavyAADefinition.Id, out int heavyAAType))
            throw new InvalidOperationException("An anti-air tower is missing from the building catalogue.");
        barrageStats = defenseCatalog[barrageType];
        heavyAAStats = defenseCatalog[heavyAAType];
        var footprint = new ObstacleRectangle(-4, -9, 5, 5);
        if (citadelView != null)
        {
            Vector3 position = citadelView.GlobalPosition;
            footprint = new ObstacleRectangle(Mathf.RoundToInt(position.X / DeploymentLayout.CellSize - 2.5f),
                Mathf.RoundToInt(position.Z / DeploymentLayout.CellSize - 2.5f), 5, 5);
            if (!FitsMap(footprint))
            {
                GD.PushWarning("Citadel must fit inside the map. Using the default position.");
                footprint = new ObstacleRectangle(-4, -9, 5, 5);
            }
        }
        else citadelView = GD.Load<PackedScene>("res://WORLD/BUILDINGS/CITADEL/Citadel.tscn").Instantiate<Node3D>();

        float citadelHeightOnTerrain = FoundationHeight(footprint, out float citadelRelief);
        if (citadelRelief > 0.5f) GD.PushWarning("Citadel footprint crosses uneven terrain. Use a flatter site.");
        Citadel = new CommandStructure(footprint, citadelHealth > 0f ? citadelHealth : citadelDefinition.Health,
            Factions.Blue, citadelDefinition, units.Catalog.DamageRules, readyView: citadelView, groundHeight: citadelHeightOnTerrain);
        Add(Citadel);
        citadelView.GlobalPosition = Citadel.Center;
        Workers = new CitadelWorkerProduction(workerSettings, units.Catalog, Carbon);
        releaseWorker = type => Release(Citadel, type);
        RegisterDeposits(root);
        if (root.GetNodeOrNull<Node3D>("Resources") is Node3D resources) RegisterDeposits(resources);
        if (deposits.Count > 0) RebuildObstacles();
    }

    private void RegisterDeposits(Node container)
    {
        foreach (Node child in container.GetChildren())
        {
            if (child is not CarbonDeposit deposit) continue;
            ObstacleRectangle area = deposit.Footprint();
            if (!FitsMap(area))
            {
                GD.PushWarning($"Carbon deposit '{deposit.Name}' is outside the command map.");
                deposit.Hide();
                continue;
            }
            deposit.PlaceOnMap(area, Center(area), root.GetPathTo(deposit).ToString());
            deposits.Add(deposit);
        }
    }

    public bool CanPlaceExtractor(CarbonDeposit deposit) => TryPlanExtractor(deposit, out _, out _);

    public bool PlaceExtractor(CarbonDeposit deposit)
    {
        if (!TryPlanExtractor(deposit, out int worker, out Vector3 goal)) return false;
        movement.SetGoal(worker, goal);
        units.Target[worker] = -1;
        extractorJobs.Add(new ExtractorJob(worker, deposit, goal));
        deposit.WorkerAssigned = true;
        return true;
    }

    private bool TryPlanExtractor(CarbonDeposit deposit, out int worker, out Vector3 goal)
    {
        worker = -1;
        goal = default;
        if (!CanOperateCitadel || !GodotObject.IsInstanceValid(deposit) || !deposits.Contains(deposit) ||
            deposit.Depleted || deposit.WorkerAssigned || OverlapsPaint(deposit.Area) ||
            units.Wrecks?.OverlapsBuildingHusk(deposit.Area) == true ||
            deposit.Extractor is { Destroyed: true } or { BuildProgress: >= 1f }) return false;

        float nearest = float.PositiveInfinity;
        Vector2 center = (deposit.Area.Min + deposit.Area.Max) * 0.5f;
        for (int id = 0; id < units.Count; id++)
        {
            if (units.Type[id] != workerType || units.Faction[id] != Factions.Blue ||
                units.Health[id] <= 0f) continue;
            float distance = ObstacleField.Xz(units.Position[id]).DistanceSquaredTo(center);
            if (distance >= nearest || HasExtractorJob(id)) continue;
            nearest = distance;
            worker = id;
        }

        return worker >= 0 && TryExtractorWorksite(units.Position[worker], deposit.Area,
            units.Catalog[workerType].Footprint, out goal);
    }

    private bool HasExtractorJob(int worker)
    {
        foreach (var job in extractorJobs)
            if (job.Worker == worker) return true;
        return false;
    }

    private bool TryExtractorWorksite(Vector3 from, ObstacleRectangle area, float radius, out Vector3 goal)
    {
        float clearance = radius + 0.40f;
        Vector2 min = area.Min, max = area.Max, center = (min + max) * 0.5f;
        Vector2[] points =
        {
            new(center.X, min.Y - clearance), new(max.X + clearance, center.Y),
            new(center.X, max.Y + clearance), new(min.X - clearance, center.Y)
        };
        float best = float.PositiveInfinity;
        goal = default;
        foreach (Vector2 point in points)
        {
            Vector3 candidate = units.OnTerrain(new Vector3(point.X, 0f, point.Y));
            float distance = ObstacleField.Xz(candidate - from).LengthSquared();
            if (distance >= best || !OnMap(candidate, radius) ||
                field?.IntersectsCircle(point, radius) == true) continue;
            best = distance;
            goal = candidate;
        }
        return float.IsFinite(best);
    }

    private void StepExtractorJobs(float delta)
    {
        for (int i = extractorJobs.Count - 1; i >= 0; i--)
        {
            ExtractorJob job = extractorJobs[i];
            int id = job.Worker;
            CarbonDeposit deposit = job.Deposit;
            bool cancelled = (uint)id >= (uint)units.Count || units.Health[id] <= 0f ||
                deposit.Depleted || deposit.Extractor?.Destroyed == true ||
                units.Goal[id].DistanceSquaredTo(job.Goal) > 0.01f;
            if (cancelled)
            {
                deposit.WorkerAssigned = false;
                extractorJobs.RemoveAt(i);
                continue;
            }
            if (ObstacleField.Xz(units.Position[id] - job.Goal).LengthSquared() > 0.20f * 0.20f)
                continue;

            if (deposit.Extractor == null)
            {
                var extractor = new CarbonExtractor(deposit, extractorSettings, units.Catalog.DamageRules);
                deposit.Extractor = extractor;
                Add(extractor);
            }
            deposit.Extractor.Work(delta);
            if (deposit.Extractor.BuildProgress < 1f) continue;
            deposit.WorkerAssigned = false;
            extractorJobs.RemoveAt(i);
        }
    }

    public float MapHalfWidth => movement.MapHalfWidth;
    public bool OnMap(Vector3 point, float radius = 0f) => movement.OnMap(point, radius);

    private bool FitsMap(ObstacleRectangle area) => area.IsValid &&
        OnMap(new Vector3(area.Min.X, 0f, area.Min.Y)) &&
        OnMap(new Vector3(area.Max.X, 0f, area.Max.Y));

    public ObstacleRectangle Snap(Vector3 point, CommandBuildType type = CommandBuildType.Fabricator)
    {
        int width = type == CommandBuildType.LaserTower ? laserStats.Width :
            type == CommandBuildType.RocketTower ? rocketStats.Width :
            type == CommandBuildType.TwinMinigunTower ? minigunStats.Width :
            type == CommandBuildType.MissileBarrageTower ? barrageStats.Width :
            type == CommandBuildType.LongRangeAATower ? heavyAAStats.Width :
            type == CommandBuildType.ShieldGenerator ? shieldStats.Width : CommandStructure.FabricatorCells;
        int depth = type == CommandBuildType.LaserTower ? laserStats.Depth :
            type == CommandBuildType.RocketTower ? rocketStats.Depth :
            type == CommandBuildType.TwinMinigunTower ? minigunStats.Depth :
            type == CommandBuildType.MissileBarrageTower ? barrageStats.Depth :
            type == CommandBuildType.LongRangeAATower ? heavyAAStats.Depth :
            type == CommandBuildType.ShieldGenerator ? shieldStats.Depth : CommandStructure.FabricatorCells;
        const float grid = DeploymentLayout.CellSize;
        return new ObstacleRectangle(Mathf.RoundToInt(point.X / grid - width * 0.5f),
            Mathf.RoundToInt(point.Z / grid - depth * 0.5f), width, depth);
    }

    public Vector3 Center(ObstacleRectangle area)
    {
        Vector2 center = (area.Min + area.Max) * 0.5f;
        return new Vector3(center.X, FoundationHeight(area, out _), center.Y);
    }

    // Sample vertices inside the footprint as well as its boundary, using the gameplay surface.
    public float FoundationHeight(ObstacleRectangle area, out float relief)
    {
        relief = 0f;
        var terrain = units.Grounding?.Field;
        if (terrain == null) return 0f;
        return terrain.FootprintHeight(area.Min, area.Max, out relief);
    }

    public bool CanPlace(ObstacleRectangle area, CommandBuildType type)
    {
        if (type == CommandBuildType.Extractor || !FitsMap(area) || Carbon.Amount < Cost(type) || OverlapsPaint(area) ||
            units.Wrecks?.OverlapsBuildingHusk(area) == true) return false;
        int towerCount = defenses?.ActiveCount ?? 0;
        if ((type is CommandBuildType.LaserTower or CommandBuildType.RocketTower or CommandBuildType.ShieldGenerator or CommandBuildType.TwinMinigunTower or CommandBuildType.MissileBarrageTower or CommandBuildType.LongRangeAATower) &&
            towerCount >= LaserTowerLimit) return false;

        FoundationHeight(area, out float relief);
        if (relief > 0.5f) return false;
        Vector2 min = area.Min, max = area.Max;
        foreach (var deposit in deposits)
            if (Overlaps(min, max, deposit.Area)) return false;
        foreach (var wall in authoredWalls)
            if (Overlaps(min, max, wall)) return false;
        int fabricators = 0;
        foreach (var building in structures)
        {
            if (building.Destroyed || building is CarbonExtractor) continue;
            if (building.Production != null) fabricators++;
            if (Overlaps(min, max, building.Area)) return false;
        }
        if (type == CommandBuildType.Fabricator && fabricators >= FabricatorLimit) return false;
        for (int id = 0; id < units.Count; id++)
        {
            if (units.Health[id] <= 0) continue;
            Vector2 p = ObstacleField.Xz(units.Position[id]);
            Vector2 closest = new(Mathf.Clamp(p.X, min.X, max.X), Mathf.Clamp(p.Y, min.Y, max.Y));
            float radius = units.Catalog[units.Type[id]].Footprint;
            if (p.DistanceSquaredTo(closest) <= radius * radius) return false;
        }
        return true;
    }

    private bool OverlapsPaint(ObstacleRectangle area)
    {
        Vector2 min = area.Min, max = area.Max;
        foreach (var rock in paintedRocks)
            if (Overlaps(min, max, rock)) return true;
        foreach (var water in paintedWater)
            if (Overlaps(min, max, water)) return true;
        return false;
    }

    private static bool Overlaps(Vector2 min, Vector2 max, ObstacleRectangle other)
    {
        Vector2 otherMin = other.Min, otherMax = other.Max;
        return min.X < otherMax.X && max.X > otherMin.X && min.Y < otherMax.Y && max.Y > otherMin.Y;
    }

    private int Cost(CommandBuildType type) =>
        type == CommandBuildType.LaserTower ? LaserTowerCost :
        type == CommandBuildType.RocketTower ? RocketTowerCost :
        type == CommandBuildType.TwinMinigunTower ? TwinMinigunTowerCost :
        type == CommandBuildType.MissileBarrageTower ? MissileBarrageTowerCost :
        type == CommandBuildType.LongRangeAATower ? LongRangeAATowerCost :
        type == CommandBuildType.ShieldGenerator ? ShieldGeneratorCost : CommandStructure.FabricatorCost;

    public bool Place(ObstacleRectangle area, CommandBuildType type)
    {
        if (!CanPlace(area, type) || !Carbon.TrySpend(Cost(type))) return false;
        float height = FoundationHeight(area, out _);
        if (type == CommandBuildType.LaserTower)
            AddTower(area, Factions.Blue, height, laserDefinition, laserStats);
        else if (type == CommandBuildType.RocketTower)
            AddTower(area, Factions.Blue, height, rocketDefinition, rocketStats);
        else if (type == CommandBuildType.TwinMinigunTower)
            AddTower(area, Factions.Blue, height, minigunDefinition, minigunStats);
        else if (type == CommandBuildType.MissileBarrageTower)
            AddTower(area, Factions.Blue, height, barrageDefinition, barrageStats);
        else if (type == CommandBuildType.LongRangeAATower)
            AddTower(area, Factions.Blue, height, heavyAADefinition, heavyAAStats);
        else if (type == CommandBuildType.ShieldGenerator)
            AddTower(area, Factions.Blue, height, shieldDefinition, shieldStats);
        else
            Add(new CommandStructure(area, fabricatorDefinition.Health, Factions.Blue, fabricatorDefinition,
                units.Catalog.DamageRules, Release, groundHeight: height));
        return true;
    }

    internal void AddStartingTower(CommandLaserPlacement placement)
    {
        var definition = placement.EffectiveDefinition;
        if (!defenseCatalog.TryResolve(definition.Id, out int type))
        {
            GD.PushWarning($"Starting defense '{definition.Id}' is missing from the building catalogue.");
            return;
        }
        var area = placement.Footprint(placement.GlobalPosition);
        float height = FoundationHeight(area, out float relief);
        if (!FitsMap(area) || relief > 0.5f || defenses?.ActiveCount >= LaserTowerLimit || OverlapsPaint(area) ||
            authoredWalls.Any(wall => Overlaps(area.Min, area.Max, wall)) ||
            deposits.Any(deposit => Overlaps(area.Min, area.Max, deposit.Area)) ||
            structures.Any(structure => Overlaps(area.Min, area.Max, structure.Area)))
        {
            GD.PushWarning("A starting defense was skipped because its site is blocked or uneven, or the tower limit was reached.");
            return;
        }
        AddTower(area, placement.Faction, height, definition, defenseCatalog[type]);
    }

    private void AddTower(ObstacleRectangle area, byte faction, float height, BuildingDefinition definition, BuildingStats stats)
    {
        if (defenses == null)
        {
            defenseRenderer = new DefenseRenderer { Name = "CommandDefenseRenderer" };
            root.AddChild(defenseRenderer);
            defenseRenderer.Initialize(Array.Empty<BuildingPlacement>(), defenseCatalog, TowerBaseHeight, battle: true);
            defenses = new DefenseSystem(defenseRenderer, LaserTowerLimit);
        }
        var tower = new CommandStructure(area, stats.Health, faction, definition,
            units.Catalog.DamageRules, readyView: new Node3D { Name = stats.AirDefense.Enabled ? stats.Id : stats.Id == TwinMinigunTowerMeshes.Id ? "TwinMinigunTower" : stats.ShieldEmitter.Enabled ? "ShieldGenerator" : stats.ProjectileType >= 0 ? "RocketTower" : "RailgunTower" }, groundHeight: height) { IsTower = true };
        defenses.AddTower(tower, stats, TowerBaseHeight, height);
        if (stats.ShieldEmitter.Enabled) units.AddShieldTower(tower, stats.ShieldEmitter);
        Add(tower);
    }

    public bool HasDefenses => defenses?.ActiveCount > 0;
    public void StepDefenses(SpatialGrid grid, ProjectileSystem projectiles, float delta, int tick) =>
        defenses?.Step(units, grid, CoverField, projectiles, delta, tick, 1f / 60f);
    internal void FinishDefenseEffects(float delta) =>
        defenses?.Step(units, null, CoverField, null, delta, -1, 1f / 60f);
    public void AdvanceDefenseEffects(float delta) => defenseRenderer?.Advance(delta);

    public void AddHostileFabricator(Vector3 point) => AddStartingFabricator(point, Factions.Red);

    public void AddStartingFabricator(Vector3 point, byte faction)
    {
        var area = Snap(point);
        if (!FitsMap(area) || OverlapsPaint(area)) return;
        float height = FoundationHeight(area, out float relief);
        if (faction == Factions.Blue && (relief > 0.5f ||
            authoredWalls.Any(wall => Overlaps(area.Min, area.Max, wall)) ||
            deposits.Any(deposit => Overlaps(area.Min, area.Max, deposit.Area)) ||
            structures.Any(structure => Overlaps(area.Min, area.Max, structure.Area))))
        {
            GD.PushWarning("A starting Fabricator overlaps an obstacle or sits on uneven terrain.");
            return;
        }
        var fabricator = new CommandStructure(area, fabricatorDefinition.Health, faction, fabricatorDefinition,
            units.Catalog.DamageRules, faction == Factions.Blue ? Release : null, groundHeight: height);
        if (faction == Factions.Blue) fabricator.Construct(float.MaxValue);
        Add(fabricator);
    }

    private void Add(CommandStructure building)
    {
        structures.Add(building);
        if (building.View.GetParent() == null) root.AddChild(building.View);
        if (building is not CarbonExtractor) RebuildObstacles();
    }

    private void RebuildObstacles()
    {
        var areas = new List<ObstacleRectangle>(authoredWalls.Length + paintedRocks.Length + structures.Count + deposits.Count);
        areas.AddRange(authoredWalls);
        areas.AddRange(paintedRocks);
        foreach (var deposit in deposits) areas.Add(deposit.Area);
        foreach (var structure in structures)
            if (!structure.Destroyed && structure is not CarbonExtractor) areas.Add(structure.Area);
        CoverField = areas.Count == 0 ? null : new ObstacleField(areas.ToArray(), movement.MapHalfWidth);
        areas.AddRange(movementWater);
        baseMovementField = areas.Count == 0 ? null : movementWater.Length == 0 && bridges.Length == 0
            ? CoverField : new ObstacleField(areas.ToArray(), movement.MapHalfWidth, bridges);
        RefreshWreckObstacles();
    }

    internal void RefreshWreckObstacles()
    {
        field = units.Wrecks?.Combine(baseMovementField, movement.MapHalfWidth) ?? baseMovementField;
        movement.SetObstacles(field);
    }

    public void RemoveDestroyed(ShockwaveRenderer effects = null)
    {
        bool removed = false, obstaclesChanged = false;
        foreach (var building in structures)
        {
            if (!building.Destroyed) continue;
            effects?.EmitBuildingDestruction(building, units.Grounding?.Field);
            if (units.Wrecks?.TryAddBuilding(building, building.Center.Y) == true) obstaclesChanged = true;
            if (building is CarbonExtractor extractor)
            {
                var deposit = extractor.Deposit;
                extractorJobs.RemoveAll(job => job.Deposit == deposit);
                deposit.WorkerAssigned = false;
                deposit.Extractor = null;
            }
            else
            {
                if (building != Citadel && building.Production == null) defenses?.RemoveTower(building);
                obstaclesChanged = true;
            }
            building.View.QueueFree();
            removed = true;
        }
        if (removed)
            structures.RemoveAll(building => building.Destroyed);
        if (obstaclesChanged) RebuildObstacles();
    }

    public void Step(float delta)
    {
        StepExtractorJobs(delta);
        if (CanOperateCitadel) Workers.Step(delta, releaseWorker);
        foreach (var building in structures)
        {
            building.Construct(delta);
            if (building is CarbonExtractor extractor) extractor.Extract(delta, Carbon);
            if (!building.Destroyed && building.Ready && building.Release != null) building.Production?.Step(delta, recipes, Carbon, building.Release);
        }
    }

    public bool CanSetRally(CommandStructure building) =>
        building != null && building.Ready && !building.Destroyed && (building == Citadel || building.Release != null);

    public bool CanRally(Vector3 point) =>
        OnMap(point, units.Catalog.MaxFootprint) &&
        field?.IntersectsCircle(ObstacleField.Xz(point), units.Catalog.MaxFootprint) != true;

    private bool Release(CommandStructure building, byte type)
    {
        building.UnitCapBlocked = units.Count >= units.Position.Length - ReservedWaveSlots;
        if (building.UnitCapBlocked) return false;
        float radius = units.Catalog[type].Footprint;
        Vector3 spawn = building.FrontEdge + Vector3.Back * (radius + 0.15f);
        Vector3 exit = spawn + Vector3.Back * (radius + 1.0f);
        bool workerExit = building == Citadel;
        if (!IsClearGround(spawn, radius) || !IsClearGround(exit, radius) ||
            field?.BlocksCorridor(ObstacleField.Xz(spawn), ObstacleField.Xz(exit), radius) == true ||
            (workerExit && !SpawnClear(spawn, radius))) return false;

        Vector3 anchor = building.Rally ?? (exit + Vector3.Back * (3f + radius));
        Vector3? destination = FindReleaseGoal(building, anchor, spawn, radius, workerExit);
        if (workerExit && !destination.HasValue) return false;

        // Fabricator launches may overlap. Separation sorts out the units during movement.
        int id = units.TrySpawn(type, Factions.Blue, spawn);
        if (id < 0) return false;
        if (units.Catalog[type].Shape != UnitShape.Circle) units.Facing[id] = Vector2.Down;
        movement.SetGoal(id, destination ?? exit, exit);
        return true;
    }

    private Vector3? FindReleaseGoal(CommandStructure building, Vector3 anchor, Vector3 spawn, float radius, bool workerExit)
    {
        float spacing = radius * 2f + 0.45f;
        float minimumDistanceSquared = (radius + 2f) * (radius + 2f);
        if (!workerExit)
        {
            // Distribute arrivals around the flag without reserving slots or checking live units.
            const int columns = 5, slots = 15;
            for (int attempt = 0; attempt < slots; attempt++)
            {
                int slot = (building.NextReleaseSlot + attempt) % slots;
                Vector3 goal = anchor + Vector3.Right * ((slot % columns - 2) * spacing)
                    + Vector3.Back * ((slot / columns - 1) * spacing);
                if (goal.DistanceSquaredTo(spawn) < minimumDistanceSquared || !IsClearGround(goal, radius)) continue;
                building.NextReleaseSlot = (slot + 1) % slots;
                return goal;
            }
        }

        for (int row = 0; row < 16; row++)
        for (int column = 0; column < 11; column++)
        {
            int lateral = (column + 1) / 2 * (column % 2 == 0 ? -1 : 1);
            Vector3 goal = anchor + Vector3.Right * (lateral * spacing) + Vector3.Back * (row * spacing);
            if (goal.DistanceSquaredTo(spawn) < minimumDistanceSquared) continue;
            if (workerExit ? FreePosition(goal, radius, true) : IsClearGround(goal, radius)) return goal;
        }
        return null;
    }

    // Citadel Worker releases still wait for the hatch to clear.
    private bool SpawnClear(Vector3 spawn, float radius)
    {
        for (int id = 0; id < units.Count; id++)
        {
            if (units.Health[id] <= 0) continue;
            float clearance = radius + units.Catalog[units.Type[id]].Footprint + 0.08f;
            if (ObstacleField.Xz(units.Position[id] - spawn).LengthSquared() < clearance * clearance) return false;
        }
        return true;
    }

    public bool IsSpawnClear(Vector3 point, float radius) => FreePosition(point, radius, false);

    private bool IsClearGround(Vector3 point, float radius) =>
        OnMap(point, radius) && field?.IntersectsCircle(ObstacleField.Xz(point), radius) != true;

    private bool FreePosition(Vector3 point, float radius, bool checkGoals)
    {
        if (!IsClearGround(point, radius)) return false;
        for (int id = 0; id < units.Count; id++)
        {
            if (units.Health[id] <= 0) continue;
            float clearance = radius + units.Catalog[units.Type[id]].Footprint + 0.08f;
            float squared = clearance * clearance;
            if (ObstacleField.Xz(point - units.Position[id]).LengthSquared() < squared ||
                checkGoals && ObstacleField.Xz(point - units.Goal[id]).LengthSquared() < squared) return false;
        }
        return true;
    }

    public bool TryGround(Camera3D camera, Vector2 point, out Vector3 ground)
    {
        Vector3 origin = camera.ProjectRayOrigin(point);
        Vector3 ray = camera.ProjectRayNormal(point);
        ground = default;
        if (ray.Y >= -0.00001f || origin.Y < 0f) return false;
        Vector3 toFlat = ray * (-origin.Y / ray.Y);
        var terrain = units.Grounding?.Field;
        float hit = terrain?.SurfaceHit(origin, toFlat, 1f) ?? 1f;
        ground = origin + toFlat * Mathf.Min(1f, hit);
        ground = units.OnTerrain(ground);
        return true;
    }

    public CommandStructure Pick(Camera3D camera, Vector2 pointer, out CarbonDeposit deposit)
    {
        deposit = null;
        CommandStructure picked = null;
        float nearest = float.PositiveInfinity;
        Vector3 origin = camera.ProjectRayOrigin(pointer);
        Vector3 ray = camera.ProjectRayNormal(pointer);
        foreach (var building in structures)
        {
            if (building.Destroyed) continue;
            float height = building == Citadel ? 4.3f :
                building is CarbonExtractor ? 3.15f : building.Production == null ? TowerBaseHeight + 0.7f : 1.95f;
            Vector2 min = building.Area.Min, max = building.Area.Max;
            float start = 0f, end = nearest;
            if (!ClipRay(origin.X, ray.X, min.X, max.X, ref start, ref end) ||
                !ClipRay(origin.Y, ray.Y, building.Center.Y, building.Center.Y + height, ref start, ref end) ||
                !ClipRay(origin.Z, ray.Z, min.Y, max.Y, ref start, ref end)) continue;
            nearest = start;
            picked = building;
        }
        // Static resource checks are only made on a click.
        foreach (var resource in deposits)
        {
            if (!GodotObject.IsInstanceValid(resource)) continue;
            Vector2 min = resource.Area.Min, max = resource.Area.Max;
            float start = 0f, end = nearest;
            float y = resource.GlobalPosition.Y;
            if (!ClipRay(origin.X, ray.X, min.X, max.X, ref start, ref end) ||
                !ClipRay(origin.Y, ray.Y, y, y + resource.PickHeight, ref start, ref end) ||
                !ClipRay(origin.Z, ray.Z, min.Y, max.Y, ref start, ref end) || start >= nearest) continue;
            nearest = start;
            picked = null;
            deposit = resource;
        }
        return picked;
    }

    private static bool ClipRay(float origin, float direction, float min, float max, ref float start, ref float end)
    {
        if (Mathf.Abs(direction) < 0.00001f) return origin >= min && origin <= max;
        float a = (min - origin) / direction, b = (max - origin) / direction;
        start = Mathf.Max(start, Mathf.Min(a, b));
        end = Mathf.Min(end, Mathf.Max(a, b));
        return start <= end;
    }
}
