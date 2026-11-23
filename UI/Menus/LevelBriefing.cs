// Shows the selected campaign map and authored forces before deploying into gameplay.
using System;
using System.Linq;
using Godot;

namespace MassiveRts;

public partial class LevelBriefing : Control
{
    internal CampaignDefinition Campaign { get; init; }
    internal LevelDefinition Level { get; init; }
    internal PackedScene MapScene { get; init; }
    private Label status;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var background = new ColorRect { Color = new Color("#121a25"), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(background);
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var margin = new MarginContainer();
        AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (string edge in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride("margin_" + edge, 24);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 16);
        margin.AddChild(body);
        AddLabel(body, $"{Campaign.DisplayName}   /   {Level.MenuName}", 30);

        var columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 24);
        body.AddChild(columns);
        var preview = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 2f };
        columns.AddChild(preview);
        var scroll = new ScrollContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        columns.AddChild(scroll);
        var details = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        details.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(details);

        var instance = MapScene.Instantiate();
        if (instance is CommandSandbox map)
        {
            BuildMapPreview(preview, map);
            AddForces(details, map);
        }
        else
        {
            instance.Free();
            preview.AddChild(new Label { Text = "Map preview unavailable", HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center });
            AddLabel(details, "Enemy forces", 22);
            AddLabel(details, "No enemy roster available.", 16);
            AddLabel(details, "Player starting forces", 22);
            AddLabel(details, "No starting roster available.", 16);
        }
        if (!string.IsNullOrWhiteSpace(Level.Description)) AddLabel(details, Level.Description, 16);

        if (Campaign.Id == AutoBattlerCampaignStore.CampaignId)
            details.AddChild(UIButtonFactory.Create("Headquarters Recruitment",
                () => Navigate(() => MenuNavigation.OpenHeadquarters(this, Campaign, recruitment: true))));
        else
        {
            var upgrades = UIButtonFactory.Create("Upgrades / Recruit", () => { }, UIButtonStyle.Secondary);
            upgrades.Disabled = true;
            upgrades.TooltipText = "Coming soon";
            details.AddChild(upgrades);
        }
        status = AddLabel(body, "", 16);
        status.Hide();

        var actions = new HBoxContainer();
        body.AddChild(actions);
        actions.AddChild(UIButtonFactory.Create("Back", Back, UIButtonStyle.Secondary));
        actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var deploy = UIButtonFactory.Create("DEPLOY", Deploy, UIButtonStyle.Primary);
        actions.AddChild(deploy);
        deploy.GrabFocus();
    }

    // =========================================================
    // Render the authored scene once, without running the battle simulation.
    private void BuildMapPreview(Control panel, CommandSandbox map)
    {
        var frame = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
        panel.AddChild(frame);
        var viewport = new SubViewport { Size = new Vector2I(960, 720), OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled, GuiDisableInput = true };
        AddChild(viewport);
        map.AuthoringPreview = true;
        map.LevelSettings = Level;
        viewport.AddChild(map);
        map.UpdateGroundBounds();
        try { map.GetNodeOrNull<WorldPaint>("WorldPaint")?.Compile(); }
        catch (InvalidOperationException error) { GD.PushWarning(error.Message); }
        LevelLayoutPreview.Build(map, redOnly: Campaign.Id == AutoBattlerCampaignStore.CampaignId);

        var camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal,
            Size = map.MapSize * 1.08f, Far = 18000f };
        viewport.AddChild(camera);
        camera.Position = Vector3.Up * 8000f;
        camera.LookAt(Vector3.Zero, Vector3.Forward);
        camera.MakeCurrent();
        frame.Texture = viewport.GetTexture();
        viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
    }

    private void AddForces(Control details, CommandSandbox map)
    {
        var groups = map.GetNodeOrNull<Node3D>("StartingUnits")?.GetChildren().OfType<CommandUnitPlacement>()
            .Where(group => group.Unit != null).ToArray() ?? Array.Empty<CommandUnitPlacement>();
        long Count(CommandUnitPlacement.Team team) => groups.Where(group => group.Faction == team).Sum(group => (long)group.SoldierCount);

        AddLabel(details, "Enemy forces", 22);
        AddLabel(details, $"Starting units: {Count(CommandUnitPlacement.Team.Red):N0}", 16);
        var defenses = map.GetNodeOrNull<Node3D>("StartingDefenses")?.GetChildren().OfType<CommandLaserPlacement>()
            .ToArray() ?? Array.Empty<CommandLaserPlacement>();
        int redDefenses = defenses.Count(defense => defense.Red);
        if (redDefenses > 0) AddLabel(details, $"Starting defenses: {redDefenses:N0}", 16);
        if (map.AutoBattlerRules == null)
        {
            long reinforcements = map.WaveDefinitions.Where(wave => wave != null)
                .Sum(wave => wave.Enemies.Values.Sum(count => (long)count));
            AddLabel(details, $"Incoming waves: {map.WaveDefinitions.Count:N0} · {reinforcements:N0} units", 16);
        }

        if (Campaign.Id == AutoBattlerCampaignStore.CampaignId && map.AutoBattlerRules is AutoBattlerRules rules)
        {
            foreach (var enemy in groups.Where(group => group.Faction == CommandUnitPlacement.Team.Red)
                         .GroupBy(group => group.Unit.DisplayName))
                AddLabel(details, $"{enemy.Key} × {enemy.Sum(group => (long)group.SoldierCount):N0}", 16);
            AddLabel(details, "Objective", 22);
            AddLabel(details, "Eliminate Red soldiers and defenses.", 16);
            if (redDefenses > 0) AddLabel(details, "Deploy units that can attack structures to destroy Red defenses.", 16);
            AddLabel(details, "Owned army", 22);
            var allowed = rules.AllowedBlueUnitIds ?? Array.Empty<string>();
            var catalog = GD.Load<UnitCatalog>("res://GAMEPLAY/UNITS/CATALOG/DefaultUnitCatalog.tres");
            if (allowed.Length > 0)
            {
                var names = allowed.Select(id => catalog.Definitions.FirstOrDefault(unit => unit?.Id == id)?.DisplayName ?? id);
                AddLabel(details, "Map allows: " + string.Join(", ", names), 16);
            }
            try
            {
                var owned = AutoBattlerCampaignStore.Open(ProfileStore.Selected.Id, out _);
                bool any = false;
                foreach (var unit in catalog.Definitions)
                {
                    if (unit == null || owned.Owned(unit.Id) == 0 || (allowed.Length > 0 && !allowed.Contains(unit.Id))) continue;
                    AddLabel(details, $"{unit.DisplayName} × {owned.Owned(unit.Id):N0}", 16);
                    any = true;
                }
                if (!any) AddLabel(details, "No eligible soldiers owned. Visit Headquarters to recruit.", 16);
            }
            catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                AddLabel(details, error.Message, 16);
            }
            return;
        }

        AddLabel(details, "Player starting forces", 22);
        if (map.AutoBattlerRules is AutoBattlerRules legacyRules)
        {
            AddLabel(details, $"Deployment allowance: {legacyRules.BlueUnitLimit:N0} units", 16);
            var allowed = legacyRules.AllowedBlueUnitIds;
            if (allowed.Length > 0)
            {
                var catalog = GD.Load<UnitCatalog>("res://GAMEPLAY/UNITS/CATALOG/DefaultUnitCatalog.tres");
                var names = allowed.Select(id => catalog.Definitions.FirstOrDefault(unit => unit?.Id == id)?.DisplayName ?? id);
                AddLabel(details, "Available units: " + string.Join(", ", names), 16);
            }
            return;
        }

        long blue = Count(CommandUnitPlacement.Team.Blue) + (map.SpawnDefaultBlueSquad ? CommandMovement.SquadSize : 0);
        AddLabel(details, $"Starting units: {blue:N0}", 16);
        foreach (var roster in groups.Where(group => group.Faction == CommandUnitPlacement.Team.Blue)
                     .GroupBy(group => group.Unit.DisplayName))
            AddLabel(details, $"{roster.Key} × {roster.Sum(group => (long)group.SoldierCount):N0}", 16);
        if (map.SpawnDefaultBlueSquad) AddLabel(details, $"Default infantry squad × {CommandMovement.SquadSize:N0}", 16);
        AddLabel(details, $"Starting defenses: {defenses.Count(defense => !defense.Red):N0}", 16);
        AddLabel(details, $"Starting Carbon: {map.StartingCarbon:N0}", 16);
    }

    private static Label AddLabel(Control parent, string text, int size)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontSizeOverride("font_size", size);
        parent.AddChild(label);
        return label;
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (!input.IsActionPressed("ui_cancel")) return;
        GetViewport().SetInputAsHandled();
        Back();
    }

    private void Back() => Navigate(() => MenuNavigation.ReturnToCampaign(this, Campaign.Id));

    private void Deploy() => Navigate(() => LevelLauncher.Launch(this, Campaign.Id, Level.Id));

    private void Navigate(Action action)
    {
        try { action(); }
        catch (InvalidOperationException error)
        {
            status.Text = error.Message;
            status.Show();
        }
    }
}
