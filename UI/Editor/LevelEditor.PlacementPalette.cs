// Connects the AutoBattler authoring palette to existing Level Editor tools.
using Godot;

namespace MassiveRts;

public partial class LevelEditor
{
    private UnitPlacementBar placementPalette;
    private Control[] palettePages;
    private Button[] paletteTabs;
    private Button paletteSelect, paletteMove, paletteErase;
    private int paletteDefenseIndex;
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

        Color enemy = Factions.Color(Factions.Red);
        int towers = defenseDefinitions.Length;
        palettePages = new[]
        {
            placementPalette.BuildCards(body, units.Length, (units.Length + 1) / 2, i => units[i].DisplayName,
                i => UnitPlacementBar.Stats(units[i].Health, units[i].Damage), (icon, i) =>
                {
                    icon.SetOutline(UnitOutline.FromDefinition(units[i], UnitOutline.EffectiveSize(units[i])));
                    icon.SetColor(enemy);
                }, i =>
                {
                    unitIndex = i;
                    PickPaletteTool(Tool.Soldiers);
                }, paletteTools, selectFirst: false),
            placementPalette.BuildCards(body, towers, towers,
                i => defenseDefinitions[i].DisplayName,
                i => UnitPlacementBar.Stats(defenseDefinitions[i].Health,
                    defenseDefinitions[i].AttacksSoldiers ? defenseDefinitions[i].Damage : 0f),
                (icon, i) =>
                {
                    var defense = defenseDefinitions[i];
                    icon.SetDefense(defense.Projectile != null, defense.ShieldEmitterEnabled);
                    icon.SetColor(enemy);
                }, i =>
                {
                    paletteDefenseIndex = i;
                    PickPaletteTool(Tool.Defense);
                }, paletteTools, selectFirst: false),
            ToolPage(new[] { "Rocks", "Forest", "Water", "Elevation", "Bridge", "Industrial Walls" },
                new[] { Tool.Rock, Tool.Forest, Tool.Water, Tool.Elevation, Tool.Bridge, Tool.Wall }),
            ToolPage(new[] { "Blue Deployment Zone", "Deployment Line", "Opening Camera" },
                new[] { Tool.Deployment, Tool.DeploymentLine, Tool.Camera }),
            simulation
        };
        ShowPalettePage(0);

        Control ToolPage(string[] labels, Tool[] options) =>
            placementPalette.BuildCards(body, labels.Length, labels.Length, i => labels[i],
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
        for (int i = 0; i < palettePages.Length; i++) palettePages[i].Visible = i == page;
    }

    private void PickPaletteTool(Tool next)
    {
        if (next != Tool.Move) selected = null;
        SwitchTool(next);
    }

    private void SyncPaletteTool(Tool next)
    {
        switch (next)
        {
            case Tool.Select: paletteSelect.ButtonPressed = true; break;
            case Tool.Move: paletteMove.ButtonPressed = true; break;
            case Tool.Erase: paletteErase.ButtonPressed = true; break;
        }
    }

    private void SetPlacementPaletteMode()
    {
        bool legacy = !mode.IsAutoBattler;
        toolsHeading.Text = legacy ? "Map tools" : "Tool settings";
        toolPicker.Visible = legacyUnitPicker.Visible = legacyDefenseHeading.Visible = defensePicker.Visible =
            legacyUnitDivider.Visible = legacyDefenseDivider.Visible = legacy;
        if (!legacy) ShowPalettePage(0);
    }
}
