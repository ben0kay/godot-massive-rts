// Presents player battle telemetry and controls without owning the simulation.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed partial class TacticalBattleHud : Control
{
    private sealed class FactionReadout
    {
        internal Label Count, Strength;
        internal readonly Label[] Overview = new Label[4];
        internal (int Count, long Strength, int Health, int Deaths) Previous = (-1, -1, -1, -1);
    }

    private static bool overviewExpanded = true;
    private readonly FactionReadout[] readouts = new FactionReadout[Factions.Count];
    private readonly List<byte> factions = new();
    private VBoxContainer command, scenario, controls, overview, mapDock;
    private HBoxContainer commandRow, objectiveDock;
    private GridContainer overviewGrid;
    private Label clock, objectiveText, arrivals;
    private Button pauseButton, speedButton, overviewToggle, withdrawButton;
    private ProgressBar objectiveBar;
    private UnitState units;
    private AirSquadron air;
    private StructureHealth objective;
    private TacticalMinimap minimap;
    private int previousSecond = -1, page;
    private float previousObjective = -1f;
    private bool protect, cinematic;

    internal void Build(string mode, string level, string goal, Action pause, Action speed, Action withdraw)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        scenario = Dock();
        var info = TacticalHudStyle.Panel(scenario, TacticalHudStyle.Silver);
        info.AddChild(TacticalHudStyle.Label(mode, 13, TacticalHudStyle.Text));
        info.AddChild(TacticalHudStyle.Label(level, 14, TacticalHudStyle.Text));
        info.AddChild(TacticalHudStyle.Label(goal, 12, TacticalHudStyle.Silver));
        arrivals = TacticalHudStyle.Label("", 12, TacticalHudStyle.Silver);
        info.AddChild(arrivals);

        command = Dock();
        commandRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        command.AddChild(commandRow);
        objectiveDock = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        command.AddChild(objectiveDock);
        var objectivePanel = TacticalHudStyle.Panel(objectiveDock, TacticalHudStyle.Silver);
        objectiveText = TacticalHudStyle.Label("", 13, TacticalHudStyle.Text);
        objectivePanel.AddChild(objectiveText);
        objectiveBar = new ProgressBar { ShowPercentage = false, MaxValue = 1, MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(258, 4) };
        objectivePanel.AddChild(objectiveBar);

        controls = Dock();
        var controlRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        controls.AddChild(controlRow);
        pauseButton = TacticalHudStyle.Button("Ⅱ", "Pause or resume", pause);
        speedButton = TacticalHudStyle.Button("1×", "Cycle simulation speed", speed);
        controlRow.AddChild(pauseButton);
        controlRow.AddChild(speedButton);
        withdrawButton = TacticalHudStyle.Button("Withdraw", "End the current battle", withdraw);
        controlRow.AddChild(withdrawButton);

        overview = Dock();
        var sheet = TacticalHudStyle.Panel(overview, TacticalHudStyle.Silver);
        overviewToggle = TacticalHudStyle.Button("", "Toggle battle overview", () =>
        {
            overviewExpanded = !overviewExpanded;
            RefreshOverview();
        });
        sheet.AddChild(overviewToggle);
        overviewGrid = new GridContainer { MouseFilter = MouseFilterEnum.Ignore };
        overviewGrid.AddThemeConstantOverride("h_separation", 12);
        sheet.AddChild(overviewGrid);
        RefreshOverview();

        mapDock = Dock();
        minimap = new TacticalMinimap();
        mapDock.AddChild(minimap);
        Resized += Arrange;
        Reset();
    }

    internal TacticalMinimap Minimap => minimap;

    private VBoxContainer Dock()
    {
        var dock = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        dock.MinimumSizeChanged += () => Callable.From(Arrange).CallDeferred();
        AddChild(dock);
        return dock;
    }

    // =========================================================
    private void Arrange()
    {
        if (mapDock == null) return;
        foreach (var dock in new[] { command, scenario, controls, overview, mapDock }) dock.ResetSize();
        const float margin = 12;
        scenario.Position = new Vector2(margin, margin);
        controls.Position = new Vector2(Size.X - controls.Size.X - margin, margin);
        float top = margin;
        if (Size.X * .5f - command.Size.X * .5f < scenario.Size.X + 2 * margin ||
            Size.X * .5f + command.Size.X * .5f > controls.Position.X - margin)
            top += Mathf.Max(scenario.Size.Y, controls.Size.Y) + margin;
        command.Position = new Vector2((Size.X - command.Size.X) * .5f, top);
        overview.Position = new Vector2(margin, Size.Y - overview.Size.Y - margin);
        mapDock.Position = Size - mapDock.Size - Vector2.One * margin;
    }

    private void RefreshOverview()
    {
        overviewGrid.Visible = overviewExpanded;
        overviewToggle.Text = overviewExpanded ? "▥  BATTLE OVERVIEW  ▴" : "▥  BATTLE OVERVIEW  ▾";
        overview.ResetSize();
    }

    internal void Begin(UnitState source, AirSquadron aircraft, StructureHealth citadel, bool defend, bool[] expected)
    {
        units = source;
        air = aircraft;
        objective = citadel;
        protect = defend;
        factions.Clear();
        foreach (byte faction in Factions.DisplayOrder)
            if (expected[faction] || Factions.ShowWhenEmpty(faction) || source.FactionAlive[faction] > 0 || (air?.Alive(faction) ?? 0) > 0)
                factions.Add(faction);
        page = 0;
        BuildFactionReadouts();
        objectiveDock.Visible = citadel != null;
        if (citadel != null)
        {
            Color color = Factions.Color(citadel.Faction);
            objectiveBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = color });
            objectiveText.AddThemeColorOverride("font_color", color);
        }
        Visible = !cinematic;
        Refresh();
        Arrange();
    }

    private static void ClearChildren(Container parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void BuildFactionReadouts()
    {
        ClearChildren(commandRow);
        ClearChildren(overviewGrid);
        Array.Clear(readouts);
        const int perPage = 4;
        int first = page * perPage, count = Math.Min(perPage, factions.Count - first);
        overviewGrid.Columns = count + 1;
        overviewGrid.AddChild(TacticalHudStyle.Label("", 12, TacticalHudStyle.Silver));
        for (int i = 0; i <= count; i++)
        {
            if (i == (count + 1) / 2)
            {
                var center = TacticalHudStyle.Panel(commandRow, TacticalHudStyle.Silver);
                center.GetParent<Control>().SizeFlagsVertical = SizeFlags.ShrinkBegin;
                clock = TacticalHudStyle.Label("", 24, TacticalHudStyle.Text, HorizontalAlignment.Center);
                clock.CustomMinimumSize = new Vector2(86, 0);
                center.AddChild(clock);
                if (factions.Count > perPage)
                    center.AddChild(TacticalHudStyle.Button($"Factions {page + 1}/{(factions.Count + perPage - 1) / perPage}",
                        "Next faction page", () =>
                        {
                            page = (page + 1) % ((factions.Count + perPage - 1) / perPage);
                            BuildFactionReadouts();
                            Refresh();
                        }));
                previousSecond = -1;
            }
            if (i == count) break;
            byte faction = factions[first + i];
            Color accent = Factions.Color(faction);
            var side = TacticalHudStyle.Panel(commandRow, accent);
            side.AddChild(TacticalHudStyle.Label($"◆  {Factions.Name(faction).ToUpperInvariant()}", 11, accent));
            var countLabel = TacticalHudStyle.Label("", 24, accent);
            var strengthLabel = TacticalHudStyle.Label("", 12, accent);
            side.AddChild(countLabel);
            side.AddChild(TacticalHudStyle.Label("UNITS REMAINING", 10, TacticalHudStyle.Silver));
            side.AddChild(strengthLabel);
            readouts[faction] = new FactionReadout { Count = countLabel, Strength = strengthLabel };
            overviewGrid.AddChild(TacticalHudStyle.Label(Factions.Name(faction), 12, accent));
        }
        string[] names = { "◆  Units", "▥  Strength", "+  Army health", "×  Casualties" };
        for (int row = 0; row < names.Length; row++)
        {
            overviewGrid.AddChild(TacticalHudStyle.Label(names[row], 12, TacticalHudStyle.Text));
            for (int i = 0; i < count; i++)
            {
                byte faction = factions[first + i];
                var cell = TacticalHudStyle.Label("", 12, Factions.Color(faction), HorizontalAlignment.Right);
                readouts[faction].Overview[row] = cell;
                overviewGrid.AddChild(cell);
            }
        }
        commandRow.ResetSize();
        command.ResetSize();
        overview.ResetSize();
        SetClock(elapsed);
    }

    private double elapsed;

    internal void SetClock(double seconds)
    {
        elapsed = seconds;
        int whole = (int)seconds;
        if (clock == null || whole == previousSecond) return;
        previousSecond = whole;
        clock.Text = whole >= 3600 ? $"{whole / 3600}:{whole / 60 % 60:00}:{whole % 60:00}" :
            $"{whole / 60:00}:{whole % 60:00}";
    }

    internal void SetControls(bool paused, float speed, bool ended)
    {
        pauseButton.Text = paused ? "▶" : "Ⅱ";
        pauseButton.Disabled = speedButton.Disabled = withdrawButton.Disabled = ended;
        speedButton.Text = $"{speed:0.##}×";
    }

    internal void SetArrivals(string value)
    {
        TacticalHudStyle.Set(arrivals, value);
        arrivals.Visible = value.Length > 0;
    }

    internal void Refresh()
    {
        if (units == null || cinematic) return;
        foreach (byte faction in factions)
        {
            var readout = readouts[faction];
            if (readout == null) continue;
            var ground = units.ArmyStatistics[faction];
            var airborne = air?.ArmyStatistics(faction) ?? default;
            double maximum = ground.MaxHealth + airborne.MaxHealth;
            int health = maximum > 0 ? Mathf.RoundToInt((float)Math.Clamp((ground.Health + airborne.Health) * 100d / maximum, 0d, 100d)) : 0;
            var next = (Count: units.FactionAlive[faction] + (air?.Alive(faction) ?? 0),
                Strength: ground.Strength + airborne.Strength, Health: health,
                Deaths: ground.Casualties + airborne.Casualties);
            var previous = readout.Previous;
            if (next.Count != previous.Count) readout.Count.Text = readout.Overview[0].Text = $"{next.Count:N0}";
            if (next.Strength != previous.Strength)
            {
                readout.Overview[1].Text = $"{next.Strength:N0}";
                readout.Strength.Text = next.Strength >= 1000 ? $"STRENGTH {next.Strength / 1000d:0.#}K" : $"STRENGTH {next.Strength}";
            }
            if (next.Health != previous.Health) readout.Overview[2].Text = $"{next.Health}%";
            if (next.Deaths != previous.Deaths) readout.Overview[3].Text = $"{next.Deaths:N0}";
            readout.Previous = next;
        }
        if (objective != null && objective.Health != previousObjective)
        {
            previousObjective = objective.Health;
            float fraction = objective.Health / objective.MaxHealth;
            objectiveBar.Value = fraction;
            objectiveText.Text = $"▥  {(protect ? "PROTECT CITADEL" : "ENEMY CITADEL")}   {fraction:P0}   {Mathf.CeilToInt(objective.Health):N0}/{objective.MaxHealth:N0} HP";
        }
    }

    internal void ToggleCinematic()
    {
        cinematic = !cinematic;
        Visible = units != null && !cinematic;
        if (Visible) Refresh();
    }

    internal void Reset()
    {
        Hide();
        units = null;
        air = null;
        objective = null;
        previousObjective = -1f;
        SetClock(0);
        SetArrivals("");
        minimap.Reset();
    }
}
