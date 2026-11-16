// Reserves building footprints and releases soldiers into checked exit and destination positions.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal enum CommandBuildType { Fabricator, LaserTower }

internal sealed class CommandBuildings
{
    private const int FabricatorLimit = 32;
    private const int LaserTowerLimit = 32;
    internal const int LaserTowerCost = 750;
    private const float TowerBaseHeight = 1f;
    private readonly BuildingDefinition laserDefinition;
    private readonly BuildingStats laserStats;
    private readonly RuntimeBuildingCatalog defenseCatalog;
    private DefenseRenderer defenseRenderer;
    private DefenseSystem defenses;
    private readonly Node3D root;
    private readonly UnitState units;
    private readonly CommandMovement movement;
    private readonly List<CommandStructure> structures = new();
    private readonly BatchRecipe[] recipes;
    private readonly ObstacleRectangle[] authoredWalls;
    private readonly List<CarbonDeposit> deposits = new();
    private readonly Func<byte, bool> releaseWorker;
    public CitadelWorkerProduction Workers { get; }
    private readonly BuildingDefinition citadelDefinition, fabricatorDefinition;
    private ObstacleField field;
    public readonly CarbonBalance Carbon = new();
    public CommandStructure Citadel { get; }
    public ObstacleField Obstacles => field;
    public string ArmourName(CommandStructure building) => units.Catalog.DamageRules.ArmourName(building.ArmourClass);
    public int ReservedWaveSlots { get; set; }
    public IReadOnlyList<CommandStructure> Structures => structures;
    public BatchRecipe Recipe(int id) => recipes[id];
    public bool WorkerProductionOpen { get; set; } = true;
    public bool CanOperateCitadel => WorkerProductionOpen && !Citadel.Destroyed && Citadel.Ready;
    public bool QueueWorker() => Workers.Enqueue(CanOperateCitadel);

