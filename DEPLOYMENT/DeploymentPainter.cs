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

    public void Initialize(RuntimeUnitCatalog unitCatalog, Mesh[] shapes)
    {
        catalog = unitCatalog;
        Layout = new DeploymentLayout(catalog);
        previews = new DeploymentPreview[catalog.Count];
        for (int i = 0; i < previews.Length; i++)
            previews[i] = new DeploymentPreview(this, shapes[i], catalog[i].Height);
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

    public void Clear()
    {
        Layout.Clear();
        foreach (DeploymentPreview preview in previews) preview.Clear();
        Changed?.Invoke();
    }

    private const string SavePath = "user://deployment.json";

    private sealed record SavedPlacement(int X, int Z, byte Faction, string UnitId);
    private sealed record SavedDeployment(int Version, SavedPlacement[] Placements);
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
        file.StoreString(JsonSerializer.Serialize(new SavedDeployment(1, saved)));
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
                if (saved == null || saved.Version != 1 || saved.Placements == null) return "Unsupported deployment format.";
                if (saved.Placements.Length > Limit) return "Saved deployment exceeds the current troop limit.";
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

        Layout = validated;
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
