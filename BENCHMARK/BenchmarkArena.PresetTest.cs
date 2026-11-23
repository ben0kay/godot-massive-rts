// Runs a private level-editor deployment using the authored battlefield and benchmark combat.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MassiveRts;

public partial class BenchmarkArena
{
    private CommandSandbox presetTestMap;
    private Node3D presetTestPreview;
    private SoldierPlacement[] presetRed;
    private readonly List<BuildingPlacement> presetDefenses = new();
    private ObstacleRectangle[] presetCover, presetWater;
    private ForestCover presetForest;
    private WorldPaint presetPaint;
    private ObstacleField presetSight, presetPaintCollision;

    private ObstacleField BattleSight => presetTestMap != null ? presetSight : obstacles;

    internal void ConfigurePresetTest(CommandSandbox map)
    {
        presetTestMap = map;
        openInDeployment = true;
        map.AuthoringPreview = true;
        AddChild(map);
    }

    private void PreparePresetTest()
    {
        var map = presetTestMap;
        map.UpdateGroundBounds();
        terrain = map.GetNodeOrNull<CommandTerrain>("Terrain")?.Field;
        var paint = presetPaint = map.GetNodeOrNull<WorldPaint>("WorldPaint");
        var (rocks, water) = paint?.Compile() ?? (Array.Empty<ObstacleRectangle>(), Array.Empty<ObstacleRectangle>());
        presetCover = map.ReadAuthoredWalls().Concat(rocks).ToArray();
        presetWater = water;
        weather.SetWater(paint?.MovementWater ?? water);
        presetForest = paint?.Forest;

        var red = new List<SoldierPlacement>();
        foreach (var child in map.GetNodeOrNull<Node3D>("StartingUnits")?.GetChildren() ?? new Godot.Collections.Array<Node>())
        {
            if (child is not CommandUnitPlacement group || group.Faction != CommandUnitPlacement.Team.Red) continue;
            if (!catalog.TryResolve(group.Unit?.Id, out byte type))
            {
                if (campaignRoster != null) campaignMapError = "The map contains an unrecognized Red soldier type.";
                continue;
            }
            var facing = Vector2.Up.Rotated(-group.GlobalRotation.Y);
            foreach (var position in group.Positions(catalog[type].Footprint))
            {
                if (campaignRoster != null && red.Count == Config.MaxSoldiers)
                {
                    campaignMapError = "The authored Red army exceeds the simulation's soldier capacity.";
                    break;
                }
                red.Add(new SoldierPlacement(0, 0, Factions.Red, type) { AuthoredPosition = position, AuthoredFacing = facing });
            }
        }
        presetRed = red.ToArray();

        if (map.GetNodeOrNull<Node3D>("StartingDefenses") is Node3D defenses)
            foreach (var child in defenses.GetChildren())
                if (child is CommandLaserPlacement tower && tower.Red)
                {
                    if (!buildings.TryResolve(tower.EffectiveDefinition?.Id, out int type))
                    {
                        if (campaignRoster != null) campaignMapError = "The map contains an unrecognized Red defense.";
                        continue;
                    }
                    if (campaignRoster != null && presetDefenses.Count == DeploymentLayout.MaxBuildings)
                    {
                        campaignMapError = "The authored Red defenses exceed the simulation's defense capacity.";
                        break;
                    }
                    var area = tower.Footprint(tower.GlobalPosition);
                    presetDefenses.Add(new BuildingPlacement(area.X, area.Z, Factions.Red, type,
                        campaignRoster != null ? area.GridSize : DeploymentLayout.CellSize));
                }

        presetPaintCollision = BuildPresetField(presetCover.Concat(presetWater)
            .Concat(presetDefenses.Select(defense => defense.Footprint(buildings))));
        painter.BrushFaction = Factions.Blue;
        if (campaignRoster == null) painter.PaintRestriction = CanDeployBlue;
        painter.SetPreviewTerrain(terrain);
        presetTestPreview = LevelLayoutPreview.Build(map, redOnly: campaignRoster != null);
        if (campaignRoster != null) PrepareCampaignDeployment();
    }

    private bool CanDeployBlue(SoldierPlacement placement)
    {
        if (placement.Faction != Factions.Blue) return false;
        Vector3 position = placement.Position();
        float radius = catalog[placement.Type].Footprint;
        float half = presetTestMap.MapSize * 0.5f;
        var rules = presetTestMap.PresetRules;
        bool hasZone = rules.LineAxis != PresetBattleRules.DeploymentAxis.Disabled || rules.BlueDeploymentCells.Count > 0;
        return Mathf.Abs(position.X) + radius <= half && Mathf.Abs(position.Z) + radius <= half &&
            (!hasZone || rules.AllowsBlueDeployment(position, radius)) &&
            presetPaintCollision?.IntersectsCircle(ObstacleField.Xz(position), radius) != true;
    }

    private ObstacleField BuildPresetField(IEnumerable<ObstacleRectangle> areas, Rect2[] bridges = null)
    {
        var rectangles = areas.ToArray();
        return rectangles.Length == 0 ? null : new ObstacleField(rectangles, presetTestMap.MapSize * 0.5f, bridges);
    }

    private void RefreshPresetBattleFields()
    {
        var survivingDefenses = defenseSystem?.Structures.Where(structure => structure?.Destroyed == false)
            .Select(structure => structure.Area) ?? Enumerable.Empty<ObstacleRectangle>();
        var cover = presetCover.Concat(survivingDefenses).ToArray();
        presetSight = BuildPresetField(cover);
        SetBattleObstacles(BuildPresetField(cover.Concat(presetPaint?.MovementWater ?? presetWater), presetPaint?.BridgeAreas), null);
    }
}
