// Standalone flat-ground scene for testing Fortress Defense squad commands.
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Godot;

namespace MassiveRts;

[Tool]
public partial class CommandSandbox : Node3D
{
    private CommandMovement movement;
    private ArmyRenderer army;
    private UnitState units;
    private CommandOverlay overlay;
    private BenchmarkSummary summary;
    private CommandPerformanceHud performance;
    private ArmyBalanceHud armyBalance;
    internal const int NonWaveUnitCapacity = 2048;
    internal const int WaveUnitCapacity = 2048;
    [Export(PropertyHint.Range, "0,50000,1")] public float CitadelHealth { get; set; }
    [Export(PropertyHint.Range, "30,1800,1")] public float OpeningCameraSize { get; set; } = 42f;
    private CommandBuildings buildings;
    private CommandBottomHud bottomHud;
    private FortressWaves waves;
    private WaveHud waveHud;
    private ProjectileRenderer projectileRenderer;
    private LevelDefinition levelSettings;
    private float lastMapSize;
    private bool reducedMapSize;

    [Export]
    public LevelDefinition LevelSettings
    {
        get => levelSettings;
        set
        {
            if (levelSettings == value) return;
            bool watch = Engine.IsEditorHint() && IsInsideTree();
            if (watch && levelSettings != null) levelSettings.Changed -= OnLevelSettingsChanged;
            levelSettings = value;
            if (!watch) return;
            if (levelSettings != null) levelSettings.Changed += OnLevelSettingsChanged;
            OnLevelSettingsChanged();
        }
    }

    private float MapSize => LevelSettings?.MapSize ?? LevelDefinition.DefaultMapSize;

    private void OnLevelSettingsChanged()
    {
        float size = MapSize;
        if (size == lastMapSize) return;
        reducedMapSize = size < lastMapSize;
        lastMapSize = size;
        UpdateConfigurationWarnings();
    }

    public override void _EnterTree()
    {
        // Campaign levels keep a matching resource beside their scene. Recover it
        // when an inherited scene loses its exported resource assignment.
        if (levelSettings == null && !string.IsNullOrEmpty(SceneFilePath))
        {
            string settingsPath = SceneFilePath.GetBaseName() + ".tres";
            if (ResourceLoader.Exists(settingsPath) &&
                GD.Load<Resource>(settingsPath) is LevelDefinition settings &&
                settings.ScenePath == SceneFilePath)
                levelSettings = settings;
        }

        if (!Engine.IsEditorHint()) return;
        lastMapSize = MapSize;
        if (levelSettings != null) levelSettings.Changed += OnLevelSettingsChanged;
    }

    public override void _ExitTree()
    {
        if (Engine.IsEditorHint() && levelSettings != null)
            levelSettings.Changed -= OnLevelSettingsChanged;
    }

    [Export] public CitadelWorkerSettings WorkerProductionSettings { get; set; }
    [Export] public bool ShowGroundShadows { get; set; } = true;
    [Export] public bool SpawnDefaultBlueSquad { get; set; } = true;
    [Export] public WaveSpawnEdge EnemySpawnEdge { get; set; } = WaveSpawnEdge.South;
    [Export] public WaveSpawnPattern EnemySpawnPattern { get; set; }
    [Export(PropertyHint.Range, "1,24,0.5")] public float EnemySpawnSpacing { get; set; } = 3f;
    // Stream/Scatter only: higher units-per-second means shorter gaps between enemy spawns.
    [Export(PropertyHint.Range, "1,60,0.25")] public float EnemySpawnRate { get; set; } = 6.25f;
    [Export(PropertyHint.Range, "2,24,1")] public int EnemiesPerRow { get; set; } = 7;
    [Export(PropertyHint.Range, "0.25,10,0.25")] public float EnemyRowDelay { get; set; } = 1.5f;
    [Export] public Godot.Collections.Array<WaveDefinition> WaveDefinitions { get; set; } = new();
    [Export(PropertyHint.Range, "1,120,1")] public float BetweenWavesSeconds { get; set; } = 12f;
    private const string SummaryTitle = "Fortress Defense · Command Sandbox";
    private double hudClock;
    private double selectionClock;

