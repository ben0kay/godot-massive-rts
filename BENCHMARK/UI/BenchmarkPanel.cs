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
    public bool ReuseCorridors => reuseCorridors.ButtonPressed;
    public bool ProfileObstacles => profileObstacles.ButtonPressed;
    public readonly Button PauseButton = new() { Text = "Pause" };
    private PanelContainer left, overlay;
    private CheckButton performanceToggle;
    private bool sidePanelsVisible = true;

    private void UpdatePanelVisibility()
    {
        left.Visible = sidePanelsVisible;
        overlay.Visible = sidePanelsVisible && performanceToggle.ButtonPressed;
    }
    public static readonly int[] Counts = { 1000, 5000, 10000, 20000, 25000, 50000, 100000 };
    public int RequestedCount => Counts[CountChoice.Selected];
    // =========================================================
    public override void _Ready()
    {
        left = new PanelContainer
        {
            AnchorBottom = 1, OffsetLeft = 12, OffsetRight = 322, OffsetTop = 12, OffsetBottom = -12
        };
        AddChild(left);
        var shell = new VBoxContainer();
        left.AddChild(shell);
        shell.AddChild(new Label { Text = "BENCH0 • MASSIVE RTS" });
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true
        };
        shell.AddChild(scroll);
        var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
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
        preset.AddChild(new Label
        {
            Text = "New formations use all six shapes in mixed modes; Melee excludes archers.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
        Field(preset, "Ranged %", RangedPercent);
        TerrainChoice.AddItem("Flat ground");
        TerrainChoice.AddItem("Test plateau");
        Field(preset, "Preset terrain", TerrainChoice);
        preset.AddChild(TestWall);
        AddButton(preset, "Spawn / Reset selected", () => SpawnRequested?.Invoke());

        var cameraControls = Section(column, "Camera", true, out _);
        Field(cameraControls, "Rotation (0° = straight, 45° = diagonal)", cameraRotation);
        cameraControls.AddChild(cameraRotationValue);
        cameraRotation.ValueChanged += degrees =>
        {
            cameraRotationValue.Text = $"{degrees:0}°" + (degrees == 45 ? " (original diagonal)" : "");
            CameraRotationChanged?.Invoke((float)degrees);
        };
        AddButton(cameraControls, "Reset camera rotation (45°)", () => cameraRotation.Value = 45);

        var combat = Section(column, "Live combat", false, out _);
        Field(combat, "Ranged fire-rate multiplier", FireRate);
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
        var soldierControls = new VBoxContainer();
        paintControls.AddChild(soldierControls);
        var factionLabel = new Label { Text = "Paint faction" };
        paintControls.AddChild(factionLabel);
        paintControls.AddChild(FactionChoice);
        for (int type = 0; type < Catalog.Count; type++)
            BrushChoice.AddItem($"{Catalog[type].Shape} • {Catalog[type].Name}", type);
        Field(soldierControls, "Drag left to paint • right to erase", BrushChoice);
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
            factionLabel.Visible = FactionChoice.Visible = tool != 1;
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
        display.AddChild(new Label { Text = "WASD / arrows: pan\nMouse wheel: zoom\nSpace: pause • F1: side panels\nF4: performance details" });

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
        var right = new VBoxContainer
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

    private static VBoxContainer Section(VBoxContainer parent, string title, bool open, out Button header, bool alignRight = false)
    {
        var button = new Button { ToggleMode = true, ButtonPressed = open, Alignment = alignRight ? HorizontalAlignment.Right : HorizontalAlignment.Left };
        button.Text = (open ? "▾ " : "▸ ") + title;
        parent.AddChild(button);
        var body = new VBoxContainer { Visible = open };
        parent.AddChild(body);
        button.Toggled += expanded =>
        {
            body.Visible = expanded;
            button.Text = (expanded ? "▾ " : "▸ ") + title;
        };
        header = button;
        return body;
    }

    private static void AddButton(VBoxContainer column, string title, Action action)
    {
        var button = new Button { Text = title };
        button.Pressed += action;
        column.AddChild(button);
    }
    private static void Field(VBoxContainer column, string title, Control input)
    { column.AddChild(new Label { Text = title }); column.AddChild(input); }
    // =========================================================
    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (input is not InputEventKey key || !key.Pressed || key.Echo) return;
        if (key.PhysicalKeycode == Controls.ToggleSidePanels)
        {
            sidePanelsVisible = !sidePanelsVisible;
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
