// Standalone flat-ground scene for testing Fortress Defense squad commands.
using System.Collections.Generic;
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
    [Export(PropertyHint.Range, "0,50000,1")] public float CitadelHealth { get; set; }
    [Export(PropertyHint.Range, "30,1800,1")] public float OpeningCameraSize { get; set; } = 42f;
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
        var cameraStart = GetNodeOrNull<Marker3D>("CameraStart");
        camera.SetOpeningView(cameraStart?.GlobalPosition ?? Vector3.Zero, OpeningCameraSize,
            cameraStart == null ? 45f : Mathf.RadToDeg(cameraStart.GlobalRotation.Y));
        float halfWidth = DeploymentLayout.WorldHalfWidth;
        var groundMaterial = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://BENCHMARK/RTS/CommandGround.gdshader")
        };
        groundMaterial.SetShaderParameter("map_half_width", halfWidth);
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = Vector2.One * (halfWidth * 3f) },
            MaterialOverride = groundMaterial
        });

        var settings = GD.Load<SimulationSettings>(SimulationSettings.DefaultPath) ?? new SimulationSettings();
        var catalog = GD.Load<UnitCatalog>("res://GAMEPLAY/UNITS/CATALOG/DefaultUnitCatalog.tres").Compile(settings.EffectiveMovementSpeedMultiplier);
        units = new UnitState(UnitCapacity, catalog);
        movement = new CommandMovement(units);
        float spacing = 2f * catalog[RuntimeUnitCatalog.Circle].Footprint + 0.3f;
        var blueSpawn = GetNodeOrNull<Marker3D>("BlueSpawn");
        Vector3 blueStart = blueSpawn?.GlobalPosition ?? Vector3.Back * -1.5f * spacing;
        blueStart.Y = 0f;
        Vector3 blueRight = blueSpawn == null ? Vector3.Right : Vector3.Right.Rotated(Vector3.Up, blueSpawn.GlobalRotation.Y);
        for (int i = 0; i < CommandMovement.SquadSize; i++)
        {
            Vector3 position = CommandMovement.Slot(blueStart, blueRight,
                CommandMovement.DefaultColumns, i, CommandMovement.SquadSize, spacing);
            units.TrySpawn(RuntimeUnitCatalog.Circle, Factions.Blue, position);
        }
        buildings = new CommandBuildings(this, units, movement, CitadelHealth, ReadAuthoredWalls(), GetNodeOrNull<CitadelView>("Citadel"));

        if (GetNodeOrNull<Marker3D>("HostileFabricator") is Marker3D hostile)
            buildings.AddHostileFabricator(hostile.GlobalPosition);

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
            Text = "Fortress Defense Command Sandbox\nCTRL Build Menu · Click or box-select · Right-click: move · Right-drag: straight formation\nRight Shift + Right-drag: freeform formation\nK + left-click: Attack-move\nWASD or arrows to pan · Wheel to zoom"
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

    // Register editor-placed box meshes as XZ rectangles in the shared obstacle field.
    private ObstacleRectangle[] ReadAuthoredWalls()
    {
        var walls = new List<ObstacleRectangle>();
        if (GetNodeOrNull<Node3D>("Obstacles") is not Node3D authored) return walls.ToArray();

        const float grid = DeploymentLayout.CellSize;
        foreach (Node child in authored.GetChildren())
        {
            if (child is not MeshInstance3D mesh || mesh.Mesh is not BoxMesh box) continue;
            var basis = mesh.GlobalTransform.Basis;
            if (!Mathf.IsZeroApprox(basis.X.Y) || !Mathf.IsZeroApprox(basis.X.Z) ||
                !Mathf.IsZeroApprox(basis.Y.X) || !Mathf.IsZeroApprox(basis.Y.Z) ||
                !Mathf.IsZeroApprox(basis.Z.X) || !Mathf.IsZeroApprox(basis.Z.Y))
            {
                GD.PushWarning($"Command wall '{mesh.Name}' must be axis-aligned.");
                continue;
            }

            Vector3 center = mesh.GlobalPosition;
            Vector2 halfSize = new Vector2(Mathf.Abs(basis.X.X) * box.Size.X, Mathf.Abs(basis.Z.Z) * box.Size.Z) * 0.5f;
            Vector2 min = (new Vector2(center.X, center.Z) - halfSize) / grid;
            Vector2 max = (new Vector2(center.X, center.Z) + halfSize) / grid;
            Vector2I start = new(Mathf.RoundToInt(min.X), Mathf.RoundToInt(min.Y));
            Vector2I end = new(Mathf.RoundToInt(max.X), Mathf.RoundToInt(max.Y));
            var wall = new ObstacleRectangle(start.X, start.Y, end.X - start.X, end.Y - start.Y);
            if (!wall.IsValid || !min.IsEqualApprox(new Vector2(start.X, start.Y)) ||
                !max.IsEqualApprox(new Vector2(end.X, end.Y)))
            {
                GD.PushWarning($"Command wall '{mesh.Name}' must fit on the {grid}-unit grid within the map.");
                continue;
            }
            walls.Add(wall);
        }
        return walls.ToArray();
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
        bool changed = waves.Step((float)delta);
        buildings.RemoveDestroyed();
        if (changed || previousCount != units.Count)
        {
            long start = Stopwatch.GetTimestamp();
            army.Sync();
            performance.RecordSync(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
        projectileRenderer.Sync(waves.Projectiles);
    }
}
