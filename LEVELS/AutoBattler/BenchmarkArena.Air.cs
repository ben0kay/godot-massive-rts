// Starts authored Clash flights on the shared battle clock, outside the ground roster.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

public partial class BenchmarkArena
{
    private AirSquadron airSquadron;
    private bool generatedMixedRoster;

    private UnitDefinition[] aircraftRoster = Array.Empty<UnitDefinition>();
    private AirSquadron.Flight[] aircraftFlights = Array.Empty<AirSquadron.Flight>();
    private int BlueAircraftCount, RedAircraftCount;
    private readonly Dictionary<string, Vector2[]> testBlueAircraft = new();
    private readonly Dictionary<string, MultiMeshInstance3D> testAircraftPreviews = new();
    private UnitDefinition testAircraftBrush;
    private MouseButton testAircraftButton;
    private Vector2? previousAircraftCell;
    private Vector2? testBombOrder;
    private bool testBombOrderEdited, placingTestBombOrder;
    private Button testBombButton;
    private readonly Label3D[] testBombMarkers = new Label3D[Factions.Count];

    private Vector2? ReadBombOrder(byte faction)
    {
        if (faction == Factions.Blue && testBombOrderEdited) return testBombOrder;
        return autoBattlerPlaytestMap.AutoBattlerRules.OpeningBombOrders.TryGetValue(faction, out var target)
            ? target : null;
    }

    private Vector2[] Starts(UnitDefinition unit, byte faction) =>
        faction == Factions.Blue && testBlueAircraft.TryGetValue(unit.Id, out var points)
            ? points : autoBattlerPlaytestMap.AutoBattlerRules.ReadAircraftStarts(faction, unit.Id);

    private void RefreshAircraftFlights()
    {
        var rules = autoBattlerPlaytestMap.AutoBattlerRules;
        var flights = new List<AirSquadron.Flight>();
        BlueAircraftCount = RedAircraftCount = 0;
        foreach (byte faction in Factions.DisplayOrder)
        {
            if (!AutoBattlerRules.AllowsAircraftFaction(faction)) continue;
            int remaining = AirSquadron.MaxPerFaction;
            foreach (var unit in aircraftRoster)
            {
                var positions = Starts(unit, faction);
                int requestedCount = positions.Length;
                if (requestedCount == 0 && !(faction == Factions.Blue && testBlueAircraft.ContainsKey(unit.Id)))
                    requestedCount = rules.ReadLegacyFlightCount(faction, unit.Id);
                int count = Mathf.Clamp(requestedCount, 0, remaining);
                if (count == 0) continue;
                flights.Add(new AirSquadron.Flight(unit, faction, count, positions, unit.CanBombGround ? ReadBombOrder(faction) : null));
                remaining -= count;
            }
            if (faction == Factions.Blue) BlueAircraftCount = AirSquadron.MaxPerFaction - remaining;
            else RedAircraftCount = AirSquadron.MaxPerFaction - remaining;
        }
        aircraftFlights = flights.ToArray();
    }

    private void SelectTestAircraft(UnitDefinition unit)
    {
        if (testAircraftBrush == null && unit == null) return;
        painter.CancelDrag();
        previousAircraftCell = null;
        testAircraftButton = MouseButton.None;
        testAircraftBrush = unit;
        placingTestBombOrder = false;
        if (unit?.CanBombGround == true && testBombButton == null && panel.DeploymentOrderControls != null)
        {
            testBombButton = UIButtonFactory.Create("Bomb", () =>
            {
                placingTestBombOrder = true;
                panel.Status.Text = "Click a ground spot for the opening bombing run.";
            }, UIButtonStyle.Compact);
            panel.DeploymentOrderControls.AddChild(testBombButton);
            panel.DeploymentOrderControls.MoveChild(testBombButton, 0);
        }
        if (testBombButton != null) testBombButton.Visible = unit?.CanBombGround == true;
        painter.TrapInput = unit != null ? HandleTestAircraftInput : null;
        if (unit?.CanBombGround == true)
            panel.Status.Text = "Paint bombers · Shift-click a bombing target · Shift-right-click to clear";
    }

