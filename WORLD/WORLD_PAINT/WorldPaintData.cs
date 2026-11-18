// Stores one level's occupied rock and water cells and their appearance.
using Godot;
using Cells = Godot.Collections.Array<Godot.Vector2I>;

namespace MassiveRts;

[Tool, GlobalClass]
public partial class WorldPaintData : Resource
{
    private float rockHeight = 3f;
    private Color rockColor = new("#535861"), waterColor = new("#237d9b");

    private Color deepWaterColor = new("#104953"), shorelineColor = new("#506b63");
    private float shorelineWidth = 0.9f, bankHeight = 0.16f, rippleStrength = 0.045f, rippleSpeed = 0.45f;

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

    [ExportGroup("Water appearance")]
    [Export]
    public Color WaterColor
    {
        get => waterColor;
        set { waterColor = value; EmitChanged(); }
    }

    [Export]
    public Color DeepWaterColor
    {
        get => deepWaterColor;
        set { deepWaterColor = value; EmitChanged(); }
    }

    [Export]
    public Color ShorelineColor
    {
        get => shorelineColor;
        set { shorelineColor = value; EmitChanged(); }
    }

    [Export(PropertyHint.Range, "0.25,1.5,0.05,suffix:u")]
    public float ShorelineWidth
    {
        get => shorelineWidth;
        set { shorelineWidth = Mathf.Clamp(value, 0.25f, 1.5f); EmitChanged(); }
    }

    [Export(PropertyHint.Range, "0,0.45,0.01,suffix:u")]
    public float BankHeight
    {
        get => bankHeight;
        set { bankHeight = Mathf.Clamp(value, 0f, 0.45f); EmitChanged(); }
    }

    [Export(PropertyHint.Range, "0,0.15,0.005")]
    public float RippleStrength
    {
        get => rippleStrength;
        set { rippleStrength = Mathf.Clamp(value, 0f, 0.15f); EmitChanged(); }
    }

    [Export(PropertyHint.Range, "0,2,0.05")]
    public float RippleSpeed
    {
        get => rippleSpeed;
        set { rippleSpeed = Mathf.Clamp(value, 0f, 2f); EmitChanged(); }
    }
}
