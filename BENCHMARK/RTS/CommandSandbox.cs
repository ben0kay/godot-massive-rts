// Standalone flat-ground scene for testing Fortress Defense squad commands.
using System.Runtime.InteropServices;
using System.Diagnostics;
using Godot;

namespace MassiveRts;

public partial class CommandSandbox : Node3D
{
    private CommandMovement movement;
    private ArmyRenderer army;
    private UnitState units;
    private CommandOverlay overlay;
    private BenchmarkSummary summary;
    private CommandPerformanceHud performance;
    private ArmyBalanceHud armyBalance;
    private SelectionHud selection;
    private const int UnitCapacity = 2048;
    [Export(PropertyHint.Range, "1,50000,1")] public float CitadelHealth { get; set; } = 5000f;
    private CommandBuildings buildings;
    private FabricationHud fabrication;
    private FortressWaves waves;
    private WaveHud waveHud;
    private ProjectileRenderer projectileRenderer;
    [Export] public Godot.Collections.Array<WaveDefinition> WaveDefinitions { get; set; } = new();
    [Export(PropertyHint.Range, "1,120,1")] public float BetweenWavesSeconds { get; set; } = 12f;
    private const string SummaryTitle = "Fortress Defense · Command Sandbox";
    private double hudClock;
    private double selectionClock;

    public override void _Ready()
    {
        var camera = new IsometricCamera();
        AddChild(camera);
        camera.MakeCurrent();
        camera.Frame(42f);
        camera.Size = 42f;
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

        waves = new FortressWaves(units, buildings, movement, GetNode<Marker3D>("EnemySpawn").GlobalPosition,
            WaveDefinitions, Mathf.Max(1f, BetweenWavesSeconds));
        projectileRenderer = new ProjectileRenderer();
        AddChild(projectileRenderer);
        projectileRenderer.Initialize(2048, catalog.Projectiles);

        var hud = new CanvasLayer();
        AddChild(hud);
        overlay = new CommandOverlay { View = camera, Units = units };
        hud.AddChild(overlay);
        var input = new SquadCommandInput();

        hud.AddChild(new Label
        {
            Position = new Vector2(20f, 48f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Text = "Fortress Defense Command Sandbox\nCTRL Build Menu · Click or box-select · Right-click to move · Right-drag formation\nK + left-click: Attack-move\nWASD or arrows to pan · Wheel to zoom"
        });
        var rightHud = new VBoxContainer
        {
            AnchorLeft = 1, AnchorRight = 1,
            OffsetLeft = -296, OffsetRight = -12, OffsetTop = 12,
            GrowVertical = Control.GrowDirection.End,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        hud.AddChild(rightHud);
        summary = new BenchmarkSummary();
        rightHud.AddChild(summary);
        performance = new CommandPerformanceHud();
        rightHud.AddChild(performance);
        performance.Initialize(waves);
        performance.Refresh(waves, 0, 0);
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
        waveHud = new WaveHud
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetLeft = -145f, OffsetRight = 145f,
            OffsetTop = 40f, GrowVertical = Control.GrowDirection.End
        };
        hud.AddChild(waveHud);
        waveHud.Initialize(waves.Start);
        waveHud.Refresh(waves, buildings.Citadel);
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
        if (hudClock < 1.0 / 3.0) return;

        hudClock = 0;
        summary.Refresh(SummaryTitle, units, waves.Projectiles.Count);
        armyBalance.RefreshCounts();
        waveHud.Refresh(waves, buildings.Citadel);
        performance.Refresh(waves, movement.ActiveCount, overlay.Selection.Count);
        if (!waves.Defeat && !waves.Victory)
            foreach (var structure in buildings.Structures) structure.RefreshProductionBar();
    }

    private void RefreshSelection()
    {
        selection.Visible = overlay.Building == null;
        if (selection.Visible) selection.Refresh(units, CollectionsMarshal.AsSpan(overlay.Selection));
    }

    public override void _PhysicsProcess(double delta)
    {
        if (waves.Defeat || waves.Victory) return;
        int previousCount = units.Count;
        buildings.Step((float)delta);
        if (waves.Step((float)delta) || previousCount != units.Count)
        {
            long start = Stopwatch.GetTimestamp();
            army.Sync();
            performance.RecordSync(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
        projectileRenderer.Sync(waves.Projectiles);
    }
}