    public override string[] _GetConfigurationWarnings()
    {
        if (!IsInsideTree()) return System.Array.Empty<string>();
        var warnings = new List<string>();
        if (reducedMapSize)
            warnings.Add("The map was reduced. Content beyond the new bounds may be skipped or repositioned when you play. Scene nodes are kept.");

        float half = MapSize * 0.5f;
        bool Outside(Vector3 point, float padding = 0f) =>
            Mathf.Abs(point.X) + padding > half || Mathf.Abs(point.Z) + padding > half;
        var outside = new List<string>();
        foreach (string name in new[] { "CameraStart", "BlueSpawn", "Citadel", "HostileFabricator" })
        {
            if (name == "BlueSpawn" && !SpawnDefaultBlueSquad) continue;
            float padding = name == "Citadel" ? 5f : name == "HostileFabricator" ? 3f : 0f;
            if (GetNodeOrNull<Node3D>(name) is Node3D item && Outside(item.GlobalPosition, padding))
                outside.Add(name);
        }
        if (GetNodeOrNull<Node3D>("Obstacles") is Node3D obstacles)
            foreach (Node child in obstacles.GetChildren())
                if (child is MeshInstance3D mesh && mesh.Mesh is BoxMesh)
                {
                    Aabb bounds = mesh.GlobalTransform * mesh.GetAabb();
                    if (Outside(bounds.Position) || Outside(bounds.End)) outside.Add(mesh.Name.ToString());
                }
        if (GetNodeOrNull<Node3D>("StartingUnits") is Node3D startingUnits)
            foreach (Node child in startingUnits.GetChildren())
                if (child is CommandUnitPlacement placement && Outside(placement.GlobalPosition))
                    outside.Add(placement.Name.ToString());
        if (GetNodeOrNull<Node3D>("Terrain") is Node3D terrain)
            foreach (Node child in terrain.GetChildren())
                if (child is TerrainHill hill && Outside(hill.GlobalPosition, hill.Radius))
                    outside.Add(hill.Name.ToString());

        if (outside.Count > 0)
            warnings.Add($"Review these objects outside the {MapSize:0} × {MapSize:0} map bounds: {string.Join(", ", outside)}.");
        return warnings.ToArray();
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            UpdateConfigurationWarnings();
            return;
        }
        float halfWidth = MapSize * 0.5f;
        if (GetNodeOrNull<MeshInstance3D>("Ground") is MeshInstance3D ground &&
            ground.GetActiveMaterial(0) is ShaderMaterial shared)
        {
            var material = (ShaderMaterial)shared.Duplicate();
            material.SetShaderParameter("map_half_width", halfWidth);
            ground.MaterialOverride = material;
        }
        var camera = new IsometricCamera { MaximumZoomSize = halfWidth * 3f, MapOverviewEnabled = true };
        AddChild(camera);
        camera.MakeCurrent();
        var terrain = GetNodeOrNull<CommandTerrain>("Terrain")?.Field;
        var cameraStart = GetNodeOrNull<Marker3D>("CameraStart");
        Vector3 cameraFocus = cameraStart?.GlobalPosition ?? Vector3.Zero;
        cameraFocus.Y += terrain?.At(cameraFocus.X, cameraFocus.Z) ?? 0f;
        camera.SetOpeningView(cameraFocus, OpeningCameraSize,
            cameraStart == null ? 45f : Mathf.RadToDeg(cameraStart.GlobalRotation.Y));

        var settings = GD.Load<SimulationSettings>(SimulationSettings.DefaultPath) ?? new SimulationSettings();
        var catalog = GD.Load<UnitCatalog>("res://GAMEPLAY/UNITS/CATALOG/DefaultUnitCatalog.tres").Compile(settings.EffectiveMovementSpeedMultiplier);
        units = new UnitState(NonWaveUnitCapacity + CountWaveSlots(), catalog, terrain: terrain);
        movement = new CommandMovement(units, halfWidth);
        buildings = new CommandBuildings(this, units, movement, CitadelHealth, ReadAuthoredWalls(), GetNodeOrNull<Node3D>("Citadel"),
            WorkerProductionSettings ?? GD.Load<CitadelWorkerSettings>("res://GAMEPLAY/PRODUCTION/DefaultCitadelWorkerSettings.tres"));
        if (SpawnDefaultBlueSquad)
        {
            var blueSpawn = GetNodeOrNull<Marker3D>("BlueSpawn");
            float spacing = 2f * catalog[RuntimeUnitCatalog.Circle].Footprint + 0.3f;
            Vector3 start = blueSpawn?.GlobalPosition ?? Vector3.Back * -1.5f * spacing;
            start.Y = 0f;
            SpawnFormation(RuntimeUnitCatalog.Circle, Factions.Blue, start, blueSpawn?.GlobalRotation.Y ?? 0f,
                CommandMovement.SquadSize);
        }

        if (GetNodeOrNull<Marker3D>("HostileFabricator") is Marker3D hostile)
            buildings.AddHostileFabricator(hostile.GlobalPosition);
        SpawnAuthoredUnits();

        army = new ArmyRenderer();
        AddChild(army);
        army.Initialize(units, FacetedSoldierMeshes.Create(catalog), ShowGroundShadows ? GroundShadowMeshes.Soldier() : null, ShowGroundShadows);

        waves = new FortressWaves(units, buildings, movement, EnemySpawnEdge,
            WaveDefinitions, Mathf.Max(1f, BetweenWavesSeconds), EnemySpawnPattern, EnemySpawnSpacing,
            EnemiesPerRow, EnemyRowDelay, EnemySpawnRate);
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
            Text = "Fortress Defense Command Sandbox\nCTRL Build Menu · Click or box-select · Right-click: move · Right-drag: straight formation\nRight Shift + Right-drag: freeform formation\nK + left-click: Attack-move · Hold K + left-drag: attack formation\nWASD or arrows to pan · Wheel to zoom · Home: map overview"
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

