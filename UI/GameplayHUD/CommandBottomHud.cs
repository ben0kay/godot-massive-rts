// Owns the command UI frame and switches its contents with selection.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

public partial class CommandBottomHud : Control
{
    private readonly Label carbon = new() { Position = new Vector2(20, 14), MouseFilter = MouseFilterEnum.Ignore };
    private readonly VBoxContainer menu = new() { Visible = false };
    private readonly PanelContainer panel = new();
    private readonly SelectionHud soldiers = new();
    private readonly VBoxContainer structureView = new(), workerView = new(), depositView = new();
    private readonly Label depositAmount = new(), depositStatus = new(), workerStatus = new(), workerQueue = new();
    private readonly Label extractorStatus = new(), workerHint = new();
    private readonly ProgressBar workerProgress = new() { CustomMinimumSize = new Vector2(270, 16) };
    private readonly List<byte> recipeTypes = new();
    private Button buildWorker;
    private readonly Label title = new(), cost = new(), status = new(), rally = new();
    private readonly ItemList types = new()
    {
        CustomMinimumSize = new Vector2(440, 132),
        MaxColumns = 2,
        SameColumnWidth = true
    };
    private readonly CheckButton automatic = new() { Text = "Automatic production" };
    private readonly Button clear = new() { Text = "Clear rally" };
    private readonly ProgressBar progress = new() { CustomMinimumSize = new Vector2(270, 16) };
    private readonly HBoxContainer rallyControls = new();
    private HBoxContainer controls;
    private int previousCarbon = -1;
    private CommandStructure selected;
    internal System.Action<CommandBuildType> PlaceRequested;
    internal bool MenuOpen { get => menu.Visible; set => menu.Visible = value; }

