// Mouse placement and an instanced preview for the benchmark deployment layout.
using System.Text.Json;
using Godot;

namespace MassiveRts;

public partial class DeploymentPainter : Node3D
{
    public DeploymentLayout Layout { get; private set; }
    private RuntimeUnitCatalog catalog;
    private DeploymentPreview[] previews;
    public Camera3D Camera { get; set; }
    public int Limit { get; set; }
    public byte BrushFaction { get; set; }
    public byte BrushType { get; set; }
    public event System.Action Changed;
    public event System.Action<string> EditRejected;
    private MeshInstance3D walls, rectanglePreview;
    private bool obstacleTool, dragging;
    private MouseButton dragButton;
    private Vector2I dragStart;
    public float ObstacleHeight { get; set; }
    public Color ObstacleTint { get; set; }
    public Mesh ObstacleMesh => walls.Mesh;

    public void SetObstacleTool(bool enabled)
    {
        obstacleTool = enabled;
        CancelDrag();
    }

    public void CancelDrag()
    {
        dragging = false;
        rectanglePreview.Hide();
    }

    public void Initialize(RuntimeUnitCatalog unitCatalog, Mesh[] shapes)
    {
        catalog = unitCatalog;
        Layout = new DeploymentLayout(catalog);
        previews = new DeploymentPreview[catalog.Count];
        for (int i = 0; i < previews.Length; i++)
            previews[i] = new DeploymentPreview(this, shapes[i], catalog[i].Height);
        walls = new MeshInstance3D { CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(walls);
        rectanglePreview = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = Vector3.One }, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.67f, 0.75f, 0.84f, 0.32f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            }
        };
        AddChild(rectanglePreview);
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (!Visible) return;
        if (obstacleTool)
        {
            HandleObstacleInput(input);
            return;
        }
        Vector2 screen;
        bool erase;
        if (input is InputEventMouseButton button && button.Pressed &&
            (button.ButtonIndex == MouseButton.Left || button.ButtonIndex == MouseButton.Right))
        {
            screen = button.Position;
            erase = button.ButtonIndex == MouseButton.Right;
        }
        else if (input is InputEventMouseMotion motion &&
            (motion.ButtonMask & (MouseButtonMask.Left | MouseButtonMask.Right)) != 0)
        {
            screen = motion.Position;
            erase = (motion.ButtonMask & MouseButtonMask.Right) != 0;
        }
        else return;

        if (!TryCell(screen, out Vector2I cell)) return;
        bool changed = false;
        for (int z = -1; z <= 1; z++)
        for (int x = -1; x <= 1; x++)
        {
            var point = cell + new Vector2I(x, z);
            bool existed = Layout.TryGet(point, out SoldierPlacement previous);
            var placement = new SoldierPlacement(point.X, point.Y, BrushFaction, BrushType);
            bool edited = erase ? Layout.TryErase(point, out _) : Layout.TryPaint(placement, Limit, out _);
            if (!edited) continue;
            if (existed) previews[previous.Type].Remove(point);
            if (!erase) previews[placement.Type].Add(placement);
            changed = true;
        }
        if (changed) Changed?.Invoke();
    }

    private bool TryCell(Vector2 screen, out Vector2I cell)
    {
        cell = default;
        Vector3 origin = Camera.ProjectRayOrigin(screen);
        Vector3 direction = Camera.ProjectRayNormal(screen);
        if (direction.Y >= 0 || origin.Y < 0) return false;
        Vector3 ground = origin - direction * (origin.Y / direction.Y);
        cell = new Vector2I(Mathf.FloorToInt(ground.X / DeploymentLayout.CellSize),
            Mathf.FloorToInt(ground.Z / DeploymentLayout.CellSize));
        return true;
    }

    // Release may be consumed by a GUI control. Cancel it if it never reaches _UnhandledInput.
    public override void _Input(InputEvent input)
    {
        if (dragging && input is InputEventMouseButton button && !button.Pressed && button.ButtonIndex == dragButton)
            Callable.From(() => { if (dragging && !Input.IsMouseButtonPressed(dragButton)) CancelDrag(); }).CallDeferred();
    }

    private void HandleObstacleInput(InputEvent input)
    {
        if (input is InputEventMouseButton button &&
            (button.ButtonIndex == MouseButton.Left || button.ButtonIndex == MouseButton.Right))
        {
            if (button.Pressed)
            {
                if (dragging || !TryCell(button.Position, out dragStart)) return;
                var first = new ObstacleRectangle(dragStart.X, dragStart.Y, 1, 1);
                if (!first.IsValid) return;
                dragging = true;
                dragButton = button.ButtonIndex;
                ShowRectangle(first);
            }
            else if (dragging && button.ButtonIndex == dragButton)
            {
                CancelDrag();
                if (!TryCell(button.Position, out Vector2I end)) return;
                if (Layout.TryEditObstacles(DragArea(end), dragButton == MouseButton.Right, out string message))
                {
                    RebuildWalls();
                    Changed?.Invoke();
                }
                else if (message != null) EditRejected?.Invoke(message);
            }
        }
        else if (dragging && input is InputEventMouseMotion motion && TryCell(motion.Position, out Vector2I hover))
            ShowRectangle(DragArea(hover));
    }

    private ObstacleRectangle DragArea(Vector2I end)
    {
        end.X = Mathf.Clamp(end.X, -DeploymentLayout.HalfWidth, DeploymentLayout.HalfWidth);
        end.Y = Mathf.Clamp(end.Y, -DeploymentLayout.HalfWidth, DeploymentLayout.HalfWidth);
        return new ObstacleRectangle(Mathf.Min(dragStart.X, end.X), Mathf.Min(dragStart.Y, end.Y),
            Mathf.Abs(dragStart.X - end.X) + 1, Mathf.Abs(dragStart.Y - end.Y) + 1);
    }

    private void ShowRectangle(ObstacleRectangle area)
    {
        Vector2 size = area.Max - area.Min, center = (area.Min + area.Max) * 0.5f;
        rectanglePreview.Scale = new Vector3(size.X, ObstacleHeight + 0.04f, size.Y);
        rectanglePreview.Position = new Vector3(center.X, ObstacleHeight * 0.5f, center.Y);
        rectanglePreview.Show();
    }

    private void RebuildWalls() => walls.Mesh = ObstacleRenderer.Build(Layout.Obstacles.Field, ObstacleHeight, ObstacleTint);

    public void Clear()
    {
        CancelDrag();
        Layout.Clear();
        RebuildWalls();
        foreach (DeploymentPreview preview in previews) preview.Clear();
        Changed?.Invoke();
    }

    private const string SavePath = "user://deployment.json";

    private sealed record SavedPlacement(int X, int Z, byte Faction, string UnitId);
    private sealed record SavedDeployment(int Version, SavedPlacement[] Placements, ObstacleRectangle[] Obstacles);
    private readonly record struct LegacyPlacement(int X, int Z, byte Faction, byte Kind);

    public string Save()
    {
        var saved = new SavedPlacement[Layout.Placements.Count];
        for (int i = 0; i < saved.Length; i++)
        {
            SoldierPlacement placement = Layout.Placements[i];
            saved[i] = new SavedPlacement(placement.X, placement.Z, placement.Faction, catalog[placement.Type].Id);
        }
        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        if (file == null) return $"Save failed ({FileAccess.GetOpenError()}).";
        file.StoreString(JsonSerializer.Serialize(new SavedDeployment(2, saved, Layout.Obstacles.Field == null
            ? System.Array.Empty<ObstacleRectangle>() : Layout.Obstacles.Field.Rectangles.ToArray())));
        return file.GetError() == Error.Ok ? "Deployment saved." : "Could not write deployment.";
    }

    public string Load()
    {
        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        if (file == null) return "No readable saved deployment.";
        if (file.GetLength() > 16_000_000) return "Saved deployment is too large.";

        var validated = new DeploymentLayout(catalog);
        try
        {
            using var document = JsonDocument.Parse(file.GetAsText());
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                var legacy = document.RootElement.Deserialize<LegacyPlacement[]>();
                if (legacy.Length > Limit) return "Saved deployment exceeds the current troop limit.";
                foreach (LegacyPlacement placement in legacy)
                {
                    byte type = placement.Kind switch
                    {
                        0 => RuntimeUnitCatalog.Circle,
                        1 => RuntimeUnitCatalog.Triangle,
                        _ => byte.MaxValue
                    };
                    if (!TryLoadPlacement(validated, placement.X, placement.Z, placement.Faction, type))
                        return "Saved deployment contains invalid placements.";
                }
            }
            else
            {
                var saved = document.RootElement.Deserialize<SavedDeployment>();
                if (saved == null || saved.Version is not (1 or 2) || saved.Placements == null) return "Unsupported deployment format.";
                if (saved.Placements.Length > Limit) return "Saved deployment exceeds the current troop limit.";
                if (saved.Version == 2)
                {
                    if (!ObstacleLayout.TryLoad(saved.Obstacles, out ObstacleLayout obstacles))
                        return "Saved deployment contains invalid obstacle rectangles.";
                    validated.SetLoadedObstacles(obstacles);
                }
                foreach (SavedPlacement placement in saved.Placements)
                {
                    if (placement == null || !catalog.TryResolve(placement.UnitId, out byte type) ||
                        !TryLoadPlacement(validated, placement.X, placement.Z, placement.Faction, type))
                        return "Saved deployment contains invalid placements.";
                }
            }
        }
        catch (JsonException)
        {
            return "Saved deployment is invalid.";
        }

        CancelDrag();
        Layout = validated;
        RebuildWalls();
        foreach (DeploymentPreview preview in previews) preview.Clear();
        foreach (SoldierPlacement placement in Layout.Placements) previews[placement.Type].Add(placement);
        Changed?.Invoke();
        return "Deployment loaded.";
    }

    private bool TryLoadPlacement(DeploymentLayout draft, int x, int z, byte faction, byte type)
    {
        return !draft.TryGet(new Vector2I(x, z), out _) &&
            draft.TryPaint(new SoldierPlacement(x, z, faction, type), Limit, out _);
    }
}