        bottomHud = new CommandBottomHud();
        hud.AddChild(bottomHud);
        bottomHud.Initialize(catalog, buildings);
        waveHud = new WaveHud
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetLeft = -145f, OffsetRight = 145f,
            OffsetTop = 40f, GrowVertical = Control.GrowDirection.End
        };
        hud.AddChild(waveHud);
        waveHud.Initialize(waves.Start);
        waveHud.Refresh(waves, buildings.Citadel);
        AddChild(input);
        input.Initialize(overlay, movement, buildings, bottomHud);
        RefreshSelection();
    }

    private int CountWaveSlots()
    {
        long count = 0;
        foreach (var wave in WaveDefinitions)
        {
            if (wave == null) throw new System.InvalidOperationException("Each wave needs a definition.");
            foreach (var entry in wave.Enemies)
            {
                if (entry.Value <= 0) throw new System.InvalidOperationException($"Invalid wave count for {entry.Key}.");
                count += entry.Value;
            }
        }
        if (count > WaveUnitCapacity)
            throw new System.InvalidOperationException($"Authored waves can contain up to {WaveUnitCapacity:N0} soldiers in total.");
        return (int)count;
    }

    private void SpawnAuthoredUnits()
    {
        if (GetNodeOrNull<Node3D>("StartingUnits") is not Node3D container) return;
        foreach (Node child in container.GetChildren())
        {
            if (child is not CommandUnitPlacement placement) continue;
            if (placement.Unit == null || !units.Catalog.TryResolve(placement.Unit.Id, out byte type))
            {
                GD.PushWarning($"Starting unit '{placement.Name}' needs a registered unit definition.");
                continue;
            }
            int count = Mathf.Clamp(placement.Count, 1, 256);
            if (count > NonWaveUnitCapacity - units.Count)
            {
                GD.PushWarning($"Starting unit group '{placement.Name}' exceeds the starting and produced troop capacity.");
                continue;
            }
            SpawnFormation(type, (byte)placement.Faction, placement.GlobalPosition, placement.GlobalRotation.Y, count);
        }
    }

    private void SpawnFormation(byte type, byte faction, Vector3 start, float yaw, int count)
    {
        float spacing = 2f * units.Catalog[type].Footprint + 0.3f;
        Vector3 right = Vector3.Right.Rotated(Vector3.Up, yaw);
        int columns = Mathf.Min(count, CommandMovement.DefaultColumns);
        for (int slot = 0; slot < count; slot++)
        {
            Vector3 point = CommandMovement.Slot(start, right, columns, slot, count, spacing);
            if (buildings.IsSpawnClear(point, units.Catalog[type].Footprint)) units.TrySpawn(type, faction, point);
            else GD.PushWarning($"Starting unit at X/Z {point.X:0.##}, {point.Z:0.##} is blocked.");
        }
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
            if (!wall.IsValid || !movement.OnMap(new Vector3(wall.Min.X, 0f, wall.Min.Y)) ||
                !movement.OnMap(new Vector3(wall.Max.X, 0f, wall.Max.Y)) || !min.IsEqualApprox(new Vector2(start.X, start.Y)) ||
                !max.IsEqualApprox(new Vector2(end.X, end.Y)))
            {
                GD.PushWarning($"Command wall '{mesh.Name}' must fit on the {grid}-unit grid within the map.");
                continue;
            }
            Vector3 groundPosition = mesh.GlobalPosition;
            var terrain = units.Grounding?.Field;
            var bounds = mesh.GlobalTransform * mesh.GetAabb();
            float relief = 0f;
            groundPosition.Y += (terrain?.FootprintHeight(wall.Min, wall.Max, out relief) ?? 0f) - bounds.Position.Y;
            if (relief > 0.5f) GD.PushWarning($"Command wall '{mesh.Name}' crosses uneven terrain.");
            mesh.GlobalPosition = groundPosition;
            walls.Add(wall);
        }
        return walls.ToArray();
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint()) return;
        buildings.AdvanceDefenseEffects((float)delta);
        summary.Sample(delta);
        hudClock += delta;
        selectionClock += delta;
        if (selectionClock >= 0.08)
        {
            selectionClock = 0;
            RefreshSelection();
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
        bottomHud.Refresh(buildings, units, CollectionsMarshal.AsSpan(overlay.Selection), overlay.Building, overlay.Deposit);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint() || waves.Defeat || waves.Victory) return;
        int previousCount = units.Count;
        buildings.Step((float)delta);
        bool changed = waves.Step((float)delta);
        if (waves.Defeat || waves.Victory) buildings.WorkerProductionOpen = false;
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
