// Owns BENCH0 lifecycle and fixed-step orchestration; shared systems remain independent.
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Godot;
namespace MassiveRts;

public partial class BenchmarkArena : Node3D
{
    [Export] public BenchmarkConfig Config { get; set; } = new();
    [Export] public UnitCatalog SoldierCatalog { get; set; }
    internal const string UnitCatalogPath = "res://GAMEPLAY/UNITS/CATALOG/DefaultUnitCatalog.tres";
    private const string BuildingCatalogPath = "res://WORLD/BUILDINGS/CATALOG/DefaultBuildingCatalog.tres";
    private RuntimeUnitCatalog editorCatalog;
    private DeploymentLayout editorLayout;
    private DeploymentPainter.BattleSettings editorSettings;
    private bool openInDeployment;

    internal (DeploymentLayout Layout, DeploymentPainter.BattleSettings Settings) CaptureTestSetup() =>
        (painter.Layout, ReadBattleSettings());

    // Validate against the new footprints before replacing an existing test scene.
    internal void ConfigureEditorTest(UnitCatalog drafts, DeploymentLayout previous, DeploymentPainter.BattleSettings settings)
    {
        var simulation = GD.Load<SimulationSettings>(SimulationSettings.DefaultPath) ?? new SimulationSettings();
        editorCatalog = drafts.Compile(simulation.EffectiveMovementSpeedMultiplier);
        if (previous != null)
        {
            buildings = new RuntimeBuildingCatalog(GD.Load<BuildingCatalog>(BuildingCatalogPath),
                editorCatalog.DamageRules, editorCatalog.Projectiles);
            editorLayout = new DeploymentLayout(editorCatalog, buildings, previous.GridSize);
            editorLayout.SetLoadedObstacles(previous.Obstacles);
            foreach (var defense in previous.Defenses)
                if (!editorLayout.TryPlaceDefense(defense))
                    throw new InvalidOperationException("A test defense no longer fits the layout.");
            foreach (var soldier in previous.Placements)
                if (!editorLayout.TryPaint(soldier, out _))
                    throw new InvalidOperationException("The draft footprints overlap in this setup. Move the affected units before applying the draft.");
        }
        editorSettings = settings;
        openInDeployment = true;
    }

