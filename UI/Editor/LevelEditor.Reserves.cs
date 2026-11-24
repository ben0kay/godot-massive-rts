// Hosts the off-map reserve painting grid and battlefield entry preview.
using System.Linq;
using Godot;

namespace MassiveRts;

public partial class LevelEditor
{
    private bool editingReserve, previewReserveEntry;
    private Button reserveGridButton, reserveEntryButton;
    private Vector3 ReserveOrigin => new(0, 0,
        Mathf.Ceil(map.MapSize * 0.5f / Grid) * Grid + RedReserveFormation.HalfWidth + 32f);
    private Vector3 PaintOrigin => editingReserve ? ReserveOrigin : Vector3.Zero;
    private float PaintHalfWidth => editingReserve ? RedReserveFormation.HalfWidth : map.MapSize * 0.5f;
    private string UnitContainerName => editingReserve ? RedReserveFormation.ContainerName : "StartingUnits";

    private Node3D ReserveContainer()
    {
        var container = map.GetNodeOrNull<Node3D>(RedReserveFormation.ContainerName);
        if (container != null) return container;
        container = new Node3D { Name = RedReserveFormation.ContainerName, Position = ReserveOrigin };
        map.AddChild(container);
        container.Owner = map;
        return container;
    }

    private void ToggleReserveGrid()
    {
        EndPaint();
        editingReserve = !editingReserve;
        selected = null;
        reserveGridButton.Text = editingReserve ? "Back to battlefield" : "Red staging";
        reserveEntryButton.Disabled = editingReserve;
        ShowPalettePage(0);
        SwitchTool(editingReserve ? Tool.Soldiers : Tool.Select);
        RefreshLayout();
        FrameEditor();
        SetStatus(editingReserve ? "Red staging · paint toward the highlighted south edge" : "");
    }

    private bool FitsStagingFormation(CommandUnitPlacement group, Vector3 point)
    {
        var unit = group.Unit;
        float radius = UnitOutline.FromDefinition(unit, UnitOutline.EffectiveSize(unit)).Footprint;
        Vector3 center = point - ReserveOrigin;
        var basis = new Basis(Vector3.Up, group.Rotation.Y);
        int count = group.SoldierCount;
        for (int i = 0; i < count; i++)
        {
            Vector3 local = group.PaintedOffsets.Length > 0
                ? center + basis * new Vector3(group.PaintedOffsets[i].X, 0, group.PaintedOffsets[i].Y)
                : CommandMovement.Slot(center, basis.X, Mathf.Min(count, CommandMovement.DefaultColumns), i, count, radius * 2f + 0.3f);
            if (Mathf.Abs(local.X) + radius > RedReserveFormation.HalfWidth ||
                Mathf.Abs(local.Z) + radius > RedReserveFormation.HalfWidth) return false;
        }
        return true;
    }

    private void SetReserveEntry(Vector3 point)
    {
        var rules = map.AutoBattlerRules;
        float half = map.MapSize * 0.5f;
        var edge = Mathf.Abs(point.X) > Mathf.Abs(point.Z)
            ? point.X > 0 ? RedReserveFormation.EntryEdge.East : RedReserveFormation.EntryEdge.West
            : point.Z > 0 ? RedReserveFormation.EntryEdge.South : RedReserveFormation.EntryEdge.North;
        float along = edge switch
        {
            RedReserveFormation.EntryEdge.East => point.Z,
            RedReserveFormation.EntryEdge.West => -point.Z,
            RedReserveFormation.EntryEdge.South => -point.X,
            _ => point.X
        };
        SetValue((rules.RedFormationEdge, rules.RedReserveEdgeOffset), ((int)edge, along / half * 100f), value =>
        {
            (rules.RedFormationEdge, rules.RedReserveEdgeOffset) = value;
        });
    }

