// Uses a two-click line preview before committing an undoable trench object.
using System.Linq;
using Godot;

namespace MassiveRts;

public partial class LevelEditor
{
    private TerrainTrench trenchDraft;
    private MeshInstance3D trenchPreview;
    private float trenchWidth = 8f, trenchDepth = 2.4f, trenchRamp = 8f;

    private void TrenchControls()
    {
        Number(context, "Floor width", trenchWidth, 6, 24, 0.5).ValueChanged += value => trenchWidth = (float)value;
        Number(context, "Depth", trenchDepth, 0.5, 8, 0.1).ValueChanged += value => trenchDepth = (float)value;
        Number(context, "Ramp length at each end", trenchRamp, 4, 32, 0.5).ValueChanged += value => trenchRamp = (float)value;
        Heading(context, "Click both ends or drag and release along a map axis. Right-click to cancel or remove.", 15);
    }

    private Vector2? trenchPress;

    private bool HandleTrench(InputEvent input)
    {
        if (input is InputEventMouseMotion motion && trenchDraft != null)
        {
            if (WorldPoint(motion.Position, out var hover)) UpdateTrenchDraft(ObstacleField.Xz(Snap(hover)));
            else if (trenchPreview != null) trenchPreview.Hide();
            return true;
        }
        if (input is not InputEventMouseButton mouse ||
            mouse.ButtonIndex is not (MouseButton.Left or MouseButton.Right)) return false;
        GetViewport().SetInputAsHandled();
        if (!mouse.Pressed)
        {
            if (mouse.ButtonIndex == MouseButton.Left && trenchPress is Vector2 start)
            {
                trenchPress = null;
                if (trenchDraft != null && start.DistanceSquaredTo(mouse.Position) > 25f &&
                    WorldPoint(mouse.Position, out var end))
                    CommitTrench(ObstacleField.Xz(Snap(end)));
            }
            return true;
        }
        if (mouse.ButtonIndex == MouseButton.Right && trenchDraft != null)
        {
            CancelTrench();
            return true;
        }
        if (!WorldPoint(mouse.Position, out var hit)) return true;
        Vector2 point = ObstacleField.Xz(Snap(hit));
        var terrain = map.GetNode<CommandTerrain>("Terrain");
        if (mouse.ButtonIndex == MouseButton.Right)
        {
            var remove = terrain.GetChildren().OfType<TerrainTrench>().LastOrDefault(trench => trench.Floor.HasPoint(ObstacleField.Xz(hit)));
            if (remove != null) RemoveObject(remove);
            return true;
        }
        if (trenchDraft == null)
        {
            if (terrain.GetChildren().OfType<TerrainTrench>().Count() >= TerrainTrench.MaxPerMap)
            {
                SetStatus($"A map supports up to {TerrainTrench.MaxPerMap} trenches.");
                return true;
            }
            trenchDraft = new TerrainTrench { Name = "Trench", Start = point, End = point };
            trenchPress = mouse.Position;
            SetStatus("Click the end of the trench or drag and release.");
            return true;
        }

        CommitTrench(point);
        return true;
    }

    private void CommitTrench(Vector2 point)
    {
        var terrain = map.GetNode<CommandTerrain>("Terrain");
        UpdateTrenchDraft(point);
        if (!CanPlaceTrench(terrain, trenchDraft))
        {
            SetStatus("Use a longer line for both ramps. Keep trenches within the map and clear of other trenches, cliffs, rock and water.");
            return;
        }
        var placed = trenchDraft;
        trenchDraft = null;
        CancelTrench();
        if (!TryAddPlacement(placed, "Terrain", Vector3.Zero)) placed.Free();
    }

    private bool CanPlaceTrench(CommandTerrain terrain, TerrainTrench draft)
    {
        if (!draft.Fits(map.MapSize * 0.5f)) return false;
        if (terrain.GetChildren().OfType<TerrainTrench>().Any(other => draft.Bounds.Intersects(other.Bounds))) return false;
        foreach (var cliff in terrain.GetChildren().OfType<TerrainCliff>())
        {
            if (cliff.Points == null || cliff.Points.Length == 0) continue;
            var bounds = new Rect2(cliff.Points[0], Vector2.Zero);
            foreach (var point in cliff.Points) bounds = bounds.Expand(point);
            if (draft.Bounds.Intersects(bounds)) return false;
        }
        return !paint.Data.WaterCells.Concat(paint.Data.RockCells).Any(cell =>
            draft.Bounds.Intersects(new Rect2((Vector2)cell * Grid, Vector2.One * Grid)));
    }

    private void UpdateTrenchDraft(Vector2 point)
    {
        var draft = trenchDraft;
        Vector2 difference = point - draft.Start;
        draft.End = Mathf.Abs(difference.X) >= Mathf.Abs(difference.Y)
            ? new Vector2(point.X, draft.Start.Y) : new Vector2(draft.Start.X, point.Y);
        draft.Width = trenchWidth;
        draft.Depth = trenchDepth;
        draft.Ramp = trenchRamp;
        if (trenchPreview == null)
        {
            trenchPreview = new MeshInstance3D { Mesh = new ImmediateMesh(),
                MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    VertexColorUseAsAlbedo = true, NoDepthTest = true } };
            map.AddChild(trenchPreview, false, InternalMode.Back);
        }

        var mesh = (ImmediateMesh)trenchPreview.Mesh;
        mesh.ClearSurfaces();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
        bool valid = draft.Fits(map.MapSize * 0.5f);
        var tint = valid ? new Color("#ffc383") : new Color("#ff6963");
        Vector2 axis = (draft.End - draft.Start).Normalized();
        Vector2 across = new(-axis.Y, axis.X);
        float ramp = Mathf.Min(draft.Ramp, draft.Length * 0.5f);
        Vector2 a = draft.Start + axis * ramp, b = draft.End - axis * ramp;
        float baseA = Surface(new Vector3(draft.Start.X, 0f, draft.Start.Y));
        float baseB = Surface(new Vector3(draft.End.X, 0f, draft.End.Y));
        Vector2 center = (draft.Start + draft.End) * 0.5f;
        float centerHeight = Surface(new Vector3(center.X, 0f, center.Y));
        float floor = Mathf.Min(centerHeight, Mathf.Min(baseA, baseB)) - draft.Depth;
        Vector2 half = across * (draft.Width * 0.5f);
        void Line(Vector2 from, float y, Vector2 to, float endY)
        {
            mesh.SurfaceSetColor(tint);
            mesh.SurfaceAddVertex(new Vector3(from.X, y + 0.12f, from.Y));
            mesh.SurfaceSetColor(tint);
            mesh.SurfaceAddVertex(new Vector3(to.X, endY + 0.12f, to.Y));
        }
        foreach (int side in new[] { -1, 1 })
        {
            Vector2 shift = half * side;
            Line(draft.Start + shift, baseA, a + shift, floor);
            Line(a + shift, floor, b + shift, floor);
            Line(b + shift, floor, draft.End + shift, baseB);
        }
        Line(a - half, floor, a + half, floor);
        Line(b - half, floor, b + half, floor);
        mesh.SurfaceEnd();
        trenchPreview.Show();
    }

    private void CancelTrench()
    {
        trenchPress = null;
        trenchDraft?.Free();
        trenchDraft = null;
        trenchPreview?.QueueFree();
        trenchPreview = null;
    }
}