    private RuntimeUnitCatalog catalog;
    private float projectileLifetime;
    private Mesh[] soldierMeshes;
    private Mesh soldierShadow;
    private UnitState units;
    private SpatialGrid grid;
    private SeparationSystem separation;
    private HardSeparation hardSeparation;
    private MeleeSteering meleeSteering;
    private TrampleSystem trample;
    private DreadnaughtCombat dreadnaughts;
    private byte[] voluntaryMovement;
    private AlliedYieldSettings alliedYieldSettings;
    private ProjectileSystem projectiles;
    private ArmyRenderer armyRenderer;
    private ProjectileRenderer projectileRenderer;
    private ShockwaveRenderer shockwaves;
    private DeathFeedbackSystem deathFeedback;
    private UnitVisualConfig unitVisuals;
    private UnitVisualConfig battleUnitVisuals;
    private DeathEffectRenderer deathRenderer;
    private IsometricCamera camera;
    private BattlefieldWeather weather;
    private AttackRangeOverlay attackRanges, shieldRanges;
    private BenchmarkPanel panel;
    private ArmyBalanceHud armyBalanceHud;
    private DeploymentStrengthBar deploymentStrengthBar;
    private DeploymentFactory deployment;
    private int autoAttackCount;
    private GuardGroup[] guards;
    private int[] autoAttackIds;
    private DeploymentPainter painter;
    private RuntimeBuildingCatalog buildings;
    private DefenseSystem defenseSystem;
    private StructureCombat defenseCombat;
    private DefenseRenderer defenseRenderer;
    private ObstacleField obstacles;
    private ObstacleNavigation navigation;
    private MeshInstance3D battleWalls, battleObstacleShadows, flatGround;
    private Node3D terrainMeshes;
    private TerrainHeightField terrain;
    private int groundSamples;
    private double terrainCompileMs, terrainMeshMs, groundingMs;
    private double routeBuildMs;
    private BenchmarkMode mode;
    private uint battleSeed;
    private const int InitialTargetsPerFrame = 512;
    private int requested, targetCount, tick, preparedTargets;
    private bool spawning, paused;
    private double accumulator, hudClock, targetingAccumulator;
    private float targetingStepSeconds, separationPushPerStep;
    private int targetingStep;
    private long skippedSteps;
    private MovementProfile movementProfile;
    private double movementMs, separationMs, gridMs, targetingMs, combatMs, projectileMs, simulationMs, renderMs;
    private int measuredFrames, measuredTicks, maxTicksInFrame;
    private int measuredSyncs, denseUploads, sparseUploads, changedWrites;
    private double worstTickMs, armyUploadMs;
    private double timeSinceArmySync;
    private bool smoothingWasEnabled;
    private static long Stamp() => Stopwatch.GetTimestamp();
    private static double Elapsed(long start) => (Stamp() - start) * 1000.0 / Stopwatch.Frequency;
    // =========================================================
    public override void _Ready()
    {
        var simulationSettings = GD.Load<SimulationSettings>(SimulationSettings.DefaultPath) ?? new SimulationSettings();
        float movementScale = simulationSettings.EffectiveMovementSpeedMultiplier;
        catalog = editorCatalog ?? (SoldierCatalog ?? GD.Load<UnitCatalog>(UnitCatalogPath)).Compile(movementScale);
        // Keep the distance cap implied by the authored projectile lifetime.
        projectileLifetime = Config.ProjectileLifetime / movementScale;
        unitVisuals = UnitVisualConfig.Load();
        battleUnitVisuals = (UnitVisualConfig)unitVisuals.Duplicate();
        soldierMeshes = FacetedSoldierMeshes.Create(catalog, battleUnitVisuals);
        soldierShadow = GroundShadowMeshes.Soldier();
        RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
        Config.MaxSoldiers = Mathf.Clamp(Config.MaxSoldiers, 1000, 100000);
        Config.SpawnPerFrame = Mathf.Clamp(Config.SpawnPerFrame, 100, 10000);
        Config.TickSeconds = float.IsFinite(Config.TickSeconds)
            ? Mathf.Clamp(Config.TickSeconds, 1f / 120f, 0.1f) : 1f / 60f;
        targetingStepSeconds = Mathf.Max(Config.TickSeconds, BenchmarkConfig.TargetingStepSeconds);
        float correctionScale = Config.TickSeconds / BenchmarkConfig.TargetingStepSeconds;
        Config.MaxStepsPerFrame = Mathf.Clamp(Config.MaxStepsPerFrame, 1, 5);
        Config.TargetRefreshTicks = Mathf.Clamp(Config.TargetRefreshTicks, 1, 60);
        Config.QueryCandidateLimit = Mathf.Clamp(Config.QueryCandidateLimit, 8, 1024);
        Config.MeleeAccelerationSeconds = ClampFiniteSetting(Config.MeleeAccelerationSeconds, 0.6f, 0.15f);
        Config.RangedAccelerationSeconds = ClampFiniteSetting(Config.RangedAccelerationSeconds, 0.6f, 0.32f);
        Config.CellSize = Mathf.Clamp(Config.CellSize, 2, 20);
        Config.SeparationNeighbourBudget = Mathf.Clamp(Config.SeparationNeighbourBudget, 1, 128);
        Config.SeparationMaxPushPerTick = float.IsFinite(Config.SeparationMaxPushPerTick)
            ? Mathf.Clamp(Config.SeparationMaxPushPerTick, 0.01f, 1f) : 0.35f;
        separationPushPerStep = Config.SeparationMaxPushPerTick * correctionScale;
        // Leave a small margin inside melee reach at the requested body spacing.
        float maximumGap = 0.25f;
        for (int i = 0; i < catalog.Count; i++)
        {
            ref readonly UnitStats type = ref catalog[i];
            if (!type.Ranged) maximumGap = Mathf.Min(maximumGap, Mathf.Max(0, type.Reach - type.Footprint - 0.02f));
        }
        Config.SeparationGap = Mathf.Clamp(float.IsFinite(Config.SeparationGap) ? Config.SeparationGap : 0.05f, 0, maximumGap);
        alliedYieldSettings = new AlliedYieldSettings(
            ClampFiniteSetting(Config.AlliedRangedClearanceFraction, 1f, 0.5f),
            ClampFiniteSetting(Config.AlliedRangedYieldPairCap, 0.25f, 0.06f) * correctionScale,
            ClampFiniteSetting(Config.AlliedRangedYieldMeleeShare, 1f, 0.7f),
            ClampFiniteSetting(Config.AlliedRangedExitPairCap, 0.25f, 0.06f) * correctionScale);
        Config.MaxProjectiles = Mathf.Clamp(Config.MaxProjectiles, 100, 100000);
        var benchmarkGround = (ShaderMaterial)GD.Load<ShaderMaterial>("res://WORLD/TERRAIN/RENDERING/SteelSlate.tres").Duplicate();
        benchmarkGround.SetShaderParameter("show_map_boundary", false);
        flatGround = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(5000, 5000),
            Material = benchmarkGround } };
        AddChild(flatGround);
        camera = new IsometricCamera(); AddChild(camera); camera.Current = true;
        var authoredBuildings = GD.Load<BuildingCatalog>(BuildingCatalogPath);
        if (IsOffense)
        {
            var definitions = new Godot.Collections.Array<BuildingDefinition>(authoredBuildings.Definitions);
            definitions.Add(GD.Load<BuildingDefinition>("res://WORLD/BUILDINGS/DEFINITIONS/Citadel.tres"));
            authoredBuildings = new BuildingCatalog { Definitions = definitions };
        }
        buildings ??= new RuntimeBuildingCatalog(authoredBuildings, catalog.DamageRules, catalog.Projectiles);
        panel = new BenchmarkPanel
        {
            Catalog = catalog, Buildings = buildings, AutoBattlerPlaytest = autoBattlerPlaytestMap != null,
            CircleMeshToggle = CreateCircleMeshToggle()
        };
        AddChild(panel);
        if (campaignRoster != null)
        {
            panel.Hide();
            panel.SetProcessUnhandledKeyInput(false);
        }
        weather = new BattlefieldWeather(camera);
        panel.WeatherChanged += weather.Configure;
        panel.CameraRotationChanged += camera.SetYawDegrees;
        panel.CameraTiltChanged += camera.SetTiltDegrees;
        panel.CameraFreeRoamChanged += enabled =>
        {
            painter?.CancelDrag();
            camera.SetFreeRoam(enabled);
        };
        attackRanges = new AttackRangeOverlay();
        AddChild(attackRanges);
        shieldRanges = new AttackRangeOverlay();
        AddChild(shieldRanges);
        // Keep the gameplay bar separate from BENCHMARK's developer panels.
        var gameplayHudLayer = new CanvasLayer { Layer = 2 };
        AddChild(gameplayHudLayer);
        if (autoBattlerPlaytestMap != null) AddAutoBattlerClock(gameplayHudLayer);
        armyBalanceHud = GD.Load<PackedScene>("res://UI/GameplayHUD/ArmyBalanceHud.tscn").Instantiate<ArmyBalanceHud>();
        gameplayHudLayer.AddChild(armyBalanceHud);
        deploymentStrengthBar = new DeploymentStrengthBar { Visible = false };
        gameplayHudLayer.AddChild(deploymentStrengthBar);
        Config.ObstacleHeight = float.IsFinite(Config.ObstacleHeight) ? Mathf.Clamp(Config.ObstacleHeight, 0.2f, 8f) : 1f;
        Config.RouteSectorSize = float.IsFinite(Config.RouteSectorSize) ? Mathf.Clamp(Config.RouteSectorSize, 4f, 32f) : 12f;
        painter = new DeploymentPainter
        {
            Camera = camera, Visible = false, ObstacleHeight = Config.ObstacleHeight, ObstacleTint = Config.ObstacleTint
        };
        AddChild(painter);
        painter.Initialize(catalog, soldierMeshes, buildings);
        if (autoBattlerPlaytestMap != null && campaignRoster == null) painter.Orders.AddControls(panel.DeploymentOrderControls);
        painter.SetPlacementGridVisible(panel.ShowPlacementGrid);
        panel.PlacementGridChanged += painter.SetPlacementGridVisible;
        painter.Changed += UpdateDeploymentStatus;
        painter.EditRejected += message =>
        {
            if (campaignHud != null) campaignHud.Report(message);
            else panel.Status.Text = message + "\n" + DeploymentCount();
        };
        panel.PaintToolSelected += painter.SetPaintTool;
        panel.DefenseSelected += type => painter.DefenseType = type;
        battleWalls = new MeshInstance3D { CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(battleWalls);
        battleObstacleShadows = new MeshInstance3D { CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(battleObstacleShadows);
        panel.ShieldShellsChanged += enabled => armyRenderer?.SetShieldShellsVisible(enabled);
        panel.ShieldDomesChanged += enabled => defenseRenderer?.SetShieldDomesVisible(enabled);
        panel.GroundShadowsChanged += _ => ApplyArmyLighting();
        if (autoBattlerPlaytestMap == null) ConfigureBenchmarkLighting(benchmarkGround);
        if (campaignRoster == null) RefreshSavedBattles();
        panel.SpawnRequested += SpawnSelectedBattle;
        panel.SavePresetRequested += SaveBattlePreset;
        panel.ClearRequested += () =>
        {
            if (painter.Visible) painter.Clear();
            else if (autoBattlerPlaytestMap != null) EnterDeployment();
            else Clear();
        };
        panel.PaintRequested += EnterDeployment;
        panel.BattleRequested += StartPaintedBattle;
        panel.SaveRequested += () => panel.Status.Text = painter.Save();
        panel.LoadRequested += () => panel.Status.Text = painter.Load() + "\n" + DeploymentCount();
        panel.BrushSelected += type => painter.BrushType = type;
        panel.SoldierPaintType.ItemSelected += painter.SetSoldierPaintType;
        panel.SoldierBrushSizeChoice.ItemSelected += index =>
            painter.SoldierBrushSize = panel.SoldierBrushSizeChoice.GetItemId((int)index);
        painter.SoldierBrushSize = panel.SoldierBrushSizeChoice.GetItemId(panel.SoldierBrushSizeChoice.Selected);
        panel.FactionSelected += faction => painter.BrushFaction = faction;
        panel.PauseRequested += () => { if (offenseBattleEnded) return; paused = !paused; panel.PauseButton.Text = paused ? "Resume" : "Pause"; };
        shockwaves = new ShockwaveRenderer();
        AddChild(shockwaves);
        deathFeedback = new DeathFeedbackSystem(unitVisuals);
        deathRenderer = new DeathEffectRenderer();
        AddChild(deathRenderer);
        deathRenderer.Initialize(catalog, deathFeedback);
        panel.DeathEffects.Toggled += enabled =>
        {
            deathFeedback.Enabled = enabled;
            if (enabled) return;
            deathFeedback.Clear();
            deathRenderer.Sync(deathFeedback);
        };
        panel.PaintSpread.ValueChanged += value => painter.SoldierBrushSpread = (float)value;
        if (openInDeployment)
        {
            if (autoBattlerPlaytestMap != null) PrepareAutoBattlerPlaytest();
            if (editorSettings != null) panel.RestoreBattleSettings(editorSettings);
            if (editorLayout != null) painter.ReplaceLayout(editorLayout);
            EnterDeployment();
        }
        else Spawn();
    }
    // =========================================================
    private void Clear()
    {
        armyBalanceHud?.Reset();
        ShowAutoBattlerClock(false);
        campaignBattleRunning = false;
        ClearReserveMarch(redWave);
        blueWave = redWave = null;
        if (dreadnaughts != null)
        {
            RemoveChild(dreadnaughts);
            dreadnaughts.QueueFree();
            dreadnaughts = null;
        }
        guards = null;
        autoAttackIds = null;
        autoAttackCount = 0;
        shockwaves.Clear();
        deathFeedback.Clear();
        deathRenderer.Sync(deathFeedback);
        defenseSystem = null;
        offenseCitadel = null;
        offenseBattleEnded = false;
        defenseCombat = null;
        if (defenseRenderer != null)
        {
            RemoveChild(defenseRenderer);
            defenseRenderer.QueueFree();
            defenseRenderer = null;
        }
        if (terrainMeshes != null)
        {
            RemoveChild(terrainMeshes);
            terrainMeshes.QueueFree();
            terrainMeshes = null;
        }
        terrain = null;
        flatGround.Visible = autoBattlerPlaytestMap == null;
        if (armyRenderer != null) { RemoveChild(armyRenderer); armyRenderer.QueueFree(); armyRenderer = null; }
        if (projectileRenderer != null) { RemoveChild(projectileRenderer); projectileRenderer.QueueFree(); projectileRenderer = null; }
        obstacles = null; navigation = null; battleWalls.Mesh = null; battleObstacleShadows.Mesh = null;
        meleeSteering = null;
        units = null; projectiles = null; grid = null; separation = null; hardSeparation = null; trample = null; voluntaryMovement = null; deployment = null; spawning = false;
        targetingAccumulator = 0; targetingStep = 0;
        accumulator = 0; tick = 0; preparedTargets = 0; skippedSteps = 0; paused = false; panel.PauseButton.Text = "Pause";
        timeSinceArmySync = 0; smoothingWasEnabled = false;
        panel.Summary.ResetSamples(); ResetTimings(); panel.Status.Text = "Cleared";
    }
    private void Spawn()
    {
        Clear();
        SetDeploymentEditing(false);
        requested = panel.RequestedCount; targetCount = Mathf.Min(requested, Config.MaxSoldiers);
        mode = (BenchmarkMode)panel.ModeChoice.Selected;
        if (panel.TerrainChoice.Selected == 1) PreparePlateau();
        if (panel.TestWall.ButtonPressed)
        {
            // Keep the preset wall on the flat plain when the plateau is active.
            int wallX = terrain == null ? -2 : -6;
            var wall = new ObstacleField(new[] { new ObstacleRectangle(wallX, -12, 4, 24) });
            SetBattleObstacles(wall, ObstacleRenderer.Build(wall, Config.ObstacleHeight, Config.ObstacleTint));
        }
        units = CreateUnitState();
        if (mode is BenchmarkMode.Melee or BenchmarkMode.Ranged) grid = new SpatialGrid(targetCount, Config.CellSize, profileTargeting: true);
        projectiles = new ProjectileSystem(Config.MaxProjectiles, catalog.Projectiles, projectileLifetime);
        deployment = new DeploymentFactory(targetCount, (ulong)panel.Seed.Value, (Deployment)panel.LayoutChoice.Selected, mode, (float)panel.RangedPercent.Value / 100);
        spawning = true;
        Deployment chosenLayout = (Deployment)panel.LayoutChoice.Selected;
        camera.Frame(chosenLayout >= Deployment.LayeredRanks
            ? Mathf.Sqrt(targetCount) * 7f + 180f
            : Mathf.Sqrt(targetCount / 2f) * 4f + (chosenLayout == Deployment.DistributedBattlefield ? 350f : 80f));
    }
    private void PreparePlateau()
    {
        float rise = float.IsFinite(Config.PlateauRise) ? Mathf.Clamp(Config.PlateauRise, 0.5f, 8f) : 4f;
        float shoulder = float.IsFinite(Config.PlateauShoulder) ? Mathf.Clamp(Config.PlateauShoulder, 16f, 64f) : 20f;
        shoulder = Mathf.Max(shoulder, 3f * rise);
        long start = Stamp();
        terrain = PlateauScenario.Create(rise, shoulder);
        terrainCompileMs = Elapsed(start);
        start = Stamp();
        terrainMeshes = TerrainRenderer.Build(terrain, rise, 2500f);
        AddChild(terrainMeshes);
        if (RealBenchmarkShadows) RefreshBenchmarkGroundLighting();
        flatGround.Visible = false;
        terrainMeshMs = Elapsed(start);
    }

    private UnitState CreateUnitState(uint? seed = null)
    {
        int capacity = targetCount + ReserveCapacity;
        battleSeed = seed ?? (uint)panel.Seed.Value;
        var feedback = mode is BenchmarkMode.Melee or BenchmarkMode.Ranged
            ? new HitFeedbackSystem(capacity, Config.HitFeedback ?? new HitFeedbackSettings())
            : null;
        var recoil = (mode is BenchmarkMode.Ranged or BenchmarkMode.Melee) && catalog.HasRangedRecoil
            ? new RangedRecoilSystem(capacity) : null;
        return new UnitState(capacity, catalog, feedback, terrain, recoil)
        {
            CircleUsesFacing = unitVisuals.UseCircleRobotMesh,
            DeathFeedback = mode is BenchmarkMode.Melee or BenchmarkMode.Ranged ? deathFeedback : null
        };
    }

    private void SetDeploymentEditing(bool editing)
    {
        painter.CancelDrag();
        CancelBlueDisruptorLine();
        painter.Visible = editing;
        panel.EditingDeployment = editing;
        deploymentStrengthBar.Visible = editing && campaignRoster == null;
        if (deploymentStrengthBar.Visible) deploymentStrengthBar.Refresh(painter.Layout);
    }

    private string DeploymentCount() => autoBattlerPlaytestMap != null
        ? $"Blue: {painter.Layout.Placements.Count:N0} deployed · No roster cap\nRed: {autoBattlerRed.Length:N0} units · {autoBattlerDefenses.Count} defenses"
        : $"Painted {painter.Layout.Placements.Count:N0} • " +
        $"{painter.Layout.Obstacles.Field?.RectangleCount ?? 0} wall rectangles • {painter.Layout.Defenses.Count} defenses";

    private void SetBattleObstacles(ObstacleField field, Mesh mesh)
    {
        obstacles = field;
        obstacles?.ResetCounters();
        battleWalls.Mesh = mesh;
        battleObstacleShadows.Mesh = GroundShadowMeshes.Obstacles(field, Config.ObstacleHeight);
    }
    private void UpdateDeploymentStatus()
    {
        if (campaignHud != null) UpdateCampaignDeployment();
        else panel.Status.Text = DeploymentCount();
        if (deploymentStrengthBar.Visible) deploymentStrengthBar.Refresh(painter.Layout);
    }

    private void EnterDeployment()
    {
        if (painter.Visible) return;
        Clear();
        SetDeploymentEditing(true);
        if (autoBattlerPlaytestMap != null)
        {
            autoBattlerForest?.ResetVisibility();
            autoBattlerPlaytestPreview.Show();
            autoBattlerPlaytestMap.FrameOpening(camera);
        }
        else camera.Frame(100);
        UpdateDeploymentStatus();
    }

    private const string SavedBattlesDirectory = "user://benchmark_presets";

    private DeploymentPainter.BattleSettings ReadBattleSettings()
    {
        var orders = new GuardOrder[Factions.Count];
        for (int faction = 0; faction < orders.Length; faction++) orders[faction] = panel.OrderFor(faction);
        return new DeploymentPainter.BattleSettings((int)panel.Seed.Value, orders, (float)panel.GuardRadius.Value);
    }

    private void RefreshSavedBattles(string selectedFile = "")
    {
        var choice = panel.SavedBattleChoice;
        choice.Clear();
        choice.AddItem("Generated battle");
        var files = DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(SavedBattlesDirectory))
            ? DirAccess.GetFilesAt(SavedBattlesDirectory) : Array.Empty<string>();
        int selected = 0;
        foreach (string file in files)
        {
            if (!file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
            int index = choice.ItemCount;
            choice.AddItem(file[..^5]);
            choice.SetItemMetadata(index, file);
            if (file == selectedFile) selected = index;
        }
        choice.Select(selected);
    }

    private void SaveBattlePreset()
    {
        string name = panel.PresetName.Text.Trim();
        if (name.Length == 0 || name == "Generated battle")
        {
            panel.Status.Text = "Enter a distinct preset name.";
            return;
        }
        foreach (char character in name)
        {
            if (char.IsLetterOrDigit(character) || character is ' ' or '-' or '_') continue;
            panel.Status.Text = "Use letters, numbers, spaces, hyphens or underscores in preset names.";
            return;
        }
        if (painter.Layout.Placements.Count == 0)
        {
            panel.Status.Text = "Paint soldiers before saving a battle preset.";
            return;
        }
        Error directoryError = DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(SavedBattlesDirectory));
        if (directoryError != Error.Ok)
        {
            panel.Status.Text = $"Could not open preset storage ({directoryError}).";
            return;
        }
        string file = name + ".json";
        string destination = $"{SavedBattlesDirectory}/{file}";
        if (FileAccess.FileExists(destination))
        {
            panel.Status.Text = "A preset already has that name. Choose another name.";
            return;
        }
        string temporary = destination + ".tmp";
        string message = painter.Save(temporary, ReadBattleSettings());
        if (message != "Deployment saved.")
        {
            panel.Status.Text = message;
            return;
        }
        Error result = DirAccess.RenameAbsolute(ProjectSettings.GlobalizePath(temporary), ProjectSettings.GlobalizePath(destination));
        if (result != Error.Ok)
        {
            panel.Status.Text = $"Could not save preset ({result}).";
            return;
        }
        RefreshSavedBattles(file);
        panel.Status.Text = $"Saved battle preset: {name}";
    }

    private void SpawnSelectedBattle()
    {
        var choice = panel.SavedBattleChoice;
        if (choice.Selected <= 0)
        {
            Spawn();
            return;
        }
        string file = choice.GetItemMetadata(choice.Selected).AsString();
        string message = painter.ReadDeployment($"{SavedBattlesDirectory}/{file}", out var layout, out var settings);
        if (layout == null || settings == null)
        {
            panel.Status.Text = layout == null ? message : "Saved battle settings are missing.";
            return;
        }
        StartPaintedBattle(layout, settings, ObstacleRenderer.Build(layout.Obstacles.Field, Config.ObstacleHeight, Config.ObstacleTint));
    }

    private void StartPaintedBattle() => StartPaintedBattle(painter.Layout, ReadBattleSettings(), painter.ObstacleMesh);

    private void StartPaintedBattle(DeploymentLayout layout, DeploymentPainter.BattleSettings settings, Mesh wallMesh)
    {
        IReadOnlyList<SoldierPlacement> placements = layout.Placements;
        IReadOnlyList<BuildingPlacement> defenses = layout.Defenses;
        ReserveWave plannedBlue = null, plannedRed = null;
        if (autoBattlerPlaytestMap != null)
        {
            if ((placements.Count == 0 && blueReserve.Count == 0) || (autoBattlerRed.Length == 0 && autoBattlerDefenses.Count == 0 && AuthoredRedReserve == 0))
            {
                panel.Status.Text = placements.Count == 0 ? "Paint Blue troops in the deployment area." :
                    "Add Red troops or defenses in the Level Editor before testing.";
                return;
            }
            if ((long)placements.Count + autoBattlerRed.Length + AuthoredRedReserve +
                blueReserveCount > Config.MaxSoldiers)
            {
                ReportStartError("Starting troops and reinforcements exceed soldier capacity.");
                return;
            }
            if (!PrepareReserveWaves(out string error))
            {
                blueWave = redWave = null;
                ReportStartError(error);
                return;
            }
            plannedBlue = blueWave;
            plannedRed = redWave;
            placements = placements.Concat(autoBattlerRed).ToArray();
            defenses = autoBattlerDefenses;
        }
        int factionMask = 0;
        bool ranged = false;
        foreach (SoldierPlacement placement in placements)
        {
            factionMask |= 1 << placement.Faction;
            ranged |= catalog[placement.Type].Ranged || placement.Type == RuntimeUnitCatalog.Dreadnaught;
        }
        if (plannedBlue != null) factionMask |= 1 << Factions.Blue;
        if (plannedRed != null) factionMask |= 1 << Factions.Red;
        ranged |= plannedBlue?.Soldiers.Any(soldier => catalog[soldier.Type].Ranged || soldier.Type == RuntimeUnitCatalog.Dreadnaught) == true ||
            plannedRed?.Soldiers.Any(soldier => catalog[soldier.Type].Ranged || soldier.Type == RuntimeUnitCatalog.Dreadnaught) == true;
        foreach (var defense in defenses) factionMask |= 1 << defense.Faction;
        if ((placements.Count == 0 && plannedBlue == null && plannedRed == null) || System.Numerics.BitOperations.PopCount((uint)factionMask) < 2)
        {
            panel.Status.Text = "Place soldiers and at least two teams.\n" + DeploymentCount();
            return;
        }
        if (IsOffense && campaignMapError != null) { ReportStartError(campaignMapError); return; }
        var orders = settings.Orders;
        if (IsOffense)
        {
            orders = (GuardOrder[])orders.Clone();
            orders[Factions.Red] = GuardOrder.Return;
        }
        var openingOverlay = autoBattlerPlaytestMap != null && ReferenceEquals(layout, painter.Layout) ? painter.Orders : null;
        var redOrders = autoBattlerPlaytestMap?.AutoBattlerRules?.ReadRedOrders();
        DeploymentOrderOverlay.Order RedOrder(SoldierPlacement placement) =>
            redOrders?.GetValueOrDefault(AutoBattlerRules.RedOrderKey(placement.Position()));
        DeploymentOrderOverlay.Order OrderFor(SoldierPlacement placement)
        {
            var order = placement.Faction == Factions.Red ? RedOrder(placement) : openingOverlay?.For(placement);
            return order?.Mode == DeploymentOrderOverlay.Behavior.OpeningOrder ? order : null;
        }
        bool HardGuard(SoldierPlacement placement) => IsOffense && placement.Faction == Factions.Red &&
            RedOrder(placement)?.Mode == DeploymentOrderOverlay.Behavior.HardGuard;
        bool AutoAttack(SoldierPlacement placement) =>
            orders[placement.Faction] == GuardOrder.AutoAttack || OrderFor(placement) != null ||
            (IsOffense && placement.Faction == Factions.Red && RedOrder(placement)?.Mode == DeploymentOrderOverlay.Behavior.AutoAttack);
        var snapshot = new SoldierPlacement[placements.Count];
        int attackerCount = 0;
        foreach (SoldierPlacement placement in placements)
            if (AutoAttack(placement))
                snapshot[attackerCount++] = placement;

        Clear();
        blueWave = plannedBlue;
        redWave = plannedRed;
        reserveClock = 0;
        SetDeploymentEditing(false);
        requested = targetCount = snapshot.Length;
        autoAttackCount = attackerCount;
        if (attackerCount < snapshot.Length)
        {
            var guardGroups = new List<GuardGroup>();
            int next = attackerCount;
            for (int faction = 0; faction < Factions.Count; faction++)
            {
                GuardOrder order = orders[faction];
                if (order == GuardOrder.AutoAttack || (IsOffense && faction == Factions.Red)) continue;
                int start = next;
                foreach (SoldierPlacement placement in placements)
                    if (placement.Faction == faction && !AutoAttack(placement) && !HardGuard(placement)) snapshot[next++] = placement;
                if (start < next)
                    guardGroups.Add(new GuardGroup(start, next, snapshot, settings.GuardRadius, order));
            }
            if (IsOffense)
            {
                var defenders = placements.Where(placement => placement.Faction == Factions.Red && !AutoAttack(placement));
                foreach (var group in defenders.GroupBy(placement =>
                {
                    var authored = RedOrder(placement);
                    var after = authored?.After ?? DeploymentOrderOverlay.AfterCombat.StayStill;
                    return (Hard: HardGuard(placement), After: after,
                        Squad: after == DeploymentOrderOverlay.AfterCombat.AutoFight ? authored : null);
                }))
                {
                    int start = next;
                    foreach (var placement in group) snapshot[next++] = placement;
                    var behavior = group.Key.After switch
                    {
                        DeploymentOrderOverlay.AfterCombat.ReturnToSpawn => GuardOrder.Return,
                        DeploymentOrderOverlay.AfterCombat.AutoFight => GuardOrder.Counterattack,
                        _ => GuardOrder.Hold
                    };
                    guardGroups.Add(new GuardGroup(start, next, snapshot, group.Key.Hard ? 6f : 12f,
                        behavior, hardGuard: group.Key.Hard, offense: true));
                }
            }
            guards = guardGroups.ToArray();
        }
        mode = ranged ? BenchmarkMode.Ranged : BenchmarkMode.Melee;
        if (autoBattlerPlaytestMap == null) SetBattleObstacles(layout.CollisionField, wallMesh);
        else
        {
            autoBattlerPlaytestPreview.Hide();
            terrain = autoBattlerPlaytestMap.GetNodeOrNull<CommandTerrain>("Terrain")?.Field;
        }
        if (defenses.Count > 0)
        {
            defenseRenderer = new DefenseRenderer();
            AddChild(defenseRenderer);
            defenseRenderer.SetShieldDomesVisible(panel.ShowShieldDomes && panel.ShieldsEnabled);
            defenseRenderer.Initialize(defenses, buildings, Config.ObstacleHeight, battle: true, terrain: terrain);
            defenseSystem = new DefenseSystem(defenses, buildings, Config.ObstacleHeight, defenseRenderer, catalog.DamageRules, layout.Obstacles.Field, terrain);
        }
        if (IsOffense && defenseSystem != null && offenseCitadelSlot >= 0 && offenseCitadelSlot < defenseSystem.Structures.Count)
            offenseCitadel = defenseSystem.Structures[offenseCitadelSlot];
        if (autoBattlerPlaytestMap != null) RefreshAutoBattlerFields();
        units = CreateUnitState((uint)settings.Seed);
        if (autoBattlerPlaytestMap != null && (openingOverlay != null || redOrders?.Count > 0))
            units.OpeningOrders = OpeningOrders.Create(snapshot, attackerCount, OrderFor, autoBattlerPlaytestMap.MapSize * 0.5f, catalog);
        defenseSystem?.RegisterShieldEmitters(units);
        units.Forest = autoBattlerForest;
        var redTraps = autoBattlerPlaytestMap?.AutoBattlerRules.RedDisruptorCells;
        if (redTraps != null && (redTraps.Count > 0 || blueDisruptors.Count > 0))
            units.Disruptors = new DisruptorGrid(blueDisruptors, redTraps);
        if (defenseSystem != null) defenseCombat = new StructureCombat(units, defenseSystem.Structures);
        grid = new SpatialGrid(units.Position.Length, Config.CellSize, profileTargeting: campaignRoster == null);
        projectiles = new ProjectileSystem(Config.MaxProjectiles, catalog.Projectiles, projectileLifetime);
        deployment = new DeploymentFactory(snapshot, (ulong)settings.Seed);
        spawning = true;

        void ReportStartError(string message)
        {
            panel.Status.Text = message;
            campaignHud?.Report(message);
        }
    }

    // =========================================================
    public override void _Process(double delta)
    {
        // Also handles an empty battlefield or a terminal objective left by the preceding frame.
        if (!spawning && offenseCitadel != null && !offenseBattleEnded) TryFinishOffense();
        if (!paused) weather.Advance((float)delta);
        units?.Forest?.UpdateVisibility(units, (float)delta);
        if (obstacles != null) obstacles.ProfileQueries = panel.ProfileObstacles;
        if (navigation != null)
        {
            navigation.ReuseCorridors = panel.ReuseCorridors;
            navigation.ReuseWaypoints = panel.ReuseWaypoints;
        }
        panel.Summary.Sample(delta);
        if (!paused)
        {
            shockwaves.Advance((float)delta);
            deathFeedback.Advance((float)delta);
            defenseRenderer?.Advance((float)delta);
        }
        if (spawning)
        {
            if (units.Count < targetCount)
            {
                int first = units.Count;
                deployment.Append(units, Config.SpawnPerFrame);
                if ((obstacles != null || campaignRoster != null) && !ValidateSpawn(first))
                {
                    if (campaignHud != null)
                    {
                        EnterDeployment();
                        campaignHud.Report("A starting soldier overlaps an obstruction or the map edge. Check the authored positions or move the Blue deployment.");
                        return;
                    }
                    Clear();
                    panel.Status.Text = "Spawn or march endpoint overlaps a wall or the battlefield edge.";
                    return;
                }
                panel.Status.Text = $"Preparing {units.Count:N0} / {requested:N0} soldiers";
                if (units.Count < targetCount) return;
                if (guards == null) autoAttackCount = units.Count;
                grid?.Rebuild(units);
            }
            if (grid != null && preparedTargets < autoAttackCount)
            {
                int end = Math.Min(autoAttackCount, preparedTargets + InitialTargetsPerFrame);
                grid.PrunePursuitCells = panel.PrunePursuitCells;
                CombatSystem.PrepareTargets(units, grid, preparedTargets, end);
                preparedTargets = end;
                panel.Status.Text = $"Preparing targets {preparedTargets:N0} / {autoAttackCount:N0}";
                if (preparedTargets < autoAttackCount) return;
            }
            if (units.Count == targetCount)
            {
                if (grid != null) trample = TrampleSystem.Create(units, autoAttackCount);
                if (obstacles != null && mode != BenchmarkMode.RenderingOnly)
                    navigation = new ObstacleNavigation(obstacles, catalog, units.Position.Length, Config.RouteSectorSize,
                        Mathf.Clamp(Config.RouteCacheCapacity, 64, 8192), Mathf.Clamp(Config.RoutesPerFrame, 1, 64));
                CreateArmyRenderer();
                projectileRenderer = new ProjectileRenderer(); AddChild(projectileRenderer);
                projectileRenderer.Initialize(Config.MaxProjectiles, catalog.Projectiles, projectiles,
                    autoBattlerPlaytestMap?.LevelSettings?.SmokeWind ?? MuzzleSmoke.DefaultWind);
                dreadnaughts = DreadnaughtCombat.Create(units);
                if (dreadnaughts != null)
                {
                    AddChild(dreadnaughts);
                    dreadnaughts.SyncVisuals();
                }
                spawning = false; panel.Status.Text = $"Requested {requested:N0}\nSpawned {units.Count:N0} • {mode}";
                armyBalanceHud.BeginBattle(units, IsOffense ? offenseCitadel : null);
                ShowAutoBattlerClock(true);
                if (campaignHud != null)
                {
                    campaignBattleRunning = true;
                    campaignHud.ShowBattle();
                    campaignHud.ShowArrivals(ReserveCountdown());
                }
                panel.Summary.ResetSamples();
            }
        }
        else if (units != null && !paused && mode != BenchmarkMode.RenderingOnly && (units.Alive > 0 || projectiles.Count > 0 || ReservesPending))
        {
            if (navigation != null)
            {
                if (navigation.QueueDepth == 0) navigation.AdvanceFrame();
                else
                {
                    long routeStart = Stamp();
                    navigation.BuildQueuedRoutes();
                    routeBuildMs += Elapsed(routeStart);
                }
            }
            bool smooth = panel.SmoothMotion && panel.Separation != SeparationMode.Hard;
            if (!smooth && smoothingWasEnabled) armyRenderer.ClearPoseHistory();
            accumulator += delta;
            timeSinceArmySync += delta;
            int steps = 0;
            while (!offenseBattleEnded && accumulator >= Config.TickSeconds && steps < Config.MaxStepsPerFrame)
            {
                if (ReservesPending) ReleaseReserves(Config.TickSeconds);
                if (smooth)
                {
                    long snapshotStart = Stamp();
                    armyRenderer.CapturePose();
                    renderMs += Elapsed(snapshotStart);
                }
                Step(Config.TickSeconds);
                accumulator -= Config.TickSeconds;
                steps++;
                if (offenseBattleEnded) { accumulator = 0; break; }
            }
            if (accumulator >= Config.TickSeconds)
            { long skipped = (long)(accumulator / Config.TickSeconds); skippedSteps += skipped; accumulator -= skipped * Config.TickSeconds; }
            measuredTicks += steps;
            maxTicksInFrame = Math.Max(maxTicksInFrame, steps);
            // Avoid interpolated uploads at uncapped render rates when no tick ran.
            bool intermediate = smooth && armyRenderer.HasInterpolatedMotion && timeSinceArmySync >= 1.0 / 60.0 - 0.000001;
            if (steps > 0 || intermediate || (!smooth && smoothingWasEnabled))
            {
                long start = Stamp();
                float blend = smooth && !offenseBattleEnded ? (float)Math.Clamp(accumulator / Config.TickSeconds, 0.0, 1.0) : 1f;
                armyRenderer.Sync(blend);
                SyncReserveMarch();
                dreadnaughts?.SyncVisuals();
                if (steps > 0) projectileRenderer.Sync(projectiles);
                timeSinceArmySync = 0;
                renderMs += Elapsed(start);
                measuredSyncs++;
                armyUploadMs += armyRenderer.UploadMilliseconds;
                denseUploads += armyRenderer.DenseUploads;
                sparseUploads += armyRenderer.SparseUploads;
                changedWrites += armyRenderer.ChangedWrites;
            }
            smoothingWasEnabled = smooth;
            AdvanceAutoBattlerClock(steps);
            if (campaignHud != null) UpdateCampaignOutcome(steps);
            else if (!offenseBattleEnded && ReserveCapacity > 0)
            {
                string countdown = ReserveCountdown();
                if (panel.Status.Text != countdown) panel.Status.Text = countdown;
            }
        }
        if (!paused)
        {
            projectileRenderer?.AdvanceEffects((float)delta);
            if (!spawning && units?.Alive == 0 && projectiles?.Count == 0)
                defenseSystem?.Step(units, null, BattleSight, null, (float)delta, -1, targetingStepSeconds);
        }
        shieldRanges.RefreshShields(units, panel.ShowShieldRadii && panel.ShieldsEnabled && !spawning);
        attackRanges.Refresh(units, camera, (panel.Separation != SeparationMode.Off || panel.StrongEnemySeparation) && units?.Alive > 1
            ? Config.SeparationGap : 0f, panel.ShowAttackRanges && !spawning);
        shockwaves.Sync();
        deathRenderer.Sync(deathFeedback);
        measuredFrames++; hudClock += delta;
        if (hudClock >= 0.3)
        {
            armyBalanceHud?.RefreshCounts();
            if (campaignRoster == null) UpdateOverlay();
            hudClock = 0;
            ResetTimings();
        }
    }
    // =========================================================
    private bool ValidateSpawn(int first)
    {
        for (int i = first; i < units.Count; i++)
        {
            float radius = catalog[units.Type[i]].Footprint;
            if (!ValidGroundPoint(units.Position[i], radius) ||
                (mode == BenchmarkMode.Movement && !ValidGroundPoint(units.Goal[i], radius)))
                return false;
        }
        return true;
    }

    private bool ValidGroundPoint(Vector3 position, float radius)
    {
        float edge = obstacles?.Boundary ?? autoBattlerPlaytestMap.MapSize * 0.5f;
        return Mathf.Abs(position.X) + radius <= edge && Mathf.Abs(position.Z) + radius <= edge &&
            obstacles?.IntersectsCircle(ObstacleField.Xz(position), radius) != true;
    }

    private void AcquireGuards(int acquisitionStep)
    {
        bool anyGuarding = false, promoted = false;
        for (int faction = 0; faction < guards.Length; faction++)
        {
            GuardGroup group = guards[faction];
            if (group == null) continue;
            if (!group.Acquire(units, grid, Config, acquisitionStep, targetingStepSeconds, BattleSight))
            {
                anyGuarding = true;
                continue;
            }

            if (autoAttackIds == null)
            {
                autoAttackIds = new int[units.Position.Length];
                for (int id = 0; id < autoAttackCount; id++) autoAttackIds[id] = id;
            }
            for (int id = group.Start; id < group.End; id++)
                autoAttackIds[autoAttackCount++] = id;
            guards[faction] = null;
            promoted = true;
        }
        if (!anyGuarding)
        {
            guards = null;
            autoAttackIds = null;
        }
        if (promoted) trample = TrampleSystem.Create(units, autoAttackCount, autoAttackIds, trample);
    }

    private void AcquireDefenseTargets(int step)
    {
        if (defenseCombat == null || !HasDefenseAttackers) return;
        const int interval = 6;
        for (int slot = step % interval; slot < autoAttackCount; slot += interval)
        {
            int id = autoAttackIds == null ? slot : autoAttackIds[slot];
            if (units.Health[id] <= 0f) continue;
            ref readonly var type = ref catalog[units.Type[id]];
            bool opening = units.OpeningOrders?.Active(id) == true;
            var sight = StructureCombat.Sight(type, units, BattleSight);
            bool siege = offenseCitadel?.Destroyed == false && units.Faction[id] == Factions.Blue &&
                (type.TargetsUnits || type.TargetsStructures) && type.AgainstStructures(type.Damage) > 0f;
            if (siege && defenseCombat.TryPrioritize(id, type, offenseCitadel, sight, obstacles, tick, navigation,
                !opening && (units.Target[id] < 0 || units.Health[units.Target[id]] <= 0f))) continue;
            if (opening || !type.TargetsStructures)
            {
                if (IsOffense) defenseCombat.SetTarget(id, null);
                continue;
            }

            // Reuse the shared soldier acquisition result and its normal search budget.
            int soldier = units.Target[id];
            if (soldier >= 0 && units.Health[soldier] <= 0f) soldier = -1;
            Vector3 position = units.Position[id];
            float soldierDistance = soldier >= 0 ? position.DistanceTo(units.Position[soldier]) : float.PositiveInfinity;
            var building = defenseCombat.Acquire(id, type, soldierDistance, true, true, obstacles, sight, tick, navigation);
            Vector2 ground = ObstacleField.Xz(position);
            if (building != null && soldier >= 0 &&
                ground.DistanceSquaredTo(StructureCombat.Edge(building.Area, ground)) >= soldierDistance * soldierDistance)
                defenseCombat.SetTarget(id, null);
        }
    }

    private void AttackDefenses(float dt)
    {
        if (defenseCombat == null || !HasDefenseAttackers) return;
        for (int slot = 0; slot < autoAttackCount; slot++)
        {
            int id = autoAttackIds == null ? slot : autoAttackIds[slot];
            if (defenseCombat.Target(id)?.Destroyed != false || trample?.OverridesAttack(id) == true) continue;
            defenseCombat.Attack(id, projectiles, dt, StructureCombat.Sight(catalog[units.Type[id]], units, BattleSight), shockwaves);
        }
    }

    private void Step(float dt)
    {
        if (units.OpeningOrders?.Advance(units, dt) == false) units.OpeningOrders = null;
        deathFeedback.BeginTick();
        units.Disruptors?.Step(units, dt);
        long total = Stamp(), start;
        if (mode == BenchmarkMode.Melee || mode == BenchmarkMode.Ranged)
        {
            start = Stamp(); grid.Rebuild(units); gridMs += Elapsed(start);
            units.Shields?.Step(units, grid, dt, panel.ShieldInterval, 0f, panel.ShieldsEnabled);
            // Keep expensive searches together so pursuit still builds at most one index per acquisition step.
            // Movement and attack execution check target health on every core tick.
            targetingAccumulator += dt;
            int acquisitionStep = -1;
            if (targetingAccumulator >= targetingStepSeconds)
            {
                targetingAccumulator -= targetingStepSeconds;
                acquisitionStep = targetingStep++;
                start = Stamp();
                grid.PrunePursuitCells = panel.PrunePursuitCells;
                CombatSystem.Acquire(units, grid, Config, acquisitionStep, targetingStepSeconds, BattleSight,
                    panel.RangedTargetVariety, battleSeed, panel.SlowPursuitRefresh,
                    panel.DelayFarMeleeRetarget, panel.LimitPursuitQueries, autoAttackCount, autoAttackIds);
                if (guards != null) AcquireGuards(acquisitionStep);
                AcquireDefenseTargets(acquisitionStep);
                targetingMs += Elapsed(start);
            }
            start = Stamp();
            units.HitFeedback?.Advance(units, dt);
            units.RangedRecoil?.Advance(units, dt);
            defenseSystem?.Step(units, grid, BattleSight, projectiles, dt, acquisitionStep, targetingStepSeconds);
            combatMs += Elapsed(start);
        }
        SeparationMode separationMode = panel.Separation;
        bool hard = separationMode == SeparationMode.Hard;
        bool strongEnemies = panel.StrongEnemySeparation;
        bool separate = (separationMode != SeparationMode.Off || strongEnemies) && units.Alive > 1;
        float boundary = obstacles?.Boundary ?? (autoBattlerPlaytestMap?.MapSize * 0.5f ?? DeploymentLayout.WorldHalfWidth);
        if (hard && separate)
        {
            start = Stamp();
            hardSeparation ??= new HardSeparation(units.Position.Length);
            const string packingFailure = "Hard separation could not make room. Allow more overlap, spread units out, or switch mode.";
            bool ready = hardSeparation.Begin(units, obstacles, boundary, panel.HardOverlap, out bool repacked);
            separationMs += Elapsed(start);
            if (!ready)
            {
                panel.Status.Text = packingFailure;
                tick++;
                simulationMs += Elapsed(total);
                return;
            }
            if (repacked)
            {
                if (panel.Status.Text == packingFailure) panel.Status.Text = "Hard separation ready.";
                trample = null;
                grid?.Rebuild(units);
            }
        }
        else if (hardSeparation != null)
        {
            hardSeparation = null;
            trample = grid == null ? null : TrampleSystem.Create(units, autoAttackCount);
        }
        bool profileMovement = panel.ProfileMovement;
        int samplePhase = profileMovement ? tick % MovementProfile.SampleStride : -1;
        start = Stamp();
        var activeTrample = hard ? null : trample;
        activeTrample?.BeginTick(units, dt, obstacles);
        byte[] movementFlags = separate && !hard && panel.AlliedYield
            ? (voluntaryMovement ??= new byte[units.Position.Length]) : null;
        bool holdMelee = grid != null && panel.MeleeHold;
        bool sidestepMelee = grid != null && separate && panel.MeleeSidestep;
        if (!holdMelee && !sidestepMelee) meleeSteering = null;
        else if (meleeSteering == null || !meleeSteering.Matches(holdMelee, sidestepMelee))
            meleeSteering = new MeleeSteering(units.Position.Length, holdMelee, sidestepMelee);
        MovementSystem.Step(units, mode, dt, separate ? Config.SeparationGap : -1f, ref movementProfile, samplePhase, movementFlags,
            Config.MeleeAccelerationSeconds, Config.RangedAccelerationSeconds, activeTrample, obstacles, navigation, grid, meleeSteering, autoAttackCount, autoAttackIds, HasDefenseAttackers ? defenseCombat : null,
            BattleSight, autoBattlerPlaytestMap != null);
        if (guards != null)
            foreach (GuardGroup group in guards)
                group?.Move(units, dt, separate ? Config.SeparationGap : 0f, obstacles, navigation, movementFlags);
        float tramplePadding = activeTrample?.MoveCharges(units, grid, dt, obstacles, shockwaves, stopOnSurvivors: strongEnemies) ?? 0f;
        double movementElapsed = Elapsed(start);
        movementMs += movementElapsed;
        if (profileMovement)
        {
            movementProfile.Ticks++;
            movementProfile.TotalMilliseconds += movementElapsed;
        }
        float separationPadding = 0f;
        if (separate)
        {
            start = Stamp();
            if (hard)
            {
                hardSeparation.End(units, obstacles, boundary);
                if (grid != null) grid.Rebuild(units);
            }
            else
            {
                separation ??= new SeparationSystem(units.Position.Length);
                separationPadding = separationPushPerStep;
                separation.Step(units, 1, Config.SeparationGap,
                    Config.SeparationNeighbourBudget, separationPadding, tick, movementFlags, alliedYieldSettings,
                    trample, obstacles, panel.WeightedSeparation, strongEnemyContact: strongEnemies,
                    regularSeparation: separationMode == SeparationMode.Normal);
            }
            separationMs += Elapsed(start);
        }
        if (activeTrample != null)
        {
            start = Stamp();
            activeTrample.MeasureRunUp(units, dt);
            double runUp = Elapsed(start);
            movementMs += runUp;
            if (profileMovement)
            {
                movementProfile.TotalMilliseconds += runUp;
                movementProfile.RunUpMilliseconds += runUp;
            }
        }
        if (units.Grounding != null)
        {
            start = Stamp();
            groundSamples += units.Grounding.Apply(units);
            groundingMs += Elapsed(start);
        }
        shockwaves.SampleCavalry(units, dt);
        float extraMovementPadding = separationPadding + tramplePadding;
        if (mode == BenchmarkMode.Melee || mode == BenchmarkMode.Ranged)
        {
            start = Stamp();
            CombatSystem.Step(units, grid, projectiles, (float)panel.FireRate.Value, dt, extraMovementPadding, separate ? Config.SeparationGap : 0f, activeTrample, BattleSight, shockwaves, HasDefenseAttackers ? defenseCombat : null, useDreadnaughtContact: dreadnaughts != null, meleeObstacles: obstacles);
            dreadnaughts?.Step(grid, projectiles, BattleSight, dt, units.MaximumTravelSpeed * dt + extraMovementPadding, (float)panel.FireRate.Value);
            AttackDefenses(dt);
            combatMs += Elapsed(start);
            if (mode == BenchmarkMode.Ranged || dreadnaughts != null || projectiles.Count > 0) { start = Stamp(); projectiles.Step(units, grid, Config, dt, extraMovementPadding, BattleSight, shockwaves); projectileMs += Elapsed(start); }
        }
        // Commit the objective result before destruction cleanup / navigation rebuilding.
        // A finished Offense battle needs no new route graph or additional simulation ticks.
        if (offenseCitadel != null) TryFinishOffense();
        if (defenseSystem != null && defenseSystem.RemoveDestroyed(out var remainingObstacles))
        {
            if (autoBattlerPlaytestMap != null) RefreshAutoBattlerFields();
            else SetBattleObstacles(remainingObstacles, battleWalls.Mesh);
            navigation = offenseBattleEnded || obstacles == null ? null : new ObstacleNavigation(obstacles, catalog, units.Position.Length, Config.RouteSectorSize,
                Mathf.Clamp(Config.RouteCacheCapacity, 64, 8192), Mathf.Clamp(Config.RoutesPerFrame, 1, 64));
        }
        tick++;
        double tickMs = Elapsed(total);
        simulationMs += tickMs;
        worstTickMs = Math.Max(worstTickMs, tickMs);
    }
    // =========================================================
    private void UpdateOverlay()
    {
        double divisor = Math.Max(1, measuredFrames);
        string costPerTick = measuredTicks > 0 ? $"{simulationMs / measuredTicks:F3} ms" : "N/A";
        string separationPerTick = measuredTicks > 0 ? $"{separationMs / measuredTicks:F3} ms" : "N/A";
        bool separating = (panel.Separation == SeparationMode.Normal || panel.StrongEnemySeparation) && units?.Alive > 1 && !spawning && mode != BenchmarkMode.RenderingOnly;
        int overlapPairs = separating ? separation?.OverlapPairs ?? 0 : 0;
        int separationCaps = separating ? separation?.BudgetHits ?? 0 : 0;
        var viewport = GetViewport().GetViewportRid();
        double renderCpu = RenderingServer.ViewportGetMeasuredRenderTimeCpu(viewport);
        double renderGpu = RenderingServer.ViewportGetMeasuredRenderTimeGpu(viewport);
        double syncDivisor = Math.Max(1, measuredSyncs);
        string separationSamples = separating && separation != null
            ? $"{separation.MeanPenetration:F3} / {separation.MaxPenetration:F3}"
            : "N/A";
        string yieldStats = separating && panel.AlliedYield && separation != null
            ? $"Yield skip / push / exit  {separation.YieldSkipped:N0} / {separation.YieldPushed:N0} / {separation.ExitCapped:N0}\n"
            : "";
        if (panel.Separation == SeparationMode.Hard && hardSeparation?.Ready == true)
            yieldStats = $"Hard separation blocked moves  {hardSeparation.BlockedMoves:N0}\n";
        panel.Summary.Refresh(
            $"BENCH0 · {(painter.Visible ? "DEPLOYMENT" : paused ? "PAUSED" : spawning ? "PREPARING" : mode.ToString())}",
            units, projectiles?.Count ?? 0);

        double achievedRate = hudClock > 0 ? measuredTicks / hudClock : 0;
        double targetingTickToMsPerFrame = 1000.0 / Stopwatch.Frequency / divisor;
        string PursuitLine(string label, PursuitReason reason, bool showChanges = false)
        {
            PursuitSample sample = grid?.PursuitTiming(reason) ?? default;
            double msPerTick = sample.SearchTicks * 1000.0 / Stopwatch.Frequency / Math.Max(1, measuredTicks);
            double queriesPerSecond = hudClock > 0 ? sample.Queries / hudClock : 0;
            string average = sample.Queries == 0 ? "—" : $"{sample.SearchTicks * 1_000_000.0 / Stopwatch.Frequency / sample.Queries:F1} µs/query";
            string changes = showChanges && sample.Queries > 0
                ? $" · {(double)sample.ChangedTargets / sample.Queries:P0} changed" : "";
            return $"{label}\n{queriesPerSecond:N0}/s · {msPerTick:F3} ms/tick\n{average}{changes}\n";
        }

        string ScheduledLine(string label, ScheduledPursuitCause cause)
        {
            PursuitSample sample = grid?.ScheduledPursuitTiming(cause) ?? default;
            double queriesPerSecond = hudClock > 0 ? sample.Queries / hudClock : 0;
            double msPerTick = sample.SearchTicks * 1000.0 / Stopwatch.Frequency / Math.Max(1, measuredTicks);
            return $"  {label}  {queriesPerSecond:N0}/s · {msPerTick:F3} ms/tick\n";
        }

        int pursuitQueries = (grid?.PursuitTiming(PursuitReason.LiveRefresh).Queries ?? 0) +
            (grid?.PursuitTiming(PursuitReason.ScheduledAcquisition).Queries ?? 0) +
            (grid?.PursuitTiming(PursuitReason.ExtraReplacement).Queries ?? 0);
        string shieldStats = units?.Shields is { } shields
            ? $"\nShields · {shields.SourceCount} emitters · {shields.ShieldedCount} shielded\n" +
              $"Last coverage / update  {shields.CoverageMilliseconds:F3} / {shields.UpdateMilliseconds:F3} ms\n" +
              $"Visible shells  {armyRenderer?.ShieldVisualCount ?? 0} · sync {(armyRenderer?.ShieldVisualMilliseconds ?? 0):F3} ms\n"
            : "";
        panel.CpuMetrics.Text = shieldStats +
            $"Simulation rate  {achievedRate:F1} / {1f / Config.TickSeconds:F0} Hz\nSimulation / tick  {costPerTick}\nWorst tick  {worstTickMs:F2} ms\nSeparation / tick  {separationPerTick}\n" +
            $"Ticks / frame  {measuredTicks / divisor:F2} · peak {maxTicksInFrame}\nSkipped ticks  {skippedSteps:N0}\n\n" +
            $"CPU time / rendered frame (ms)\nSimulation  {simulationMs / divisor:F3}\nMovement  {movementMs / divisor:F3}\n" +
            MovementBreakdown() +
            $"Separation  {separationMs / divisor:F3}\nGrid rebuild  {gridMs / divisor:F3}\nTargeting  {targetingMs / divisor:F3}\n" +
            $"  Local enemy search  {(grid?.LocalSearchTicks ?? 0) * targetingTickToMsPerFrame:F3}\n" +
            $"  Long-range pursuit  {(grid?.PursuitSearchTicks ?? 0) * targetingTickToMsPerFrame:F3}\n" +
            $"  Pursuit tree build  {(grid?.PursuitBuildTicks ?? 0) * targetingTickToMsPerFrame:F3}\n" +
            "\nPursuit search breakdown\n" +
            PursuitLine("Live refresh", PursuitReason.LiveRefresh, showChanges: true) +
            PursuitLine("Scheduled acquisition", PursuitReason.ScheduledAcquisition) +
            PursuitLine("Extra replacement", PursuitReason.ExtraReplacement) + "\n" +
            "Scheduled acquisition causes\n" +
            ScheduledLine("No previous target", ScheduledPursuitCause.NoPreviousTarget) +
            ScheduledLine("Melee near loss", ScheduledPursuitCause.MeleeNearLoss) +
            ScheduledLine("Melee far loss", ScheduledPursuitCause.MeleeFarLoss) +
            ScheduledLine("Ranged near loss", ScheduledPursuitCause.RangedNearLoss) +
            ScheduledLine("Ranged far loss", ScheduledPursuitCause.RangedFarLoss) +
            $"Replacement budget exhausted  {grid?.ReplacementBudgetExhaustedSteps ?? 0:N0} steps\n" +
            $"Eligible replacements deferred  {grid?.DeferredReplacements ?? 0:N0}\n" +
            $"Pursuit deferred by shared cap  {grid?.PursuitBudgetDeferrals ?? 0:N0}\n" +
            $"Pursuit deferred by melee delay  {grid?.PursuitDelayDeferrals ?? 0:N0}\n" +
            $"Pursuit results in local range  {grid?.NearbyPursuitResults ?? 0:N0} / {pursuitQueries:N0}\n" +
            $"  Without local search that step  {grid?.NearbyPursuitResultsWithoutLocalSearch ?? 0:N0}\n\n" +
            $"Combat  {combatMs / divisor:F3}\nProjectiles  {projectileMs / divisor:F3}\n\n" +
            $"Query caps  {grid?.LimitedQueries ?? 0:N0}\nProjectile cap drops  {projectiles?.Dropped ?? 0:N0}\n" +
            $"Separation pairs / caps  {overlapPairs:N0} / {separationCaps:N0}\n" +
            $"Candidates / unit / pass  {(separating ? separation?.CandidatesPerSoldier ?? 0f : 0f):F1}\n" +
            $"Penetration avg / max  {separationSamples}\n" + yieldStats;

        string obstacleStats = obstacles == null ? "No active walls.\n" :
            $"Walls  {obstacles.RectangleCount}\nLOS checks / blocks  {obstacles.LosQueries:N0} / {obstacles.LosBlocks:N0}\nClipped moves  {obstacles.ClippedMoves:N0}\n";
        if (navigation != null)
        {
            obstacleStats += $"Route build  {routeBuildMs / divisor:F3} ms/frame\n" +
                $"Route cache hit / miss  {navigation.CacheHits:N0} / {navigation.CacheMisses:N0}\n" +
                $"Routes built / queued  {navigation.RoutesBuilt:N0} / {navigation.QueueDepth:N0}\n" +
                $"Longest route wait  {navigation.MaxWaitFrames} frames\n" +
                $"Route waits / cached stalls / queue full  {navigation.RouteWaits:N0} / {navigation.CachedRouteStalls:N0} / {navigation.QueueFullWaits:N0}\n" +
                (navigation.LastIndividualFailure.Length > 0 ? $"Last failed individual route  {navigation.LastIndividualFailure}\n" : "") +
                $"Corridor results reused  {navigation.CorridorReuses:N0}\n" +
                $"Route waypoints reused  {navigation.WaypointReuses:N0}\n";
        }
        if (obstacles != null && panel.ProfileObstacles)
        {
            obstacleStats += "\nCalls / HUD interval · sampled µs/call\n" +
                QueryLine("Corridor", obstacles.Sample(ObstacleField.Query.Corridor)) +
                QueryLine("Move clip", obstacles.Sample(ObstacleField.Query.Clip)) +
                QueryLine("First-hit sweep", obstacles.Sample(ObstacleField.Query.Sweep)) +
                QueryLine("Line of sight", obstacles.Sample(ObstacleField.Query.Sight)) +
                QueryLine("Circle overlap", obstacles.Sample(ObstacleField.Query.Circle));
        }
        if (terrain != null)
        {
            obstacleStats += $"\nPlateau  {terrain.ChunkCount} chunks\n" +
                $"Samples / tick  {groundSamples / (double)Math.Max(1, measuredTicks):N0}\n" +
                $"Grounding  {groundingMs / Math.Max(1, measuredTicks):F3} ms/tick\n" +
                $"Setup  {terrainCompileMs:F2} + {terrainMeshMs:F2} ms";
        }
        panel.ObstacleMetrics.Text = obstacleStats.TrimEnd();

        panel.EngineMetrics.Text =
            $"Render sync  {renderMs / divisor:F3} ms/frame\n" +
            $"Render sync / tick  {renderMs / Math.Max(1, measuredTicks):F3} ms\n" +
            $"Army upload / sync  {armyUploadMs / syncDivisor:F3} ms\n" +
            $"Dense / sparse / writes per sync\n{denseUploads / syncDivisor:F1} / {sparseUploads / syncDivisor:F1} / {changedWrites / syncDivisor:F0}\n\n" +
            $"Viewport CPU  {ViewportTime(renderCpu)}\nViewport GPU  {ViewportTime(renderGpu)}\n" +
            $"Draw calls  {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame):F0}\n" +
            $"Rendered objects  {Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame):F0}\n" +
            $"Soldier instances  {armyRenderer?.InstanceCount ?? 0:N0}\n" +
            $"Managed heap  {GC.GetTotalMemory(false) / 1048576.0:F1} MiB\nStatic memory  {StaticMemory()}";
    }

    private static string QueryLine(string label, ObstacleField.QuerySample sample) =>
        $"{label}  {sample.Calls:N0} · {(sample.Timed > 0 ? $"{sample.Microseconds:F2}" : "—")}\n";

    private static float ClampFiniteSetting(float value, float max, float fallback) =>
        float.IsFinite(value) ? Mathf.Clamp(value, 0, max) : fallback;

    private static string ViewportTime(double milliseconds)
    {
        return double.IsFinite(milliseconds) && milliseconds > 0 ? $"{milliseconds:F2} ms" : "N/A";
    }
    private static string StaticMemory()
    {
        double bytes = Performance.GetMonitor(Performance.Monitor.MemoryStatic);
        return bytes > 0 ? $"{bytes / 1048576.0:F1} MiB" : "N/A";
    }
    private string MovementBreakdown()
    {
        if (!panel.ProfileMovement) return "";
        int ticks = movementProfile.Ticks;
        if (ticks == 0) return "Movement breakdown: waiting for simulation ticks\n\n";
        double sampleToMsPerTick = 1000.0 * MovementProfile.SampleStride / Stopwatch.Frequency / ticks;
        double sidestep = movementProfile.SidestepTicks * sampleToMsPerTick;
        double hold = movementProfile.HoldTicks * sampleToMsPerTick;
        double runUp = movementProfile.RunUpMilliseconds / ticks;
        double remaining = Math.Max(0, movementProfile.TotalMilliseconds / ticks - sidestep - hold - runUp);
        return "\nMovement breakdown (ms/tick)\n" +
            $"Melee sidestep  ~{sidestep:F3}\n" +
            $"  Sampled calls / blocked  {movementProfile.SidestepCalls:N0} / {movementProfile.BlockedSidesteps:N0}\n" +
            $"Melee contact hold  ~{hold:F3}\n" +
            $"Cavalry run-up  {runUp:F3}\n" +
            $"Remaining movement  ~{remaining:F3}\n\n";
    }

    private void ResetTimings()
    {
        movementProfile = default;
        groundSamples = 0;
        groundingMs = 0;
        movementMs = separationMs = gridMs = targetingMs = combatMs = projectileMs = simulationMs = renderMs = 0;
        worstTickMs = armyUploadMs = routeBuildMs = 0;
        grid?.ResetTargetingTimings();
        obstacles?.ResetCounters();
        navigation?.ResetCounters();
        measuredFrames = measuredTicks = maxTicksInFrame = measuredSyncs = denseUploads = sparseUploads = changedWrites = 0;
    }
}