    private void DrawReserveGrid(Node3D parent)
    {
        float half = RedReserveFormation.HalfWidth;
        var floor = new MeshInstance3D
        {
            Position = ReserveOrigin - Vector3.Up * 0.06f,
            Mesh = new PlaneMesh { Size = Vector2.One * (2f * half) },
            MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = new Color("#182535") }
        };
        parent.AddChild(floor);
        using var lines = new SurfaceTool();
        lines.Begin(Mesh.PrimitiveType.Lines);
        lines.SetMaterial(ReserveLineMaterial());
        for (float offset = -half; offset <= half; offset += Grid)
        {
            lines.SetColor(new Color("#34485b"));
            lines.AddVertex(ReserveOrigin + new Vector3(offset, 0, -half));
            lines.AddVertex(ReserveOrigin + new Vector3(offset, 0, half));
            lines.SetColor(offset == half ? new Color("#ff916d") : new Color("#34485b"));
            lines.AddVertex(ReserveOrigin + new Vector3(-half, 0, offset));
            lines.AddVertex(ReserveOrigin + new Vector3(half, 0, offset));
        }
        parent.AddChild(new MeshInstance3D { Mesh = lines.Commit() });
        parent.AddChild(new Label3D { Text = "SOUTH · FRONT OF WAVE", Position = ReserveOrigin + new Vector3(0, 2, half),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, PixelSize = 0.12f, NoDepthTest = true });
    }

    private void DrawReserveEntry(Node3D parent)
    {
        var groups = RedReserveFormation.Groups(map).Where(group => group.Unit != null).ToArray();
        var (rocks, _) = paint.Compile();
        var areas = rocks.Concat(paint.MovementWater).Concat(map.ReadAuthoredWalls())
            .Concat(Objects().OfType<CommandLaserPlacement>().Select(tower => tower.Footprint(tower.GlobalPosition))).ToArray();
        var field = areas.Length == 0 ? null : new ObstacleField(areas, map.MapSize * 0.5f, paint.BridgeAreas);
        using var lines = new SurfaceTool();
        lines.Begin(Mesh.PrimitiveType.Lines);
        lines.SetMaterial(ReserveLineMaterial());
        void Line(Vector3 a, Vector3 b, Color color)
        {
            lines.SetColor(color);
            a.Y = Surface(a) + 0.4f;
            b.Y = Surface(b) + 0.4f;
            lines.AddVertex(a);
            lines.AddVertex(b);
        }

        var marker = RedReserveFormation.Project(new Vector3(0, 0, RedReserveFormation.HalfWidth), 4f, map, field, 0f);
        var facing = new Vector3(marker.Facing.X, 0, marker.Facing.Y);
        var across = facing.Cross(Vector3.Up);
        Color arrow = new("#ffad6d");
        Line(marker.Start, marker.Goal, arrow);
        Line(marker.Goal, marker.Goal - facing * 5f + across * 3f, arrow);
        Line(marker.Goal, marker.Goal - facing * 5f - across * 3f, arrow);
        float widest = groups.Select(group => UnitOutline.FromDefinition(group.Unit,
            UnitOutline.EffectiveSize(group.Unit)).Footprint).DefaultIfEmpty(0f).Max();
        float minAcross = float.PositiveInfinity, maxAcross = float.NegativeInfinity;
        foreach (var group in groups)
        {
            float radius = UnitOutline.FromDefinition(group.Unit, UnitOutline.EffectiveSize(group.Unit)).Footprint;
            foreach (var point in group.Positions(radius))
            {
                var local = group.GetParent<Node3D>().ToLocal(point);
                var lane = RedReserveFormation.Project(local, radius, map, field, 0f);
                var edge = lane.Goal - facing * (4f * radius + 0.05f);
                Color color = lane.Clear ? new Color("#64e5cb") : new Color("#ff4d53");
                float side = across.Dot(edge - marker.Start);
                minAcross = Mathf.Min(minAcross, side - radius);
                maxAcross = Mathf.Max(maxAcross, side + radius);
                Line(edge, lane.Goal, color);
                Line(lane.Goal - across * radius, lane.Goal + across * radius, color);
            }
        }
        if (widest > 0f)
        {
            var handoff = marker.Start + facing * (4f * widest + 0.05f);
            Line(handoff + across * minAcross, handoff + across * maxAcross, new Color("#e1ebfd"));
        }
        parent.AddChild(new MeshInstance3D { Mesh = lines.Commit() });
    }

    private static StandardMaterial3D ReserveLineMaterial() => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true
    };
}