    private bool HandleTestAircraftInput(InputEvent input)
    {
        Vector2 screen;
        if (input is InputEventMouseButton mouse && mouse.ButtonIndex is MouseButton.Left or MouseButton.Right)
        {
            if (!mouse.Pressed)
            {
                testAircraftButton = MouseButton.None;
                previousAircraftCell = null;
                return true;
            }
            if (camera.FreeRoam && mouse.ButtonIndex == MouseButton.Right) return false;
            if (testAircraftBrush.CanBombGround && (placingTestBombOrder || mouse.ShiftPressed))
            {
                if (mouse.ButtonIndex == MouseButton.Left)
                {
                    if (!painter.TryGround(mouse.Position, out var point)) return true;
                    float half = autoBattlerPlaytestMap.MapSize * 0.5f;
                    if (Mathf.Max(Mathf.Abs(point.X), Mathf.Abs(point.Z)) >= half) return true;
                    testBombOrder = ObstacleField.Xz(point);
                }
                else testBombOrder = null;
                placingTestBombOrder = false;
                testBombOrderEdited = true;
                RefreshAircraftFlights();
                ShowAircraftPreviews(true);
                panel.Status.Text = testBombOrder.HasValue ? "Opening bombing run assigned." : "Opening bombing run cleared.";
                return true;
            }
            testAircraftButton = mouse.ButtonIndex;
            previousAircraftCell = null;
            screen = mouse.Position;
        }
        else if (input is InputEventMouseMotion motion && testAircraftButton != MouseButton.None &&
                 Input.IsMouseButtonPressed(testAircraftButton))
            screen = motion.Position;
        else return false;

        if (!painter.TryGround(screen, out var ground))
        {
            previousAircraftCell = null;
            return true;
        }
        Vector2 cell = AircraftPaint.Cell(ground);
        var unit = testAircraftBrush;
        Vector2[] existing = Starts(unit, Factions.Blue);
        int otherCount = 0;
        foreach (var flight in aircraftFlights)
            if (flight.Faction == Factions.Blue && flight.Unit != unit) otherCount += flight.Count;
        var updated = AircraftPaint.Edit(existing, previousAircraftCell ?? cell, cell,
            autoBattlerPlaytestMap.MapSize * 0.5f, testAircraftButton == MouseButton.Right,
            point => autoBattlerPlaytestMap.AutoBattlerRules.AllowsSide(new Vector3(point.X, 0f, point.Y), 1.7f, blue: true),
            AirSquadron.MaxPerFaction - otherCount);
        previousAircraftCell = cell;
        if (updated == existing) return true;
        testBlueAircraft[unit.Id] = updated;
        RefreshAircraftFlights();
        ShowAircraftPreviews(true);
        UpdateDeploymentStatus();
        return true;
    }

    private void ClearTestAircraft()
    {
        if (testBlueAircraft.Count == 0 && !testBombOrderEdited) return;
        testBombOrderEdited = false;
        testBombOrder = null;
        testBlueAircraft.Clear();
        RefreshAircraftFlights();
        ShowAircraftPreviews(true);
        UpdateDeploymentStatus();
    }

    private void ShowAircraftPreviews(bool show)
    {
        if (aircraftRoster.Length == 0) return;
        foreach (byte faction in Factions.DisplayOrder)
            if (AutoBattlerRules.AllowsAircraftFaction(faction))
                AircraftPaint.RefreshBombMarker(autoBattlerPlaytestMap, ref testBombMarkers[faction], faction,
                    show ? ReadBombOrder(faction) : null, DisruptorHeight);
        foreach (var unit in aircraftRoster)
        {
            testAircraftPreviews.TryGetValue(unit.Id, out var preview);
            if (show)
                preview = AircraftPaint.RefreshPreview(autoBattlerPlaytestMap, preview, unit,
                    Starts(unit, Factions.Blue), Starts(unit, Factions.Red), DisruptorHeight);
            if (preview == null) continue;
            preview.Visible = show;
            testAircraftPreviews[unit.Id] = preview;
        }
    }

    private void StartAirSquadron()
    {
        if (generatedMixedRoster && mode == BenchmarkMode.Ranged)
        {
            var flights = new List<AirSquadron.Flight>();
            var roster = GD.Load<UnitCatalog>(UnitCatalogPath).Aircraft;
            foreach (var unit in roster)
            {
                if (unit == null) continue;
                flights.Add(new AirSquadron.Flight(unit, Factions.Blue, unit.CanBombGround ? 2 : 4, Array.Empty<Vector2>()));
                flights.Add(new AirSquadron.Flight(unit, Factions.Red, unit.CanBombGround ? 2 : 4, Array.Empty<Vector2>()));
            }
            if (flights.Count > 0)
                CreateAirSquadron(flights.ToArray(), DeploymentLayout.WorldHalfWidth, Config.ObstacleHeight + 3f);
            return;
        }

        if (aircraftFlights.Length == 0) return;

        // Bound vertical clearance once for authored cover and defense footprints.
        float clearance = 0f;
        float obstacleHeight = Mathf.Max(Config.ObstacleHeight, 9f);
        foreach (var area in autoBattlerCover)
        {
            float ground = terrain?.FootprintHeight(area.Min, area.Max, out _) ?? 0f;
            float top = area.AirborneHeightRange?.Y ?? ground + obstacleHeight;
            clearance = Mathf.Max(clearance, top + 3f);
        }
        foreach (var defense in autoBattlerDefenses)
        {
            var area = defense.Footprint(buildings);
            float ground = terrain?.FootprintHeight(area.Min, area.Max, out _) ?? 0f;
            clearance = Mathf.Max(clearance, ground + obstacleHeight + 3f);
        }

        CreateAirSquadron(aircraftFlights, autoBattlerPlaytestMap.MapSize * 0.5f, clearance);
    }

    private void CreateAirSquadron(AirSquadron.Flight[] flights, float halfWidth, float clearance)
    {
        var scene = GD.Load<PackedScene>("res://GAMEPLAY/COMBAT/AIR/AirSquadron.tscn");
        airSquadron = scene.Instantiate<AirSquadron>();
        AddChild(airSquadron);
        airSquadron.Initialize(flights, halfWidth, terrain, clearance, catalog.DamageRules, catalog.Projectiles);
    }
}