    public CommandBuildings(Node3D root, UnitState units, CommandMovement movement, float citadelHealth, ObstacleRectangle[] authoredWalls, Node3D citadelView, CitadelWorkerSettings workerSettings)
    {
        this.root = root;
        this.units = units;
        this.movement = movement;
        this.authoredWalls = authoredWalls;
        recipes = GD.Load<ProductionRecipes>("res://GAMEPLAY/PRODUCTION/FabricatorRecipes.tres").Compile(units.Catalog);
        citadelDefinition = GD.Load<BuildingDefinition>("res://WORLD/BUILDINGS/DEFINITIONS/Citadel.tres");
        fabricatorDefinition = GD.Load<BuildingDefinition>("res://WORLD/BUILDINGS/DEFINITIONS/Fabricator001.tres");
        var defenseDefinitions = GD.Load<BuildingCatalog>("res://WORLD/BUILDINGS/CATALOG/DefaultBuildingCatalog.tres");
        laserDefinition = GD.Load<BuildingDefinition>("res://WORLD/BUILDINGS/DEFINITIONS/LaserTower001.tres");
        defenseCatalog = new RuntimeBuildingCatalog(defenseDefinitions, units.Catalog.DamageRules);
        if (!defenseCatalog.TryResolve(laserDefinition.Id, out int laserType))
            throw new InvalidOperationException("The Laser Tower is missing from the building catalogue.");
        laserStats = defenseCatalog[laserType];
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

    public float MapHalfWidth => movement.MapHalfWidth;
    public bool OnMap(Vector3 point, float radius = 0f) => movement.OnMap(point, radius);

    private bool FitsMap(ObstacleRectangle area) => area.IsValid &&
        OnMap(new Vector3(area.Min.X, 0f, area.Min.Y)) &&
        OnMap(new Vector3(area.Max.X, 0f, area.Max.Y));

    public ObstacleRectangle Snap(Vector3 point, CommandBuildType type = CommandBuildType.Fabricator)
    {
        int width = type == CommandBuildType.LaserTower ? laserStats.Width : CommandStructure.FabricatorCells;
        int depth = type == CommandBuildType.LaserTower ? laserStats.Depth : CommandStructure.FabricatorCells;
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
        if (!FitsMap(area) || Carbon.Amount < Cost(type)) return false;
        int towerCount = defenses?.ActiveCount ?? 0;
        if (type == CommandBuildType.LaserTower ? towerCount >= LaserTowerLimit
            : structures.Count - towerCount >= FabricatorLimit + 1) return false;

        FoundationHeight(area, out float relief);
        if (relief > 0.5f) return false;
        Vector2 min = area.Min, max = area.Max;
        foreach (var deposit in deposits)
            if (Overlaps(min, max, deposit.Area)) return false;
        foreach (var wall in authoredWalls)
            if (Overlaps(min, max, wall)) return false;
        foreach (var building in structures)
        {
            if (building.Destroyed) continue;
            if (Overlaps(min, max, building.Area)) return false;
        }
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

    private static bool Overlaps(Vector2 min, Vector2 max, ObstacleRectangle other)
    {
        Vector2 otherMin = other.Min, otherMax = other.Max;
        return min.X < otherMax.X && max.X > otherMin.X && min.Y < otherMax.Y && max.Y > otherMin.Y;
    }

    private static int Cost(CommandBuildType type) =>
        type == CommandBuildType.LaserTower ? LaserTowerCost : CommandStructure.FabricatorCost;

    public bool Place(ObstacleRectangle area, CommandBuildType type)
    {
        if (!CanPlace(area, type) || !Carbon.TrySpend(Cost(type))) return false;
        float height = FoundationHeight(area, out _);
        if (type == CommandBuildType.LaserTower)
        {
            if (defenses == null)
            {
                defenseRenderer = new DefenseRenderer { Name = "CommandDefenseRenderer" };
                root.AddChild(defenseRenderer);
                defenseRenderer.Initialize(Array.Empty<BuildingPlacement>(), defenseCatalog, TowerBaseHeight, battle: true);
                defenses = new DefenseSystem(defenseRenderer, LaserTowerLimit);
            }

            var tower = new CommandStructure(area, laserStats.Health, Factions.Blue, laserDefinition,
                units.Catalog.DamageRules, readyView: new Node3D { Name = "LaserTower" }, groundHeight: height);
            defenses.AddTower(tower, laserStats, TowerBaseHeight, height);
            Add(tower);
        }
        else
            Add(new CommandStructure(area, fabricatorDefinition.Health, Factions.Blue, fabricatorDefinition,
                units.Catalog.DamageRules, Release, groundHeight: height));
        return true;
    }

    public bool HasDefenses => defenses?.ActiveCount > 0;
    public void StepDefenses(SpatialGrid grid, float delta, int tick) =>
        defenses?.Step(units, grid, field, delta, tick, 1f / 60f);
    public void AdvanceDefenseEffects(float delta) => defenseRenderer?.Advance(delta);

    public void AddHostileFabricator(Vector3 point)
    {
        var area = Snap(point);
        if (!FitsMap(area)) return;
        Add(new CommandStructure(area, fabricatorDefinition.Health, Factions.Red, fabricatorDefinition,
            units.Catalog.DamageRules, groundHeight: FoundationHeight(area, out _)));
    }

    private void Add(CommandStructure building)
    {
        structures.Add(building);
        if (building.View.GetParent() == null) root.AddChild(building.View);
        RebuildObstacles();
    }

    private void RebuildObstacles()
    {
        var areas = new List<ObstacleRectangle>(authoredWalls.Length + structures.Count + deposits.Count);
        areas.AddRange(authoredWalls);
        foreach (var deposit in deposits) areas.Add(deposit.Area);
        foreach (var structure in structures)
            if (!structure.Destroyed) areas.Add(structure.Area);
        field = areas.Count == 0 ? null : new ObstacleField(areas.ToArray());
        movement.SetObstacles(field);
    }

    public void RemoveDestroyed()
    {
        bool removed = false;
        foreach (var building in structures)
        {
            if (!building.Destroyed) continue;
            if (building != Citadel && building.Production == null) defenses?.RemoveTower(building);
            building.View.QueueFree();
            removed = true;
        }
        if (removed)
        {
            structures.RemoveAll(building => building.Destroyed);
            RebuildObstacles();
        }
    }

    public void Step(float delta)
    {
        if (CanOperateCitadel) Workers.Step(delta, releaseWorker);
        foreach (var building in structures)
        {
            building.Construct(delta);
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
        if (!OnMap(spawn, radius) || !OnMap(exit, radius) || !SpawnClear(spawn, radius) ||
            field?.IntersectsCircle(ObstacleField.Xz(spawn), radius) == true ||
            field?.IntersectsCircle(ObstacleField.Xz(exit), radius) == true ||
            field?.BlocksCorridor(ObstacleField.Xz(spawn), ObstacleField.Xz(exit), radius) == true) return false;

        Vector3 anchor = building.Rally ?? (exit + Vector3.Back * (3f + radius));
        // Each slot is checked against the actual footprints and reserved goals.
        float spacing = radius * 2f + 0.45f;
        for (int row = 0; row < 16; row++)
        for (int column = 0; column < 11; column++)
        {
            int lateral = (column + 1) / 2 * (column % 2 == 0 ? -1 : 1);
            Vector3 goal = anchor + Vector3.Right * (lateral * spacing) + Vector3.Back * (row * spacing);
            if (!FreeGoal(goal, radius) || goal.DistanceSquaredTo(spawn) < Mathf.Pow(radius + 2f, 2)) continue;

            int id = units.TrySpawn(type, Factions.Blue, spawn);
            if (id < 0) return false;
            if (units.Catalog[type].Shape != UnitShape.Circle) units.Facing[id] = Vector2.Down;
            movement.SetGoal(id, goal, exit);
            return true;
        }
        return false;
    }

    // Check the hatch itself against live units; a clear static exit corridor is checked separately.
    // This lets successive units follow one another without waiting for the entire path to empty.
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

    private bool FreeGoal(Vector3 point, float radius) => FreePosition(point, radius, true);

    private bool FreePosition(Vector3 point, float radius, bool checkGoals)
    {
        if (!OnMap(point, radius) || field?.IntersectsCircle(ObstacleField.Xz(point), radius) == true) return false;
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
            float height = building == Citadel ? 4.3f : building.Production == null ? TowerBaseHeight + 0.7f : 1.95f;
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
