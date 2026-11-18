// Shared compact benchmark panel with a rolling rendered-frame sample.
using System;
using Godot;

namespace MassiveRts;

public partial class BenchmarkSummary : PanelContainer
{
    private readonly double[] frames = new double[120];
    private int cursor, count;
    private readonly Label metrics = new()
    {
        HorizontalAlignment = HorizontalAlignment.Right,
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
        MouseFilter = MouseFilterEnum.Ignore
    };

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        metrics.AddThemeFontSizeOverride("font_size", 14);
        AddChild(metrics);
    }

    public void Sample(double delta)
    {
        frames[cursor] = delta * 1000;
        cursor = (cursor + 1) % frames.Length;
        count = Math.Min(count + 1, frames.Length);
    }

    public void ResetSamples() => cursor = count = 0;

    public void Refresh(string title, UnitState units, int? projectiles = null)
    {
        double sum = 0, worst = 0;
        for (int i = 0; i < count; i++)
        {
            sum += frames[i];
            worst = Math.Max(worst, frames[i]);
        }

        double average = count > 0 ? sum / count : 0;
        var factions = new System.Text.StringBuilder();
        foreach (byte faction in Factions.DisplayOrder)
        {
            int alive = units?.FactionAlive[faction] ?? 0;
            if (alive == 0 && !Factions.ShowWhenEmpty(faction)) continue;
            if (factions.Length > 0) factions.Append(" · ");
            factions.Append($"{Factions.Name(faction)} {alive:N0}");
        }
        metrics.Text = $"{title}\n{Engine.GetFramesPerSecond():F0} FPS · {average:F2} ms avg\nWorst frame  {worst:F2} ms\n" +
            $"Alive  {units?.Alive ?? 0:N0} / {units?.Count ?? 0:N0}\n" +
            factions.ToString() +
            (projectiles.HasValue ? $"\nProjectiles  {projectiles.Value:N0}" : "");
    }
}
