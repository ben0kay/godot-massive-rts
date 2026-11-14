// Reserves building footprints and releases soldiers into checked exit and destination positions.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed class CommandBuildings
{
    private const int FabricatorLimit = 32;
    private readonly Node3D root;
    private readonly UnitState units;
    private readonly CommandMovement movement;
    private readonly List<CommandStructure> structures = new();
    private readonly BatchRecipe[] recipes;
    private ObstacleField field;
    public readonly CarbonBalance Carbon = new();
    public IReadOnlyList<CommandStructure> Structures => structures;
    public BatchRecipe Recipe(int id) => recipes[id];

    public CommandBuildings(Node3D root, UnitState units, CommandMovement movement, float citadelHealth)
    {
        this.root = root;
        this.units = units;
        this.movement = movement;
        recipes = GD.Load<ProductionRecipes>("res://GAMEPLAY/PRODUCTION/FabricatorRecipes.tres").Compile(units.Catalog);
        Add(new CommandStructure(new ObstacleRectangle(-4, -9, 5, 5), citadelHealth, true, null));
    }

    public static bool OnMap(Vector3 point, float radius = 0) =>
        Mathf.Abs(point.X) + radius <= DeploymentLayout.WorldHalfWidth &&
        Mathf.Abs(point.Z) + radius <= DeploymentLayout.WorldHalfWidth;

    public static ObstacleRectangle Snap(Vector3 point)
    {
        const int cells = CommandStructure.FabricatorCells;
        const float grid = DeploymentLayout.CellSize;
        return new ObstacleRectangle(Mathf.RoundToInt(point.X / grid - cells * 0.5f),
            Mathf.RoundToInt(point.Z / grid - cells * 0.5f), cells, cells);
    }

    public static Vector3 Center(ObstacleRectangle area)
    {
        Vector2 center = (area.Min + area.Max) * 0.5f;
        return new Vector3(center.X, 0, center.Y);
    }

    public bool CanPlace(ObstacleRectangle area)
    {
        if (!area.IsValid || structures.Count >= FabricatorLimit + 1 || Carbon.Amount < CommandStructure.FabricatorCost)
            return false;

        Vector2 min = area.Min, max = area.Max;
        foreach (var building in structures)
        {
            if (building.Destroyed) continue;
            Vector2 otherMin = building.Area.Min, otherMax = building.Area.Max;
            if (min.X < otherMax.X && max.X > otherMin.X && min.Y < otherMax.Y && max.Y > otherMin.Y)
                return false;
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

    public bool Place(ObstacleRectangle area)
    {
        if (!CanPlace(area) || !Carbon.TrySpend(CommandStructure.FabricatorCost)) return false;
        Add(new CommandStructure(area, 1200f, false, Release));
        return true;
    }

    private void Add(CommandStructure building)
    {
        structures.Add(building);
        root.AddChild(building.View);
        RebuildObstacles();
    }

    private void RebuildObstacles()
    {
        var areas = new List<ObstacleRectangle>(structures.Count);
        foreach (var structure in structures)
            if (!structure.Destroyed) areas.Add(structure.Area);
        field = areas.Count == 0 ? null : new ObstacleField(areas.ToArray());
        movement.SetObstacles(field);
    }

    public void Step(float delta)
    {
        bool removed = false;
        foreach (var building in structures)
        {
            if (!building.Destroyed) continue;
            building.View.QueueFree();
            removed = true;
        }
        if (removed)
        {
            structures.RemoveAll(building => building.Destroyed);
            RebuildObstacles();
        }

        foreach (var building in structures)
        {
            building.Construct(delta);
            if (building.Ready) building.Production?.Step(delta, recipes, Carbon, building.Release);
        }
    }

    public bool CanRally(Vector3 point) =>
        OnMap(point, units.Catalog.MaxFootprint) &&
        field?.IntersectsCircle(ObstacleField.Xz(point), units.Catalog.MaxFootprint) != true;

    private bool Release(CommandStructure building, byte type)
    {
        building.UnitCapBlocked = units.Count >= units.Position.Length;
        if (building.UnitCapBlocked) return false;
        float radius = units.Catalog[type].Footprint;
        float front = (building.Area.Max.Y - building.Area.Min.Y) * 0.5f;
        Vector3 spawn = building.Center + Vector3.Back * (front + radius + 0.15f);
        Vector3 exit = spawn + Vector3.Back * (radius + 1.0f);
        if (!OnMap(spawn, radius) || !OnMap(exit, radius) || !ExitClear(spawn, exit, radius) ||
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
            units.Facing[id] = Vector2.Down;
            movement.SetGoal(id, goal, exit);
            return true;
        }
        return false;
    }

    private bool ExitClear(Vector3 spawn, Vector3 exit, float radius)
    {
        Vector3 path = exit - spawn;
        float inverseLengthSquared = 1f / path.LengthSquared();
        for (int id = 0; id < units.Count; id++)
        {
            if (units.Health[id] <= 0) continue;
            Vector3 offset = units.Position[id] - spawn;
            float along = Mathf.Clamp(offset.Dot(path) * inverseLengthSquared, 0f, 1f);
            float clearance = radius + units.Catalog[units.Type[id]].Footprint + 0.08f;
            if ((offset - path * along).LengthSquared() < clearance * clearance) return false;
        }
        return true;
    }

    private bool FreeGoal(Vector3 point, float radius)
    {
        if (!OnMap(point, radius) || field?.IntersectsCircle(ObstacleField.Xz(point), radius) == true) return false;
        for (int id = 0; id < units.Count; id++)
        {
            if (units.Health[id] <= 0) continue;
            float clearance = radius + units.Catalog[units.Type[id]].Footprint + 0.08f;
            float squared = clearance * clearance;
            if (point.DistanceSquaredTo(units.Position[id]) < squared ||
                point.DistanceSquaredTo(units.Goal[id]) < squared) return false;
        }
        return true;
    }

    public static bool TryGround(Camera3D camera, Vector2 point, out Vector3 ground, float height = 0f)
    {
        Vector3 origin = camera.ProjectRayOrigin(point);
        Vector3 ray = camera.ProjectRayNormal(point);
        ground = default;
        if (ray.Y >= -0.00001f || origin.Y < height) return false;
        ground = origin + ray * ((height - origin.Y) / ray.Y);
        ground.Y = height;
        return true;
    }

    public CommandStructure Pick(Camera3D camera, Vector2 pointer)
    {
        CommandStructure picked = null;
        float nearest = float.PositiveInfinity;
        Vector3 origin = camera.ProjectRayOrigin(pointer);
        Vector3 ray = camera.ProjectRayNormal(pointer);
        foreach (var building in structures)
        {
            if (building.Destroyed) continue;
            float height = building.Production == null ? 4.3f : 1.95f;
            Vector2 min = building.Area.Min, max = building.Area.Max;
            float start = 0f, end = nearest;
            if (!ClipRay(origin.X, ray.X, min.X, max.X, ref start, ref end) ||
                !ClipRay(origin.Y, ray.Y, 0f, height, ref start, ref end) ||
                !ClipRay(origin.Z, ray.Z, min.Y, max.Y, ref start, ref end)) continue;
            nearest = start;
            picked = building;
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
