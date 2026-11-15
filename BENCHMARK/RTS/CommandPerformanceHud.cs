// Displays Command Sandbox timings and controls its single-pass separation mode.
using System;
using Godot;

namespace MassiveRts;

public partial class CommandPerformanceHud : PanelContainer
{
    private readonly Label metrics = new() { HorizontalAlignment = HorizontalAlignment.Right };
    private double syncMs;
    private int syncs;

    internal void Initialize(FortressWaves waves)
    {
        var rows = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        var separation = new CheckButton
        {
            Text = "Normal separation", ButtonPressed = waves.NormalSeparation, FocusMode = FocusModeEnum.None
        };
        separation.Toggled += enabled => waves.NormalSeparation = enabled;
        MouseFilter = MouseFilterEnum.Ignore;
        metrics.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(rows);
        rows.AddChild(separation);
        rows.AddChild(metrics);
    }

    internal void RecordSync(double milliseconds)
    {
        syncMs += milliseconds;
        syncs++;
    }

    internal void Refresh(FortressWaves waves, int moving, int selected)
    {
        int ticks = Math.Max(1, waves.SampleTicks);
        metrics.Text = $"CPU average · ms per physics tick\n" +
            $"Command movement  {waves.MovementMs / ticks:F3}\n" +
            $"Separation  {waves.SeparationMs / ticks:F3}\n" +
            $"Combat grid rebuilds  {waves.GridMs / ticks:F3}\n" +
            $"Army render sync  {syncMs / ticks:F3}\n" +
            $"Active orders  {moving:N0} · Selected  {selected:N0}\n" +
            $"Render syncs  {syncs:N0}\n" +
            $"Last tick overlap pairs  {waves.OverlapPairs:N0}\n" +
            $"Neighbour budget hits  {waves.NeighbourBudgetHits:N0}";
        waves.ResetTimings();
        syncMs = 0;
        syncs = 0;
    }
}
