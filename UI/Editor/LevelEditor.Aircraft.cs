// Paints authored aircraft starts independently of the ground soldier roster.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

public partial class LevelEditor
{
    private UnitDefinition aircraftBrush;
    private Vector2[] aircraftStroke;
    private bool aircraftHadStarts;
    private MouseButton aircraftStrokeButton;
    private Vector2? lastAircraftCell;
    private readonly Dictionary<UnitDefinition, MultiMeshInstance3D> aircraftPreviews = new();
    private Label3D editorBombMarker;

    private bool placingEditorBombOrder;

    private void BuildAircraftControls()
    {
        placingEditorBombOrder = false;
        Heading(context, "Drag to paint. Right-drag to erase.", 15);
        if (aircraftBrush?.CanBombGround != true) return;
        Button(context, "Bomb", () =>
        {
            placingEditorBombOrder = true;
            SetStatus("Click a ground spot for the opening bombing run.");
        });
        Button(context, "Clear Bomb Order", () =>
        {
            placingEditorBombOrder = false;
            SetOpeningBombOrder(null);
        });
    }

    private bool HandleAircraftPaint(InputEvent input)
    {
        if (input is InputEventMouseButton mouse && mouse.ButtonIndex is MouseButton.Left or MouseButton.Right)
        {
            if (!mouse.Pressed) { EndAircraftStroke(); return true; }
            if (!WorldPoint(mouse.Position, out var point)) return false;
            if (aircraftBrush.CanBombGround && (placingEditorBombOrder || mouse.ShiftPressed))
            {
                EndAircraftStroke();
                placingEditorBombOrder = false;
                SetOpeningBombOrder(mouse.ButtonIndex == MouseButton.Left ? ObstacleField.Xz(point) : null);
                return true;
            }
            EndAircraftStroke();
            var rules = map.AutoBattlerRules;
            aircraftStroke = rules.ReadAircraftStarts(Factions.Red, aircraftBrush.Id);
            aircraftHadStarts = rules.HasAircraftStarts(Factions.Red, aircraftBrush.Id);
            aircraftStrokeButton = mouse.ButtonIndex;
            PaintAircraft(point);
            return true;
        }
        if (input is InputEventMouseMotion motion && aircraftStroke != null)
        {
            if (WorldPoint(motion.Position, out var point)) PaintAircraft(point);
            else lastAircraftCell = null;
            return true;
        }
        return false;
    }

    private void SetOpeningBombOrder(Vector2? destination)
    {
        var orders = map.AutoBattlerRules.OpeningBombOrders;
        int faction = Factions.Red;
        Vector2? before = orders.TryGetValue(faction, out var previous) ? previous : null;
        if (before == destination) return;
        if (destination is { } point && Mathf.Max(Mathf.Abs(point.X), Mathf.Abs(point.Y)) >= map.MapSize * 0.5f)
        {
            SetStatus("Choose a bombing target inside the battlefield.");
            return;
        }

        void Apply(Vector2? target)
        {
            if (target.HasValue) orders[faction] = target.Value;
            else orders.Remove(faction);
            RefreshAircraftPreview();
        }
        Apply(destination);
        Remember(() => Apply(before), () => Apply(destination));
        SetStatus(destination.HasValue ? "Opening bombing run assigned." : "Opening bombing run cleared.");
    }

    private void PaintAircraft(Vector3 world)
    {
        Vector2 cell = AircraftPaint.Cell(world);
        var from = lastAircraftCell ?? cell;
        lastAircraftCell = cell;
        var rules = map.AutoBattlerRules;
        var existing = rules.ReadAircraftStarts(Factions.Red, aircraftBrush.Id);
        int available = AirSquadron.MaxPerFaction;
        foreach (var unit in aircraft)
        {
            if (unit == aircraftBrush) continue;
            int painted = rules.ReadAircraftStarts(Factions.Red, unit.Id).Length;
            available -= painted > 0 ? painted : rules.ReadLegacyFlightCount(Factions.Red, unit.Id);
        }
        var points = AircraftPaint.Edit(existing, from, cell, map.MapSize * 0.5f,
            aircraftStrokeButton == MouseButton.Right, capacity: available);
        if (points != existing) SetAircraftStarts(Factions.Red, aircraftBrush.Id, points);
        SetStatus($"{Factions.Name(Factions.Red)} {aircraftBrush.DisplayName}: {points.Length} / {AirSquadron.MaxPerFaction}");
    }

    private void EndAircraftStroke()
    {
        if (aircraftStroke == null) return;
        var before = aircraftStroke;
        bool hadStarts = aircraftHadStarts;
        byte faction = Factions.Red;
        string id = aircraftBrush.Id;
        var rules = map.AutoBattlerRules;
        var after = rules.ReadAircraftStarts(faction, id);
        aircraftStroke = null;
        lastAircraftCell = null;
        if (before == after) return;
        Remember(() =>
        {
            if (hadStarts) SetAircraftStarts(faction, id, before);
            else
            {
                rules.RestoreLegacyAircraftStarts(faction, id);
                RefreshAircraftPreview();
            }
        }, () => SetAircraftStarts(faction, id, after));
    }

    private void SetAircraftStarts(byte faction, string id, Vector2[] points)
    {
        map.AutoBattlerRules.SetAircraftStarts(faction, id, points);
        RefreshAircraftPreview();
    }

    private void RefreshAircraftPreview()
    {
        if (!AutoBattlerRules.SupportsAircraft(mode.CampaignId)) return;
        var rules = map.AutoBattlerRules;
        AircraftPaint.RefreshBombMarker(map, ref editorBombMarker, Factions.Red,
            rules.OpeningBombOrders.TryGetValue(Factions.Red, out var target) && !editingReserve ? target : null, Surface);
        foreach (var unit in aircraft)
        {
            var red = rules.ReadAircraftStarts(Factions.Red, unit.Id);
            aircraftPreviews.TryGetValue(unit, out var preview);
            preview = AircraftPaint.RefreshPreview(map, preview, unit, System.Array.Empty<Vector2>(), red, Surface);
            if (preview != null)
            {
                preview.Visible = !editingReserve && red.Length > 0;
                aircraftPreviews[unit] = preview;
            }
        }
    }
}
