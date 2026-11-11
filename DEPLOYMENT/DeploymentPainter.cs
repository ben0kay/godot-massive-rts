// Mouse placement and an instanced preview for the benchmark deployment layout.
using Godot;

namespace MassiveRts;

public partial class DeploymentPainter : MultiMeshInstance3D
{
    public DeploymentLayout Layout { get; } = new();
    public Camera3D Camera { get; set; }
    public int Capacity { get; set; }
    public int Limit { get; set; }
    public int Brush { get; set; }
    public event System.Action Changed;

    public void InitializePreview()
    {
        if (Multimesh != null) return;
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true
        };
        Multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = new BoxMesh { Size = new Vector3(1.7f, 0.1f, 1.7f), Material = material },
            InstanceCount = Capacity,
            VisibleInstanceCount = 0
        };
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (!Visible) return;
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

        Vector3 origin = Camera.ProjectRayOrigin(screen);
        Vector3 direction = Camera.ProjectRayNormal(screen);
        if (direction.Y >= 0 || origin.Y < 0) return;
        Vector3 ground = origin - direction * (origin.Y / direction.Y);
        var cell = new Vector2I(Mathf.FloorToInt(ground.X / DeploymentLayout.CellSize),
            Mathf.FloorToInt(ground.Z / DeploymentLayout.CellSize));
        bool changed = false;
        for (int z = -1; z <= 1; z++)
        for (int x = -1; x <= 1; x++)
        {
            var point = cell + new Vector2I(x, z);
            int slot;
            bool edited = erase ? Layout.TryErase(point, out slot) :
                Layout.TryPaint(new SoldierPlacement(point.X, point.Y, (byte)(Brush / 2), (byte)(Brush % 2)), Limit, out slot);
            if (!edited) continue;
            if (slot < Layout.Placements.Count) Write(slot);
            changed = true;
        }
        if (!changed) return;
        Multimesh.VisibleInstanceCount = Layout.Placements.Count;
        Changed?.Invoke();
    }

    public void Clear()
    {
        Layout.Clear();
        Multimesh.VisibleInstanceCount = 0;
        Changed?.Invoke();
    }

    private const string SavePath = "user://deployment.json";

    public string Save()
    {
        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        if (file == null) return $"Save failed ({FileAccess.GetOpenError()}).";
        file.StoreString(System.Text.Json.JsonSerializer.Serialize(Layout.Placements));
        return file.GetError() == Error.Ok ? "Deployment saved." : "Could not write deployment.";
    }

    public string Load()
    {
        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        if (file == null) return "No readable saved deployment.";
        if (file.GetLength() > 10_000_000) return "Saved deployment is too large.";
        SoldierPlacement[] saved;
        try
        {
            saved = System.Text.Json.JsonSerializer.Deserialize<SoldierPlacement[]>(file.GetAsText());
        }
        catch (System.Text.Json.JsonException)
        {
            return "Saved deployment is invalid.";
        }
        if (saved == null || saved.Length > Limit) return "Saved deployment exceeds the current limit or is invalid.";
        var validated = new DeploymentLayout();
        foreach (SoldierPlacement placement in saved)
            if (!validated.TryPaint(placement, Limit, out _))
                return "Saved deployment contains invalid placements.";
        if (validated.Placements.Count != saved.Length) return "Saved deployment contains duplicate cells.";

        Layout.Clear();
        foreach (SoldierPlacement placement in saved)
        {
            Layout.TryPaint(placement, Limit, out int slot);
            Write(slot);
        }
        Multimesh.VisibleInstanceCount = Layout.Placements.Count;
        Changed?.Invoke();
        return "Deployment loaded.";
    }

    private void Write(int slot)
    {
        SoldierPlacement placement = Layout.Placements[slot];
        Vector3 position = placement.Position() + Vector3.Up * 0.07f;
        Basis basis = placement.Kind == 0 ? Basis.Identity : Basis.Identity.Scaled(new Vector3(0.65f, 1, 0.65f));
        Multimesh.SetInstanceTransform(slot, new Transform3D(basis, position));
        Multimesh.SetInstanceColor(slot, placement.Faction == 0 ? new Color(0.12f, 0.45f, 1f) : new Color(1f, 0.15f, 0.12f));
    }
}
