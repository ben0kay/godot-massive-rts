// Paints authored aircraft starts independently of the ground soldier roster.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

public partial class LevelEditor
{
    private UnitDefinition aircraftBrush;
    private byte aircraftFaction;
    private Vector2[] aircraftStroke;
    private bool aircraftHadStarts;
    private MouseButton aircraftStrokeButton;
    private Vector2? lastAircraftCell;
    private readonly Dictionary<UnitDefinition, MultiMeshInstance3D> aircraftPreviews = new();

    private void BuildAircraftControls() =>
        Heading(context, "Drag to paint. Right-drag to erase.", 15);

    private bool HandleAircraftPaint(InputEvent input)
    {
        if (input is InputEventMouseButton mouse && mouse.ButtonIndex is MouseButton.Left or MouseButton.Right)
        {
            if (!mouse.Pressed) { EndAircraftStroke(); return true; }
            if (!WorldPoint(mouse.Position, out var point)) return false;
            EndAircraftStroke();
            var rules = map.AutoBattlerRules;
            aircraftStroke = rules.ReadAircraftStarts(aircraftFaction, aircraftBrush.Id);
            aircraftHadStarts = rules.HasAircraftStarts(aircraftFaction, aircraftBrush.Id);
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

    private void PaintAircraft(Vector3 world)
    {
        Vector2 cell = AircraftPaint.Cell(world);
        var from = lastAircraftCell ?? cell;
        lastAircraftCell = cell;
        var rules = map.AutoBattlerRules;
        var existing = rules.ReadAircraftStarts(aircraftFaction, aircraftBrush.Id);
        int available = AirSquadron.MaxPerFaction;
        foreach (var unit in aircraft)
        {
            if (unit == aircraftBrush) continue;
            int painted = rules.ReadAircraftStarts(aircraftFaction, unit.Id).Length;
            available -= painted > 0 ? painted : rules.ReadLegacyFlightCount(aircraftFaction, unit.Id);
        }
        var points = AircraftPaint.Edit(existing, from, cell, map.MapSize * 0.5f,
            aircraftStrokeButton == MouseButton.Right, capacity: available);
        if (points != existing) SetAircraftStarts(aircraftFaction, aircraftBrush.Id, points);
        SetStatus($"{Factions.Name(aircraftFaction)} {aircraftBrush.DisplayName}: {points.Length} / {AirSquadron.MaxPerFaction}");
    }

    private void EndAircraftStroke()
    {
        if (aircraftStroke == null) return;
        var before = aircraftStroke;
        bool hadStarts = aircraftHadStarts;
        byte faction = aircraftFaction;
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
        foreach (var unit in aircraft)
        {
            var blue = rules.ReadAircraftStarts(Factions.Blue, unit.Id);
            var red = rules.ReadAircraftStarts(Factions.Red, unit.Id);
            aircraftPreviews.TryGetValue(unit, out var preview);
            preview = AircraftPaint.RefreshPreview(map, preview, unit, blue, red, Surface);
            if (preview != null)
            {
                preview.Visible = !editingReserve && blue.Length + red.Length > 0;
                aircraftPreviews[unit] = preview;
            }
        }
    }
}
