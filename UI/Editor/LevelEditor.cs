// Hosts the F6 mode picker, shared map controls and save/load workflow.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MassiveRts;

public partial class LevelEditor : Node
{
    private VBoxContainer start, toolsPanel, context, toolbar, mapToolsContext, unitContext, defenseContext;
    private Control interfaceRoot, toolSidebar;
    private Label toolsHeading;
    private Control legacyUnitPicker, legacyDefenseHeading, legacyUnitDivider, legacyDefenseDivider;
    private Label status, startStatus, strength;
    private OptionButton toolPicker, defensePicker, newLevelTerrain;
    private HBoxContainer campaignButtons;
    private GridContainer levelGrid;
    private Button returnToLevel;
    private string selectedCampaignId;
    private LevelEditorSection authoringSection;
    private Button[] authoringButtons;
    private BuildingDefinition[] defenseDefinitions;
    private LineEdit levelTitle;
    private TextEdit levelDescription;
    private SpinBox mapSize;
    private CheckBox planView, snap, showGrid;
    private ConfirmationDialog confirmation;
    private AcceptDialog settingsDialog;
    private Action accepted;
    private CampaignCatalog catalog;
    private LevelEditorMode mode;
    private CampaignDefinition campaign;
    private CommandSandbox map;
    private Node runningTest;
    private Button testButton;
    private IsometricCamera camera;
    private LevelDefinition metadata;
    private string activeId;
    private bool dirty, previewing;
    private UnitDefinition[] units, aircraft;
    private readonly Stack<(Action Undo, Action Redo)> undo = new(), redo = new();
    private readonly List<Node3D> detached = new();

    // =========================================================
    public override void _Ready()
    {
        if (!LevelEditorSave.CanWrite)
        {
            var message = new Label { Text = "Open LevelEditor.tscn in the Godot editor and press F6." };
            AddChild(message);
            return;
        }
        var roster = ResourceLoader.Load<UnitCatalog>(BenchmarkArena.UnitCatalogPath, cacheMode: ResourceLoader.CacheMode.IgnoreDeep);
        units = roster.Definitions.Where(unit => unit != null).ToArray();
        aircraft = roster.Aircraft.Where(unit => unit != null).ToArray();
        defenseDefinitions = GD.Load<BuildingCatalog>("res://WORLD/BUILDINGS/CATALOG/DefaultBuildingCatalog.tres")
            .Definitions.Where(definition => definition != null).ToArray();
        BuildInterface();
        ShowStart();
    }

