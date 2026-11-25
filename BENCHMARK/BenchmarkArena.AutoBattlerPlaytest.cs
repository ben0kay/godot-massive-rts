// Runs a private level-editor deployment using the authored battlefield and benchmark combat.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MassiveRts;

public partial class BenchmarkArena
{
    private CommandSandbox autoBattlerPlaytestMap;
    private Node3D autoBattlerPlaytestPreview;
    private SoldierPlacement[] autoBattlerRed;
    private readonly List<BuildingPlacement> autoBattlerDefenses = new();
    private ObstacleRectangle[] autoBattlerCover, autoBattlerWater;
    private ForestCover autoBattlerForest;
    private WorldPaint autoBattlerPaint;
    private ObstacleField autoBattlerSight, autoBattlerPaintCollision;

    private readonly DisruptorGrid.LineTool blueDisruptorLine = new();
    private HashSet<Vector2I> blueDisruptors = new();
    private MultiMeshInstance3D blueDisruptorView, blueDisruptorPreview;
    private bool IsOffense => autoBattlerPlaytestMap?.AutoBattlerRules?.Objective == AutoBattlerRules.ObjectiveType.DestroyCitadel;
    private int offenseCitadelSlot = -1;
    private StructureHealth offenseCitadel;
    private bool HasDefenseAttackers => units.HasStructureAttackers || IsOffense;

    private float DisruptorHeight(Vector3 point) =>
        autoBattlerPlaytestMap.GetNodeOrNull<CommandTerrain>("Terrain")?.Field?.At(point.X, point.Z) ?? 0f;

    private void PreviewBlueDisruptors(IReadOnlyCollection<Vector2I> cells)
    {
        if (blueDisruptorPreview != null)
            DisruptorGridVisual.Refresh(blueDisruptorPreview, cells, Factions.Blue, DisruptorHeight);
    }

    private void CancelBlueDisruptorLine()
    {
        blueDisruptorLine.Cancel();
        PreviewBlueDisruptors(Array.Empty<Vector2I>());
    }

    private bool HandleBlueDisruptors(InputEvent input) =>
        blueDisruptorLine.Handle(input, painter.TryGround, blueDisruptors, campaignRoster.OwnedDisruptorTiles,
            cell => DisruptorGrid.Inside(cell, autoBattlerPlaytestMap.MapSize) &&
                autoBattlerPlaytestMap.AutoBattlerRules.AllowsBlueDeployment(DisruptorGrid.Center(cell), DisruptorGrid.Size * 0.5f),
            draft =>
            {
                blueDisruptors = draft;
                DisruptorGridVisual.Refresh(blueDisruptorView, draft, Factions.Blue, DisruptorHeight);
            }, campaignHud.ReportDisruptors, PreviewBlueDisruptors);

    private ObstacleField BattleSight => autoBattlerPlaytestMap != null ? autoBattlerSight : obstacles;

    internal void ConfigureAutoBattlerPlaytest(CommandSandbox map)
    {
        autoBattlerPlaytestMap = map;
        openInDeployment = true;
        map.AuthoringPreview = true;
        AddChild(map);
    }

