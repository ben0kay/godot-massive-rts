// Saves one starting unit or squad as scene data, with an editor-only representative mesh.
using Godot;
using System.Collections.Generic;

namespace MassiveRts;

[Tool]
public partial class CommandUnitPlacement : Marker3D
{
    public enum Team { Blue, Red }
    [Export] public UnitDefinition Unit { get; set; }
    [Export] public Team Faction { get; set; }
    [Export(PropertyHint.Range, "1,256,1")] public int Count { get; set; } = 1;
    [Export] public Vector2[] PaintedOffsets { get; set; } = System.Array.Empty<Vector2>();
    internal int SoldierCount => PaintedOffsets.Length == 0 ? Mathf.Clamp(Count, 1, 256) : PaintedOffsets.Length;
    internal IEnumerable<Vector3> Positions(float footprint) => PositionsAt(GlobalPosition, GlobalRotation.Y, footprint);

    private IEnumerable<Vector3> PositionsAt(Vector3 center, float yaw, float footprint)
    {
        var basis = new Basis(Vector3.Up, yaw);
        if (PaintedOffsets.Length != 0)
        {
            foreach (var offset in PaintedOffsets) yield return center + basis * new Vector3(offset.X, 0, offset.Y);
            yield break;
        }
        int columns = Mathf.Min(SoldierCount, CommandMovement.DefaultColumns);
        for (int slot = 0; slot < SoldierCount; slot++)
            yield return CommandMovement.Slot(center, basis.X, columns, slot, SoldierCount, footprint * 2f + 0.3f);
    }

    internal bool FitsSide(PresetBattleRules rules, Vector3 center)
    {
        if (Unit == null) return false;
        float footprint = UnitOutline.FromDefinition(Unit, UnitOutline.EffectiveSize(Unit)).Footprint;
        foreach (var position in PositionsAt(center, IsInsideTree() ? GlobalRotation.Y : Rotation.Y, footprint))
            if (!rules.AllowsSide(position, footprint, Faction == Team.Blue)) return false;
        return true;
    }

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
        if (changedUnit || SoldierCount != previewCount)
        {
            previewCount = SoldierCount;
            label.Text = Unit == null ? "Choose unit" : $"{SoldierCount} × {Unit.DisplayName}";
        }
        if (CommandTerrain.Find(this) is CommandTerrain terrain)
        {
            Vector3 position = GlobalPosition;
            position.Y = terrain.SampleHeight(position.X, position.Z);
            if (!GlobalPosition.IsEqualApprox(position)) GlobalPosition = position;
        }
    }
}
