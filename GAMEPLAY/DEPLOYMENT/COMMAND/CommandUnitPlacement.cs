// Saves one starting unit or squad as scene data, with an editor-only representative mesh.
using Godot;

namespace MassiveRts;

[Tool]
public partial class CommandUnitPlacement : Marker3D
{
    public enum Team { Blue, Red }
    [Export] public UnitDefinition Unit { get; set; }
    [Export] public Team Faction { get; set; }
    [Export(PropertyHint.Range, "1,256,1")] public int Count { get; set; } = 1;
    private MeshInstance3D model;
    private Label3D label;
    private UnitDefinition previewUnit;
    private Team previewTeam;
    private int previewCount = -1;

    public override void _Ready()
    {
        if (!Engine.IsEditorHint()) { SetProcess(false); return; }
        model = new MeshInstance3D();
        label = new Label3D { Position = Vector3.Up * 1.8f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            PixelSize = 0.015f, FontSize = 24 };
        AddChild(model, false, InternalMode.Back);
        AddChild(label, false, InternalMode.Back);
    }

    public override void _Process(double delta)
    {
        if (model == null) return;
        bool changedUnit = Unit != previewUnit;
        if (changedUnit || Faction != previewTeam)
        {
            previewUnit = Unit;
            previewTeam = Faction;
            model.Mesh = Unit == null ? null : FacetedSoldierMeshes.CreatePlacementPreview(Unit, ArmyRenderer.FactionColor((byte)Faction));
            model.Position = Vector3.Up * ((Unit?.Height ?? 0f) * (Unit?.SizeMultiplier ?? 1f) * 0.5f);
        }
        if (changedUnit || Count != previewCount)
        {
            previewCount = Count;
            label.Text = Unit == null ? "Choose unit" : $"{Count} × {Unit.DisplayName}";
        }
        if (CommandTerrain.Find(this) is CommandTerrain terrain)
        {
            Vector3 position = GlobalPosition;
            position.Y = terrain.SampleHeight(position.X, position.Z);
            if (!GlobalPosition.IsEqualApprox(position)) GlobalPosition = position;
        }
    }
}
