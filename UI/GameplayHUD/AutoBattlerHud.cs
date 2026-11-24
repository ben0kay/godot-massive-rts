// Presents deployment, preparation, combat and results for player-facing AutoBattler.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed partial class AutoBattlerHud : CanvasLayer
{
    private readonly VBoxContainer body = new();
    private readonly VBoxContainer deployment = new();
    private readonly Label status = MenuLayout.Label("", muted: true);
    private readonly Label strength = MenuLayout.Label("");
    private readonly Label arrival = MenuLayout.Label("", muted: true);
    private readonly Label result = MenuLayout.Label("");
    private readonly Button fight, retry, withdraw;
    private readonly Action refreshReserve;
    private readonly Dictionary<byte, Label> roster = new();

    public AutoBattlerHud(string title, (byte Type, string Name)[] choices,
        Action<byte> select, Action<int> brush, Action<bool> fill, Action clear, Action start,
        Action reset, Action retreat, Action leave, Func<byte, int> maxReserve,
        Func<byte, int> reserved, Action<byte, int> reserve, Action<float> arrivalSeconds)
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
            var reserveRow = new HBoxContainer();
            deployment.AddChild(reserveRow);
            var reserveType = new OptionButton();
            foreach (var choice in choices) reserveType.AddItem(choice.Name, choice.Type);
            reserveRow.AddChild(MenuLayout.Label("Reserve"));
            reserveRow.AddChild(reserveType);
            var quantity = new SpinBox { Step = 1, CustomMinimumSize = new Vector2(85, 0) };
            reserveRow.AddChild(quantity);
            var delay = new SpinBox { MinValue = 0, MaxValue = 299, Step = 1, Value = 30, Suffix = "s" };
            reserveRow.AddChild(delay);
            delay.TooltipText = "Reserve arrival time after Fight";
            delay.ValueChanged += seconds => arrivalSeconds((float)seconds);
            quantity.TooltipText = "Soldiers of this type assigned to the reserve wave";
            quantity.ValueChanged += value =>
            {
                byte type = (byte)reserveType.GetSelectedId();
                int count = Math.Min((int)value, maxReserve(type));
                quantity.SetValueNoSignal(count);
                reserve(type, count);
            };
            void RefreshReserve()
            {
                byte type = (byte)reserveType.GetSelectedId();
                quantity.MaxValue = Math.Max(quantity.MaxValue, maxReserve(type));
                quantity.SetValueNoSignal(reserved(type));
                quantity.MaxValue = maxReserve(type);
            }
            reserveType.ItemSelected += _ => RefreshReserve();
            refreshReserve = RefreshReserve;

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
        body.AddChild(arrival);
        arrival.Hide();
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
        arrival.Hide();
        refreshReserve?.Invoke();
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

    internal void ShowArrivals(string text)
    {
        if (arrival.Text != text) arrival.Text = text;
        arrival.Visible = text.Length > 0;
    }

    internal void ShowResult(string outcome, string detail)
    {
        arrival.Hide();
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
