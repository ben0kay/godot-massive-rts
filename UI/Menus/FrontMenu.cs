// Owns front-end screen actions, campaign mode selection and shared back/error handling.
using System;
using System.Linq;
using System.IO;
using Godot;

namespace MassiveRts;

public partial class FrontMenu : Control
{
    public enum MenuScreen { Boot, Main, Campaign, Options, Skirmishes, FortressDefense, About, Conquest, Missions }
    [Export] public MenuScreen Screen { get; set; }
    internal string ReturnCampaignId { get; set; }
    private VBoxContainer campaignModes;
    private VBoxContainer campaignLevels;
    private Button lastModeButton;
    private static bool startupOptionsApplied;
    private VBoxContainer content;
    private Label status;
    private AcceptDialog createDialog;
    private LineEdit profileName;
    private Label profileError;

    // =========================================================
    // Build only the active screen and defer redirects until the scene is ready.
    public override void _Ready()
    {
        if (Screen != MenuScreen.Boot && ProfileStore.Selected == null)
        {
            Callable.From(() => Run(() => MenuNavigation.Open(this, MenuNavigation.Boot))).CallDeferred();
        }

        string title = Screen switch
        {
            MenuScreen.Boot => "Select Profile",
            MenuScreen.Main => "Godot Massive RTS",
            MenuScreen.Campaign => "Campaign",
            MenuScreen.Conquest => "Conquest",
            MenuScreen.Missions => "Missions",
            MenuScreen.Skirmishes => "Skirmishes",
            MenuScreen.FortressDefense => "Fortress Defense",
            MenuScreen.About => "About",
            _ => "Options"
        };
        content = MenuLayout.Create(this, title, Screen == MenuScreen.Main ? ProfileStore.Selected?.Name ?? "" : "", wide: Screen == MenuScreen.Skirmishes);
        status = MenuLayout.Label("", muted: true);
        status.Hide();
        if (Screen != MenuScreen.Boot && ProfileStore.Selected == null)
        {
            content.AddChild(status);
            return;
        }

        switch (Screen)
        {
            case MenuScreen.Boot: BuildBoot(); break;
            case MenuScreen.Main: BuildMain(); break;
            case MenuScreen.Campaign:
                AddButton("Conquest", () => MenuNavigation.OpenMode(this, MenuScreen.Conquest), UIButtonStyle.Primary).GrabFocus();
                AddButton("Missions", () => MenuNavigation.OpenMode(this, MenuScreen.Missions), UIButtonStyle.Primary);
                AddButton("Back", Back);
                break;
            case MenuScreen.Conquest:
                content.AddChild(MenuLayout.Label("The territory-map campaign is planned.", muted: true));
                AddButton("Back", Back).GrabFocus();
                break;
            case MenuScreen.Missions: BuildCampaign(); break;
            case MenuScreen.FortressDefense: BuildCampaign(); break;
            case MenuScreen.Skirmishes: BuildSkirmishes(); break;
            case MenuScreen.About:
                content.AddChild(MenuLayout.Label("Godot Massive RTS · A large-scale tactical strategy game built with Godot and C#."));
                AddButton("Back", Back).GrabFocus();
                break;
            case MenuScreen.Options: BuildOptions(); break;
        }
        content.AddChild(status);
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (Screen == MenuScreen.Boot || !input.IsActionPressed("ui_cancel")) return;
        GetViewport().SetInputAsHandled();
        Run(Back);
    }

    // =========================================================
    // Keep profile selection explicit and show validation errors within the creation dialog.
    private void BuildBoot()
    {
        if (!startupOptionsApplied)
        {
            startupOptionsApplied = true;
            Run(() =>
            {
                OptionsStore.Apply(OptionsStore.Load(out bool recovered));
                if (recovered) Report("Recovered display options from the previous backup.");
            });
        }

        Button first = null;
        Run(() =>
        {
            var profiles = ProfileStore.Load(out bool recovered);
            if (recovered) Report("Recovered profiles from the previous backup.");
            if (profiles.Count == 0)
                content.AddChild(MenuLayout.Label("Create a profile to start a campaign.", muted: true));
            else
            {
                var list = MenuLayout.ScrollList(content);
                foreach (var profile in profiles)
                {
                    var button = UIButtonFactory.Create(profile.Name, () => Run(() =>
                    {
                        ProfileStore.Select(profile.Id);
                        MenuNavigation.Open(this, MenuNavigation.Main);
                    }));
                    list.AddChild(button);
                    first ??= button;
                }
            }
        });
        var create = AddButton("Create Profile", ShowCreateDialog, UIButtonStyle.Primary);
        AddButton("Exit Game", () => GetTree().Quit(), UIButtonStyle.Danger);
        (first ?? create).GrabFocus();
    }

