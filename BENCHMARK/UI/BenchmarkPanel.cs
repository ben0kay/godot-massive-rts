// Benchmark-only controls and throttled, permanent performance readout.
using System;
using Godot;
namespace MassiveRts;

public partial class BenchmarkPanel : CanvasLayer
{
    public event Action SpawnRequested, ClearRequested, PauseRequested;
    public event Action PaintRequested, BattleRequested, SaveRequested, LoadRequested;
    public event Action<float> CameraRotationChanged;
    public event Action<bool> GroundShadowsChanged;
    public event Action<bool> PlacementGridChanged;
    private readonly CheckButton placementGridToggle = new()
    {
        Text = "Show placement grid", ButtonPressed = true
    };
    public bool ShowPlacementGrid => placementGridToggle.ButtonPressed;
    private readonly CheckButton groundShadows = new() { Text = "Ground shadows", ButtonPressed = true };
    public bool GroundShadows => groundShadows.ButtonPressed;
    public RuntimeUnitCatalog Catalog { private get; set; }
    private readonly CheckButton smoothMotion = new() { Text = "Smooth soldier motion", ButtonPressed = true };
    private readonly HSlider cameraRotation = new() { MinValue = 0, MaxValue = 360, Step = 1, Value = 45,
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
    private readonly Label cameraRotationValue = new() { Text = "45° (original diagonal)" };
    public bool SmoothMotion => smoothMotion.ButtonPressed;
    private readonly OptionButton separationChoice = new();
    private readonly CheckButton alliedYield = new() { Text = "Allied archer yielding", ButtonPressed = true };
    private readonly CheckButton meleeSidestep = new() { Text = "Melee sidestep", ButtonPressed = true };
    private readonly CheckButton meleeHold = new() { Text = "Hold melee contact", ButtonPressed = true };
    private readonly CheckButton weightedSeparation = new() { Text = "Footprint-weighted separation", ButtonPressed = true };
    private readonly CheckButton rangedTargetVariety = new() { Text = "Ranged target variety", ButtonPressed = true };
    private readonly CheckButton slowPursuitRefresh = new() { Text = "Slower long-range retargeting", ButtonPressed = true };
    public bool SlowPursuitRefresh => slowPursuitRefresh.ButtonPressed;
    private readonly CheckButton delayFarMeleeRetarget = new() { Text = "Delay distant melee retargeting" };
    private readonly CheckButton limitPursuitQueries = new() { Text = "Cap long-range searches (512/step)" };
    public bool DelayFarMeleeRetarget => delayFarMeleeRetarget.ButtonPressed;
    public bool LimitPursuitQueries => limitPursuitQueries.ButtonPressed;
    private readonly CheckButton prunePursuitCells = new() { Text = "Prune pursuit cells", ButtonPressed = true };
    public bool PrunePursuitCells => prunePursuitCells.ButtonPressed;
    public bool RangedTargetVariety => rangedTargetVariety.ButtonPressed;
    public bool MeleeSidestep => meleeSidestep.ButtonPressed;
    public bool MeleeHold => meleeHold.ButtonPressed;
    public bool WeightedSeparation => weightedSeparation.ButtonPressed;
    public bool AlliedYield => alliedYield.ButtonPressed;
    public SeparationMode Separation { get; private set; } = SeparationMode.Normal;
    public RuntimeBuildingCatalog Buildings { get; set; }
    public readonly OptionButton DefenseChoice = new();
    private readonly OptionButton[] teamOrders = new OptionButton[Factions.Count];
    public GuardOrder OrderFor(int faction) => (GuardOrder)teamOrders[faction].GetSelectedId();
    public readonly SpinBox GuardRadius = new() { MinValue = 1, MaxValue = 100, Step = 1, Value = 12 };
    public readonly OptionButton PaintToolChoice = new();
    public readonly OptionButton TerrainChoice = new();
    public readonly CheckButton TestWall = new() { Text = "Test wall in preset" };
    public readonly OptionButton BrushChoice = new();
    public readonly OptionButton SoldierBrushSizeChoice = new();
    public readonly OptionButton FactionChoice = new();
    private VBoxContainer paintControls, paintSection;
    private Button paintHeader;
    public bool EditingDeployment
    {
        set
        {
            paintSection.Visible = value;
            if (value) paintHeader.ButtonPressed = true;
        }
    }
    public readonly OptionButton CountChoice = new(), ModeChoice = new(), LayoutChoice = new();
    public readonly SpinBox Seed = new() { MinValue = 0, MaxValue = 2147483647, Value = 12345, Step = 1 };
    public readonly SpinBox FireRate = new() { MinValue = 0.2, MaxValue = 20, Step = 0.2, Value = 1 };
    public readonly SpinBox RangedPercent = new() { MinValue = 0, MaxValue = 100, Step = 5, Value = 40 };
    public readonly Label Status = new();
    public readonly Label Metrics = DebugLabel(), CpuMetrics = DebugLabel(), ObstacleMetrics = DebugLabel(), EngineMetrics = DebugLabel();
    private readonly CheckButton profileMovement = new() { Text = "Profile movement" };
    public bool ProfileMovement => profileMovement.ButtonPressed;
    private readonly CheckButton profileObstacles = new() { Text = "Sample obstacle queries" };
    private readonly CheckButton reuseCorridors = new() { Text = "Reuse corridor checks", ButtonPressed = true };
    private readonly CheckButton reuseWaypoints = new() { Text = "Reuse route waypoints" };
    public bool ReuseWaypoints => reuseWaypoints.ButtonPressed;
    public bool ReuseCorridors => reuseCorridors.ButtonPressed;
    public bool ProfileObstacles => profileObstacles.ButtonPressed;
    public readonly Button PauseButton = new() { Text = "Pause" };
    private PanelContainer left, overlay;
    private VBoxContainer right;
    private CheckButton performanceToggle;
    private bool leftPanelVisible = true, rightPanelVisible = true;

    // =========================================================
    // F1 controls both sides; F2 affects the left side without touching the right HUD.
    private void UpdatePanelVisibility()
    {
        left.Visible = leftPanelVisible;
        right.Visible = rightPanelVisible;
        overlay.Visible = rightPanelVisible && performanceToggle.ButtonPressed;
    }
    public static readonly int[] Counts = { 1000, 5000, 10000, 20000, 25000, 50000, 100000 };
    public int RequestedCount => Counts[CountChoice.Selected];
    // =========================================================
    public override void _Ready()
    {
        left = new PanelContainer
        {
            AnchorBottom = 1, OffsetLeft = 12, OffsetRight = 452, OffsetTop = 12, OffsetBottom = -12
        };
        AddChild(left);
        var shell = new VBoxContainer();
        left.AddChild(shell);
        var heading = new Label { Text = "BENCH0  •  MASSIVE RTS" };
        heading.AddThemeFontSizeOverride("font_size", 17);
        heading.AddThemeColorOverride("font_color", new Color("#CCE8F6"));
        shell.AddChild(heading);
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true
        };
        shell.AddChild(scroll);
        var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(column);

        var preset = Section(column, "Preset battle", true, out _);
        foreach (int count in Counts) CountChoice.AddItem(count.ToString("N0"));
        foreach (string mode in new[] { "A — Rendering only", "B — Movement", "C — Melee", "D — Ranged" }) ModeChoice.AddItem(mode);
        foreach (string layout in new[]
        {
            "Opposing lines", "Dense clusters", "Distributed battlefield",
            "Layered ranks — full roster", "Cavalry pincer — flanking wings",
            "Wedge assault vs shield wall", "Encirclement — defenders vs ring",
            "Three-way clash — Blue / Red / Green"
        }) LayoutChoice.AddItem(layout);
        Field(preset, "Total soldiers", CountChoice); Field(preset, "Mode", ModeChoice);
        Field(preset, "Layout", LayoutChoice); Field(preset, "Seed", Seed);
        preset.AddChild(Hint("New formations include all six shapes; Melee excludes archers."));
        Field(preset, "Ranged %", RangedPercent);
        TerrainChoice.AddItem("Flat ground");
        TerrainChoice.AddItem("Test plateau");
        Field(preset, "Preset terrain", TerrainChoice);
        preset.AddChild(TestWall);
        AddButton(preset, "Spawn / Reset selected", () => SpawnRequested?.Invoke());

        var cameraControls = Section(column, "Camera", true, out _);
        cameraRotation.TooltipText = "0° = straight, 45° = original diagonal view";
        cameraRotationValue.Text = "45°";
        cameraRotationValue.CustomMinimumSize = new Vector2(46f, 0f);
        cameraRotationValue.HorizontalAlignment = HorizontalAlignment.Right;
        var rotationRow = Field(cameraControls, "Camera yaw", cameraRotation);
        rotationRow.AddChild(cameraRotationValue);
        cameraRotation.ValueChanged += degrees =>
        {
            cameraRotationValue.Text = $"{degrees:0}°";
            CameraRotationChanged?.Invoke((float)degrees);
        };
        AddButton(cameraControls, "Reset camera rotation (45°)", () => cameraRotation.Value = 45);

        var combat = Section(column, "Live combat", false, out _);
        FireRate.TooltipText = "Live multiplier applied to ranged attacks";
        Field(combat, "Fire-rate ×", FireRate);
        combat.AddChild(rangedTargetVariety);
        combat.AddChild(slowPursuitRefresh);
        combat.AddChild(prunePursuitCells);
        combat.AddChild(delayFarMeleeRetarget);
        combat.AddChild(limitPursuitQueries);
        var separation = Section(column, "Live separation", false, out _);
        separationChoice.AddItem("Off", (int)SeparationMode.Off);
        separationChoice.AddItem("Normal", (int)SeparationMode.Normal);
        separationChoice.AddItem("Strong", (int)SeparationMode.Strong);
        separationChoice.Select(1);
        separationChoice.ItemSelected += index => Separation = (SeparationMode)separationChoice.GetItemId((int)index);
        Field(separation, "Separation mode", separationChoice);
        separation.AddChild(alliedYield);
        separation.AddChild(meleeSidestep);
        separation.AddChild(meleeHold);
        separation.AddChild(weightedSeparation);

        AddButton(column, "Edit painted deployment", () => PaintRequested?.Invoke());
        paintSection = new VBoxContainer { Visible = false };
        column.AddChild(paintSection);
        paintControls = Section(paintSection, "Paint tools", true, out paintHeader);
        FactionChoice.AddItem("Blue", Factions.Blue);
        FactionChoice.AddItem("Red", Factions.Red);
        FactionChoice.AddItem("Green", Factions.Green);
        PaintToolChoice.AddItem("Soldiers");
        PaintToolChoice.AddItem("Obstacles");
        PaintToolChoice.AddItem("Defenses");
        Field(paintControls, "Paint tool", PaintToolChoice);
        placementGridToggle.Toggled += enabled => PlacementGridChanged?.Invoke(enabled);
        paintControls.AddChild(placementGridToggle);
        var soldierControls = new VBoxContainer();
        paintControls.AddChild(soldierControls);
        var factionRow = Field(paintControls, "Paint faction", FactionChoice);
        for (int type = 0; type < Catalog.Count; type++)
            BrushChoice.AddItem($"{Catalog[type].Shape} • {Catalog[type].Name}", type);
        Field(soldierControls, "Soldier type", BrushChoice);
        soldierControls.AddChild(Hint("Left-drag: paint   •   Right-drag: erase"));
        foreach (int size in new[] { 1, 3, 5, 10 })
            SoldierBrushSizeChoice.AddItem($"{size}×{size}", size);
        SoldierBrushSizeChoice.Select(1); // Preserve the current 3×3 default.
        Field(soldierControls, "Paint size", SoldierBrushSizeChoice);
        var wallHelp = new Label { Text = "Drag left to draw rectangle\nDrag right to erase area", Visible = false };
        paintControls.AddChild(wallHelp);
        var defenseControls = new VBoxContainer { Visible = false };
        paintControls.AddChild(defenseControls);
        for (int type = 0; type < Buildings.Count; type++) DefenseChoice.AddItem(Buildings[type].Name, type);
        Field(defenseControls, "Defense type", DefenseChoice);
        defenseControls.AddChild(new Label { Text = "Left click to place • right click to remove" });
        PaintToolChoice.ItemSelected += tool =>
        {
            soldierControls.Visible = tool == 0;
            wallHelp.Visible = tool == 1;
            defenseControls.Visible = tool == 2;
            factionRow.Visible = tool != 1;
        };
        var orders = Section(paintControls, "Battle orders", true, out _);
        string[] teamNames = { "Blue", "Red", "Green" };
        for (int faction = 0; faction < Factions.Count; faction++)
        {
            var choice = new OptionButton();
            choice.AddItem("Auto Attack", (int)GuardOrder.AutoAttack);
            choice.AddItem("Hold Ground", (int)GuardOrder.Hold);
            choice.AddItem("Hold Ground – Return to Position", (int)GuardOrder.Return);
            choice.AddItem("Hold Ground – Auto Attack", (int)GuardOrder.Counterattack);
            choice.Select(0);
            teamOrders[faction] = choice;
            Field(orders, teamNames[faction], choice);
        }
        Field(orders, "Melee guard radius", GuardRadius);
        AddButton(paintControls, "Start painted battle", () => BattleRequested?.Invoke());
        AddButton(paintControls, "Save deployment", () => SaveRequested?.Invoke());
        AddButton(paintControls, "Load deployment", () => LoadRequested?.Invoke());
        var display = Section(column, "Display and controls", false, out _);
        display.AddChild(smoothMotion);
        groundShadows.Toggled += enabled => GroundShadowsChanged?.Invoke(enabled);
        display.AddChild(groundShadows);
        performanceToggle = new CheckButton { Text = "Performance details (F4)", ButtonPressed = true };
        performanceToggle.Toggled += _ => UpdatePanelVisibility();
        display.AddChild(performanceToggle);
        display.AddChild(Hint("WASD / arrows: pan  •  Wheel: zoom\nSpace: pause  •  F1: both panels  •  F2: left only\nF4: performance details"));

        var actions = new HBoxContainer();
        shell.AddChild(actions);
        var clear = new Button { Text = "Clear all", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        clear.Pressed += () => ClearRequested?.Invoke();
        actions.AddChild(clear);
        PauseButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        PauseButton.Pressed += () => PauseRequested?.Invoke();
        actions.AddChild(PauseButton);
        Status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        shell.AddChild(Status);
        right = new VBoxContainer
        {
            AnchorLeft = 1, AnchorRight = 1, AnchorBottom = 1,
            OffsetLeft = -296, OffsetRight = -12, OffsetTop = 12, OffsetBottom = -12,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        AddChild(right);
        var compact = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        right.AddChild(compact);
        compact.AddChild(Metrics);
        overlay = new PanelContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        right.AddChild(overlay);
        var debugScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        overlay.AddChild(debugScroll);
        var debug = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        debugScroll.AddChild(debug);
        var cpuDebug = Section(debug, "CPU and separation", false, out _, true);
        cpuDebug.AddChild(profileMovement);
        cpuDebug.AddChild(CpuMetrics);
        var obstacleDebug = Section(debug, "Obstacles and terrain", false, out _, true);
        obstacleDebug.AddChild(profileObstacles);
        obstacleDebug.AddChild(reuseCorridors);
        obstacleDebug.AddChild(reuseWaypoints);
        obstacleDebug.AddChild(ObstacleMetrics);
        Section(debug, "Rendering and memory", false, out _, true).AddChild(EngineMetrics);
    }
    private static Label DebugLabel()
    {
        var label = new Label { HorizontalAlignment = HorizontalAlignment.Right,
            AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontSizeOverride("font_size", 14);
        return label;
    }

    // =========================================================
    // Shared horizontal label/value row: one line instead of two for every tweakable field.
    private static HBoxContainer Field(VBoxContainer column, string title, Control input)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 8);
        var label = new Label
        {
            Text = title,
            VerticalAlignment = VerticalAlignment.Center,
            ClipText = true,
            TooltipText = title,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = 1f
        };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", new Color("#C7D2E0"));
        row.AddChild(label);
        input.CustomMinimumSize = new Vector2(Mathf.Max(input.CustomMinimumSize.X, 166f), input.CustomMinimumSize.Y);
        input.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        input.SizeFlagsStretchRatio = 1.2f;
        if (input is OptionButton choice)
        {
            // Long layout/order names should not force the complete panel to grow.
            choice.FitToLongestItem = false;
            choice.ClipText = true;
        }
        row.AddChild(input);
        column.AddChild(row);
        return row;
    }

    // =========================================================
    private static Label Hint(string text)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeColorOverride("font_color", new Color("#91A5B8"));
        label.AddThemeFontSizeOverride("font_size", 12);
        return label;
    }

