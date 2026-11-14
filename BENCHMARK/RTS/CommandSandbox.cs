// Standalone flat-ground scene for testing Fortress Defense squad commands.
using System.Runtime.InteropServices;
using Godot;

namespace MassiveRts;

public partial class CommandSandbox : Node3D
{
    private CommandMovement movement;
    private ArmyRenderer army;
    private UnitState units;
    private CommandOverlay overlay;
    private BenchmarkSummary summary;
    private ArmyBalanceHud armyBalance;
    private SelectionHud selection;
    private const int UnitCapacity = 2048;
    [Export(PropertyHint.Range, "1,50000,1")] public float CitadelHealth { get; set; } = 5000f;
    private CommandBuildings buildings;
    private FabricationHud fabrication;
    private const string SummaryTitle = "Fortress Defense · Command Sandbox";
    private double hudClock;
    private double selectionClock;

    public override void _Ready()
    {
        var camera = new IsometricCamera();
        AddChild(camera);
        camera.MakeCurrent();
        camera.Frame(30f);
        camera.Size = 30f;
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(5000f, 5000f) },
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = new Color(0.25f, 0.26f, 0.29f)
            }
        });

        var settings = GD.Load<SimulationSettings>(SimulationSettings.DefaultPath) ?? new SimulationSettings();
        var catalog = GD.Load<UnitCatalog>("res://GAMEPLAY/UNITS/CATALOG/DefaultUnitCatalog.tres").Compile(settings.EffectiveMovementSpeedMultiplier);
        units = new UnitState(UnitCapacity, catalog);
        movement = new CommandMovement(units);
        float spacing = 2f * catalog[RuntimeUnitCatalog.Circle].Footprint + 0.3f;
        for (int i = 0; i < CommandMovement.SquadSize; i++)
        {
            Vector3 position = CommandMovement.Slot(Vector3.Back * -1.5f * spacing, Vector3.Right,
                CommandMovement.DefaultColumns, i, CommandMovement.SquadSize, spacing);
            units.TrySpawn(RuntimeUnitCatalog.Circle, Factions.Blue, position);
        }
        buildings = new CommandBuildings(this, units, movement, Mathf.Max(1, CitadelHealth));

        army = new ArmyRenderer();
        AddChild(army);
        army.Initialize(units, FacetedSoldierMeshes.Create(catalog), null, false);

        var hud = new CanvasLayer();
        AddChild(hud);
        overlay = new CommandOverlay { View = camera, Units = units };
        hud.AddChild(overlay);
        var input = new SquadCommandInput();

        hud.AddChild(new Label
        {
            Position = new Vector2(20f, 48f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Text = "Fortress Defense Command Sandbox\nCTRL Build Menu · Click or box-select · Right-click to move · Right-drag formation\nWASD or arrows to pan · Wheel to zoom"
        });
        summary = new BenchmarkSummary
        {
            AnchorLeft = 1, AnchorRight = 1,
            OffsetLeft = -296, OffsetRight = -12, OffsetTop = 12,
            GrowVertical = Control.GrowDirection.End
        };
        hud.AddChild(summary);
        summary.Refresh(SummaryTitle, units);

        armyBalance = GD.Load<PackedScene>("res://UI/GameplayHUD/ArmyBalanceHud.tscn").Instantiate<ArmyBalanceHud>();
        hud.AddChild(armyBalance);
        armyBalance.BeginBattle(units);

        selection = new SelectionHud
        {
            AnchorRight = 1, AnchorTop = 1, AnchorBottom = 1,
            OffsetLeft = 12, OffsetRight = -12, OffsetTop = -96, OffsetBottom = -12,
            GrowVertical = Control.GrowDirection.Begin
        };
        hud.AddChild(selection);
        fabrication = new FabricationHud();
        hud.AddChild(fabrication);
        fabrication.Initialize(catalog);
        fabrication.Refresh(buildings, null);
        AddChild(input);
        input.Initialize(overlay, movement, buildings, fabrication);
        RefreshSelection();
    }

    public override void _Process(double delta)
    {
        summary.Sample(delta);
        hudClock += delta;
        selectionClock += delta;
        if (selectionClock >= 0.08)
        {
            selectionClock = 0;
            RefreshSelection();
            fabrication.Refresh(buildings, overlay.Building);
        }
        if (hudClock < 0.3) return;

        hudClock = 0;
        summary.Refresh(SummaryTitle, units);
        armyBalance.RefreshCounts();
    }

    private void RefreshSelection()
    {
        selection.Visible = overlay.Building == null;
        if (selection.Visible) selection.Refresh(units, CollectionsMarshal.AsSpan(overlay.Selection));
    }

    public override void _PhysicsProcess(double delta)
    {
        int previousCount = units.Count;
        buildings.Step((float)delta);
        if (movement.Step((float)delta) || previousCount != units.Count) army.Sync();
    }
}