    private void ShowCreateDialog()
    {
        if (createDialog == null)
        {
            createDialog = new AcceptDialog
            {
                Title = "Create Profile",
                DialogHideOnOk = false,
                Exclusive = true,
                Unresizable = true
            };
            AddChild(createDialog);
            createDialog.GetOkButton().Text = "Create";
            UIButtonFactory.Apply(createDialog.GetOkButton(), UIButtonStyle.Primary);
            UIButtonFactory.Apply(createDialog.AddCancelButton("Cancel"), UIButtonStyle.Compact);
            var form = new VBoxContainer { CustomMinimumSize = new Vector2(360, 0) };
            createDialog.AddChild(form);
            profileName = new LineEdit { PlaceholderText = "Commander name (1–32 characters)" };
            profileError = MenuLayout.Label("", muted: true);
            form.AddChild(profileName);
            form.AddChild(profileError);
            void Submit() => Run(() =>
            {
                ProfileStore.Create(profileName.Text);
                MenuNavigation.Open(this, MenuNavigation.Main);
            }, profileError);
            createDialog.Confirmed += Submit;
            profileName.TextSubmitted += _ => Submit();
        }
        profileName.Clear();
        profileError.Hide();
        createDialog.PopupCentered(new Vector2I(460, 210));
        profileName.GrabFocus();
    }

    private void BuildMain()
    {
        AddButton("Campaign", () => MenuNavigation.Open(this, MenuNavigation.Campaign), UIButtonStyle.Primary).GrabFocus();
        AddButton("Skirmishes", () => MenuNavigation.OpenMode(this, MenuScreen.Skirmishes));
        AddButton("Fortress Defense", () => MenuNavigation.OpenMode(this, MenuScreen.FortressDefense));
        AddButton("Options", () => MenuNavigation.Open(this, MenuNavigation.Options));
        AddButton("Switch Profile", Back);
        AddButton("About", () => MenuNavigation.OpenMode(this, MenuScreen.About));
        AddButton("Exit Game", () => GetTree().Quit(), UIButtonStyle.Danger);
    }

    private void BuildSkirmishes()
    {
        var columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", UIMasterConfig.Current.Spacing);
        content.AddChild(columns);
        AddButton("Back", Back).GrabFocus();
        Run(() =>
        {
            var catalog = CampaignCatalog.Load();
            foreach (string id in CampaignCatalog.SkirmishIds)
            {
                var campaign = catalog.Campaigns.First(entry => entry.Id == id);
                var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                panel.AddThemeStyleboxOverride("panel", MenuLayout.PanelStyle);
                columns.AddChild(panel);
                var column = new VBoxContainer();
                panel.AddChild(column);
                column.AddChild(MenuLayout.Label(campaign.DisplayName));
                var list = MenuLayout.ScrollList(column, expand: true);
                if (campaign.Levels.Count == 0) list.AddChild(MenuLayout.Label("No levels yet", muted: true));
                foreach (var level in CampaignCatalog.OrderedLevels(campaign))
                {
                    var button = UIButtonFactory.Create(level.CustomName.Length == 0 ? level.Id : $"{level.Id} · {level.MenuName}",
                        () => Run(() => LevelLauncher.Brief(this, id, level.Id)), UIButtonStyle.Primary);
                    button.Disabled = !level.Enabled || !MenuNavigation.SceneExists(level.ScenePath);
                    list.AddChild(button);
                }
            }
        });
    }

    // =========================================================
    // Build mode choices in catalog order and open a level list only after selection or return.
    private void BuildCampaign()
    {
        campaignModes = new VBoxContainer();
        campaignLevels = new VBoxContainer { Visible = false };
        int spacing = UIMasterConfig.Current.Spacing;
        campaignModes.AddThemeConstantOverride("separation", spacing);
        campaignLevels.AddThemeConstantOverride("separation", spacing);
        content.AddChild(campaignModes);
        content.AddChild(campaignLevels);
        var modes = MenuLayout.ScrollList(campaignModes);
        var back = AddButton("Back", Back);
        back.GrabFocus();

        Run(() =>
        {
            Button first = null;
            CampaignDefinition returning = null;
            foreach (var campaign in CampaignCatalog.Load().Campaigns.Where(entry =>
                Screen == MenuScreen.FortressDefense ? entry.Id == "OpenRTS" :
                entry.Id != "OpenRTS" && !CampaignCatalog.IsSkirmish(entry.Id)))
            {
                var button = new Button { Text = campaign.Id == AutoBattlerCampaignStore.CampaignId
                    ? "Clash · Headquarters" : campaign.DisplayName };
                UIButtonFactory.Apply(button, UIButtonStyle.Primary);
                button.Pressed += () => Run(() =>
                {
                    lastModeButton = button;
                    ShowCampaignLevels(campaign, back);
                });
                button.TooltipText = campaign.Description ?? "";
                modes.AddChild(button);
                first ??= button;
                if (campaign.Id == ReturnCampaignId)
                {
                    returning = campaign;
                    lastModeButton = button;
                }
            }

            if (Screen == MenuScreen.FortressDefense && returning == null)
                returning = CampaignCatalog.Load().Campaigns.First(entry => entry.Id == "OpenRTS");
            if (returning != null) ShowCampaignLevels(returning, back);
            else first?.GrabFocus();
        });
    }