    // =========================================================
    // Left sections have colored headers and an inset body; right diagnostics stay unchanged.
    private static VBoxContainer Section(VBoxContainer parent, string title, bool open, out Button header, bool alignRight = false)
    {
        var button = new Button
        {
            ToggleMode = true, ButtonPressed = open,
            Alignment = alignRight ? HorizontalAlignment.Right : HorizontalAlignment.Left
        };
        button.Text = (open ? "▾ " : "▸ ") + title;
        if (!alignRight)
        {
            button.AddThemeFontSizeOverride("font_size", 15);
            button.AddThemeColorOverride("font_color", new Color("#D5EDFC"));
            button.AddThemeStyleboxOverride("normal", SectionStyle(new Color("#24374B")));
            button.AddThemeStyleboxOverride("hover", SectionStyle(new Color("#304B62")));
            button.AddThemeStyleboxOverride("pressed", SectionStyle(new Color("#304B62")));
        }
        parent.AddChild(button);

        var body = new VBoxContainer();
        if (!alignRight)
        {
            var background = new PanelContainer { Visible = open };
            background.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color("#1C2735") });
            parent.AddChild(background);
            var inset = new MarginContainer();
            inset.AddThemeConstantOverride("margin_left", 10);
            inset.AddThemeConstantOverride("margin_right", 10);
            inset.AddThemeConstantOverride("margin_top", 8);
            inset.AddThemeConstantOverride("margin_bottom", 10);
            background.AddChild(inset);
            inset.AddChild(body);
            body.AddThemeConstantOverride("separation", 7);
            button.Toggled += expanded =>
            {
                background.Visible = expanded;
                button.Text = (expanded ? "▾ " : "▸ ") + title;
            };
        }
        else
        {
            body.Visible = open;
            parent.AddChild(body);
            button.Toggled += expanded =>
            {
                body.Visible = expanded;
                button.Text = (expanded ? "▾ " : "▸ ") + title;
            };
        }
        header = button;
        return body;
    }

    private static StyleBoxFlat SectionStyle(Color background) => new()
    {
        BgColor = background,
        BorderColor = new Color("#64A9CA"),
        BorderWidthLeft = 3,
        ContentMarginLeft = 10,
        ContentMarginRight = 8,
        ContentMarginTop = 6,
        ContentMarginBottom = 6
    };

    private static void AddButton(VBoxContainer column, string title, Action action)
    {
        var button = new Button { Text = title, CustomMinimumSize = new Vector2(0f, 30f) };
        button.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color("#29425A") });
        button.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = new Color("#355873") });
        button.Pressed += action;
        column.AddChild(button);
    }
    // =========================================================
    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (input is not InputEventKey key || !key.Pressed || key.Echo) return;
        if (key.PhysicalKeycode == Controls.ToggleSidePanels)
        {
            // F1 always hides both if either is visible; press again to show both.
            bool showBoth = !leftPanelVisible && !rightPanelVisible;
            leftPanelVisible = rightPanelVisible = showBoth;
            UpdatePanelVisibility();
            GetViewport().SetInputAsHandled();
        }
        else if (key.PhysicalKeycode == Controls.ToggleLeftPanel)
        {
            leftPanelVisible = !leftPanelVisible;
            UpdatePanelVisibility();
            GetViewport().SetInputAsHandled();
        }
        else if (key.PhysicalKeycode == Key.F4)
        {
            performanceToggle.ButtonPressed = !performanceToggle.ButtonPressed;
            GetViewport().SetInputAsHandled();
        }
        if (key.PhysicalKeycode == Key.Space) PauseRequested?.Invoke();
    }
}
