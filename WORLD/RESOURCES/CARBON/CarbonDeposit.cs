// Authorable Carbon reserve with a static footprint and runtime-only remaining quantity.
using Godot;

namespace MassiveRts;

[Tool]
public partial class CarbonDeposit : Node3D
{
    [Export(PropertyHint.Range, "0,1000000,1,or_greater")] public int InitialCarbon { get; set; } = 5000;
    [Export(PropertyHint.Range, "0.25,3,0.05")] public float VisualSize { get; set; } = 1f;
    [Export(PropertyHint.Range, "1,16,1")] public int FootprintCells { get; set; } = 3;
    public string DepositId { get; private set; } = "";
    public int RemainingCarbon { get; private set; }
    public bool Depleted => RemainingCarbon == 0;
    internal ObstacleRectangle Area { get; private set; }
    private Node3D model;
    internal float PickHeight => 2.5f * model.Scale.Y;
    private double previewClock;

    public override void _Ready()
    {
        RemainingCarbon = Mathf.Max(0, InitialCarbon);
        model = GetNode<Node3D>("Model");
        ApplySize();
        SetProcess(Engine.IsEditorHint());
    }

    public override void _Process(double delta)
    {
        previewClock += delta;
        if (previewClock < 0.25) return;
        previewClock = 0;
        ApplySize();
        if (CommandTerrain.Find(this) is not CommandTerrain terrain) return;
        var area = Footprint();
        Vector3 point = GlobalPosition;
        point.Y = terrain.SampleFootprint(area.Min, area.Max);
        if (!point.IsEqualApprox(GlobalPosition)) GlobalPosition = point;
    }

    private void ApplySize()
    {
        // Keep decorative geometry within the gameplay footprint.
        float maxScale = Mathf.Clamp(FootprintCells, 1, 16) * DeploymentLayout.CellSize / 4.5f;
        Vector3 scale = Vector3.One * Mathf.Min(Mathf.Clamp(VisualSize, 0.25f, 3f), maxScale);
        if (model.Scale != scale) model.Scale = scale;
    }

    internal ObstacleRectangle Footprint()
    {
        int cells = Mathf.Clamp(FootprintCells, 1, 16);
        float half = cells * 0.5f;
        return new ObstacleRectangle(Mathf.RoundToInt(GlobalPosition.X / DeploymentLayout.CellSize - half),
            Mathf.RoundToInt(GlobalPosition.Z / DeploymentLayout.CellSize - half), cells, cells);
    }

    internal void PlaceOnMap(ObstacleRectangle area, Vector3 center, string id)
    {
        DepositId = id;
        Area = area;
        GlobalPosition = center;
    }
}