    internal void Initialize(RuntimeUnitCatalog catalog, CommandBuildings buildings)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        carbon.AddThemeFontSizeOverride("font_size", 21);
        AddChild(carbon);
        AddChild(panel);
        panel.AnchorRight = panel.AnchorTop = panel.AnchorBottom = 1;
        panel.OffsetLeft = 12;
        panel.OffsetRight = -12;
        panel.OffsetTop = -12;
        panel.OffsetBottom = -12;
        panel.GrowVertical = GrowDirection.Begin;
        panel.AddThemeStyleboxOverride("panel", UIButtonFactory.Box(new Color("#17212d"), new Color("#2b4156"), 10));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 18);
        panel.AddChild(row);
        row.AddChild(menu);
        menu.AddChild(new Label { Text = "Build" });
        menu.AddChild(UIButtonFactory.Create($"Fabricator  ·  {CommandStructure.FabricatorCost} Carbon",
            () => PlaceRequested?.Invoke(CommandBuildType.Fabricator), UIButtonStyle.Compact));
        menu.AddChild(UIButtonFactory.Create($"Laser Tower  ·  {CommandBuildings.LaserTowerCost} Carbon",
            () => PlaceRequested?.Invoke(CommandBuildType.LaserTower), UIButtonStyle.Compact));
        menu.AddChild(UIButtonFactory.Create("Carbon Extractor",
            () => PlaceRequested?.Invoke(CommandBuildType.Extractor), UIButtonStyle.Compact));

        var context = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(context);
        context.AddChild(soldiers);
        context.AddChild(workerHint);
        workerHint.Text = "To build an Extractor, open Ctrl Build and choose a Carbon Deposit.";
        context.AddChild(structureView);
        context.AddChild(depositView);
        depositView.AddChild(new Label { Text = "Carbon Deposit" });
        depositView.AddChild(depositAmount);
        depositView.AddChild(depositStatus);
        structureView.AddChild(title);
        structureView.AddChild(extractorStatus);
        structureView.AddChild(workerView);
        buildWorker = UIButtonFactory.Create($"Build Worker  ·  {buildings.Workers.Cost:N0} Carbon",
            () => { buildings.QueueWorker(); RefreshWorkers(buildings); }, UIButtonStyle.Compact);
        workerView.AddChild(buildWorker);
        workerView.AddChild(workerQueue);
        workerView.AddChild(workerProgress);
        workerView.AddChild(workerStatus);
        UIButtonFactory.Apply(clear, UIButtonStyle.Compact);
        foreach (ProgressBar meter in new[] { progress, workerProgress })
            meter.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color("#60b891") });
        controls = new HBoxContainer { Visible = false };
        structureView.AddChild(controls);
        var roster = new VBoxContainer();
        controls.AddChild(roster);
        roster.AddChild(new Label { Text = "Unit type" });
        roster.AddChild(types);
        for (byte i = 0; i < catalog.Count; i++)
        {
            if (buildings.Recipe(i).Size == 0) continue;
            recipeTypes.Add(i);
            types.AddItem(catalog[i].Name);
        }

        var productionDetails = new VBoxContainer();
        controls.AddChild(productionDetails);
        productionDetails.AddChild(cost);
        productionDetails.AddChild(progress);
        productionDetails.AddChild(status);
        productionDetails.AddChild(automatic);
        structureView.AddChild(rallyControls);
        rallyControls.AddChild(rally);
        rallyControls.AddChild(clear);
        types.ItemSelected += index =>
        {
            if (selected?.Release != null && selected.Ready) selected.Production.RecipeIndex = recipeTypes[(int)index];
        };
        automatic.Toggled += enabled => { if (selected?.Production != null) selected.Production.Automatic = enabled; };
        clear.Pressed += () => { if (buildings.CanSetRally(selected)) selected.Rally = null; };
    }

    internal void Refresh(CommandBuildings buildings, UnitState units, ReadOnlySpan<int> selection, CommandStructure building, CarbonDeposit deposit)
    {
        if (previousCarbon != buildings.Carbon.Amount)
        {
            previousCarbon = buildings.Carbon.Amount;
            carbon.Text = $"Carbon  {previousCarbon:N0}";
        }
        if (building?.Destroyed == true) building = null;
        bool showDeposit = GodotObject.IsInstanceValid(deposit);
        structureView.Visible = building != null;
        depositView.Visible = building == null && showDeposit;
        soldiers.Visible = building == null && !showDeposit;
        bool changed = selected != building;
        selected = building;
        workerHint.Visible = soldiers.Visible && selection.Length == 1 &&
            units.Catalog.TryResolve("worker", out byte workerType) &&
            units.Type[selection[0]] == workerType && units.Health[selection[0]] > 0f;
        if (soldiers.Visible) soldiers.Refresh(units, selection);
        if (depositView.Visible)
        {
            depositAmount.Text = $"Remaining  ·  {deposit.RemainingCarbon:N0} Carbon";
            CarbonExtractor extractor = deposit.Extractor;
            depositStatus.Text = deposit.Depleted ? "Depleted" :
                extractor?.Destroyed == false ? extractor.BuildProgress < 1f
                    ? $"Extractor constructing · {extractor.BuildProgress:P0}" :
                    $"Extractor active · +{extractor.CarbonPerSecond} Carbon/s" :
                deposit.WorkerAssigned ? "Worker travelling to deposit" : "Untapped";
        }
        if (building == null) return;
        extractorStatus.Visible = building is CarbonExtractor;
        if (building is CarbonExtractor extractorBuilding)
            extractorStatus.Text = extractorBuilding.BuildProgress < 1f
                ? $"Constructing · {extractorBuilding.BuildProgress:P0}" :
                extractorBuilding.Deposit.Depleted ? "Deposit depleted" :
                $"+{extractorBuilding.CarbonPerSecond} Carbon/s · {extractorBuilding.Deposit.RemainingCarbon:N0} remaining";

        workerView.Visible = building == buildings.Citadel;
        if (workerView.Visible) RefreshWorkers(buildings);
        var production = building.Production;
        bool controlsWereVisible = controls.Visible;
        controls.Visible = building.Release != null && building.Ready;
        title.Text = production == null ? $"{building.DisplayName}   ·   {building.Health:N0} / {building.MaxHealth:N0} HP" :
            !building.Ready ? $"Fabricator   ·   Constructing {building.Built:P0}" :
            $"Fabricator   ·   {building.Health:N0} / {building.MaxHealth:N0} HP";
        title.Text += $"   ·   {buildings.ArmourName(building)} armour";
        rallyControls.Visible = buildings.CanSetRally(building);
        if (rallyControls.Visible)
        {
            rally.Text = building.Rally.HasValue ? "Rally set · Right-click to move" : "No rally · Right-click terrain to set";
            clear.Disabled = !building.Rally.HasValue;
        }
        if (!controls.Visible) return;
        if (changed || !controlsWereVisible)
        {
            types.DeselectAll();
            int row = production.RecipeIndex < 0 ? -1 : recipeTypes.IndexOf((byte)production.RecipeIndex);
            if (row >= 0)
            {
                types.Select(row);
                types.EnsureCurrentIsVisible();
            }
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
    }

    private void RefreshWorkers(CommandBuildings buildings)
    {
        var workers = buildings.Workers;
        string reason = workers.BlockReason(buildings.CanOperateCitadel);
        buildWorker.Disabled = reason != null;
        buildWorker.TooltipText = reason ?? "";
        workerQueue.Text = $"Queued  ·  {workers.Queued} / {workers.Limit}";
        workerProgress.Value = workers.Progress * 100f;
        workerStatus.Text = reason ?? (workers.WaitingForExit
            ? buildings.Citadel.UnitCapBlocked ? "Exit blocked · Unit cap reached" : "Exit blocked"
            : workers.Queued > 0 ? "Producing" : "Idle");
    }

}
