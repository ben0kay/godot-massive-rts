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
    private Mesh[] soldierMeshes;
    private UnitState units;
    private SpatialGrid grid;
    private SeparationSystem separation;
    private byte[] voluntaryMovement;
    private AlliedYieldSettings alliedYieldSettings;
    private ProjectileSystem projectiles;
    private ArmyRenderer armyRenderer;
    private ProjectileRenderer projectileRenderer;
    private IsometricCamera camera;
    private BenchmarkPanel panel;
    private DeploymentFactory deployment;
    private DeploymentPainter painter;
    private BenchmarkMode mode;
    private const int InitialTargetsPerFrame = 512;
    private int requested, targetCount, tick, preparedTargets;
    private bool spawning, paused;
    private double accumulator, hudClock;
    private long skippedSteps;
    private readonly double[] frames = new double[120];
    private int frameCursor, frameSamples;
    private double movementMs, separationMs, gridMs, targetingMs, combatMs, projectileMs, simulationMs, renderMs;
    private int measuredFrames, measuredTicks, maxTicksInFrame;
    private int measuredSyncs, denseUploads, sparseUploads, changedWrites;
    private double worstTickMs, armyUploadMs;
    private static long Stamp() => Stopwatch.GetTimestamp();
    private static double Elapsed(long start) => (Stamp() - start) * 1000.0 / Stopwatch.Frequency;
    // =========================================================
    public override void _Ready()
    {
        catalog = (SoldierCatalog ?? GD.Load<UnitCatalog>("res://UNITS/CATALOG/DefaultUnitCatalog.tres")).Compile();
        soldierMeshes = FacetedSoldierMeshes.Create(catalog);
        RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
        Config.MaxSoldiers = Mathf.Clamp(Config.MaxSoldiers, 1000, 100000);
        Config.SpawnPerFrame = Mathf.Clamp(Config.SpawnPerFrame, 100, 10000);
        Config.TickSeconds = Mathf.Clamp(Config.TickSeconds, 1f / 120, 0.1f);
        Config.MaxStepsPerFrame = Mathf.Clamp(Config.MaxStepsPerFrame, 1, 5);
        Config.TargetRefreshTicks = Mathf.Clamp(Config.TargetRefreshTicks, 1, 60);
        Config.QueryCandidateLimit = Mathf.Clamp(Config.QueryCandidateLimit, 8, 1024);
        Config.CellSize = Mathf.Clamp(Config.CellSize, 2, 20);
        Config.SeparationNeighbourBudget = Mathf.Clamp(Config.SeparationNeighbourBudget, 1, 128);
        Config.SeparationMaxPushPerTick = float.IsFinite(Config.SeparationMaxPushPerTick)
            ? Mathf.Clamp(Config.SeparationMaxPushPerTick, 0.01f, 1f) : 0.35f;
        // Leave a small margin inside melee reach at the requested body spacing.
        float maximumGap = 0.25f;
        for (int i = 0; i < catalog.Count; i++)
        {
            ref readonly UnitStats type = ref catalog[i];
            if (!type.Ranged) maximumGap = Mathf.Min(maximumGap, Mathf.Max(0, type.Reach - type.Footprint - 0.02f));
        }
        Config.SeparationGap = Mathf.Clamp(float.IsFinite(Config.SeparationGap) ? Config.SeparationGap : 0.05f, 0, maximumGap);
        alliedYieldSettings = new AlliedYieldSettings(
            ClampYieldSetting(Config.AlliedRangedClearanceFraction, 1f, 0.5f),
            ClampYieldSetting(Config.AlliedRangedYieldPairCap, 0.25f, 0.06f),
            ClampYieldSetting(Config.AlliedRangedYieldMeleeShare, 1f, 0.7f),
            ClampYieldSetting(Config.AlliedRangedExitPairCap, 0.25f, 0.06f));
        Config.MaxProjectiles = Mathf.Clamp(Config.MaxProjectiles, 100, 100000);
        var ground = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(5000, 5000),
            Material = new StandardMaterial3D { AlbedoColor = new Color(0.075f, 0.085f, 0.10f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } } };
        AddChild(ground);
        camera = new IsometricCamera(); AddChild(camera); camera.Current = true;
        panel = new BenchmarkPanel { Catalog = catalog }; AddChild(panel);
        painter = new DeploymentPainter { Camera = camera, Visible = false };
        AddChild(painter);
        painter.Initialize(catalog, soldierMeshes);
        painter.Changed += UpdateDeploymentStatus;
        panel.SpawnRequested += Spawn;
        panel.ClearRequested += () => { if (painter.Visible) painter.Clear(); else Clear(); };
        panel.PaintRequested += EnterDeployment;
        panel.BattleRequested += StartPaintedBattle;
        panel.SaveRequested += () => panel.Status.Text = painter.Save();
        panel.LoadRequested += () => panel.Status.Text = painter.Load() + "\n" + DeploymentCount();
        panel.BrushChoice.ItemSelected += index => painter.BrushType = (byte)panel.BrushChoice.GetItemId((int)index);
        panel.FactionChoice.ItemSelected += index => painter.BrushFaction = (byte)panel.FactionChoice.GetItemId((int)index);
        panel.CountChoice.ItemSelected += _ =>
        {
            if (!painter.Visible) return;
            painter.Limit = Math.Min(Config.MaxSoldiers, panel.RequestedCount);
            UpdateDeploymentStatus();
        };
        panel.PauseRequested += () => { paused = !paused; accumulator = 0; panel.PauseButton.Text = paused ? "Resume" : "Pause"; };
        Spawn();
    }
    // =========================================================
    private void Clear()
    {
        if (armyRenderer != null) { RemoveChild(armyRenderer); armyRenderer.QueueFree(); armyRenderer = null; }
        if (projectileRenderer != null) { RemoveChild(projectileRenderer); projectileRenderer.QueueFree(); projectileRenderer = null; }
        units = null; projectiles = null; grid = null; separation = null; voluntaryMovement = null; deployment = null; spawning = false;
        accumulator = 0; tick = 0; preparedTargets = 0; skippedSteps = 0; paused = false; panel.PauseButton.Text = "Pause";
        frameSamples = 0; frameCursor = 0; ResetTimings(); panel.Status.Text = "Cleared";
    }
    private void Spawn()
    {
        Clear();
        SetDeploymentEditing(false);
        requested = panel.RequestedCount; targetCount = Mathf.Min(requested, Config.MaxSoldiers);
        mode = (BenchmarkMode)panel.ModeChoice.Selected;
        units = new UnitState(targetCount, catalog);
        if (mode is BenchmarkMode.Melee or BenchmarkMode.Ranged) grid = new SpatialGrid(targetCount, Config.CellSize);
        projectiles = new ProjectileSystem(Config.MaxProjectiles, catalog.Projectiles);
        deployment = new DeploymentFactory(targetCount, (ulong)panel.Seed.Value, (Deployment)panel.LayoutChoice.Selected, mode, (float)panel.RangedPercent.Value / 100);
        spawning = true;
        camera.Frame(Mathf.Sqrt(targetCount / 2f) * 4 + (panel.LayoutChoice.Selected == 2 ? 350 : 80));
    }
    private void SetDeploymentEditing(bool editing)
    {
        painter.Visible = editing;
        panel.EditingDeployment = editing;
    }

    private string DeploymentCount() => $"Painted {painter.Layout.Placements.Count:N0} / {painter.Limit:N0}";
    private void UpdateDeploymentStatus() => panel.Status.Text = DeploymentCount();

    private void EnterDeployment()
    {
        if (painter.Visible) return;
        Clear();
        painter.Limit = Math.Min(Config.MaxSoldiers, panel.RequestedCount);
        SetDeploymentEditing(true);
        camera.Frame(100);
        UpdateDeploymentStatus();
    }

    private void StartPaintedBattle()
    {
        var placements = painter.Layout.Placements;
        bool blue = false, red = false, ranged = false;
        foreach (SoldierPlacement placement in placements)
        {
            blue |= placement.Faction == 0;
            red |= placement.Faction == 1;
            ranged |= catalog[placement.Type].Ranged;
        }
        if (!blue || !red || placements.Count > painter.Limit)
        {
            panel.Status.Text = "Place both armies within the troop limit.\n" + DeploymentCount();
            return;
        }
        var snapshot = new SoldierPlacement[placements.Count];
        for (int i = 0; i < snapshot.Length; i++) snapshot[i] = placements[i];
        Clear();
        SetDeploymentEditing(false);
        requested = targetCount = snapshot.Length;
        mode = ranged ? BenchmarkMode.Ranged : BenchmarkMode.Melee;
        units = new UnitState(targetCount, catalog);
        grid = new SpatialGrid(targetCount, Config.CellSize);
        projectiles = new ProjectileSystem(Config.MaxProjectiles, catalog.Projectiles);
        deployment = new DeploymentFactory(snapshot, (ulong)panel.Seed.Value);
        spawning = true;
    }

    // =========================================================
    public override void _Process(double delta)
    {
        frames[frameCursor] = delta * 1000; frameCursor = (frameCursor + 1) % frames.Length; frameSamples = Math.Min(frameSamples + 1, frames.Length);
        if (spawning)
        {
            if (units.Count < targetCount)
            {
                deployment.Append(units, Config.SpawnPerFrame);
                panel.Status.Text = $"Preparing {units.Count:N0} / {requested:N0} soldiers";
                if (units.Count < targetCount) return;
                grid?.Rebuild(units);
            }
            if (grid != null && preparedTargets < targetCount)
            {
                int end = Math.Min(targetCount, preparedTargets + InitialTargetsPerFrame);
                CombatSystem.PrepareTargets(units, grid, preparedTargets, end);
                preparedTargets = end;
                panel.Status.Text = $"Preparing targets {preparedTargets:N0} / {targetCount:N0}";
                if (preparedTargets < targetCount) return;
            }
            if (units.Count == targetCount)
            {
                armyRenderer = new ArmyRenderer(); AddChild(armyRenderer); armyRenderer.Initialize(units, soldierMeshes);
                projectileRenderer = new ProjectileRenderer(); AddChild(projectileRenderer); projectileRenderer.Initialize(Config.MaxProjectiles, catalog.Projectiles);
                spawning = false; panel.Status.Text = $"Requested {requested:N0}\nSpawned {units.Count:N0} • {mode}";
                frameSamples = 0; frameCursor = 0;
            }
        }
        else if (units != null && !paused && mode != BenchmarkMode.RenderingOnly && (units.Alive > 0 || projectiles.Count > 0))
        {
            accumulator += delta; int steps = 0;
            while (accumulator >= Config.TickSeconds && steps < Config.MaxStepsPerFrame)
            { Step(Config.TickSeconds); accumulator -= Config.TickSeconds; steps++; }
            if (accumulator >= Config.TickSeconds)
            { long skipped = (long)(accumulator / Config.TickSeconds); skippedSteps += skipped; accumulator -= skipped * Config.TickSeconds; }
            measuredTicks += steps;
            maxTicksInFrame = Math.Max(maxTicksInFrame, steps);
            if (steps > 0)
            {
                long start = Stamp();
                armyRenderer.Sync();
                projectileRenderer.Sync(projectiles);
                renderMs += Elapsed(start);
                measuredSyncs++;
                armyUploadMs += armyRenderer.UploadMilliseconds;
                denseUploads += armyRenderer.DenseUploads;
                sparseUploads += armyRenderer.SparseUploads;
                changedWrites += armyRenderer.ChangedWrites;
            }
        }
        measuredFrames++; hudClock += delta;
        if (hudClock >= 0.3) { UpdateOverlay(); hudClock = 0; ResetTimings(); }
    }
    // =========================================================
    private void Step(float dt)
    {
        long total = Stamp(), start;
        if (mode == BenchmarkMode.Melee || mode == BenchmarkMode.Ranged)
        {
            start = Stamp(); grid.Rebuild(units); gridMs += Elapsed(start);
            start = Stamp(); CombatSystem.Acquire(units, grid, Config, tick); targetingMs += Elapsed(start);
        }
        SeparationMode separationMode = panel.Separation;
        bool separate = separationMode != SeparationMode.Off && units.Alive > 1;
        start = Stamp();
        byte[] movementFlags = separate && panel.AlliedYield
            ? (voluntaryMovement ??= new byte[units.Position.Length]) : null;
        MovementSystem.Step(units, mode, dt, separate ? Config.SeparationGap : -1f, movementFlags);
        movementMs += Elapsed(start);
        float separationPadding = 0f;
        if (separate)
        {
            start = Stamp();
            separation ??= new SeparationSystem(units.Position.Length);
            separationPadding = Config.SeparationMaxPushPerTick;
            separation.Step(units, separationMode == SeparationMode.Strong ? 2 : 1, Config.SeparationGap,
                Config.SeparationNeighbourBudget, separationPadding, tick, movementFlags, alliedYieldSettings);
            separationMs += Elapsed(start);
        }
        if (mode == BenchmarkMode.Melee || mode == BenchmarkMode.Ranged)
        {
            start = Stamp(); CombatSystem.Step(units, projectiles, Config, (float)panel.FireRate.Value, dt); combatMs += Elapsed(start);
            if (mode == BenchmarkMode.Ranged) { start = Stamp(); projectiles.Step(units, grid, Config, dt, separationPadding); projectileMs += Elapsed(start); }
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
            ? $"{separation.MeanPenetration:F3} / {separation.MaxPenetration:F3} • {separation.CandidatesPerSoldier:F1} checks/unit/pass"
            : "N/A";
        string yieldStats = separating && panel.AlliedYield && separation != null
            ? $"Allied yield skip / push / exit: {separation.YieldSkipped:N0} / {separation.YieldPushed:N0} / {separation.ExitCapped:N0}\n"
            : "";
        panel.Metrics.Text = $"BENCH0 — {(painter.Visible ? "DEPLOYMENT" : paused ? "PAUSED" : spawning ? "PREPARING" : mode.ToString())}\n" +
            $"FPS {Engine.GetFramesPerSecond():F0} • avg {avg:F2} ms\nRecent worst ({frameSamples} frames): {worst:F2} ms\n" +
            $"Alive {units?.Alive ?? 0:N0} / spawned {units?.Count ?? 0:N0}\nBlue {units?.Blue ?? 0:N0} • Red {units?.Red ?? 0:N0}\nProjectiles {projectiles?.Count ?? 0:N0}\n\n" +
            $"GAME CPU • ms per rendered frame\nSimulation total {simulationMs / divisor:F3}\nMovement {movementMs / divisor:F3}\nSeparation {separationMs / divisor:F3}\nGrid rebuild {gridMs / divisor:F3}\nTargeting {targetingMs / divisor:F3}\nCombat {combatMs / divisor:F3}\nProjectiles {projectileMs / divisor:F3}\nRender sync {renderMs / divisor:F3}\n" +
            $"Simulation per tick {costPerTick} (worst {worstTickMs:F2})\nSeparation per tick {separationPerTick}\nRender sync / tick {renderMs / Math.Max(1, measuredTicks):F3} ms\nArmy uploads / sync {armyUploadMs / syncDivisor:F3} ms\nDense / sparse / writes per sync {denseUploads / syncDivisor:F1} / {sparseUploads / syncDivisor:F1} / {changedWrites / syncDivisor:F0}\nTicks/frame {measuredTicks / divisor:F2} (peak {maxTicksInFrame})\n" +
            $"Skipped ticks {skippedSteps:N0}\nQuery caps (last tick) {grid?.LimitedQueries ?? 0:N0}\nProjectile cap drops {projectiles?.Dropped ?? 0:N0}\nSeparation pairs / caps (last tick) {overlapPairs:N0} / {separationCaps:N0}\nSampled penetration avg/max: {separationSamples}\n\n" +
            yieldStats +
            $"ENGINE COUNTERS (whole viewport)\nDraw calls {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame):F0}\nRendered objects {Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame):F0}\n" +
            $"Soldier instances {armyRenderer?.InstanceCount ?? 0:N0}\nManaged heap {GC.GetTotalMemory(false) / 1048576.0:F1} MiB\nStatic memory {StaticMemory()}\n" +
            $"Viewport render CPU / GPU: {ViewportTime(renderCpu)} / {ViewportTime(renderGpu)}";
    }
    private static float ClampYieldSetting(float value, float max, float fallback) =>
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
    private void ResetTimings()
    {
        movementMs = separationMs = gridMs = targetingMs = combatMs = projectileMs = simulationMs = renderMs = 0;
        worstTickMs = armyUploadMs = 0;
        measuredFrames = measuredTicks = maxTicksInFrame = measuredSyncs = denseUploads = sparseUploads = changedWrites = 0;
    }
}
