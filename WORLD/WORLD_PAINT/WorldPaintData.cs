// Stores one level's occupied rock and water cells and their appearance.
using Godot;
using Cells = Godot.Collections.Array<Godot.Vector2I>;

namespace MassiveRts;

[Tool, GlobalClass]
public partial class WorldPaintData : Resource
{
    private float rockHeight = 3f;
    private Color rockColor = new("#535861"), waterColor = new("#237d9b");

    private Cells rockCells = new(), waterCells = new();

    [Export]
    public Cells RockCells
    {
        get => rockCells;
        set { rockCells = value ?? new Cells(); EmitChanged(); }
    }

    [Export]
    public Cells WaterCells
    {
        get => waterCells;
        set { waterCells = value ?? new Cells(); EmitChanged(); }
    }

    [Export(PropertyHint.Range, "0.5,12,0.1")]
    public float RockHeight
    {
        get => rockHeight;
        set { rockHeight = Mathf.Clamp(value, 0.5f, 12f); EmitChanged(); }
    }

    [Export]
    public Color RockColor
    {
        get => rockColor;
        set { rockColor = value; EmitChanged(); }
    }

    [Export]
    public Color WaterColor
    {
        get => waterColor;
        set { waterColor = value; EmitChanged(); }
    }
}
