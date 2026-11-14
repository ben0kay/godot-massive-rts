// Presents Carbon, the build menu and the currently selected structure.
using Godot;

namespace MassiveRts;

public partial class FabricationHud : Control
{
    private readonly Label carbon = new() { Position = new Vector2(20, 14), MouseFilter = MouseFilterEnum.Ignore };
    private readonly PanelContainer menu = new() { Position = new Vector2(16, 114), Visible = false };
    private readonly PanelContainer panel = new() { Visible = false };
    private readonly Label title = new(), cost = new(), status = new(), rally = new();
    private readonly OptionButton types = new();
    private readonly CheckButton automatic = new() { Text = "Automatic production" };
    private readonly Button clear = new() { Text = "Clear rally" };
    private readonly ProgressBar progress = new() { CustomMinimumSize = new Vector2(270, 16) };
    private VBoxContainer controls;
    private int previousCarbon = -1;
    private CommandStructure selected;
    internal System.Action PlaceRequested;
    internal bool MenuOpen { get => menu.Visible; set => menu.Visible = value; }

    internal void Initialize(RuntimeUnitCatalog catalog)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        carbon.AddThemeFontSizeOverride("font_size", 21);
        AddChild(carbon);
        AddChild(menu);
        var entries = new VBoxContainer();
        menu.AddChild(entries);
        entries.AddChild(new Label { Text = "Build" });
        var fabricator = new Button { Text = $"Fabricator  ·  {CommandStructure.FabricatorCost} Carbon" };
        fabricator.Pressed += () => PlaceRequested?.Invoke();
        entries.AddChild(fabricator);

        AddChild(panel);
        panel.AnchorRight = panel.AnchorTop = panel.AnchorBottom = 1;
        panel.OffsetLeft = 12;
        panel.OffsetRight = -12;
        panel.OffsetTop = -12;
        panel.OffsetBottom = -12;
        panel.GrowVertical = GrowDirection.Begin;
        var row = new HBoxContainer();
        panel.AddChild(row);
        var details = new VBoxContainer();
        row.AddChild(details);
        details.AddChild(title);
        controls = new VBoxContainer();
        details.AddChild(controls);
        controls.AddChild(types);
        types.AddItem("Choose unit type");
        types.SetItemDisabled(0, true);
        for (int i = 0; i < catalog.Count; i++) types.AddItem(catalog[i].Name);
        controls.AddChild(cost);
        controls.AddChild(progress);
        controls.AddChild(status);
        controls.AddChild(automatic);
        controls.AddChild(rally);
        controls.AddChild(clear);
        types.ItemSelected += index => { if (selected?.Production != null) selected.Production.RecipeIndex = (int)index - 1; };
        automatic.Toggled += enabled => { if (selected?.Production != null) selected.Production.Automatic = enabled; };
        clear.Pressed += () => { if (selected != null) selected.Rally = null; };
    }

    internal void Refresh(CommandBuildings buildings, CommandStructure building)
    {
        if (previousCarbon != buildings.Carbon.Amount)
        {
            previousCarbon = buildings.Carbon.Amount;
            carbon.Text = $"Carbon  {previousCarbon:N0}";
        }
        panel.Visible = building != null;
        bool changed = selected != building;
        selected = building;
        if (building == null) return;

        var production = building.Production;
        controls.Visible = production != null && building.Ready;
        title.Text = production == null ? $"Citadel   ·   {building.Health:N0} / {building.MaxHealth:N0} HP" :
            !building.Ready ? $"Fabricator   ·   Constructing {building.Built:P0}" :
            $"Fabricator   ·   {building.Health:N0} / {building.MaxHealth:N0} HP";
        if (!controls.Visible) return;
        if (changed)
        {
            types.Select(production.RecipeIndex + 1);
            automatic.SetPressedNoSignal(production.Automatic);
        }
        if (production.RecipeIndex >= 0)
        {
            BatchRecipe recipe = buildings.Recipe(production.RecipeIndex);
            cost.Text = $"Batch {recipe.Size}  ·  {recipe.Cost} Carbon  ·  {recipe.Seconds:0.#} s";
        }
        else cost.Text = "";
        progress.Value = production.Progress * 100;
        status.Text = building.UnitCapBlocked && production.Status == "Exit Blocked"
            ? "Exit Blocked · Unit cap reached" : production.Status;
        rally.Text = building.Rally.HasValue ? "Rally set · Right-click to move" : "No rally · Right-click terrain to set";
        clear.Disabled = !building.Rally.HasValue;
    }
}
