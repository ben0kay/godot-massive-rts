// Connects the AutoBattler campaign roster and outcome to the shared arena lifecycle.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MassiveRts;

public partial class BenchmarkArena
{
    private const float CampaignBattleTimeLimit = 300f;
    private AutoBattlerCampaignState campaignRoster;
    private AutoBattlerHud campaignHud;
    private HashSet<string> allowedCampaignUnits;
    private HashSet<Vector2I> campaignZoneCells;
    private readonly Dictionary<Vector2I, List<SoldierPlacement>> campaignRedBuckets = new();
    private int campaignInitialBlue;
    private long campaignRedStrength;
    private long autoBattlerElapsedTicks;
    private Label autoBattlerClock;
    private double AutoBattlerElapsedSeconds => autoBattlerElapsedTicks * (double)Config.TickSeconds;

    private void AddAutoBattlerClock(CanvasLayer layer)
    {
        autoBattlerClock = new Label
        {
            Visible = false, AnchorLeft = 1, AnchorRight = 1,
            OffsetLeft = -110, OffsetRight = -12, OffsetTop = 12,
            HorizontalAlignment = HorizontalAlignment.Right,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        autoBattlerClock.AddThemeFontSizeOverride("font_size", 24);
        autoBattlerClock.AddThemeColorOverride("font_outline_color", Colors.Black);
        autoBattlerClock.AddThemeConstantOverride("outline_size", 5);
        layer.AddChild(autoBattlerClock);
    }

    private void ShowAutoBattlerClock(bool visible)
    {
        if (autoBattlerClock == null) return;
        autoBattlerElapsedTicks = 0;
        autoBattlerClock.Text = "00:00";
        autoBattlerClock.Visible = visible;
        panel.MakeRoomForBattleClock(visible);
    }

    private void AdvanceAutoBattlerClock(int steps)
    {
        if (autoBattlerClock == null || steps == 0) return;
        int previousSecond = (int)AutoBattlerElapsedSeconds;
        autoBattlerElapsedTicks += steps;
        int seconds = (int)AutoBattlerElapsedSeconds;
        if (seconds != previousSecond) autoBattlerClock.Text = $"{seconds / 60:00}:{seconds % 60:00}";
    }
    private bool campaignBattleRunning;
    private string campaignMapError;

    internal void ConfigureCampaignBattle(CommandSandbox map, AutoBattlerCampaignState roster)
    {
        campaignRoster = roster;
        ConfigureAutoBattlerPlaytest(map);
    }

    private void PrepareCampaignDeployment()
    {
        var rules = autoBattlerPlaytestMap.AutoBattlerRules;
        allowedCampaignUnits = new HashSet<string>(rules.AllowedBlueUnitIds ?? Array.Empty<string>(), StringComparer.Ordinal);
        campaignZoneCells = new HashSet<Vector2I>(rules.BlueDeploymentCells);
        foreach (var soldier in autoBattlerRed)
        {
            campaignRedStrength += catalog.BattleRating(soldier.Type);
            var bucket = DeploymentCell(soldier.Position());
            if (!campaignRedBuckets.TryGetValue(bucket, out var list))
                campaignRedBuckets.Add(bucket, list = new());
            list.Add(soldier);
        }

        painter.PlacementError = CampaignPlacementError;
        painter.SoldierBrushSize = 1;
        var choices = Enumerable.Range(0, catalog.Count)
            .Select(index => (Type: (byte)index, Unit: catalog[index]))
            .Where(entry => campaignRoster.Owned(entry.Unit.Id) > 0 &&
                (allowedCampaignUnits.Count == 0 || allowedCampaignUnits.Contains(entry.Unit.Id)))
            .Select(entry => (entry.Type, entry.Unit.Name)).ToArray();
        if (choices.Length > 0) painter.BrushType = choices[0].Type;

        campaignHud = new AutoBattlerHud(autoBattlerPlaytestMap.LevelSettings.MenuName, choices,
            type => painter.BrushType = type, size => painter.SoldierBrushSize = size,
            painter.Clear, StartCampaignBattle, () => EnterDeployment(),
            () => FinishCampaignBattle("Withdrawn"), ReturnFromCampaignBattle, MaxReserve, Reserved, SetReserve, seconds => blueReserveSeconds = seconds,
            spread => painter.SoldierBrushSpread = spread, CreateCircleMeshToggle());
        AddChild(campaignHud);
        if (campaignHud.DeploymentOrderControls != null) painter.Orders.AddControls(campaignHud.DeploymentOrderControls);
    }

    private static Vector2I DeploymentCell(Vector3 position) => new(
        Mathf.FloorToInt(position.X / DeploymentLayout.CellSize),
        Mathf.FloorToInt(position.Z / DeploymentLayout.CellSize));

    private string CampaignPlacementError(SoldierPlacement placement)
    {
        ref readonly var unit = ref catalog[placement.Type];
        if (painter.Layout.CountForType(placement.Type) >= campaignRoster.Owned(unit.Id) - Reserved(placement.Type) ||
            (allowedCampaignUnits.Count != 0 && !allowedCampaignUnits.Contains(unit.Id)))
            return "No eligible soldiers of this type are available. Erase placements or recruit more in Headquarters.";
        if (!painter.Layout.TryGet(new Vector2I(placement.X, placement.Z), out _) &&
            painter.Layout.Placements.Count >= Config.MaxSoldiers - autoBattlerRed.Length - AuthoredRedReserve - blueReserveCount)
            return "This placement exceeds the simulation's total soldier capacity.";

        Vector3 position = placement.Position();
        float radius = unit.Footprint;
        float half = autoBattlerPlaytestMap.MapSize * 0.5f;
        if (Mathf.Abs(position.X) + radius > half || Mathf.Abs(position.Z) + radius > half ||
            !autoBattlerPlaytestMap.AutoBattlerRules.AllowsSide(position, radius, blue: true) ||
            !InsideCampaignPaintedZone(position, radius))
            return "Place the entire soldier footprint inside the Blue deployment area.";

        Vector2 point = ObstacleField.Xz(position);
        if (autoBattlerPaintCollision?.IntersectsCircle(point, radius) == true)
            return "The soldier footprint overlaps water, rock, a wall, or a defense.";

        var center = DeploymentCell(position);
        int extent = Mathf.CeilToInt((radius + catalog.MaxFootprint) / DeploymentLayout.CellSize);
        for (int z = center.Y - extent; z <= center.Y + extent; z++)
        for (int x = center.X - extent; x <= center.X + extent; x++)
        {
            if (!campaignRedBuckets.TryGetValue(new Vector2I(x, z), out var soldiers)) continue;
            foreach (var enemy in soldiers)
            {
                float spacing = radius + catalog[enemy.Type].Footprint;
                if (point.DistanceSquaredTo(ObstacleField.Xz(enemy.Position())) < spacing * spacing)
                    return "The soldier footprint overlaps an authored enemy.";
            }
        }
        return null;
    }

    private bool InsideCampaignPaintedZone(Vector3 position, float radius)
    {
        if (campaignZoneCells.Count == 0)
            return autoBattlerPlaytestMap.AutoBattlerRules.LineAxis != AutoBattlerRules.DeploymentAxis.Disabled;
        var lower = DeploymentCell(position - new Vector3(radius, 0, radius));
        var upper = DeploymentCell(position + new Vector3(radius, 0, radius));
        Vector2 point = ObstacleField.Xz(position);
        float size = DeploymentLayout.CellSize;
        for (int z = lower.Y; z <= upper.Y; z++)
        for (int x = lower.X; x <= upper.X; x++)
        {
            var cell = new Vector2I(x, z);
            if (campaignZoneCells.Contains(cell)) continue;
            var min = new Vector2(x * size, z * size);
            Vector2 closest = point.Max(min).Min(min + Vector2.One * size);
            if (point.DistanceSquaredTo(closest) < radius * radius) return false;
        }
        return true;
    }

    private string CampaignStartError()
    {
        if (campaignMapError != null) return campaignMapError;
        if (autoBattlerRed.Length == 0 && autoBattlerDefenses.Count == 0 && AuthoredRedReserve == 0) return "This level has no authored Red forces.";
        if (campaignZoneCells.Count == 0 && autoBattlerPlaytestMap.AutoBattlerRules.LineAxis == AutoBattlerRules.DeploymentAxis.Disabled)
            return "This map has no authored Blue deployment area.";
        if (painter.Layout.Placements.Count == 0 && blueReserve.Count == 0) return "Deploy or reserve at least one owned soldier to fight.";
        if ((long)painter.Layout.Placements.Count + autoBattlerRed.Length + AuthoredRedReserve + blueReserveCount > Config.MaxSoldiers)
            return "Both armies together exceed the simulation's total soldier capacity.";
        return null;
    }

    private void UpdateCampaignDeployment()
    {
        var layout = painter.Layout;
        campaignHud.ShowDeployment(type =>
        {
            int owned = campaignRoster.Owned(catalog[type].Id), deployed = layout.CountForType(type);
            return $"Owned {owned:N0}  ·  Deployed {deployed:N0}  ·  Reserve {Reserved(type):N0}  ·  Available {owned - deployed - Reserved(type):N0}";
        }, $"Blue strength {layout.StrengthOf(Factions.Blue):N0}  ·  Red strength {campaignRedStrength:N0} + {autoBattlerDefenses.Count} defenses" +
            (AuthoredRedReserve > 0 ? $"\nRed reinforcements: {AuthoredRedReserve:N0}" : ""),
            CampaignStartError());
    }

    private void StartCampaignBattle()
    {
        string error = CampaignStartError();
        if (error != null) { campaignHud.Report(error); return; }
        campaignInitialBlue = painter.Layout.Placements.Count;
        var settings = new DeploymentPainter.BattleSettings(12345, new GuardOrder[Factions.Count], 0);
        StartPaintedBattle(painter.Layout, settings, painter.ObstacleMesh);
        if (spawning) campaignHud.ShowPreparing();
    }

    private void UpdateCampaignOutcome(int steps)
    {
        if (!campaignBattleRunning || steps == 0) return;
        if (ReserveCapacity > 0) campaignHud.ShowArrivals(ReserveCountdown());
        if (units.FactionAlive[Factions.Blue] == 0 && blueWave?.Pending != true) FinishCampaignBattle("Defeat");
        else if (units.FactionAlive[Factions.Red] == 0 && redWave?.Pending != true && projectiles.Count == 0 &&
            (defenseSystem == null || defenseSystem.Structures.All(tower => tower.Destroyed)))
            FinishCampaignBattle("Victory");
        else if (AutoBattlerElapsedSeconds >= (redWave?.Lanes != null ?
            Mathf.Max(CampaignBattleTimeLimit, redWave.TimeToFinish + 15f) : CampaignBattleTimeLimit)) FinishCampaignBattle("Stalemate");
    }

    private void FinishCampaignBattle(string result)
    {
        if (!campaignBattleRunning) return;
        campaignBattleRunning = false;
        paused = true;
        // All original Blue IDs precede Red IDs in the all-auto-attack snapshot.
        int survivors = 0;
        for (int id = 0; id < campaignInitialBlue; id++)
            if (units.Faction[id] == Factions.Blue && units.Health[id] > 0f) survivors++;
        int defenses = defenseSystem?.Structures.Count(tower => !tower.Destroyed) ?? 0;
        int arrived = blueWave?.Released ?? 0;
        string reserveResult = blueWave == null ? "" :
            $"\nBlue reserve survivors {units.FactionAlive[Factions.Blue] - survivors:N0} / {arrived:N0} arrived";
        string survivorLabel = blueWave == null ? "Blue survivors" : "Blue opening survivors";
        campaignHud.ShowResult(result,
            $"{survivorLabel} {survivors:N0} / {campaignInitialBlue:N0}  ·  Red remaining {units.FactionAlive[Factions.Red]:N0} + {defenses} defenses{reserveResult}");
    }

    private void ReturnFromCampaignBattle()
    {
        try { MenuNavigation.ReturnToCampaign(this, AutoBattlerCampaignStore.CampaignId); }
        catch (InvalidOperationException error) { campaignHud.Report(error.Message); }
    }
}
