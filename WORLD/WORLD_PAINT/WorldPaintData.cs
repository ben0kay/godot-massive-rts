// Stores one level's painted terrain cells and their appearance.
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

    private Cells rockCells = new(), waterCells = new(), forestCells = new(), marshCells = new();

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

    [Export]
    public Cells ForestCells
    {
        get => forestCells;
        set { forestCells = value ?? new Cells(); EmitChanged(); }
    }

    [Export]
    public Cells MarshCells
    {
        get => marshCells;
        set { marshCells = value ?? new Cells(); EmitChanged(); }
    }

    [Export] public Cells BlueWallCells { get; set; } = new();
    [Export] public Cells RedWallCells { get; set; } = new();
    [Export(PropertyHint.Range, "0.5,80,0.5")] public float FactionWallHeight { get; set; } = 2.5f;

    private Cells industrialWallCells = new();
    private float industrialWallHeight = 2f;

    [Export] public Cells IndustrialWallCells
    {
        get => industrialWallCells;
        set => industrialWallCells = value ?? new();
    }

    [Export(PropertyHint.Range, "0.5,80,0.5")] public float IndustrialWallHeight
    {
        get => industrialWallHeight;
        set => industrialWallHeight = Mathf.Clamp(value, 0.5f, 80f);
    }

    private Godot.Collections.Array<Rect2I> bridges = new();

    [Export]
    public Godot.Collections.Array<Rect2I> Bridges
    {
        get => bridges;
        set { bridges = value ?? new(); EmitChanged(); }
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
