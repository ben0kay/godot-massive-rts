// Displays wave progression, Citadel health and the first-wave start button.
using System;
using Godot;

namespace MassiveRts;

public partial class WaveHud : PanelContainer
{
    private readonly Label status = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Label health = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Button start = new() { Text = "Start Wave 1", FocusMode = FocusModeEnum.None };

    internal void Initialize(Action begin)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        var row = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(row);
        status.MouseFilter = health.MouseFilter = MouseFilterEnum.Ignore;
        row.AddChild(status);
        row.AddChild(health);
        row.AddChild(start);
        start.Pressed += begin;
    }

    internal void Refresh(FortressWaves waves, CommandStructure citadel)
    {
        start.Visible = waves.WaitingToStart;
        health.Text = $"Citadel  {citadel.Health:N0} / {citadel.MaxHealth:N0} HP";
        status.Text = waves.Defeat ? "Defeat · Citadel destroyed" :
            waves.Victory ? "Victory · All waves cleared" :
            waves.WaitingToStart ? "Build and position your defenders" :
            waves.Countdown > 0 ? $"Wave {waves.WaveNumber + 1} in {Mathf.CeilToInt(waves.Countdown)} s" :
            $"Wave {waves.WaveNumber} · {waves.Remaining:N0} enemies left";
    }
}
