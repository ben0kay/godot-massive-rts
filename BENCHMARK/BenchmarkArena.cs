// Owns BENCH0 lifecycle and fixed-step orchestration; shared systems remain independent.
using System;
using System.Diagnostics;
using Godot;
namespace MassiveRts;

public partial class BenchmarkArena : Node3D
{
    [Export] public BenchmarkConfig Config { get; set; } = new();
    [Export] public UnitCatalog SoldierCatalog { get; set; }
    private RuntimeUnitCatalog catalog;
    private float projectileLifetime;
    private Mesh[] soldierMeshes;
    private Mesh soldierShadow;
    private UnitState units;
    private SpatialGrid grid;
    private SeparationSystem separation;
    private MeleeSteering meleeSteering;
    private TrampleSystem trample;
    private byte[] voluntaryMovement;
    private AlliedYieldSettings alliedYieldSettings;
    private ProjectileSystem projectiles;
    private ArmyRenderer armyRenderer;
    private ProjectileRenderer projectileRenderer;
    private ShockwaveRenderer shockwaves;
    private IsometricCamera camera;
    private BenchmarkPanel panel;
    private ArmyBalanceHud armyBalanceHud;
    private DeploymentFactory deployment;
    private int autoAttackCount;
    private GuardGroup[] guards;
    private int[] autoAttackIds;
    private DeploymentPainter painter;
    private RuntimeBuildingCatalog buildings;
    private DefenseSystem defenseSystem;
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
    private readonly double[] frames = new double[120];
    private int frameCursor, frameSamples;
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
        catalog = (SoldierCatalog ?? GD.Load<UnitCatalog>("res://UNITS/CATALOG/DefaultUnitCatalog.tres")).Compile(movementScale);
        // Keep the distance cap implied by the authored projectile lifetime.
        projectileLifetime = Config.ProjectileLifetime / movementScale;
        soldierMeshes = FacetedSoldierMeshes.Create(catalog);
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
        flatGround = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(5000, 5000),
            Material = new StandardMaterial3D { AlbedoColor = new Color(0.29f, 0.30f, 0.32f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } } };
        AddChild(flatGround);
        camera = new IsometricCamera(); AddChild(camera); camera.Current = true;
        buildings = new RuntimeBuildingCatalog(GD.Load<BuildingCatalog>("res://BUILDINGS/CATALOG/DefaultBuildingCatalog.tres"));
        panel = new BenchmarkPanel { Catalog = catalog, Buildings = buildings }; AddChild(panel);
        panel.CameraRotationChanged += camera.SetYawDegrees;
        // Keep the gameplay bar separate from BENCHMARK's developer panels.
        var gameplayHudLayer = new CanvasLayer { Layer = 2 };
        AddChild(gameplayHudLayer);
        armyBalanceHud = GD.Load<PackedScene>("res://UI/GameplayHUD/ArmyBalanceHud.tscn").Instantiate<ArmyBalanceHud>();
        gameplayHudLayer.AddChild(armyBalanceHud);
        Config.ObstacleHeight = float.IsFinite(Config.ObstacleHeight) ? Mathf.Clamp(Config.ObstacleHeight, 0.2f, 8f) : 1f;
        Config.RouteSectorSize = float.IsFinite(Config.RouteSectorSize) ? Mathf.Clamp(Config.RouteSectorSize, 4f, 32f) : 12f;
        painter = new DeploymentPainter
        {
            Camera = camera, Visible = false, ObstacleHeight = Config.ObstacleHeight, ObstacleTint = Config.ObstacleTint
        };
        AddChild(painter);
        painter.Initialize(catalog, soldierMeshes, buildings);
        painter.Changed += UpdateDeploymentStatus;
        painter.EditRejected += message => panel.Status.Text = message + "\n" + DeploymentCount();
        panel.PaintToolChoice.ItemSelected += painter.SetPaintTool;
        panel.DefenseChoice.ItemSelected += index => painter.DefenseType = panel.DefenseChoice.GetItemId((int)index);
        battleWalls = new MeshInstance3D { CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(battleWalls);
        battleObstacleShadows = new MeshInstance3D { CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(battleObstacleShadows);
        panel.GroundShadowsChanged += enabled =>
        {
            battleObstacleShadows.Visible = enabled;
            armyRenderer?.SetShadowsVisible(enabled);
        };
        panel.SpawnRequested += Spawn;
        panel.ClearRequested += () => { if (painter.Visible) painter.Clear(); else Clear(); };
        panel.PaintRequested += EnterDeployment;
        panel.BattleRequested += StartPaintedBattle;
        panel.SaveRequested += () => panel.Status.Text = painter.Save();
        panel.LoadRequested += () => panel.Status.Text = painter.Load() + "\n" + DeploymentCount();
        panel.BrushChoice.ItemSelected += index => painter.BrushType = (byte)panel.BrushChoice.GetItemId((int)index);
        panel.SoldierBrushSizeChoice.ItemSelected += index =>
            painter.SoldierBrushSize = panel.SoldierBrushSizeChoice.GetItemId((int)index);
        painter.SoldierBrushSize = panel.SoldierBrushSizeChoice.GetItemId(panel.SoldierBrushSizeChoice.Selected);
        panel.FactionChoice.ItemSelected += index => painter.BrushFaction = (byte)panel.FactionChoice.GetItemId((int)index);
        panel.PauseRequested += () => { paused = !paused; panel.PauseButton.Text = paused ? "Resume" : "Pause"; };
        shockwaves = new ShockwaveRenderer();
        AddChild(shockwaves);
        Spawn();
    }
    // =========================================================
    private void Clear()
    {
        armyBalanceHud?.Reset();
        guards = null;
        autoAttackIds = null;
        autoAttackCount = 0;
        shockwaves.Clear();
        defenseSystem = null;
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
        flatGround.Visible = true;
        if (armyRenderer != null) { RemoveChild(armyRenderer); armyRenderer.QueueFree(); armyRenderer = null; }
        if (projectileRenderer != null) { RemoveChild(projectileRenderer); projectileRenderer.QueueFree(); projectileRenderer = null; }
        obstacles = null; navigation = null; battleWalls.Mesh = null; battleObstacleShadows.Mesh = null;
        meleeSteering = null;
        units = null; projectiles = null; grid = null; separation = null; trample = null; voluntaryMovement = null; deployment = null; spawning = false;
        targetingAccumulator = 0; targetingStep = 0;
        accumulator = 0; tick = 0; preparedTargets = 0; skippedSteps = 0; paused = false; panel.PauseButton.Text = "Pause";
        timeSinceArmySync = 0; smoothingWasEnabled = false;
        frameSamples = 0; frameCursor = 0; ResetTimings(); panel.Status.Text = "Cleared";
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
        flatGround.Visible = false;
        terrainMeshMs = Elapsed(start);
    }

    private UnitState CreateUnitState()
    {
        battleSeed = (uint)panel.Seed.Value;
        var feedback = mode is BenchmarkMode.Melee or BenchmarkMode.Ranged
            ? new HitFeedbackSystem(targetCount, Config.HitFeedback ?? new HitFeedbackSettings())
            : null;
        var recoil = mode == BenchmarkMode.Ranged && catalog.HasRangedRecoil
            ? new RangedRecoilSystem(targetCount) : null;
        return new UnitState(targetCount, catalog, feedback, terrain, recoil);
    }

    private void SetDeploymentEditing(bool editing)
    {
        painter.CancelDrag();
        painter.Visible = editing;
        panel.EditingDeployment = editing;
    }

    private string DeploymentCount() => $"Painted {painter.Layout.Placements.Count:N0} • " +
        $"{painter.Layout.Obstacles.Field?.RectangleCount ?? 0} wall rectangles • {painter.Layout.Defenses.Count} defenses";

    private void SetBattleObstacles(ObstacleField field, Mesh mesh)
    {
        obstacles = field;
        obstacles?.ResetCounters();
        battleWalls.Mesh = mesh;
        battleObstacleShadows.Mesh = GroundShadowMeshes.Obstacles(field, Config.ObstacleHeight);
    }
    private void UpdateDeploymentStatus() => panel.Status.Text = DeploymentCount();

    private void EnterDeployment()
    {
        if (painter.Visible) return;
        Clear();
        SetDeploymentEditing(true);
        camera.Frame(100);
        UpdateDeploymentStatus();
    }

    private void StartPaintedBattle()
    {
        var placements = painter.Layout.Placements;
        int factionMask = 0;
        bool ranged = false;
        foreach (SoldierPlacement placement in placements)
        {
            factionMask |= 1 << placement.Faction;
            ranged |= catalog[placement.Type].Ranged;
        }
        foreach (var defense in painter.Layout.Defenses) factionMask |= 1 << defense.Faction;
        if (placements.Count == 0 || System.Numerics.BitOperations.PopCount((uint)factionMask) < 2)
        {
            panel.Status.Text = "Place soldiers and at least two teams.\n" + DeploymentCount();
            return;
        }
        var orders = new GuardOrder[Factions.Count];
        for (int faction = 0; faction < orders.Length; faction++) orders[faction] = panel.OrderFor(faction);
        var snapshot = new SoldierPlacement[placements.Count];
        int attackerCount = 0;
        foreach (SoldierPlacement placement in placements)
            if (orders[placement.Faction] == GuardOrder.AutoAttack)
                snapshot[attackerCount++] = placement;

        Clear();
        SetDeploymentEditing(false);
        requested = targetCount = snapshot.Length;
        autoAttackCount = attackerCount;
        if (attackerCount < snapshot.Length)
        {
            guards = new GuardGroup[Factions.Count];
            int next = attackerCount;
            for (int faction = 0; faction < Factions.Count; faction++)
            {
                GuardOrder order = orders[faction];
                if (order == GuardOrder.AutoAttack) continue;
                int start = next;
                foreach (SoldierPlacement placement in placements)
                    if (placement.Faction == faction) snapshot[next++] = placement;
                if (start < next)
                    guards[faction] = new GuardGroup(start, next, snapshot, (float)panel.GuardRadius.Value, order);
            }
        }
        mode = ranged ? BenchmarkMode.Ranged : BenchmarkMode.Melee;
        SetBattleObstacles(painter.Layout.CollisionField, painter.ObstacleMesh);
        if (painter.Layout.Defenses.Count > 0)
        {
            defenseRenderer = new DefenseRenderer();
            AddChild(defenseRenderer);
            defenseRenderer.Initialize(painter.Layout.Defenses, buildings, Config.ObstacleHeight, battle: true);
            defenseSystem = new DefenseSystem(painter.Layout.Defenses, buildings, Config.ObstacleHeight, defenseRenderer);
        }
        units = CreateUnitState();
        grid = new SpatialGrid(targetCount, Config.CellSize, profileTargeting: true);
        projectiles = new ProjectileSystem(Config.MaxProjectiles, catalog.Projectiles, projectileLifetime);
        deployment = new DeploymentFactory(snapshot, (ulong)panel.Seed.Value);
        spawning = true;
    }

    // =========================================================
    public override void _Process(double delta)
    {
        if (obstacles != null) obstacles.ProfileQueries = panel.ProfileObstacles;
        if (navigation != null) navigation.ReuseCorridors = panel.ReuseCorridors;
        frames[frameCursor] = delta * 1000; frameCursor = (frameCursor + 1) % frames.Length; frameSamples = Math.Min(frameSamples + 1, frames.Length);
        if (!paused)
        {
            shockwaves.Advance((float)delta);
            defenseRenderer?.Advance((float)delta);
        }
        if (spawning)
        {
            if (units.Count < targetCount)
            {
                int first = units.Count;
                deployment.Append(units, Config.SpawnPerFrame);
                if (obstacles != null && !ValidateSpawn(first))
                {
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
                    navigation = new ObstacleNavigation(obstacles, catalog, units.Count, Config.RouteSectorSize,
                        Mathf.Clamp(Config.RouteCacheCapacity, 64, 8192), Mathf.Clamp(Config.RoutesPerFrame, 1, 64));
                armyRenderer = new ArmyRenderer(); AddChild(armyRenderer); armyRenderer.Initialize(units, soldierMeshes, soldierShadow, panel.GroundShadows);
                projectileRenderer = new ProjectileRenderer(); AddChild(projectileRenderer); projectileRenderer.Initialize(Config.MaxProjectiles, catalog.Projectiles);
                spawning = false; panel.Status.Text = $"Requested {requested:N0}\nSpawned {units.Count:N0} • {mode}";
                armyBalanceHud.BeginBattle(units);
                frameSamples = 0; frameCursor = 0;
            }
        }
        else if (units != null && !paused && mode != BenchmarkMode.RenderingOnly && (units.Alive > 0 || projectiles.Count > 0))
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
            bool smooth = panel.SmoothMotion;
            if (!smooth && smoothingWasEnabled) armyRenderer.ClearPoseHistory();
            accumulator += delta;
            timeSinceArmySync += delta;
            int steps = 0;
            while (accumulator >= Config.TickSeconds && steps < Config.MaxStepsPerFrame)
            {
                if (smooth)
                {
                    long snapshotStart = Stamp();
                    armyRenderer.CapturePose();
                    renderMs += Elapsed(snapshotStart);
                }
                Step(Config.TickSeconds);
                accumulator -= Config.TickSeconds;
                steps++;
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
                float blend = smooth ? (float)Math.Clamp(accumulator / Config.TickSeconds, 0.0, 1.0) : 1f;
                armyRenderer.Sync(blend);
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
        }
        shockwaves.Sync();
        measuredFrames++; hudClock += delta;
        if (hudClock >= 0.3) { armyBalanceHud?.RefreshCounts(); UpdateOverlay(); hudClock = 0; ResetTimings(); }
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
        float edge = DeploymentLayout.HalfWidth * DeploymentLayout.CellSize;
        return Mathf.Abs(position.X) + radius <= edge && Mathf.Abs(position.Z) + radius <= edge &&
            !obstacles.IntersectsCircle(ObstacleField.Xz(position), radius);
    }

    private void AcquireGuards(int acquisitionStep)
    {
        bool anyGuarding = false, promoted = false;
        for (int faction = 0; faction < guards.Length; faction++)
        {
            GuardGroup group = guards[faction];
            if (group == null) continue;
            if (!group.Acquire(units, grid, Config, acquisitionStep, targetingStepSeconds, obstacles))
            {
                anyGuarding = true;
                continue;
            }

            if (autoAttackIds == null)
            {
                autoAttackIds = new int[units.Count];
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

    private void Step(float dt)
    {
        long total = Stamp(), start;
        if (mode == BenchmarkMode.Melee || mode == BenchmarkMode.Ranged)
        {
            start = Stamp(); grid.Rebuild(units); gridMs += Elapsed(start);
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
                CombatSystem.Acquire(units, grid, Config, acquisitionStep, targetingStepSeconds, obstacles,
                    panel.RangedTargetVariety, battleSeed, panel.SlowPursuitRefresh,
                    panel.DelayFarMeleeRetarget, panel.LimitPursuitQueries, autoAttackCount, autoAttackIds);
                if (guards != null) AcquireGuards(acquisitionStep);
                targetingMs += Elapsed(start);
            }
            start = Stamp();
            units.HitFeedback?.Advance(units, dt);
            units.RangedRecoil?.Advance(units, dt);
            defenseSystem?.Step(units, grid, obstacles, dt, acquisitionStep, targetingStepSeconds);
            combatMs += Elapsed(start);
        }
        SeparationMode separationMode = panel.Separation;
        bool separate = separationMode != SeparationMode.Off && units.Alive > 1;
        bool profileMovement = panel.ProfileMovement;
        int samplePhase = profileMovement ? tick % MovementProfile.SampleStride : -1;
        start = Stamp();
        trample?.BeginTick(units, dt, obstacles);
        byte[] movementFlags = separate && panel.AlliedYield
            ? (voluntaryMovement ??= new byte[units.Position.Length]) : null;
        bool holdMelee = grid != null && panel.MeleeHold;
        bool sidestepMelee = grid != null && separate && panel.MeleeSidestep;
        if (!holdMelee && !sidestepMelee) meleeSteering = null;
        else if (meleeSteering == null || !meleeSteering.Matches(holdMelee, sidestepMelee))
            meleeSteering = new MeleeSteering(units.Count, holdMelee, sidestepMelee);
        MovementSystem.Step(units, mode, dt, separate ? Config.SeparationGap : -1f, ref movementProfile, samplePhase, movementFlags,
            Config.MeleeAccelerationSeconds, Config.RangedAccelerationSeconds, trample, obstacles, navigation, grid, meleeSteering, autoAttackCount, autoAttackIds);
        if (guards != null)
            foreach (GuardGroup group in guards)
                group?.Move(units, dt, separate ? Config.SeparationGap : 0f, obstacles, navigation, movementFlags);
        float tramplePadding = trample?.MoveCharges(units, grid, dt, obstacles) ?? 0f;
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
            separation ??= new SeparationSystem(units.Position.Length);
            separationPadding = separationPushPerStep;
            separation.Step(units, separationMode == SeparationMode.Strong ? 2 : 1, Config.SeparationGap,
                Config.SeparationNeighbourBudget, separationPadding, tick, movementFlags, alliedYieldSettings, trample, obstacles, panel.WeightedSeparation);
            separationMs += Elapsed(start);
        }
        if (trample != null)
        {
            start = Stamp();
            trample.MeasureRunUp(units, dt);
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
        float extraMovementPadding = separationPadding + tramplePadding;
        if (mode == BenchmarkMode.Melee || mode == BenchmarkMode.Ranged)
        {
            start = Stamp();
            CombatSystem.Step(units, grid, projectiles, (float)panel.FireRate.Value, dt, extraMovementPadding, separate ? Config.SeparationGap : 0f, trample, obstacles, shockwaves); combatMs += Elapsed(start);
            if (mode == BenchmarkMode.Ranged) { start = Stamp(); projectiles.Step(units, grid, Config, dt, extraMovementPadding, obstacles, shockwaves); projectileMs += Elapsed(start); }
        }
        tick++;
        double tickMs = Elapsed(total);
        simulationMs += tickMs;
        worstTickMs = Math.Max(worstTickMs, tickMs);
    }
    // =========================================================
    private void UpdateOverlay()
    {
        double sum = 0, worst = 0;
        for (int i = 0; i < frameSamples; i++) { sum += frames[i]; worst = Math.Max(worst, frames[i]); }
        double avg = frameSamples > 0 ? sum / frameSamples : 0, divisor = Math.Max(1, measuredFrames);
        string costPerTick = measuredTicks > 0 ? $"{simulationMs / measuredTicks:F3} ms" : "N/A";
        string separationPerTick = measuredTicks > 0 ? $"{separationMs / measuredTicks:F3} ms" : "N/A";
        bool separating = panel.Separation != SeparationMode.Off && units?.Alive > 1 && !spawning && mode != BenchmarkMode.RenderingOnly;
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
        panel.Metrics.Text =
            $"BENCH0 · {(painter.Visible ? "DEPLOYMENT" : paused ? "PAUSED" : spawning ? "PREPARING" : mode.ToString())}\n" +
            $"{Engine.GetFramesPerSecond():F0} FPS · {avg:F2} ms avg\nWorst frame  {worst:F2} ms\n" +
            $"Alive  {units?.Alive ?? 0:N0} / {units?.Count ?? 0:N0}\n" +
            $"Blue {units?.FactionAlive[Factions.Blue] ?? 0:N0} · Red {units?.FactionAlive[Factions.Red] ?? 0:N0} · Green {units?.FactionAlive[Factions.Green] ?? 0:N0}\n" +
            $"Projectiles  {projectiles?.Count ?? 0:N0}";

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
        panel.CpuMetrics.Text =
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
                $"Corridor results reused  {navigation.CorridorReuses:N0}\n";
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
