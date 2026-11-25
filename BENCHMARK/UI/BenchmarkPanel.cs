// Benchmark-only controls and throttled, permanent performance readout.
using System;
using Godot;
namespace MassiveRts;

public partial class BenchmarkPanel : CanvasLayer
{
	public event Action SpawnRequested, ClearRequested, PauseRequested;
	public event Action SavePresetRequested;
	public readonly OptionButton SavedBattleChoice = new();
	public readonly LineEdit PresetName = new() { PlaceholderText = "Name this battle", MaxLength = 48 };
	public event Action PaintRequested, BattleRequested, SaveRequested, LoadRequested;
	public event Action<float> CameraRotationChanged;
	public event Action<float> CameraTiltChanged;
	public event Action<bool> CameraFreeRoamChanged;
	internal event Action<BenchmarkLighting> LightingChanged;
	internal event Action<float, float, float> PlaytestLightingChanged;
	internal event Action<float> ShadowDistanceChanged;
	private readonly SpinBox shadowDistance = new() { MinValue = 100, MaxValue = 6000, Step = 50, Value = 1800 };
	internal float ShadowDistance => (float)shadowDistance.Value;
	public event Action<bool> GroundShadowsChanged;
	public event Action<bool> PlacementGridChanged;
	public event Action<long> PaintToolSelected;
	public event Action<byte> BrushSelected;
	private readonly CheckButton placementGridToggle = new()
	{
		Text = "Show placement grid", ButtonPressed = true
	};
	public bool ShowPlacementGrid => placementGridToggle.ButtonPressed;
	private readonly CheckButton groundShadows = new() { Text = "Ground shadows", ButtonPressed = true };
	public bool GroundShadows => groundShadows.ButtonPressed;
	public RuntimeUnitCatalog Catalog { private get; set; }
	public readonly CheckButton DeathEffects = new() { Text = "Death effects", ButtonPressed = true };
	public readonly HSlider PaintSpread = new() { MinValue = 0, MaxValue = 3, Step = 0.1, Value = 1, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
	private readonly CheckButton smoothMotion = new() { Text = "Smooth soldier motion", ButtonPressed = true };
	private readonly HSlider cameraRotation = new() { MinValue = 0, MaxValue = 360, Step = 1, Value = 45,
		SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
	private readonly Label cameraRotationValue = new() { Text = "45° (original diagonal)" };
	public bool SmoothMotion => smoothMotion.ButtonPressed;
	internal event Action<bool, float, float, float> WeatherChanged;

	private readonly OptionButton separationChoice = new();
	private readonly HSlider hardOverlap = new()
	{
		MaxValue = HardSeparation.MaxOverlapPercent, Step = 1, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		TooltipText = "Overlap allowance for Hard separation, relative to the combined footprint radii."
	};
	internal float HardOverlap => (float)hardOverlap.Value * 0.01f;
	private readonly CheckButton alliedYield = new() { Text = "Allied archer yielding", ButtonPressed = true };
	private readonly CheckButton meleeSidestep = new() { Text = "Melee sidestep", ButtonPressed = true };
	private readonly CheckButton meleeHold = new() { Text = "Hold melee contact", ButtonPressed = true };
	private readonly CheckButton weightedSeparation = new() { Text = "Footprint-weighted separation", ButtonPressed = true };
	private readonly CheckButton rangedTargetVariety = new() { Text = "Ranged target variety", ButtonPressed = true };
	private readonly CheckButton slowPursuitRefresh = new() { Text = "Slower long-range retargeting", ButtonPressed = true };
	public bool SlowPursuitRefresh => slowPursuitRefresh.ButtonPressed;
	private readonly CheckButton delayFarMeleeRetarget = new() { Text = "Delay distant melee retargeting", ButtonPressed = true };
	private readonly CheckButton limitPursuitQueries = new() { Text = "Cap long-range searches (512/step)", ButtonPressed = true };
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
	private readonly CheckButton enemySeparation = new() { Text = "Strong enemy separation" };
	internal bool StrongEnemySeparation => Separation != SeparationMode.Hard && enemySeparation.ButtonPressed;
	public RuntimeBuildingCatalog Buildings { get; set; }
	internal bool AutoBattlerPlaytest { get; set; }
	public event Action<int> DefenseSelected;
	private readonly OptionButton[] teamOrders = new OptionButton[Factions.Count];
	public GuardOrder OrderFor(int faction) => (GuardOrder)teamOrders[faction].GetSelectedId();

	internal void RestoreBattleSettings(DeploymentPainter.BattleSettings settings)
	{
		Seed.Value = settings.Seed;
		GuardRadius.Value = settings.GuardRadius;
		foreach (byte faction in Factions.DisplayOrder)
			teamOrders[faction].Select(teamOrders[faction].GetItemIndex((int)settings.Orders[faction]));
	}

	public readonly SpinBox GuardRadius = new() { MinValue = 1, MaxValue = 100, Step = 1, Value = 12 };
	public readonly OptionButton TerrainChoice = new();
	public readonly CheckButton TestWall = new() { Text = "Test wall in preset" };
	private UnitPlacementBar unitBar;
	private bool editingDeployment;
	private int paintTool;
	public readonly OptionButton SoldierPaintType = new();
	public readonly OptionButton SoldierBrushSizeChoice = new();
	public event Action<byte> FactionSelected;
	private VBoxContainer paintControls, paintSection;
	private Button paintHeader;
	public bool EditingDeployment
	{
		set
		{
			editingDeployment = value;
			paintSection.Visible = value;
			if (value) paintHeader.ButtonPressed = true;
			UpdatePlacementBar();
		}
	}
	public readonly OptionButton CountChoice = new(), ModeChoice = new(), LayoutChoice = new();
	public readonly SpinBox Seed = new() { MinValue = 0, MaxValue = 2147483647, Value = 12345, Step = 1 };
	public readonly SpinBox FireRate = new() { MinValue = 0.2, MaxValue = 20, Step = 0.2, Value = 1 };
	public readonly SpinBox RangedPercent = new() { MinValue = 0, MaxValue = 100, Step = 5, Value = 40 };
	public readonly Label Status = new();
	public readonly BenchmarkSummary Summary = new();
	public readonly Label CpuMetrics = DebugLabel(), ObstacleMetrics = DebugLabel(), EngineMetrics = DebugLabel();
	private readonly CheckButton profileMovement = new() { Text = "Profile movement" };
	public bool ProfileMovement => profileMovement.ButtonPressed;
	private readonly CheckButton profileObstacles = new() { Text = "Sample obstacle queries" };
	private readonly CheckButton reuseCorridors = new() { Text = "Reuse corridor checks", ButtonPressed = true };
	private readonly CheckButton reuseWaypoints = new() { Text = "Reuse route waypoints", ButtonPressed = true };
	public bool ReuseWaypoints => reuseWaypoints.ButtonPressed;
	public bool ReuseCorridors => reuseCorridors.ButtonPressed;
	public bool ProfileObstacles => profileObstacles.ButtonPressed;
	public readonly Button PauseButton = new() { Text = "Pause" };
	private PanelContainer left, overlay;
	private VBoxContainer right;
	private CheckButton performanceToggle;
	private readonly CheckButton shieldsEnabled = new() { Text = "Enable shields", ButtonPressed = true };
	internal event Action<bool> ShieldShellsChanged;
	internal event Action<bool> ShieldDomesChanged;
	private readonly CheckButton shieldDomes = new() { Text = "Show generator domes", ButtonPressed = true };
	internal bool ShowShieldDomes => shieldDomes.ButtonPressed;
	private readonly CheckButton shieldShells = new() { Text = "Show shield shells", ButtonPressed = true };
	internal bool ShowShieldShells => shieldShells.ButtonPressed;
	private readonly CheckButton shieldRadii = new() { Text = "Show generator radii" };
	internal bool ShowShieldRadii => shieldRadii.ButtonPressed;
	internal bool ShieldsEnabled => shieldsEnabled.ButtonPressed;
	private readonly SpinBox shieldInterval = new() { MinValue = 0.1, MaxValue = 1, Step = 0.05, Value = ShieldSystem.DefaultInterval };
	internal float ShieldInterval => (float)shieldInterval.Value;
	private readonly CheckButton attackRangesToggle = new() { Text = "Attack ranges on hover" };
	public bool ShowAttackRanges => attackRangesToggle.ButtonPressed;
	private bool leftPanelVisible = true, rightPanelVisible = true;

	// =========================================================
	// F1 controls both sides; F2 affects the left side without touching the right HUD.
	private void UpdatePanelVisibility()
	{
		left.Visible = leftPanelVisible;
		UpdatePlacementBar();
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
		var heading = new Label { Text = AutoBattlerPlaytest ? "AUTOBATTLER PLAYTEST" : "AUTO-BATTLE BENCHMARK" };
		heading.AddThemeFontSizeOverride("font_size", 17);
		heading.AddThemeColorOverride("font_color", new Color("#CCE8F6"));
		var titleRow = new HBoxContainer();
		heading.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		titleRow.AddChild(heading);
		if (!AutoBattlerPlaytest)
			titleRow.AddChild(UIButtonFactory.Create("Game Editor", () => MenuNavigation.Open(this, GameEditor.ScenePath)));
		shell.AddChild(titleRow);
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

		var preset = Section(column, "Scenario preset", true, out var presetHeader);
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
		SavedBattleChoice.AddItem("Generated battle");
		Field(preset, "Saved battle", SavedBattleChoice);
		AddButton(preset, "Spawn / Reset selected", () => SpawnRequested?.Invoke());
		if (AutoBattlerPlaytest)
		{
			presetHeader.Hide();
			((Control)preset.GetParent().GetParent()).Hide();
		}

		var cameraControls = Section(column, "Camera", true, out _);
		var freeRoam = new CheckButton { Text = "Perspective free roam" };
		cameraControls.AddChild(freeRoam);
		var orbitControls = new VBoxContainer();
		cameraControls.AddChild(orbitControls);
		cameraRotation.TooltipText = "0° = straight, 45° = original diagonal view";
		cameraRotationValue.Text = "45°";
		cameraRotationValue.CustomMinimumSize = new Vector2(46f, 0f);
		cameraRotationValue.HorizontalAlignment = HorizontalAlignment.Right;
		var rotationRow = Field(orbitControls, "Camera yaw", cameraRotation);
		rotationRow.AddChild(cameraRotationValue);
		cameraRotation.ValueChanged += degrees =>
		{
			cameraRotationValue.Text = $"{degrees:0}°";
			CameraRotationChanged?.Invoke((float)degrees);
		};
		var tilt = new HSlider { MinValue = 15, MaxValue = 85, Step = 0.1, Value = IsometricCamera.DefaultTilt,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		var tiltValue = new Label { Text = $"{tilt.Value:0.#}°", CustomMinimumSize = new Vector2(46f, 0f),
			HorizontalAlignment = HorizontalAlignment.Right };
		Field(orbitControls, "Camera tilt", tilt).AddChild(tiltValue);
		tilt.ValueChanged += degrees =>
		{
			tiltValue.Text = $"{degrees:0.#}°";
			CameraTiltChanged?.Invoke((float)degrees);
		};
		AddButton(orbitControls, "Reset yaw and tilt", () =>
		{
			cameraRotation.Value = 45;
			tilt.Value = IsometricCamera.DefaultTilt;
		});
		var flyHelp = new Label { Text = "Arrows / WASD to fly · Hold right-click to look\nE / Q to rise or descend · Shift to speed up",
			AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false };
		cameraControls.AddChild(flyHelp);
		freeRoam.Toggled += enabled =>
		{
			orbitControls.Visible = !enabled;
			flyHelp.Visible = enabled;
			CameraFreeRoamChanged?.Invoke(enabled);
		};

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
		separationChoice.AddItem("Hard", (int)SeparationMode.Hard);
		separationChoice.Select(1);
		Field(separation, "Separation mode", separationChoice);
		var overlapControls = new HBoxContainer();
		var overlapValue = new Label { Text = "0%", CustomMinimumSize = new Vector2(32f, 0f) };
		overlapControls.AddChild(hardOverlap);
		overlapControls.AddChild(overlapValue);
		var overlapRow = Field(separation, "Hard overlap", overlapControls);
		overlapRow.Hide();
		hardOverlap.ValueChanged += value => overlapValue.Text = $"{value:0}%";
		separationChoice.ItemSelected += index =>
		{
			Separation = (SeparationMode)separationChoice.GetItemId((int)index);
			overlapRow.Visible = Separation == SeparationMode.Hard;
			enemySeparation.Disabled = Separation == SeparationMode.Hard;
		};
		separation.AddChild(enemySeparation);
		separation.AddChild(alliedYield);
		separation.AddChild(meleeSidestep);
		separation.AddChild(meleeHold);
		separation.AddChild(weightedSeparation);

		var shields = Section(column, "Shields", false, out _);
		shields.AddChild(shieldsEnabled);
		shields.AddChild(shieldRadii);
		shields.AddChild(shieldDomes);
		shields.AddChild(shieldShells);
		shieldDomes.Toggled += _ => ShieldDomesChanged?.Invoke(ShowShieldDomes && ShieldsEnabled);
		shieldsEnabled.Toggled += _ => ShieldDomesChanged?.Invoke(ShowShieldDomes && ShieldsEnabled);
		shieldShells.Toggled += enabled => ShieldShellsChanged?.Invoke(enabled);
		Field(shields, "Coverage refresh (s)", shieldInterval);

		AddButton(column, AutoBattlerPlaytest ? "Edit Blue deployment" : "Edit painted deployment", () => PaintRequested?.Invoke());
		paintSection = new VBoxContainer { Visible = false };
		column.AddChild(paintSection);
		paintControls = Section(paintSection, "Paint tools", true, out paintHeader);
		// Single-click radio selection keeps all three tools visible without opening a dropdown.
		var toolRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		toolRow.AddThemeConstantOverride("separation", 8);
		toolRow.AddChild(new Label
		{
			Text = "Paint tool", VerticalAlignment = VerticalAlignment.Center,
			CustomMinimumSize = new Vector2(86f, 0f)
		});
		var toolOptions = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		toolOptions.AddThemeConstantOverride("separation", 8);
		toolRow.AddChild(toolOptions);
		paintControls.AddChild(toolRow);
		var toolGroup = new ButtonGroup();
		var toolButtons = new CheckBox[3];
		string[] toolNames = { "Soldiers", "Obstacles", "Defenses" };
		for (int i = 0; i < toolButtons.Length; i++)
		{
			var button = new CheckBox
			{
				Text = toolNames[i], ButtonGroup = toolGroup, ButtonPressed = i == 0
			};
			toolOptions.AddChild(button);
			toolButtons[i] = button;
		}
		placementGridToggle.Toggled += enabled => PlacementGridChanged?.Invoke(enabled);
		paintControls.AddChild(placementGridToggle);
		var soldierControls = new VBoxContainer { CustomMinimumSize = new Vector2(244, 0) };
		SoldierPaintType.AddItem("Brush");
		SoldierPaintType.AddItem("Filled rectangle");
		SoldierPaintType.Select(0);
		Field(soldierControls, "Brush type", SoldierPaintType);
		foreach (int size in new[] { 1, 3, 5, 10 })
			SoldierBrushSizeChoice.AddItem($"{size}×{size}", size);
		SoldierBrushSizeChoice.Select(1); // Preserve the current 3×3 default.
		var brushSizeRow = Field(soldierControls, "Brush size", SoldierBrushSizeChoice);
		SoldierPaintType.ItemSelected += index => brushSizeRow.Visible = index == 0;
		var density = new HBoxContainer();
		var spreadLabel = new Label { Text = "1.0×" };
		density.AddChild(PaintSpread);
		density.AddChild(spreadLabel);
		PaintSpread.ValueChanged += value => spreadLabel.Text = value == 0 ? "Touching" : $"{value:F1}×";
		PaintSpread.TooltipText = "Spacing relative to the default. The unit footprint sets the minimum.";
		Field(soldierControls, "Density", density);
		var wallHelp = new Label { Text = "Drag left to draw rectangle\nDrag right to erase area", Visible = false };
		paintControls.AddChild(wallHelp);
		var defenseControls = new VBoxContainer { Visible = false };
		paintControls.AddChild(defenseControls);
		defenseControls.AddChild(new Label { Text = "Left click to place • right click to remove" });
		for (int i = 0; i < toolButtons.Length; i++)
		{
			int tool = i;
			toolButtons[i].Toggled += selected =>
			{
				if (!selected) return;
				paintTool = tool;
				soldierControls.Visible = tool == 0;
				UpdatePlacementBar();
				wallHelp.Visible = tool == 1;
				defenseControls.Visible = tool == 2;
				PaintToolSelected?.Invoke(tool);
			};
		}
		var orders = Section(paintControls, "Battle orders", true, out var orderHeader);
		foreach (byte faction in Factions.DisplayOrder)
		{
			var choice = new OptionButton();
			choice.AddItem("Auto Attack", (int)GuardOrder.AutoAttack);
			choice.AddItem("Hold Ground", (int)GuardOrder.Hold);
			choice.AddItem("Hold Ground – Return to Position", (int)GuardOrder.Return);
			choice.AddItem("Hold Ground – Auto Attack", (int)GuardOrder.Counterattack);
			choice.Select(0);
			teamOrders[faction] = choice;
			Field(orders, Factions.Name(faction), choice);
		}
		Field(orders, "Melee guard radius", GuardRadius);
		AddButton(paintControls, "Start painted battle", () => BattleRequested?.Invoke());
		if (AutoBattlerPlaytest)
		{
			toolRow.Hide();
			orderHeader.Hide();
			((Control)orders.GetParent().GetParent()).Hide();
		}
		else
		{
			Field(paintControls, "Preset name", PresetName);
			AddButton(paintControls, "Save as preset", () => SavePresetRequested?.Invoke());
			AddButton(paintControls, "Save deployment", () => SaveRequested?.Invoke());
			AddButton(paintControls, "Load deployment", () => LoadRequested?.Invoke());
		}
		var renderingControls = Section(column, "Rendering", false, out _);
		if (AutoBattlerPlaytest)
		{
			var lighting = Section(renderingControls, "Lighting", true, out _);
			var sun = LabeledSlider(lighting, "Sun strength", 400, BattlefieldLighting.DefaultSunPercent, 1, "%");
			var fill = LabeledSlider(lighting, "Ambient fill", 200, BattlefieldLighting.DefaultFillPercent, 1, "%");
			var direction = LabeledSlider(lighting, "Sun direction", 360, 0, 1, "°");
			void ApplyLighting(double _) => PlaytestLightingChanged?.Invoke(
				(float)sun.Value / 100f, (float)fill.Value / 100f, (float)direction.Value);
			sun.ValueChanged += ApplyLighting;
			fill.ValueChanged += ApplyLighting;
			direction.ValueChanged += ApplyLighting;
			AddButton(lighting, "Reset lighting", () =>
			{
				sun.Value = BattlefieldLighting.DefaultSunPercent;
				fill.Value = BattlefieldLighting.DefaultFillPercent;
				direction.Value = 0;
			});
		}
		if (!AutoBattlerPlaytest)
		{
			var lighting = new OptionButton();
			lighting.AddItem("Current");
			lighting.AddItem("Lit");
			lighting.AddItem("Lit + real shadows");
			lighting.Select(0);
			Field(renderingControls, "Battlefield lighting", lighting);
			var distanceRow = Field(renderingControls, "Shadow distance", shadowDistance);
			distanceRow.Visible = false;
			lighting.ItemSelected += index =>
			{
				bool real = index == (long)BenchmarkLighting.RealShadows;
				groundShadows.Disabled = real;
				distanceRow.Visible = real;
				LightingChanged?.Invoke((BenchmarkLighting)index);
			};
			shadowDistance.ValueChanged += value => ShadowDistanceChanged?.Invoke((float)value);
		}
		var weather = Section(renderingControls, "Fog and haze", false, out _);
		var weatherToggle = new CheckButton { Text = "Enable fog and haze" };
		weather.AddChild(weatherToggle);
		var mist = LabeledSlider(weather, "Mist density", 100, 45, 1, "%");
		var drift = LabeledSlider(weather, "Drift speed", 6, 2, 0.1, " u/s");
		var haze = LabeledSlider(weather, "Haze", 100, 25, 1, "%");
		void UpdateWeather() => WeatherChanged?.Invoke(weatherToggle.ButtonPressed,
			(float)mist.Value / 100f, (float)drift.Value, (float)haze.Value / 100f);
		weatherToggle.Toggled += _ => UpdateWeather();
		mist.ValueChanged += _ => UpdateWeather();
		drift.ValueChanged += _ => UpdateWeather();
		haze.ValueChanged += _ => UpdateWeather();

		var display = Section(column, "Display and controls", false, out _);
		display.AddChild(smoothMotion);
		display.AddChild(DeathEffects);
		groundShadows.Toggled += enabled => GroundShadowsChanged?.Invoke(enabled);
		display.AddChild(groundShadows);
		display.AddChild(attackRangesToggle);
		display.AddChild(Hint("Gold: attack arc at target size (Circle if none)\nGreen/red: in/out of melee geometry\nBlue: shockwave radius"));
		performanceToggle = new CheckButton { Text = "Performance details (F4)", ButtonPressed = true };
		performanceToggle.Toggled += _ => UpdatePanelVisibility();
		display.AddChild(performanceToggle);
		display.AddChild(Hint("WASD / arrows: pan  •  Wheel: zoom\nSpace: pause  •  F1: both panels  •  F2: left only\nF4: performance details"));

		var actions = new HBoxContainer();
		shell.AddChild(actions);
		var clear = new Button { Text = AutoBattlerPlaytest ? "Clear test" : "Clear all", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
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
		right.AddChild(Summary);
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
		var rendering = Section(debug, "Rendering and memory", false, out _, true);
		rendering.AddChild(EngineMetrics);
		unitBar = new UnitPlacementBar { Visible = false };
		AddChild(unitBar);
		unitBar.Build(Catalog, Buildings, compact: AutoBattlerPlaytest, brushControls: soldierControls);
		unitBar.UnitSelected += type => BrushSelected?.Invoke(type);
		unitBar.FactionSelected += faction => FactionSelected?.Invoke(faction);
		unitBar.DefenseSelected += type => DefenseSelected?.Invoke(type);

	}
	internal void MakeRoomForBattleClock(bool visible) => right.OffsetTop = visible ? 46 : 12;

	private void UpdatePlacementBar()
	{
		if (unitBar == null) return;
		unitBar.ShowDefenses(paintTool == 2);
		unitBar.Visible = leftPanelVisible && editingDeployment && paintTool != 1;
		left.OffsetBottom = right.OffsetBottom = unitBar.Visible ? -unitBar.BarHeight - 24 : -12;
	}

	private static HSlider LabeledSlider(VBoxContainer column, string title, double max, double initial, double step, string suffix)
	{
		var slider = new HSlider { MaxValue = max, Value = initial, Step = step, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		var controls = new HBoxContainer();
		var value = new Label { Text = $"{initial:0.#}{suffix}", CustomMinimumSize = new Vector2(52f, 0f) };
		controls.AddChild(slider);
		controls.AddChild(value);
		slider.ValueChanged += amount => value.Text = $"{amount:0.#}{suffix}";
		Field(column, title, controls);
		return slider;
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
