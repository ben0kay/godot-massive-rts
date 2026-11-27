// Connects the AutoBattler authoring palette to existing Level Editor tools.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

public partial class LevelEditor
{
    private UnitPlacementBar placementPalette;
    private Control[] palettePages;
    private Button[] paletteTabs;
    private Button paletteSelect, paletteMove, paletteErase, paletteRedOrders;
    private int paletteDefenseIndex;
    private Button offenseCitadel;
    private readonly List<Button> aircraftCards = new();
    private VBoxContainer editorBrushControls;
    private HBoxContainer editorBrushSizes;
    private readonly ButtonGroup paletteTools = new();

    private void BuildPlacementPalette()
    {
        placementPalette = new UnitPlacementBar { Visible = false };
        interfaceRoot.AddChild(placementPalette);
        var body = placementPalette.BuildShell(154);
        var header = new HBoxContainer();
        body.AddChild(header);
        string[] tabs = { "Units", "Defenses", "Terrain", "Setup", "Simulation" };
        paletteTabs = new Button[tabs.Length];
        var tabGroup = new ButtonGroup();
        for (int i = 0; i < tabs.Length; i++)
        {
            int page = i;
            var tab = Button(header, tabs[i], () =>
            {
                if (editingReserve && page != 0) ToggleReserveGrid();
                ShowPalettePage(page);
                PickPaletteTool(Tool.Select);
            });
            tab.ToggleMode = true;
            tab.ButtonGroup = tabGroup;
            paletteTabs[i] = tab;
        }

        header.AddChild(new VSeparator());
        paletteSelect = AddPaletteAction(header, "Select", Tool.Select);
        paletteMove = AddPaletteAction(header, "Move", Tool.Move);
        paletteErase = AddPaletteAction(header, "Erase", Tool.Erase);

        reserveGridButton = Button(header, "Back to battlefield", ToggleReserveGrid);
        reserveGridButton.Hide();
        var simulation = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Begin };
        body.AddChild(simulation);
        Button(simulation, "Reinforcement settings", ShowReinforcementSettings);
        Button(simulation, "Red staging", ToggleReserveGrid);
        Button(simulation, "Red entry preview", () =>
        {
            previewReserveEntry = !previewReserveEntry || tool != Tool.ReserveEntry;
            PickPaletteTool(previewReserveEntry ? Tool.ReserveEntry : Tool.Select);
            if (previewReserveEntry) FrameEditor();
            RefreshLayout();
        });

        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 12);
        body.AddChild(columns);
        editorBrushControls = new VBoxContainer { CustomMinimumSize = new Vector2(240, 0) };
        columns.AddChild(editorBrushControls);
        var paintModes = new HBoxContainer();
        editorBrushControls.AddChild(paintModes);
        var paintModeGroup = new ButtonGroup();
        AddPaintMode("Brush", SoldierPaintMode.Brush);
        AddPaintMode("Rectangle", SoldierPaintMode.Rectangle);
        paletteRedOrders = AddPaletteAction(paintModes, "Select Red", Tool.RedOrders);
        AddPaintMode("Formation", SoldierPaintMode.Formation);
        editorBrushSizes = UnitPlacementBar.AddBrushSettings(editorBrushControls,
            size => editorBrushSize = size, value => densityScale = value, densityScale);
        editorBrushSizes.Hide();

        void AddPaintMode(string label, SoldierPaintMode paintMode)
        {
            var button = UIButtonFactory.Create(label, () => SelectPaintMode(paintMode), UIButtonStyle.Compact);
            button.ToggleMode = true;
            button.ButtonGroup = paintModeGroup;
            button.SetPressedNoSignal(paintMode == soldierPaintMode);
            paintModes.AddChild(button);
        }

        void SelectPaintMode(SoldierPaintMode paintMode)
        {
            soldierPaintMode = paintMode;
            rectangleStart = null;
            editorBrushSizes.Visible = paintMode == SoldierPaintMode.Brush;
            if (tool != Tool.Soldiers) PickPaletteTool(Tool.Soldiers);
            else UpdateTool();
        }

        var picker = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        columns.AddChild(picker);
        body.RemoveChild(simulation);
        picker.AddChild(simulation);
        Color enemy = Factions.Color(Factions.Red);
        int unitCardCount = units.Length + aircraft.Length;
        int towers = defenseDefinitions.Length;
        palettePages = new[]
        {
            placementPalette.BuildCards(picker, unitCardCount, (unitCardCount + 1) / 2,
                i => (i < units.Length ? units[i] : aircraft[i - units.Length]).DisplayName,
                i =>
                {
                    var unit = i < units.Length ? units[i] : aircraft[i - units.Length];
                    return UnitPlacementBar.Stats(unit.Health, unit.Damage);
                }, (icon, i) =>
                {
                    if (i < units.Length)
                    {
                        icon.SetOutline(UnitOutline.FromDefinition(units[i], UnitOutline.EffectiveSize(units[i])));
                        icon.SetColor(enemy);
                        return;
                    }
                    icon.SetAircraft(aircraft[i - units.Length]);
                    icon.SetColor(enemy);
                }, i =>
                {
                    if (i >= units.Length)
                    {
                        EndAircraftStroke();
                        if (editingReserve) ToggleReserveGrid();
                        aircraftBrush = aircraft[i - units.Length];
                        PickPaletteTool(Tool.Aircraft);
                        return;
                    }
                    unitIndex = i;
                    PickPaletteTool(Tool.Soldiers);
                }, paletteTools, selectFirst: false, configureCard: (card, i) =>
                {
                    if (i >= units.Length) aircraftCards.Add(card);
                }),
            BuildDefenses(),
            ToolPage(new[] { "Rocks", "Forest", "Water", "Marsh", "Road", "Ruins & Rubble", "Organic Elevation", "Tiered Elevation", "Bridge", "Industrial Walls", "Cliff Painter" },
                new[] { Tool.Rock, Tool.Forest, Tool.Water, Tool.Marsh, Tool.Road, Tool.Rubble, Tool.Elevation, Tool.TieredElevation, Tool.Bridge, Tool.Wall, Tool.Cliff }),
            ToolPage(new[] { "Blue Deployment Zone", "Deployment Line", "Opening Camera" },
                new[] { Tool.Deployment, Tool.DeploymentLine, Tool.Camera }),
            simulation
        };
        ShowPalettePage(0);

        Control BuildDefenses()
        {
            int wall = towers;
            int disruptor = towers + 1;
            int citadel = towers + 2;
            int barricade = towers + 3;
            return placementPalette.BuildCards(picker, towers + 4, (towers + 5) / 2,
                i => i < towers ? defenseDefinitions[i].DisplayName :
                    i == wall ? "Faction Wall" : i == barricade ? "Light Barricade" : i == disruptor ? DisruptorGrid.Name : "Citadel",
                i => i < towers ? UnitPlacementBar.Stats(defenseDefinitions[i].Health,
                    (defenseDefinitions[i].AttacksSoldiers || defenseDefinitions[i].AirMissileMagazine > 0) ? defenseDefinitions[i].Damage : 0f) : null,
                (icon, i) =>
                {
                    icon.SetColor(enemy);
                    if (i == disruptor) icon.SetTexture(DisruptorGridVisual.PickerIcon);
                    else if (i >= towers) icon.SetWall();
                    else
                    {
                        var defense = defenseDefinitions[i];
                        if (defense.Id == PrefabShelterMeshes.Id) icon.SetWall();
                        else icon.SetDefense(defense.Projectile != null, defense.ShieldEmitterEnabled, defense.Id == TwinMinigunTowerMeshes.Id, AntiAirTowerMeshes.Variant(defense.Id));
                    }
                },
                i =>
                {
                    if (i == barricade) PickPaletteTool(Tool.Barricade);
                    else if (i == wall) PickPaletteTool(Tool.FactionWall);
                    else if (i == disruptor) PickPaletteTool(Tool.Disruptor);
                    else
                    {
                        paletteDefenseIndex = i == citadel ? defenseDefinitions.Length : i;
                        PickPaletteTool(Tool.Defense);
                    }
                }, paletteTools, selectFirst: false, configureCard: (card, i) =>
                {
                    if (i == citadel) offenseCitadel = card;
                });
        }

        Control ToolPage(string[] labels, Tool[] options) =>
            placementPalette.BuildCards(picker, labels.Length, labels.Length, i => labels[i],
                _ => null, null, i => PickPaletteTool(options[i]), paletteTools, selectFirst: false);
    }

    private Button AddPaletteAction(HBoxContainer row, string text, Tool action)
    {
        var button = Button(row, text, () => PickPaletteTool(action));
        button.ToggleMode = true;
        button.ButtonGroup = paletteTools;
        return button;
    }

    private void ShowPalettePage(int page)
    {
        paletteTabs[page].ButtonPressed = true;
        editorBrushControls.Visible = page == 0;
        for (int i = 0; i < palettePages.Length; i++) palettePages[i].Visible = i == page;
    }

    private void PickPaletteTool(Tool next)
    {
        if (next == Tool.RedOrders && editingReserve) ToggleReserveGrid();
        if (next != Tool.Move) selected = null;
        SwitchTool(next);
    }

    private void SyncPaletteTool(Tool next)
    {
        switch (next)
        {
            case Tool.RedOrders: paletteRedOrders.ButtonPressed = true; break;
            case Tool.Select: paletteSelect.ButtonPressed = true; break;
            case Tool.Move: paletteMove.ButtonPressed = true; break;
            case Tool.Erase: paletteErase.ButtonPressed = true; break;
        }
    }

    private void SetPlacementPaletteMode()
    {
        bool legacy = !mode.IsAutoBattler;
        foreach (var card in aircraftCards) card.Visible = AutoBattlerRules.SupportsAircraft(mode.CampaignId);
        offenseCitadel.Visible = CampaignCatalog.IsOffense(mode.CampaignId) || CampaignCatalog.IsDefense(mode.CampaignId);

        if (!offenseCitadel.Visible && paletteDefenseIndex == defenseDefinitions.Length) paletteDefenseIndex = 0;
        toolsHeading.Text = legacy ? "Map tools" : "Tool settings";
        toolPicker.Visible = legacyUnitPicker.Visible = legacyDefenseHeading.Visible = defensePicker.Visible =
            legacyUnitDivider.Visible = legacyDefenseDivider.Visible = legacy;
        if (!legacy) ShowPalettePage(0);
    }
}
