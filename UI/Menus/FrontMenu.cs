// Owns front-end screen actions, campaign mode selection and shared back/error handling.
using System;
using System.IO;
using Godot;

namespace MassiveRts;

public partial class FrontMenu : Control
{
    public enum MenuScreen { Boot, Main, Campaign, Options }
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
            _ => "Options"
        };
        content = MenuLayout.Create(this, title, Screen == MenuScreen.Main ? ProfileStore.Selected?.Name ?? "" : "");
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
            case MenuScreen.Campaign: BuildCampaign(); break;
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
        AddButton("Change Profile", Back);
        AddButton("Options", () => MenuNavigation.Open(this, MenuNavigation.Options));
        AddButton("Exit Game", () => GetTree().Quit(), UIButtonStyle.Danger);
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
            foreach (var campaign in CampaignCatalog.Load().Campaigns)
            {
                var button = new Button { Text = campaign.DisplayName };
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

            if (returning != null) ShowCampaignLevels(returning, back);
            else first?.GrabFocus();
        });
    }

    // =========================================================
    // Reload progress for the active profile when showing a mode, including after a level.
    private void ShowCampaignLevels(CampaignDefinition campaign, Button back)
    {
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

        var progress = CampaignProgressStore.Load(ProfileStore.Selected.Id, out bool recovered);
        if (recovered) Report("Recovered campaign progress from the previous backup.");
        var list = MenuLayout.ScrollList(campaignLevels);
        Button firstPlayable = null;
        foreach (var level in CampaignCatalog.OrderedLevels(campaign))
        {
            bool unlocked = CampaignCatalog.IsUnlocked(campaign, level, progress);
            bool missing = level.Enabled && !MenuNavigation.SceneExists(level.ScenePath);
            string suffix = !level.Enabled ? " (Coming Soon)" : missing ? " (Scene Unavailable)" : !unlocked ? " (Locked)" :
                progress.IsComplete(campaign.Id, level.Id) ? " (Complete)" : "";
            var button = UIButtonFactory.Create(level.DisplayName + suffix, () => Run(
                () => LevelLauncher.Launch(this, campaign.Id, level.Id)), UIButtonStyle.Primary);
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
        else if (Screen == MenuScreen.Campaign && campaignLevels?.Visible == true)
        {
            campaignLevels.Hide();
            campaignModes.Show();
            Report("");
            lastModeButton?.GrabFocus();
        }
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