    // =========================================================
    private void BuildInterface()
    {
        var layer = new CanvasLayer { Layer = 30 };
        AddChild(layer);
        interfaceRoot = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        layer.AddChild(interfaceRoot);
        interfaceRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var home = Panel(interfaceRoot, Control.LayoutPreset.Center, -405, -270, 405, 270);
        start = new VBoxContainer { CustomMinimumSize = new Vector2(750, 0) };
        home.AddChild(start);
        Heading(start, "Level Editor", 34);
        Heading(start, "Choose a mode", 18);
        var sections = new HBoxContainer();
        start.AddChild(sections);
        var sectionGroup = new ButtonGroup();
        string[] sectionNames = { "Skirmishes", "Missions", "Fortress Defense" };
        authoringButtons = new Button[sectionNames.Length];
        for (int i = 0; i < sectionNames.Length; i++)
        {
            var section = (LevelEditorSection)i;
            var button = Button(sections, sectionNames[i], () =>
            {
                authoringSection = section;
                ShowStart();
            });
            button.ToggleMode = true;
            button.ButtonGroup = sectionGroup;
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            authoringButtons[i] = button;
        }
        campaignButtons = new HBoxContainer();
        start.AddChild(campaignButtons);
        Heading(start, "Available levels", 18);
        levelGrid = new GridContainer { Columns = 3, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        start.AddChild(levelGrid);
        var creation = new HBoxContainer();
        start.AddChild(creation);
        Heading(creation, "Base Terrain", 15);
        newLevelTerrain = new OptionButton { CustomMinimumSize = new Vector2(140, 0) };
        newLevelTerrain.AddItem("Industrial", (int)CommandSandbox.BaseTerrainStyle.Industrial);
        newLevelTerrain.AddItem("Steel Slate", (int)CommandSandbox.BaseTerrainStyle.SteelSlate);
        newLevelTerrain.Select(0);
        creation.AddChild(newLevelTerrain);
        var newLevel = Button(creation, "+ New Level", () => Open(null));
        newLevel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        newLevel.CustomMinimumSize = new Vector2(0, 38);
        startStatus = Heading(start, "", 15);
        startStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        var actions = new HBoxContainer();
        start.AddChild(actions);
        var backToHub = Button(actions, "Game Editor", () =>
        {
            void BackToHub() => MenuNavigation.Open(this, GameEditor.ScenePath);
            if (dirty) Confirm("Discard unsaved changes?", "Return to the Game Editor?", BackToHub);
            else BackToHub();
        });
        backToHub.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        returnToLevel = Button(actions, "Return to current level", ShowEditor);
        returnToLevel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var bar = Panel(interfaceRoot, Control.LayoutPreset.TopWide, 12, 12, -12, 124);
        toolbar = new VBoxContainer();
        bar.AddChild(toolbar);
        var buttons = new HBoxContainer();
        toolbar.AddChild(buttons);
        Heading(buttons, "Level Editor   ", 22);
        Button(buttons, "Save", () => { if (activeId == null) SaveAs(); else RequestSave(activeId); });
        Button(buttons, "Save As", SaveAs);
        Button(buttons, "Load / New", ShowStart);
        Button(buttons, "Undo", () => ApplyHistory(undo, redo, true));
        Button(buttons, "Redo", () => ApplyHistory(redo, undo, false));
        Button(buttons, "Map Size…", ShowMapSettings);
        Button(buttons, "Preview Layout", ToggleLayout);
        testButton = Button(buttons, "Test Fortress", StartTest);
        Button(buttons, "Return from Test", StopTest);
        strength = Heading(toolbar, "", 16);
        status = Heading(toolbar, "", 15);

        var sidebar = Panel(interfaceRoot, Control.LayoutPreset.LeftWide, 12, 134, 310, -12);
        toolSidebar = sidebar;
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        sidebar.AddChild(scroll);
        toolsPanel = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(toolsPanel);
        toolsHeading = Heading(toolsPanel, "Map tools", 22);
        toolPicker = new OptionButton();
        toolsPanel.AddChild(toolPicker);
        toolPicker.ItemSelected += index => { selected = null; SwitchTool((Tool)toolPicker.GetItemId((int)index)); };
        planView = new CheckBox { Text = "Top-down view", ButtonPressed = true };
        planView.Toggled += _ => FrameEditor();
        toolsPanel.AddChild(planView);
        snap = new CheckBox { Text = "Snap to grid", ButtonPressed = true };
        toolsPanel.AddChild(snap);
        showGrid = new CheckBox { Text = "Show grid", ButtonPressed = true };
        showGrid.Toggled += _ => UpdateEditorGround();
        toolsPanel.AddChild(showGrid);
        mapToolsContext = new VBoxContainer();
        toolsPanel.AddChild(mapToolsContext);
        legacyUnitDivider = new HSeparator();
        toolsPanel.AddChild(legacyUnitDivider);
        var legacyUnits = new VBoxContainer();
        toolsPanel.AddChild(legacyUnits);
        legacyUnitPicker = legacyUnits;
        Heading(legacyUnits, "Unit placement", 18);
        Button(legacyUnits, "Place units", () => { selected = null; SwitchTool(Tool.Soldiers); });
        unitContext = new VBoxContainer();
        toolsPanel.AddChild(unitContext);
        legacyDefenseDivider = new HSeparator();
        toolsPanel.AddChild(legacyDefenseDivider);
        legacyDefenseHeading = Heading(toolsPanel, "Defenses", 18);
        defensePicker = new OptionButton();
        defensePicker.AddItem("Choose a defense…");
        foreach (var definition in defenseDefinitions) defensePicker.AddItem(definition.DisplayName);
        defensePicker.ItemSelected += index =>
        {
            selected = null;
            SwitchTool(index > 0 ? Tool.Defense : Tool.Select);
        };
        toolsPanel.AddChild(defensePicker);
        defenseContext = new VBoxContainer();
        toolsPanel.AddChild(defenseContext);
        toolsPanel.AddChild(new HSeparator());
        var help = Heading(toolsPanel, "WASD / arrows to pan\nWheel to zoom · Home to frame\nCtrl+Z / Ctrl+Shift+Z", 15);
        help.Modulate = new Color("#b6c9d5");

        BuildPlacementPalette();

        confirmation = new ConfirmationDialog();
        interfaceRoot.AddChild(confirmation);
        confirmation.Confirmed += () => { var action = accepted; accepted = null; action?.Invoke(); };
        confirmation.Canceled += () => accepted = null;
    }

    // =========================================================
    private void ShowStart()
    {
        if (runningTest != null) return;
        EndPaint();
        cursor?.Hide();
        start.GetParent<Control>().Show();
        toolbar.GetParent<Control>().Hide();
        toolSidebar.Hide();
        placementPalette.Hide();
        if (map != null) map.Hide();
        try
        {
            catalog = CampaignCatalog.Load(fresh: true);
            authoringButtons[(int)authoringSection].ButtonPressed = true;
            var supported = catalog.Campaigns.Where(entry => LevelEditorMode.Supported.Any(adapter =>
                adapter.CampaignId == entry.Id && adapter.Section == authoringSection))
                .OrderBy(entry => CampaignCatalog.BattleType(entry.Id) switch
                {
                    "Offense" => 1,
                    "Defense" => 2,
                    _ => 0
                }).ToArray();
            if (!supported.Any(entry => entry.Id == selectedCampaignId))
                selectedCampaignId = supported.FirstOrDefault()?.Id;
            foreach (Node child in campaignButtons.GetChildren())
            {
                campaignButtons.RemoveChild(child);
                child.QueueFree();
            }
            campaignButtons.Visible = authoringSection != LevelEditorSection.FortressDefense;
            var tabs = new ButtonGroup();
            foreach (var entry in supported)
            {
                string id = entry.Id;
                var tab = Button(campaignButtons, entry.DisplayName, () =>
                {
                    selectedCampaignId = id;
                    RefreshLevelList();
                });
                tab.ToggleMode = true;
                tab.ButtonGroup = tabs;
                tab.ButtonPressed = id == selectedCampaignId;
                tab.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                tab.CustomMinimumSize = new Vector2(0, 36);
            }
            RefreshLevelList();
        }
        catch (Exception error) { startStatus.Text = error.Message; }
        returnToLevel.Visible = map != null;
    }

    private void RefreshLevelList()
    {
        foreach (Node child in levelGrid.GetChildren())
        {
            levelGrid.RemoveChild(child);
            child.QueueFree();
        }
        var selected = catalog?.Campaigns.FirstOrDefault(entry => entry.Id == selectedCampaignId);
        if (selected == null)
        {
            startStatus.Text = "No supported campaigns are available.";
            return;
        }
        LevelDefinition[] available = CampaignCatalog.OrderedLevels(selected);
        // Expand columns for larger campaigns so every saved level stays visible without scrolling.
        levelGrid.Columns = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(available.Length)), 3, 10);
        foreach (var level in available)
        {
            var button = Button(levelGrid, level.Id, () => Open(level));
            button.TooltipText = level.MenuName == level.Id ? level.Id : $"{level.Id} · {level.MenuName}";
            button.CustomMinimumSize = new Vector2(0, 29);
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            if (available.Length > 36) button.AddThemeFontSizeOverride("font_size", 13);
        }
        startStatus.Text = available.Length == 0 ? "This campaign has no levels yet." : "";
    }

    private void Open(LevelDefinition existing)
    {
        if (selectedCampaignId == null) return;
        var adapter = LevelEditorMode.Supported.Single(entry => entry.CampaignId == selectedCampaignId);
        if (dirty)
        {
            Confirm("Discard unsaved changes?", "Opening another map replaces this editing session.", () => OpenMap(adapter, existing));
            return;
        }
        OpenMap(adapter, existing);
    }

    private void OpenMap(LevelEditorMode adapter, LevelDefinition existing)
    {
        CommandSandbox next;
        LevelDefinition nextMetadata;
        try { next = adapter.Open(existing, out nextMetadata); }
        catch (Exception error) { startStatus.Text = error.Message; return; }
        if (existing == null) next.BaseTerrain = (CommandSandbox.BaseTerrainStyle)newLevelTerrain.GetSelectedId();
        EndElevation();
        CancelBridge();
        CancelWallStroke();
        CancelCliffStroke();
        CloseRedOrders();
        if (map != null)
        {
            RemoveChild(map);
            map.QueueFree();
        }
        foreach (var node in detached)
            if (IsInstanceValid(node) && node.GetParent() == null) node.Free();
        detached.Clear();
        undo.Clear();
        redo.Clear();
        selected = null;
        layout = null;
        placementBatches = null;
        editingReserve = previewReserveEntry = false;
        reserveGridButton.Hide();
        cursor = null;
        dirty = previewing = false;
        mode = adapter;
        selectedCampaignId = mode.CampaignId;
        authoringSection = mode.Section;
        testButton.Text = mode.IsAutoBattler ? "Test " + CampaignCatalog.BattleType(mode.CampaignId) : "Test Fortress";
        campaign = catalog.Campaigns.Single(entry => entry.Id == mode.CampaignId);
        activeId = existing?.Id;
        map = next;
        redDisruptorView = redDisruptorPreview = null;
        metadata = nextMetadata;
        AddChild(map);
        EnsurePaint();
        camera = new IsometricCamera();
        map.AddChild(camera);
        camera.MakeCurrent();
        cursor = new MeshInstance3D { CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Mesh = new BoxMesh { Size = new Vector3(1, 0.08f, 1),
                Material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color("#ffd38466") } }, Visible = false };
        map.AddChild(cursor, false, InternalMode.Back);
        SetTools();
        FrameEditor();
        RefreshMap();
        ShowEditor();
        SetStatus();
    }

    private void ShowEditor()
    {
        if (map == null) return;
        start.GetParent<Control>().Hide();
        toolbar.GetParent<Control>().Show();
        toolSidebar.Visible = !previewing && runningTest == null;
        placementPalette.Visible = toolSidebar.Visible && mode.IsAutoBattler;
        toolSidebar.OffsetBottom = placementPalette.Visible ? -placementPalette.BarHeight - 24 : -12;
        map.Visible = runningTest == null;
        if (redOrderOverlay != null) redOrderOverlay.Visible = toolSidebar.Visible;
    }

    private void SetStatus(string message = "") =>
        status.Text = $"{(mode.IsAutoBattler ? mode.SectionName + " / " : "")}{campaign.DisplayName}   /   {activeId ?? "New level"}{(dirty ? "  •  Unsaved" : "")}    {message}";

    // =========================================================
    private void SaveAs()
    {
        if (map == null || runningTest != null) return;
        CampaignDefinition latest;
        try { latest = CampaignCatalog.Load(fresh: true).Campaigns.Single(entry => entry.Id == mode.CampaignId); }
        catch (Exception error) { SetStatus(error.Message); return; }
        var existing = latest.Levels.Select(level => level.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var picker = new OptionButton { CustomMinimumSize = new Vector2(340, 0) };
        int firstFree = -1, current = -1;
        for (int number = 1; number <= CampaignCatalog.NumberedLevelLimit; number++)
        {
            string id = CampaignCatalog.NumberedLevelId(number);
            bool occupied = existing.Contains(id);
            picker.AddItem(occupied ? $"{id} (existing)" : id, number);
            int index = picker.ItemCount - 1;
            if (!occupied && firstFree < 0) firstFree = index;
            if (id == activeId) current = index;
        }
        picker.Select(firstFree >= 0 ? firstFree : Math.Max(0, current));
        var dialog = new ConfirmationDialog { Title = $"Save level as · {latest.DisplayName}" };
        var fields = new VBoxContainer();
        dialog.AddChild(fields);
        fields.AddChild(picker);
        var customName = new LineEdit { Text = metadata.CustomName, PlaceholderText = "Use the selected level number" };
        Heading(fields, "Custom name (optional)", 15);
        fields.AddChild(customName);
        interfaceRoot.AddChild(dialog);
        dialog.GetOkButton().Text = "Save";
        dialog.Confirmed += () =>
        {
            string id = CampaignCatalog.NumberedLevelId(picker.GetSelectedId());
            dialog.Hide();
            dialog.QueueFree();
            RequestSave(id, customName.Text);
        };
        dialog.Canceled += dialog.QueueFree;
        dialog.PopupCentered();
    }

    private void RequestSave(string id, string customName = null)
    {
        if (map == null || runningTest != null) return;
        EndPaint();
        LevelEditorSave.Destination target;
        try { target = LevelEditorSave.Inspect(mode, id); }
        catch (Exception error) { SetStatus(error.Message); return; }
        void Save()
        {
            try
            {
                string title = (customName ?? metadata.CustomName).Trim();
                if (title.Length == 0) title = id;
                LevelEditorSave.Write(mode, target, map, metadata, title);
                metadata.Id = id;
                metadata.DisplayName = title;
                activeId = id;
                dirty = false;
                SetStatus($"Saved {target.Scene}");
            }
            catch (Exception error) { SetStatus(error.Message); }
        }
        if (target.Existing == null) Save();
        else Confirm($"Overwrite {mode.SectionName} / {campaign.DisplayName} / {id}?",
            "This replaces the scene and level settings. A recovery copy is kept in .godot/level-editor.", Save);
    }

    private void Confirm(string title, string message, Action action)
    {
        accepted = action;
        confirmation.Title = title;
        confirmation.DialogText = message;
        confirmation.PopupCentered();
    }

    // =========================================================
    private void ToggleLayout()
    {
        if (map == null || runningTest != null) return;
        CancelBridge();
        CancelWallStroke();
        CancelCliffStroke();
        EndElevation();
        previewing = !previewing;
        if (previewing)
        {
            cursor.Hide();
            map.FrameOpening(camera);
            camera.TopDown = false;
        }
        else FrameEditor();
        UpdateEditorGround();
        RefreshLayout();
        ShowEditor();
        SetStatus(previewing ? "Opening layout preview · click Preview Layout again to edit" : "");
    }

    private void StartTest()
    {
        if (map == null || runningTest != null) return;
        CancelBridge();
        CancelWallStroke();
        CancelCliffStroke();
        EndPaint();
        try { LevelEditorSave.ValidateContent(mode, map); }
        catch (Exception error) { SetStatus(error.Message); return; }
        var packed = new PackedScene();
        if (packed.Pack(map) != Error.Ok || packed.Instantiate() is not CommandSandbox test)
        {
            SetStatus("Could not create a temporary test scene.");
            return;
        }
        if (mode.IsAutoBattler)
        {
            try
            {
                var arena = MenuNavigation.LoadScene("res://BENCHMARK/BenchmarkArena.tscn").Instantiate<BenchmarkArena>();
                arena.Config = (BenchmarkConfig)arena.Config.Duplicate();
                test.LevelSettings = metadata;
                arena.ConfigureAutoBattlerPlaytest(test);
                var container = new SubViewportContainer { Stretch = true };
                var viewport = new SubViewport { OwnWorld3D = true };
                container.AddChild(viewport);
                viewport.AddChild(arena);
                runningTest = container;
            }
            catch (Exception error)
            {
                test.Free();
                SetStatus(error.Message);
                return;
            }
        }
        else runningTest = test;
        map.Hide();
        camera.ProcessMode = ProcessModeEnum.Disabled;
        if (runningTest is SubViewportContainer presetView)
        {
            interfaceRoot.AddChild(presetView);
            presetView.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            presetView.OffsetTop = 124;
        }
        else AddChild(runningTest);
        ShowEditor();
        SetStatus(mode.IsAutoBattler ? "Paint Blue troops, then Start painted battle · Return from Test resumes editing" :
            "Temporary Fortress test · use Return from Test to resume editing");
    }

    private void StopTest()
    {
        if (runningTest == null) return;
        runningTest.GetParent().RemoveChild(runningTest);
        runningTest.QueueFree();
        runningTest = null;
        camera.ProcessMode = ProcessModeEnum.Inherit;
        camera.MakeCurrent();
        ShowEditor();
        SetStatus();
    }

    // =========================================================
    private static PanelContainer Panel(Control parent, Control.LayoutPreset anchor, float left, float top, float right, float bottom)
    {
        var panel = new PanelContainer();
        parent.AddChild(panel);
        panel.SetAnchorsAndOffsetsPreset(anchor);
        panel.OffsetLeft = left; panel.OffsetTop = top; panel.OffsetRight = right; panel.OffsetBottom = bottom;
        var style = new StyleBoxFlat { BgColor = new Color("#172432ed"),
            BorderColor = new Color("#40586b"), CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            BorderWidthTop = 1, BorderWidthBottom = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 10, ContentMarginBottom = 10 };
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    private static Label Heading(Control parent, string text, int size)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        parent.AddChild(label);
        return label;
    }

    private static Button Button(Control parent, string text, Action action)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        parent.AddChild(button);
        button.Pressed += action;
        return button;
    }

    private static SpinBox Number(Control parent, string text, double value, double min, double max, double step = 1)
    {
        Heading(parent, text, 15);
        var number = new SpinBox { MinValue = min, MaxValue = max, Step = step, Value = value };
        parent.AddChild(number);
        return number;
    }
}
