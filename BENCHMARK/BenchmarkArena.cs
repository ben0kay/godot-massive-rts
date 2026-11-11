// Owns BENCH0 lifecycle and fixed-step orchestration; shared systems remain independent.
using System;
using System.Diagnostics;
using Godot;
namespace MassiveRts;

public partial class BenchmarkArena : Node3D
{
    [Export] public BenchmarkConfig Config { get; set; } = new();
    private UnitState units;
    private SpatialGrid grid;
    private ProjectileSystem projectiles;
    private ArmyRenderer armyRenderer;
    private ProjectileRenderer projectileRenderer;
    private IsometricCamera camera;
    private BenchmarkPanel panel;
    private DeploymentFactory deployment;
    private DeploymentPainter painter;
    private BenchmarkMode mode;
    private int requested, targetCount, tick;
    private bool spawning, paused;
    private double accumulator, hudClock;
    private long skippedSteps;
    private readonly double[] frames = new double[120];
    private int frameCursor, frameSamples;
    private double movementMs, gridMs, targetingMs, combatMs, projectileMs, simulationMs, renderMs;
    private int measuredFrames, measuredTicks, maxTicksInFrame;
    private static long Stamp() => Stopwatch.GetTimestamp();
    private static double Elapsed(long start) => (Stamp() - start) * 1000.0 / Stopwatch.Frequency;
    // =========================================================
    public override void _Ready()
    {
        Config.MaxSoldiers = Mathf.Clamp(Config.MaxSoldiers, 1000, 100000);
        Config.SpawnPerFrame = Mathf.Clamp(Config.SpawnPerFrame, 100, 10000);
        Config.TickSeconds = Mathf.Clamp(Config.TickSeconds, 1f / 120, 0.1f);
        Config.MaxStepsPerFrame = Mathf.Clamp(Config.MaxStepsPerFrame, 1, 5);
        Config.TargetRefreshTicks = Mathf.Clamp(Config.TargetRefreshTicks, 1, 60);
        Config.QueryCandidateLimit = Mathf.Clamp(Config.QueryCandidateLimit, 8, 1024);
        Config.CellSize = Mathf.Clamp(Config.CellSize, 2, 20);
        Config.MaxProjectiles = Mathf.Clamp(Config.MaxProjectiles, 100, 100000);
        var ground = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(5000, 5000),
            Material = new StandardMaterial3D { AlbedoColor = new Color(0.075f, 0.085f, 0.10f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } } };
        AddChild(ground);
        camera = new IsometricCamera(); AddChild(camera); camera.Current = true;
        panel = new BenchmarkPanel(); AddChild(panel);
        painter = new DeploymentPainter { Camera = camera, Capacity = Config.MaxSoldiers, Visible = false };
        AddChild(painter);
        painter.Changed += UpdateDeploymentStatus;
        panel.SpawnRequested += Spawn;
        panel.ClearRequested += () => { if (painter.Visible) painter.Clear(); else Clear(); };
        panel.PaintRequested += EnterDeployment;
        panel.BattleRequested += StartPaintedBattle;
        panel.SaveRequested += () => panel.Status.Text = painter.Save();
        panel.LoadRequested += () => panel.Status.Text = painter.Load() + "\n" + DeploymentCount();
        panel.BrushChoice.ItemSelected += index => painter.Brush = (int)index;
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
        units = null; projectiles = null; grid = null; deployment = null; spawning = false;
        accumulator = 0; tick = 0; skippedSteps = 0; paused = false; panel.PauseButton.Text = "Pause";
        frameSamples = 0; frameCursor = 0; ResetTimings(); panel.Status.Text = "Cleared";
    }
    private void Spawn()
    {
        Clear();
        SetDeploymentEditing(false);
        requested = panel.RequestedCount; targetCount = Mathf.Min(requested, Config.MaxSoldiers);
        mode = (BenchmarkMode)panel.ModeChoice.Selected;
        units = new UnitState(targetCount);
        if (mode is BenchmarkMode.Melee or BenchmarkMode.Ranged) grid = new SpatialGrid(targetCount, Config.CellSize);
        projectiles = new ProjectileSystem(Config.MaxProjectiles);
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
        painter.InitializePreview();
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
            ranged |= placement.Kind == 1;
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
        units = new UnitState(targetCount);
        grid = new SpatialGrid(targetCount, Config.CellSize);
        projectiles = new ProjectileSystem(Config.MaxProjectiles);
        deployment = new DeploymentFactory(snapshot, (ulong)panel.Seed.Value);
        spawning = true;
    }

    // =========================================================
    public override void _Process(double delta)
    {
        frames[frameCursor] = delta * 1000; frameCursor = (frameCursor + 1) % frames.Length; frameSamples = Math.Min(frameSamples + 1, frames.Length);
        if (spawning)
        {
            deployment.Append(units, Config, Config.SpawnPerFrame);
            panel.Status.Text = $"Preparing {units.Count:N0} / {requested:N0}\nSimulation starts after deployment.";
            if (units.Count == targetCount)
            {
                armyRenderer = new ArmyRenderer(); AddChild(armyRenderer); armyRenderer.Initialize(units);
                projectileRenderer = new ProjectileRenderer(); AddChild(projectileRenderer); projectileRenderer.Initialize(Config.MaxProjectiles);
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
                long start = Stamp(); armyRenderer.Sync(); projectileRenderer.Sync(projectiles); renderMs += Elapsed(start);
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
            start = Stamp(); CombatSystem.Acquire(units, grid, Config, mode, tick); targetingMs += Elapsed(start);
        }
        start = Stamp(); MovementSystem.Step(units, Config, mode, dt, grid); movementMs += Elapsed(start);
        if (mode == BenchmarkMode.Melee || mode == BenchmarkMode.Ranged)
        {
            start = Stamp(); CombatSystem.Step(units, projectiles, Config, mode, (float)panel.FireRate.Value, dt); combatMs += Elapsed(start);
            if (mode == BenchmarkMode.Ranged) { start = Stamp(); projectiles.Step(units, grid, Config, dt); projectileMs += Elapsed(start); }
        }
        tick++; simulationMs += Elapsed(total);
    }
    // =========================================================
    private void UpdateOverlay()
    {
        double sum = 0, worst = 0;
        for (int i = 0; i < frameSamples; i++) { sum += frames[i]; worst = Math.Max(worst, frames[i]); }
        double avg = frameSamples > 0 ? sum / frameSamples : 0, divisor = Math.Max(1, measuredFrames);
        string costPerTick = measuredTicks > 0 ? $"{simulationMs / measuredTicks:F3} ms" : "N/A";
        panel.Metrics.Text = $"BENCH0 — {(painter.Visible ? "DEPLOYMENT" : paused ? "PAUSED" : spawning ? "PREPARING" : mode.ToString())}\n" +
            $"FPS {Engine.GetFramesPerSecond():F0} • avg {avg:F2} ms\nRecent worst ({frameSamples} frames): {worst:F2} ms\n" +
            $"Alive {units?.Alive ?? 0:N0} / spawned {units?.Count ?? 0:N0}\nBlue {units?.Blue ?? 0:N0} • Red {units?.Red ?? 0:N0}\nProjectiles {projectiles?.Count ?? 0:N0}\n\n" +
            $"GAME CPU • ms per rendered frame\nSimulation total {simulationMs / divisor:F3}\nMovement {movementMs / divisor:F3}\nGrid rebuild {gridMs / divisor:F3}\nTargeting {targetingMs / divisor:F3}\nCombat {combatMs / divisor:F3}\nProjectiles {projectileMs / divisor:F3}\nRender sync {renderMs / divisor:F3}\n" +
            $"Simulation per tick {costPerTick}\nTicks/frame {measuredTicks / divisor:F2} (peak {maxTicksInFrame})\n" +
            $"Skipped ticks {skippedSteps:N0}\nQuery caps (last tick) {grid?.LimitedQueries ?? 0:N0}\nProjectile cap drops {projectiles?.Dropped ?? 0:N0}\n\n" +
            $"ENGINE COUNTERS (whole viewport)\nDraw calls {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame):F0}\nRendered objects {Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame):F0}\n" +
            $"Soldier instances {armyRenderer?.InstanceCount ?? 0:N0}\nManaged heap {GC.GetTotalMemory(false) / 1048576.0:F1} MiB\nStatic memory {StaticMemory()}\n" +
            $"GPU frame time: N/A\nGPU utilization: N/A";
    }
    private static string StaticMemory()
    {
        double bytes = Performance.GetMonitor(Performance.Monitor.MemoryStatic);
        return bytes > 0 ? $"{bytes / 1048576.0:F1} MiB" : "N/A";
    }
    private void ResetTimings()
    { movementMs = gridMs = targetingMs = combatMs = projectileMs = simulationMs = renderMs = 0; measuredFrames = measuredTicks = maxTicksInFrame = 0; }
}
