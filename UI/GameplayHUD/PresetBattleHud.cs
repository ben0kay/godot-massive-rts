// Presents deployment, preparation, combat and results for player-facing Preset Battles.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed partial class PresetBattleHud : CanvasLayer
{
    private readonly VBoxContainer body = new();
    private readonly VBoxContainer deployment = new();
    private readonly Label status = MenuLayout.Label("", muted: true);
    private readonly Label strength = MenuLayout.Label("");
    private readonly Label result = MenuLayout.Label("");
    private readonly Button fight, retry, withdraw;
    private readonly Dictionary<byte, Label> roster = new();

    public PresetBattleHud(string title, (byte Type, string Name)[] choices,
        Action<byte> select, Action<int> brush, Action<bool> fill, Action clear, Action start,
        Action reset, Action retreat, Action leave)
    {
        Layer = 3;
        var panel = new PanelContainer { Position = new Vector2(16, 16), CustomMinimumSize = new Vector2(360, 0) };
        panel.AddThemeStyleboxOverride("panel", UIButtonFactory.Box(UIMasterConfig.Current.Panel,
            UIMasterConfig.Current.Accent.Darkened(0.6f), 12));
        AddChild(panel);
        panel.AddChild(body);
        body.AddThemeConstantOverride("separation", 8);
        body.AddChild(MenuLayout.Label(title));
        body.AddChild(deployment);
        deployment.AddChild(strength);
        var help = MenuLayout.Label("Left-click to paint. Right-click to erase.", muted: true);
        deployment.AddChild(help);

        if (choices.Length == 0)
            deployment.AddChild(MenuLayout.Label("No eligible soldiers owned. Visit Headquarters to recruit.", muted: true));
        else
        {
            var list = new ScrollContainer
            {
                CustomMinimumSize = new Vector2(0, Mathf.Min(175, choices.Length * 67)),
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
                FollowFocus = true
            };
            deployment.AddChild(list);
            var rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            list.AddChild(rows);
            var group = new ButtonGroup();
            foreach (var choice in choices)
            {
                var button = UIButtonFactory.Create(choice.Name, () => select(choice.Type), UIButtonStyle.Compact);
                button.ToggleMode = true;
                button.ButtonGroup = group;
                if (roster.Count == 0) button.SetPressedNoSignal(true);
                rows.AddChild(button);
                var label = MenuLayout.Label("", muted: true);
                rows.AddChild(label);
                roster.Add(choice.Type, label);
            }
            var brushRow = new HBoxContainer();
            deployment.AddChild(brushRow);
            brushRow.AddChild(MenuLayout.Label("Brush"));
            var brushes = new ButtonGroup();
            foreach (int size in new[] { 1, 3, 6, 10 })
            {
                var button = UIButtonFactory.Create($"{size}×{size}", () => brush(size), UIButtonStyle.Compact);
                button.ToggleMode = true;
                button.ButtonGroup = brushes;
                if (size == 1) button.SetPressedNoSignal(true);
                brushRow.AddChild(button);
            }
            var fillButton = new Button { Text = "Rectangle Fill", ToggleMode = true };
            UIButtonFactory.Apply(fillButton, UIButtonStyle.Compact);
            fillButton.Toggled += enabled => fill(enabled);
            fillButton.TooltipText = "Drag to paint or erase a rectangle.";
            deployment.AddChild(fillButton);
            deployment.AddChild(UIButtonFactory.Create("Clear Blue Deployment", clear, UIButtonStyle.Compact));
        }
        fight = UIButtonFactory.Create("Fight", start, UIButtonStyle.Primary);
        deployment.AddChild(fight);
        body.AddChild(result);
        body.AddChild(status);
        retry = UIButtonFactory.Create("Retry Deployment", reset, UIButtonStyle.Primary);
        withdraw = UIButtonFactory.Create("Withdraw", retreat);
        body.AddChild(retry);
        body.AddChild(withdraw);
        body.AddChild(UIButtonFactory.Create("Headquarters", leave));
    }

    internal void ShowDeployment(Func<byte, string> counts, string rating, string blocked)
    {
        deployment.Show();
        result.Hide();
        retry.Hide();
        withdraw.Hide();
        foreach (var row in roster) row.Value.Text = counts(row.Key);
        strength.Text = rating;
        fight.Disabled = blocked != null;
        Report(blocked);
    }

    internal void ShowPreparing()
    {
        deployment.Hide();
        Report("Preparing both armies…");
    }

    internal void ShowBattle()
    {
        Report(null);
        withdraw.Show();
    }

    internal void ShowResult(string outcome, string detail)
    {
        result.Text = outcome + "\n" + detail;
        result.Show();
        withdraw.Hide();
        retry.Show();
        Report(null);
    }

    internal void Report(string message)
    {
        status.Text = message ?? "";
        status.Visible = message != null;
    }
}
