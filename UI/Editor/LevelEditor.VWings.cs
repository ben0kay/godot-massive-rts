// Paints authored aircraft starts independently of the ground soldier roster.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

public partial class LevelEditor
{
    private const float VWingSpacing = 4f;
    private bool paintBlueVWings;
    private Vector2[] vWingStroke;
    private int vWingLegacyCount;
    private MouseButton vWingStrokeButton;
    private Vector2? lastVWingCell;
    private MultiMeshInstance3D vWingPreview;
    private Mesh vWingPreviewMesh;

    private void BuildVWingControls()
    {
        Heading(context, "V-wing · Drag to paint. Right-drag to erase.", 15);
        var factions = new HBoxContainer();
        context.AddChild(factions);
        var group = new ButtonGroup();
        foreach (bool blue in new[] { true, false })
        {
            var button = Button(factions, blue ? "Blue" : "Red", () =>
            {
                EndVWingStroke();
                paintBlueVWings = blue;
            });
            button.ToggleMode = true;
            button.ButtonGroup = group;
            button.SetPressedNoSignal(paintBlueVWings == blue);
        }
    }

    private bool HandleVWingPaint(InputEvent input)
    {
        if (input is InputEventMouseButton mouse && mouse.ButtonIndex is MouseButton.Left or MouseButton.Right)
        {
            if (!mouse.Pressed) { EndVWingStroke(); return true; }
            if (!WorldPoint(mouse.Position, out var point)) return false;
            EndVWingStroke();
            var rules = map.AutoBattlerRules;
            vWingStroke = paintBlueVWings ? rules.BlueVWingStarts : rules.RedVWingStarts;
            vWingLegacyCount = paintBlueVWings ? rules.BlueVWings : rules.RedVWings;
            vWingStrokeButton = mouse.ButtonIndex;
            PaintVWings(point);
            return true;
        }
        if (input is InputEventMouseMotion motion && vWingStroke != null)
        {
            if (WorldPoint(motion.Position, out var point)) PaintVWings(point);
            else lastVWingCell = null;
            return true;
        }
        return false;
    }

    private void PaintVWings(Vector3 world)
    {
        Vector2 cell = ObstacleField.Xz(world).Snapped(Vector2.One * VWingSpacing);
        var from = lastVWingCell ?? cell;
        lastVWingCell = cell;
        var rules = map.AutoBattlerRules;
        var points = new List<Vector2>(paintBlueVWings ? rules.BlueVWingStarts : rules.RedVWingStarts);
        int steps = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(Mathf.Abs(cell.X - from.X), Mathf.Abs(cell.Y - from.Y)) / VWingSpacing));
        bool changed = false;
        float limit = map.MapSize * 0.5f - VWingSpacing;
        for (int i = 0; i <= steps; i++)
        {
            Vector2 point = from.Lerp(cell, i / (float)steps).Snapped(Vector2.One * VWingSpacing);
            if (vWingStrokeButton == MouseButton.Right)
            {
                changed |= points.Remove(point);
                continue;
            }
            if (points.Count >= AirSquadron.MaxPerFaction || Mathf.Abs(point.X) > limit ||
                Mathf.Abs(point.Y) > limit || points.Contains(point)) continue;
            points.Add(point);
            changed = true;
        }
        if (changed) SetVWingStarts(paintBlueVWings, points.ToArray(), 0);
        SetStatus($"{(paintBlueVWings ? "Blue" : "Red")} V-wings: {points.Count} / {AirSquadron.MaxPerFaction}");
    }

    private void EndVWingStroke()
    {
        if (vWingStroke == null) return;
        var before = vWingStroke;
        int legacyCount = vWingLegacyCount;
        bool blue = paintBlueVWings;
        var rules = map.AutoBattlerRules;
        var after = blue ? rules.BlueVWingStarts : rules.RedVWingStarts;
        vWingStroke = null;
        lastVWingCell = null;
        if (before == after) return;
        Remember(() => SetVWingStarts(blue, before, legacyCount), () => SetVWingStarts(blue, after, 0));
    }

    private void SetVWingStarts(bool blue, Vector2[] points, int legacyCount)
    {
        var rules = map.AutoBattlerRules;
        if (blue) (rules.BlueVWingStarts, rules.BlueVWings) = (points, legacyCount);
        else (rules.RedVWingStarts, rules.RedVWings) = (points, legacyCount);
        RefreshVWingPreview();
    }

    private void RefreshVWingPreview()
    {
        if (mode.CampaignId != AutoBattlerCampaignStore.CampaignId) return;
        var rules = map.AutoBattlerRules;
        int count = rules.BlueVWingStarts.Length + rules.RedVWingStarts.Length;
        if (count == 0)
        {
            if (GodotObject.IsInstanceValid(vWingPreview)) vWingPreview.Hide();
            return;
        }
        if (!GodotObject.IsInstanceValid(vWingPreview) || vWingPreview.GetParent() != map)
        {
            vWingPreviewMesh ??= AirSquadronVisuals.MakeWing();
            vWingPreview = new MultiMeshInstance3D
            {
                Multimesh = new MultiMesh
                {
                    TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true,
                    Mesh = vWingPreviewMesh, InstanceCount = AirSquadron.MaxPerFaction * 2
                },
                MaterialOverride = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true,
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled
                },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            map.AddChild(vWingPreview, false, InternalMode.Back);
        }

        var batch = vWingPreview.Multimesh;
        int index = 0;
        AddFlight(rules.BlueVWingStarts, Factions.Blue);
        AddFlight(rules.RedVWingStarts, Factions.Red);
        batch.VisibleInstanceCount = index;
        vWingPreview.Visible = !editingReserve;

        void AddFlight(Vector2[] points, byte faction)
        {
            for (int i = 0; i < Mathf.Min(points.Length, AirSquadron.MaxPerFaction); i++)
            {
                var point = points[i];
                var position = new Vector3(point.X, 0f, point.Y);
                position.Y = Surface(position) + 1.2f;
                float heading = point.LengthSquared() > 1f ? Mathf.Atan2(-point.X, -point.Y) : faction == Factions.Blue ? 0f : Mathf.Pi;
                batch.SetInstanceTransform(index, new Transform3D(new Basis(Vector3.Up, heading), position));
                batch.SetInstanceColor(index++, Factions.Color(faction));
            }
        }
    }
}
