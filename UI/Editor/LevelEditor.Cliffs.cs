// Edits closed cliff outlines in the shared terrain palette.
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MassiveRts;

public partial class LevelEditor
{
    private readonly List<Vector2> cliffPoints = new();
    private float cliffRise = 8f;
    private MeshInstance3D cliffStroke;
    private Label3D cliffCompleteCue;

    private void CancelCliffStroke()
    {
        cliffPoints.Clear();
        cliffCompleteCue?.Hide();
        cliffCompleteCue = null;
        if (cliffStroke == null) return;
        cliffStroke.QueueFree();
        cliffStroke = null;
    }

    private bool HandleCliffPaint(InputEvent input)
    {
        if (input is InputEventMouseMotion motion)
        {
            if (cliffPoints.Count > 0 && WorldPoint(motion.Position, out var hover))
                DrawCliffStroke(ObstacleField.Xz(hover), motion.Position);
            else cliffCompleteCue?.Hide();
            return true;
        }
        if (input is not InputEventMouseButton mouse || mouse.ButtonIndex is not (MouseButton.Left or MouseButton.Right))
            return false;
        if (!mouse.Pressed) return true;
        if (mouse.ButtonIndex == MouseButton.Right && cliffPoints.Count > 0)
        {
            CancelCliffStroke();
            return true;
        }
        if (!WorldPoint(mouse.Position, out var position)) return true;
        var terrain = map.GetNode<CommandTerrain>("Terrain");
        var point = ObstacleField.Xz(position);
        if (mouse.ButtonIndex == MouseButton.Right)
        {
            var cliff = terrain.GetChildren().OfType<TerrainCliff>().LastOrDefault(item =>
                item.TryCompile(map.MapSize * 0.5f, out var outline, out _) && Geometry2D.IsPointInPolygon(point, outline));
            if (cliff != null) RemoveObject(cliff);
            return true;
        }

        if (cliffPoints.Count > 0)
        {
            if (CanCloseCliff(point, mouse.Position))
            {
                FinishCliff(terrain);
                return true;
            }
            if (cliffPoints[^1].DistanceSquaredTo(point) < Grid * Grid) return true;
        }
        if (cliffPoints.Count >= TerrainCliff.MaxPoints)
        {
            SetStatus("Click the first point to close the cliff.");
            return true;
        }
        cliffPoints.Add(point);
        DrawCliffStroke(point, mouse.Position);
        SetStatus("Click the first point to finish the cliff.");
        return true;
    }

    private void FinishCliff(CommandTerrain terrain)
    {
        if (terrain.GetChildren().OfType<TerrainCliff>().Count() >= TerrainCliff.MaxCliffs)
        {
            SetStatus($"A level supports up to {TerrainCliff.MaxCliffs} cliffs.");
            return;
        }
        var cliff = new TerrainCliff { Name = "Cliff", Points = cliffPoints.ToArray(), Rise = cliffRise };
        if (!cliff.TryCompile(map.MapSize * 0.5f, out var outline, out _))
        {
            cliff.Free();
            SetStatus("Use a simple outline with separated points and fewer long diagonal edges.");
            return;
        }
        foreach (var existing in terrain.GetChildren().OfType<TerrainCliff>())
        {
            if (!existing.TryCompile(map.MapSize * 0.5f, out var other, out _)) continue;
            if (!TerrainCliff.Overlaps(outline, other)) continue;
            cliff.Free();
            SetStatus("Cliff plateaus need separate outlines.");
            return;
        }
        if (TryAddPlacement(cliff, "Terrain", Vector3.Zero)) CancelCliffStroke();
        else cliff.Free();
    }

    private bool CanCloseCliff(Vector2 point, Vector2 mouse)
    {
        if (cliffPoints.Count < 3) return false;
        var first = cliffPoints[0];
        var world = new Vector3(first.X, 0f, first.Y);
        world.Y = Surface(world);
        return camera.UnprojectPosition(world).DistanceSquaredTo(mouse) <= 144f ||
            first.DistanceSquaredTo(point) <= Grid * Grid;
    }

    private void DrawCliffStroke(Vector2 hover, Vector2 mouse)
    {
        if (cliffStroke == null)
        {
            cliffStroke = new MeshInstance3D
            {
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Mesh = new ImmediateMesh(),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#f7cf78"),
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, NoDepthTest = true }
            };
            map.AddChild(cliffStroke, false, InternalMode.Back);
            cliffCompleteCue = new Label3D
            {
                Text = "Complete cliff",
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                NoDepthTest = true,
                FixedSize = true,
                FontSize = 20,
                OutlineSize = 6,
                Modulate = new Color("#b2ffe0"),
                Visible = false
            };
            cliffStroke.AddChild(cliffCompleteCue);
        }
        var mesh = (ImmediateMesh)cliffStroke.Mesh;
        mesh.ClearSurfaces();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
        Vector3 OnGround(Vector2 p)
        {
            var point = new Vector3(p.X, 0, p.Y);
            point.Y = Surface(point) + 0.15f;
            return point;
        }
        for (int i = 1; i < cliffPoints.Count; i++)
        {
            mesh.SurfaceAddVertex(OnGround(cliffPoints[i - 1]));
            mesh.SurfaceAddVertex(OnGround(cliffPoints[i]));
        }
        mesh.SurfaceAddVertex(OnGround(cliffPoints[^1]));
        bool closing = CanCloseCliff(hover, mouse);
        var first = OnGround(cliffPoints[0]);
        mesh.SurfaceAddVertex(closing ? first : OnGround(hover));
        cliffCompleteCue.Position = first;
        cliffCompleteCue.Offset = new Vector2(0f, 23f);
        cliffCompleteCue.Visible = closing;
        float size = closing ? 0.85f : 0.55f;
        mesh.SurfaceAddVertex(first - Vector3.Right * size);
        mesh.SurfaceAddVertex(first + Vector3.Right * size);
        mesh.SurfaceAddVertex(first - Vector3.Forward * size);
        mesh.SurfaceAddVertex(first + Vector3.Forward * size);
        mesh.SurfaceEnd();
    }
}
