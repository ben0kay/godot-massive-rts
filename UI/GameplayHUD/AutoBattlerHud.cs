// Presents deployment, preparation, combat and results for player-facing AutoBattler.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed partial class AutoBattlerHud : CanvasLayer
{
    private readonly VBoxContainer body = new();
    private readonly VBoxContainer deployment = new();
    private Control deploymentBar;
    private VBoxContainer placementShell;
    private HBoxContainer unitPage;
    private Label trapStatus;
    internal VBoxContainer DeploymentOrderControls { get; private set; }
    private readonly Label status = MenuLayout.Label("", muted: true);
    private readonly Label strength = MenuLayout.Label("");
    private readonly Label arrival = MenuLayout.Label("", muted: true);
    private readonly Label result = MenuLayout.Label("");
    private readonly Button fight, retry, withdraw;
    private readonly Action refreshReserve;
    private readonly Dictionary<byte, Label> roster = new();

    public AutoBattlerHud(string title, (byte Type, string Name)[] choices,
        Action<byte> select, Action<int> brush, Action clear, Action start,
        Action reset, Action retreat, Action leave, Func<byte, int> maxReserve,
        Func<byte, int> reserved, Action<byte, int> reserve, Action<float> arrivalSeconds, Action<float> densityChanged, CheckButton circleMeshToggle, bool skirmish = false)
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
        var help = MenuLayout.Label("Paint or select troops using the toolbar.", muted: true);
        deployment.AddChild(help);

        if (choices.Length == 0)
            deployment.AddChild(MenuLayout.Label(skirmish ? "This skirmish has no eligible preset soldiers." : "No eligible soldiers owned. Visit Headquarters to recruit.", muted: true));
        else
        {
            var palette = new UnitPlacementBar();
            deploymentBar = palette;
            AddChild(palette);
            var columns = unitPage = new HBoxContainer();
            columns.AddThemeConstantOverride("separation", 12);
            placementShell = palette.BuildShell(154);
            placementShell.AddChild(columns);
            var brushControls = new VBoxContainer { CustomMinimumSize = new Vector2(240, 0) };
            columns.AddChild(brushControls);
            DeploymentOrderControls = brushControls;
            var cardColumn = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            columns.AddChild(cardColumn);
            cardColumn.AddChild(MenuLayout.Label("UNIT PLACEMENT"));

            var list = new ScrollContainer
            {
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
                FollowFocus = true
            };
            cardColumn.AddChild(list);
            var rows = new GridContainer { Columns = Math.Min(6, choices.Length), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            list.AddChild(rows);
            var group = new ButtonGroup();
            foreach (var choice in choices)
            {
                var button = UIButtonFactory.Create(choice.Name, () => select(choice.Type), UIButtonStyle.Compact);
                button.ToggleMode = true;
                button.ButtonGroup = group;
                if (roster.Count == 0) button.SetPressedNoSignal(true);
                var card = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                rows.AddChild(card);
                card.AddChild(button);
                var label = MenuLayout.Label("", muted: true);
                label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                card.AddChild(label);
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

            UnitPlacementBar.AddBrushSettings(brushControls, brush, densityChanged);
            deployment.AddChild(UIButtonFactory.Create("Clear Blue Deployment", clear, UIButtonStyle.Compact));
        }
        fight = UIButtonFactory.Create("Fight", start, UIButtonStyle.Primary);
        deployment.AddChild(fight);
        body.AddChild(arrival);
        arrival.Hide();
        body.AddChild(result);
        body.AddChild(status);
        body.AddChild(circleMeshToggle);
        retry = UIButtonFactory.Create("Retry Deployment", reset, UIButtonStyle.Primary);
        withdraw = UIButtonFactory.Create("Withdraw", retreat);
        body.AddChild(retry);
        body.AddChild(withdraw);
        body.AddChild(UIButtonFactory.Create(skirmish ? "Back to Skirmishes" : "Headquarters", leave));
    }

    internal void AddDisruptorControls(int owned, Action<bool> activate)
    {
        if (owned <= 0) return;
        if (placementShell == null)
        {
            var palette = new UnitPlacementBar();
            deploymentBar = palette;
            AddChild(palette);
            placementShell = palette.BuildShell(154);
        }
        var tabs = new HBoxContainer();
        placementShell.AddChild(tabs);
        placementShell.MoveChild(tabs, 0);
        var defenses = new VBoxContainer { Visible = unitPage == null };
        placementShell.AddChild(defenses);
        defenses.AddChild(MenuLayout.Label("Traps"));
        var choose = new CheckButton
        {
            Text = DisruptorGrid.Name,
            Icon = DisruptorGridVisual.PickerIcon,
            TooltipText = "Drag to place. Right-drag to erase. Slows and damages enemies crossing the tiles."
        };
        choose.Toggled += enabled => activate(enabled);
        defenses.AddChild(choose);
        trapStatus = MenuLayout.Label($"{owned:N0} tiles available", muted: true);
        defenses.AddChild(trapStatus);

        var group = new ButtonGroup();
        if (unitPage != null) AddTab("Units", true, () =>
        {
            choose.ButtonPressed = false;
            defenses.Hide();
            unitPage.Show();
        });
        AddTab("Defenses", unitPage == null, () =>
        {
            unitPage?.Hide();
            defenses.Show();
        });

        void AddTab(string title, bool selected, Action action)
        {
            var button = UIButtonFactory.Create(title, action, UIButtonStyle.Compact);
            button.ToggleMode = true;
            button.ButtonGroup = group;
            button.SetPressedNoSignal(selected);
            tabs.AddChild(button);
        }
    }

    internal void ReportDisruptors(string message)
    {
        if (trapStatus != null) trapStatus.Text = message;
    }

    internal void ShowDeployment(Func<byte, string> counts, string rating, string blocked)
    {
        deployment.Show();
        deploymentBar?.Show();
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
        deploymentBar?.Hide();
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