    // =========================================================
    // Reload progress for the active profile when showing a mode, including after a level.
    private void ShowCampaignLevels(CampaignDefinition campaign, Button back)
    {
        if (campaign.Id == AutoBattlerCampaignStore.CampaignId)
        {
            MenuNavigation.OpenHeadquarters(this, campaign);
            return;
        }

        foreach (Node child in campaignLevels.GetChildren())
        {
            campaignLevels.RemoveChild(child);
            child.QueueFree();
        }

        campaignModes.Hide();
        campaignLevels.Show();
        Report("");
        campaignLevels.AddChild(MenuLayout.Label(campaign.DisplayName));
        if (!string.IsNullOrWhiteSpace(campaign.Description))
            campaignLevels.AddChild(MenuLayout.Label(campaign.Description, muted: true));
        back.GrabFocus();
        if (campaign.Levels.Count == 0)
        {
            campaignLevels.AddChild(MenuLayout.Label("Coming Soon", muted: true));
            return;
        }

        var progress = CampaignProgressStore.Load(ProfileStore.Selected.Id, out bool recovered);
        if (recovered) Report("Recovered campaign progress from the previous backup.");
        var list = MenuLayout.ScrollList(campaignLevels);
        Button firstPlayable = null;
        foreach (var level in CampaignCatalog.OrderedLevels(campaign))
        {
            bool unlocked = CampaignCatalog.IsUnlocked(level, ProfileStore.Selected);
            bool missing = level.Enabled && !MenuNavigation.SceneExists(level.ScenePath);
            string suffix = !level.Enabled ? " (Coming Soon)" : missing ? " (Scene Unavailable)" : !unlocked ? " (Locked)" :
                progress.IsComplete(campaign.Id, level.Id) ? " (Complete)" : "";
            var button = UIButtonFactory.Create(level.MenuName + suffix, () => Run(
                () => LevelLauncher.Brief(this, campaign.Id, level.Id)), UIButtonStyle.Primary);
            button.Disabled = !level.Enabled || missing || !unlocked;
            button.TooltipText = missing ? $"Scene unavailable: {level.ScenePath}" : level.Description ?? "";
            list.AddChild(button);
            if (!button.Disabled) firstPlayable ??= button;
        }

        firstPlayable?.GrabFocus();
    }

    private void BuildOptions()
    {
        var back = AddButton("Back", Back);
        back.GrabFocus();
        Run(() =>
        {
            var options = OptionsStore.Load(out bool recovered);
            if (recovered) Report("Recovered display options from the previous backup.");
            var window = new Button();
            var vsync = new Button();
            UIButtonFactory.Apply(window);
            UIButtonFactory.Apply(vsync);
            content.AddChild(window);
            content.AddChild(vsync);
            content.MoveChild(back, -1);

            void UpdateLabels()
            {
                window.Text = options.Fullscreen ? "Display Mode  ·  Fullscreen" : "Display Mode  ·  Windowed";
                vsync.Text = options.VSync ? "VSync  ·  On" : "VSync  ·  Off";
            }

            void Change(GameOptions updated)
            {
                OptionsStore.Save(updated);
                options = updated;
                UpdateLabels();
                Report("");
            }

            window.Pressed += () => Run(() => Change(options with { Fullscreen = !options.Fullscreen }));
            vsync.Pressed += () => Run(() => Change(options with { VSync = !options.VSync }));
            UpdateLabels();
            window.GrabFocus();
        });
    }

    private void Back()
    {
        if (Screen == MenuScreen.Main)
        {
            MenuNavigation.Open(this, MenuNavigation.Boot);
            ProfileStore.ClearSelection();
        }
        else if (Screen == MenuScreen.Missions && campaignLevels?.Visible == true)
        {
            campaignLevels.Hide();
            campaignModes.Show();
            Report("");
            lastModeButton?.GrabFocus();
        }
        else if (Screen is MenuScreen.Conquest or MenuScreen.Missions)
            MenuNavigation.Open(this, MenuNavigation.Campaign);
        else MenuNavigation.Open(this, MenuNavigation.Main);
    }

    private Button AddButton(string text, Action action, UIButtonStyle style = UIButtonStyle.Secondary)
    {
        var button = UIButtonFactory.Create(text, () => Run(action), style);
        content.AddChild(button);
        return button;
    }

    private void Run(Action action, Label errorLabel = null)
    {
        if (!IsInsideTree()) return;
        try { action(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            Report(error.Message, errorLabel);
        }
    }

    private void Report(string message, Label label = null)
    {
        label ??= status;
        label.Text = message;
        label.Visible = message.Length > 0;
    }
}