    private void PrepareAutoBattlerPlaytest()
    {
        var map = autoBattlerPlaytestMap;
        var sun = BattlefieldLighting.AddSun(map);
        var defaultBasis = sun.Basis;
        panel.PlaytestLightingChanged += (strength, fill, direction) =>
        {
            sun.LightEnergy = strength;
            sun.Basis = defaultBasis.Rotated(Vector3.Up, Mathf.DegToRad(direction));
            BattlefieldLighting.SetAmbientFill(fill);
        };
        TreeExiting += () => BattlefieldLighting.SetAmbientFill(BattlefieldLighting.DefaultFillPercent / 100f);
        litUnitMaterials = BattlefieldLighting.CreateUnitMaterials(catalog, battleUnitVisuals);
        map.UpdateGroundBounds();
        blueDisruptorView = new MultiMeshInstance3D { Visible = false };
        blueDisruptorPreview = new MultiMeshInstance3D { Visible = false };
        var redDisruptorView = new MultiMeshInstance3D { Visible = false };
        map.AddChild(blueDisruptorView);
        map.AddChild(blueDisruptorPreview);
        map.AddChild(redDisruptorView);
        DisruptorGridVisual.Refresh(redDisruptorView, map.AutoBattlerRules.RedDisruptorCells.ToArray(),
            Factions.Red, DisruptorHeight);
        terrain = map.GetNodeOrNull<CommandTerrain>("Terrain")?.Field;
        var paint = autoBattlerPaint = map.GetNodeOrNull<WorldPaint>("WorldPaint");
        var (rocks, water) = paint?.Compile() ?? (Array.Empty<ObstacleRectangle>(), Array.Empty<ObstacleRectangle>());
        autoBattlerCover = map.ReadAuthoredWalls(updateVisuals: true).Concat(rocks).ToArray();
        autoBattlerWater = water;
        weather.SetWater(paint?.MovementWater ?? water);
        autoBattlerForest = paint?.Forest;

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
        autoBattlerRed = red.ToArray();
        paintedRedReserveCount = (int)Math.Min(int.MaxValue,
            RedReserveFormation.Groups(map).Sum(group => (long)group.SoldierCount));

        if (map.GetNodeOrNull<Node3D>("StartingDefenses") is Node3D defenses)
            foreach (var child in defenses.GetChildren())
                if (child is CommandLaserPlacement tower && tower.Red)
                {
                    if (!buildings.TryResolve(tower.EffectiveDefinition?.Id, out int type))
                    {
                        if (campaignRoster != null) campaignMapError = "The map contains an unrecognized Red defense.";
                        continue;
                    }
                    if (campaignRoster != null && autoBattlerDefenses.Count == DeploymentLayout.MaxBuildings)
                    {
                        campaignMapError = "The authored Red defenses exceed the simulation's defense capacity.";
                        break;
                    }
                    var area = tower.Footprint(tower.GlobalPosition);
                    if (IsOffense && tower.EffectiveDefinition.Id == "citadel")
                    {
                        if (offenseCitadelSlot >= 0) campaignMapError = "Offense requires exactly one Red Citadel.";
                        offenseCitadelSlot = autoBattlerDefenses.Count;
                    }
                    autoBattlerDefenses.Add(new BuildingPlacement(area.X, area.Z, Factions.Red, type,
                        campaignRoster != null ? area.GridSize : DeploymentLayout.CellSize));
                }

        if (IsOffense && offenseCitadelSlot < 0) campaignMapError = "Offense requires exactly one Red Citadel.";
        autoBattlerPaintCollision = BuildAutoBattlerField(autoBattlerCover.Concat(autoBattlerWater)
            .Concat(autoBattlerDefenses.Select(defense => defense.Footprint(buildings))));
        painter.BrushFaction = Factions.Blue;
        if (campaignRoster == null) painter.PaintRestriction = CanDeployBlue;
        painter.SetPreviewTerrain(terrain);
        autoBattlerPlaytestPreview = LevelLayoutPreview.Build(map, redOnly: campaignRoster != null, holographicDeployment: true);
        if (campaignRoster != null) PrepareCampaignDeployment();
    }

    private bool CanDeployBlue(SoldierPlacement placement)
    {
        if (placement.Faction != Factions.Blue) return false;
        Vector3 position = placement.Position();
        float radius = catalog[placement.Type].Footprint;
        float half = autoBattlerPlaytestMap.MapSize * 0.5f;
        var rules = autoBattlerPlaytestMap.AutoBattlerRules;
        bool hasZone = rules.LineAxis != AutoBattlerRules.DeploymentAxis.Disabled || rules.BlueDeploymentCells.Count > 0;
        return Mathf.Abs(position.X) + radius <= half && Mathf.Abs(position.Z) + radius <= half &&
            (!hasZone || rules.AllowsBlueDeployment(position, radius)) &&
            autoBattlerPaintCollision?.IntersectsCircle(ObstacleField.Xz(position), radius) != true;
    }

    private ObstacleField BuildAutoBattlerField(IEnumerable<ObstacleRectangle> areas, Rect2[] bridges = null)
    {
        var rectangles = areas.ToArray();
        return rectangles.Length == 0 ? null : new ObstacleField(rectangles, autoBattlerPlaytestMap.MapSize * 0.5f, bridges);
    }

    private void RefreshAutoBattlerFields()
    {
        var survivingDefenses = defenseSystem?.Structures.Where(structure => structure?.Destroyed == false)
            .Select(structure => structure.Area) ?? Enumerable.Empty<ObstacleRectangle>();
        var cover = autoBattlerCover.Concat(survivingDefenses).ToArray();
        autoBattlerSight = BuildAutoBattlerField(cover);
        SetBattleObstacles(BuildAutoBattlerField(cover.Concat(autoBattlerPaint?.MovementWater ?? autoBattlerWater), autoBattlerPaint?.BridgeAreas), null);
    }
}
