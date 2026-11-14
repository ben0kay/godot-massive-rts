// Standalone flat-ground scene for testing Fortress Defense squad commands.
using System;
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
    private readonly int[] squadIds = new int[CommandMovement.SquadSize];
    private const string SummaryTitle = "Fortress Defense · Command Sandbox";
    private double hudClock;
    private bool wasSelected;

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
        var catalog = GD.Load<UnitCatalog>("res://UNITS/CATALOG/DefaultUnitCatalog.tres").Compile(settings.EffectiveMovementSpeedMultiplier);
        units = new UnitState(CommandMovement.SquadSize, catalog) { Count = CommandMovement.SquadSize };
        movement = new CommandMovement(units);
        for (int i = 0; i < units.Count; i++)
        {
            squadIds[i] = i;
            units.Type[i] = RuntimeUnitCatalog.Circle;
            units.Faction[i] = Factions.Blue;
            units.Health[i] = catalog[units.Type[i]].Health;
            units.Target[i] = -1;
            units.Position[i] = movement.Slot(Vector3.Back * -1.5f * movement.Spacing,
                Vector3.Right, CommandMovement.DefaultColumns, i);
            units.Dirty[i] = true;
            units.RegisterSpawn(units.Faction[i], units.Type[i]);
        }

        army = new ArmyRenderer();
        AddChild(army);
        army.Initialize(units, FacetedSoldierMeshes.Create(catalog), null, false);

        var hud = new CanvasLayer();
        AddChild(hud);
        overlay = new CommandOverlay { View = camera, Units = units, Movement = movement };
        hud.AddChild(overlay);
        var input = new SquadCommandInput();
        input.Initialize(overlay);
        AddChild(input);
        hud.AddChild(new Label
        {
            Position = new Vector2(20f, 48f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Text = "Fortress Defense Command Sandbox\nClick or box-select the squad · Right-click to move · Right-drag to set formation\nWASD or arrows to pan · Wheel to zoom"
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
        RefreshSelection();
    }

    public override void _Process(double delta)
    {
        summary.Sample(delta);
        hudClock += delta;
        if (wasSelected != overlay.Selected) RefreshSelection();
        if (hudClock < 0.3) return;

        hudClock = 0;
        summary.Refresh(SummaryTitle, units);
        armyBalance.RefreshCounts();
        if (wasSelected) RefreshSelection();
    }

    private void RefreshSelection()
    {
        wasSelected = overlay.Selected;
        selection.Refresh(units, wasSelected ? squadIds.AsSpan() : Span<int>.Empty);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (movement != null && movement.Step((float)delta)) army.Sync();
    }
}
