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
    private DeathFeedbackSystem deathFeedback;
    private DeathEffectRenderer deathRenderer;
    private UnitState units;
    private CommandOverlay overlay;
    private BenchmarkSummary summary;
    private CommandPerformanceHud performance;
    private ArmyBalanceHud armyBalance;
    internal const int NonWaveUnitCapacity = 2048;
    internal const int MaxStartingTowers = 32;
    internal const int WaveUnitCapacity = 2048;
    [Export(PropertyHint.Range, "0,50000,1")] public float CitadelHealth { get; set; }
    [Export(PropertyHint.Range, "30,1800,1")] public float OpeningCameraSize { get; set; } = 42f;
    [Export] public PresetBattleRules PresetRules { get; set; }
    [Export] public int StartingCarbon { get; set; } = 10000;
    internal bool AuthoringPreview { get; set; }
    private CommandBuildings buildings;
    private CommandBottomHud bottomHud;
    private FortressWaves waves;
    private WaveHud waveHud;
    private ProjectileRenderer projectileRenderer;
    private ShockwaveRenderer impactRings;
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

    internal float MapSize => LevelSettings?.MapSize ?? LevelDefinition.DefaultMapSize;

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
    [Export(PropertyHint.Range, "1,5000,1,suffix:u")] public float EnemySpawnWidth { get; set; } = 256f;
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
        if (AuthoringPreview) return;
        float halfWidth = MapSize * 0.5f;
        UpdateGroundBounds();
        var camera = new IsometricCamera { MaximumZoomSize = halfWidth * 3f, MapOverviewEnabled = true };
        AddChild(camera);
        camera.MakeCurrent();
        FrameOpening(camera);
        var terrain = GetNodeOrNull<CommandTerrain>("Terrain")?.Field;
        if (PresetRules != null)
        {
            GetNodeOrNull<WorldPaint>("WorldPaint")?.Compile();
            LevelLayoutPreview.Build(this);
            return;
        }

        var settings = GD.Load<SimulationSettings>(SimulationSettings.DefaultPath) ?? new SimulationSettings();
        var catalog = GD.Load<UnitCatalog>("res://GAMEPLAY/UNITS/CATALOG/DefaultUnitCatalog.tres").Compile(settings.EffectiveMovementSpeedMultiplier);
        int capacity = NonWaveUnitCapacity + CountWaveSlots();
        var hitSettings = GD.Load<HitFeedbackSettings>("res://VISUALS/FEEDBACK/UNIT_HITS/HitFeedbackSettings.tres")
            ?? new HitFeedbackSettings();
        var unitVisuals = UnitVisualConfig.Load();
        deathFeedback = new DeathFeedbackSystem(unitVisuals);
        units = new UnitState(capacity, catalog, new HitFeedbackSystem(capacity, hitSettings), terrain,
            catalog.HasRangedRecoil ? new RangedRecoilSystem(capacity) : null)
        {
            DeathFeedback = deathFeedback
        };
        movement = new CommandMovement(units, halfWidth);
        var paint = GetNodeOrNull<WorldPaint>("WorldPaint");
        var (rocks, water) = paint?.Compile() ??
            (System.Array.Empty<ObstacleRectangle>(), System.Array.Empty<ObstacleRectangle>());
        units.Forest = paint?.Forest;
        buildings = new CommandBuildings(this, units, movement, CitadelHealth, ReadAuthoredWalls(groundForPlay: true), GetNodeOrNull<Node3D>("Citadel"),
            WorkerProductionSettings ?? GD.Load<CitadelWorkerSettings>("res://GAMEPLAY/PRODUCTION/DefaultCitadelWorkerSettings.tres"), rocks, water, paint?.MovementWater ?? water, paint?.BridgeAreas ?? System.Array.Empty<Rect2>(), StartingCarbon);
        if (GetNodeOrNull<Node3D>("StartingFabricators") is Node3D fabricators)
            foreach (Node child in fabricators.GetChildren())
                if (child is Node3D marker) buildings.AddStartingFabricator(marker.GlobalPosition, Factions.Blue);
        if (GetNodeOrNull<Node3D>("StartingDefenses") is Node3D defenses)
            foreach (Node child in defenses.GetChildren())
                if (child is CommandLaserPlacement tower)
                    buildings.AddStartingTower(tower);
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
        movement.PrepareBridgeRoutes();

        army = new ArmyRenderer();
        AddChild(army);
        var soldierMeshes = FacetedSoldierMeshes.Create(catalog, unitVisuals);
        army.Initialize(units, soldierMeshes, ShowGroundShadows ? GroundShadowMeshes.Soldier() : null, ShowGroundShadows, unitVisuals);
        deathRenderer = new DeathEffectRenderer();
        AddChild(deathRenderer);
        deathRenderer.Initialize(catalog, deathFeedback);

        impactRings = new ShockwaveRenderer();
        AddChild(impactRings);
        waves = new FortressWaves(units, buildings, movement, EnemySpawnEdge,
            WaveDefinitions, Mathf.Max(1f, BetweenWavesSeconds), EnemySpawnPattern, EnemySpawnWidth,
            EnemiesPerRow, EnemyRowDelay, EnemySpawnRate, impactRings);
        projectileRenderer = new ProjectileRenderer();
        AddChild(projectileRenderer);
        projectileRenderer.Initialize(2048, catalog.Projectiles, waves.Projectiles, LevelSettings?.SmokeWind ?? MuzzleSmoke.DefaultWind);

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
            int count = placement.SoldierCount;
            if (count > NonWaveUnitCapacity - units.Count)
            {
                GD.PushWarning($"Starting unit group '{placement.Name}' exceeds the starting and produced troop capacity.");
                continue;
            }
            foreach (var point in placement.Positions(units.Catalog[type].Footprint))
                if (buildings.IsSpawnClear(point, units.Catalog[type].Footprint)) units.TrySpawn(type, (byte)placement.Faction, point);
                else GD.PushWarning($"Starting unit at X/Z {point.X:0.##}, {point.Z:0.##} is blocked.");
        }
    }

    // =========================================================
    internal void UpdateGroundBounds(bool showEditorGrid = false)
    {
        if (GetNodeOrNull<MeshInstance3D>("Ground") is not MeshInstance3D ground ||
            ground.GetActiveMaterial(0) is not ShaderMaterial shared) return;
        var material = ground.MaterialOverride as ShaderMaterial ?? (ShaderMaterial)shared.Duplicate();
        material.ResourceLocalToScene = true;
        material.SetShaderParameter("map_half_width", MapSize * 0.5f);
        material.SetShaderParameter("editor_grid_size", showEditorGrid ? DeploymentLayout.CellSize : 0f);
        ground.MaterialOverride = material;
        GetNodeOrNull<CommandTerrain>("Terrain")?.UpdateGroundMaterial(material);
    }

    internal void FrameOpening(IsometricCamera camera)
    {
        var marker = GetNodeOrNull<Marker3D>("CameraStart");
        Vector3 focus = marker?.GlobalPosition ?? Vector3.Zero;
        focus.Y += GetNodeOrNull<CommandTerrain>("Terrain")?.SampleHeight(focus.X, focus.Z) ?? 0f;
        camera.SetOpeningView(focus, OpeningCameraSize, marker == null ? 45f : Mathf.RadToDeg(marker.GlobalRotation.Y));
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
    internal ObstacleRectangle[] ReadAuthoredWalls(bool groundForPlay = false)
    {
        var walls = new List<ObstacleRectangle>();
        if (GetNodeOrNull<Node3D>("Obstacles") is not Node3D authored) return walls.ToArray();

        var terrain = groundForPlay ? GetNodeOrNull<CommandTerrain>("Terrain")?.Field : null;
        foreach (Node child in authored.GetChildren())
        {
            if (child is not MeshInstance3D mesh || mesh.Mesh is not BoxMesh) continue;
            var basis = mesh.GlobalTransform.Basis;
            if (!Mathf.IsZeroApprox(basis.X.Y) || !Mathf.IsZeroApprox(basis.X.Z) ||
                !Mathf.IsZeroApprox(basis.Y.X) || !Mathf.IsZeroApprox(basis.Y.Z) ||
                !Mathf.IsZeroApprox(basis.Z.X) || !Mathf.IsZeroApprox(basis.Z.Y))
            {
                GD.PushWarning($"Command wall '{mesh.Name}' must be axis-aligned.");
                continue;
            }

            var bounds = mesh.GlobalTransform * mesh.GetAabb();
            var wall = new ObstacleRectangle(0, 0, 0, 0)
            {
                WorldBounds = new Rect2(ObstacleField.Xz(bounds.Position), ObstacleField.Xz(bounds.Size))
            };
            if (!wall.IsValid) continue;
            if (groundForPlay)
            {
                Vector3 groundPosition = mesh.GlobalPosition;
                float relief = 0f;
                groundPosition.Y += (terrain?.FootprintHeight(wall.Min, wall.Max, out relief) ?? 0f) - bounds.Position.Y;
                if (relief > 0.5f) GD.PushWarning($"Command wall '{mesh.Name}' crosses uneven terrain.");
                mesh.GlobalPosition = groundPosition;
            }
            walls.Add(wall);
        }
        return walls.ToArray();
    }

    public override void _Process(double delta)
    {
        if (units == null) return;
        units.Forest?.UpdateVisibility(units, (float)delta);
        deathFeedback.Advance((float)delta);
        deathRenderer.Sync(deathFeedback);
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
        if (units == null) return;
        projectileRenderer.AdvanceEffects((float)delta);
        impactRings.Advance((float)delta);
        bool feedbackChanged = units.HitFeedback.Advance(units, (float)delta);
        if (units.RangedRecoil?.Active == true)
        {
            units.RangedRecoil.Advance(units, (float)delta);
            feedbackChanged = true;
        }
        if (waves.Defeat || waves.Victory)
        {
            buildings.FinishDefenseEffects((float)delta);
            impactRings.Sync();
            if (feedbackChanged) SyncArmy();
            return;
        }
        deathFeedback.BeginTick();
        int previousCount = units.Count;
        buildings.Step((float)delta);
        bool changed = waves.Step((float)delta);
        if (waves.Defeat || waves.Victory) buildings.WorkerProductionOpen = false;
        buildings.RemoveDestroyed();
        if (feedbackChanged || changed || previousCount != units.Count) SyncArmy();
        projectileRenderer.Sync(waves.Projectiles);
        impactRings.Sync();
    }

    private void SyncArmy()
    {
        long start = Stopwatch.GetTimestamp();
        army.Sync();
        performance.RecordSync(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
    }
}
