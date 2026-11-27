// Composes the AutoBattler level picker and recruitment page around one profile save.
using System;
using System.IO;
using System.Linq;
using Godot;

namespace MassiveRts;

internal sealed partial class AutoBattlerCampaignHeadquarters : Control
{
    internal CampaignDefinition Campaign { get; init; }
    internal bool OpenRecruitment { get; init; }
    private PlayerProfile profile;
    private AutoBattlerCampaignState state;
    private AutoBattlerRecruitment recruitment;
    private Label summary, status;
    private Button levelsTab, recruitmentTab;
    private Control levelsPage;
    private AutoBattlerRecruitmentPage recruitmentPage;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var config = UIMasterConfig.Current;
        var background = new ColorRect { Color = config.Background, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(background);
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var margin = new MarginContainer();
        AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (string edge in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride("margin_" + edge, config.Margin);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", config.Spacing);
        margin.AddChild(body);

        var title = MenuLayout.Label("Clash Headquarters");
        title.AddThemeFontSizeOverride("font_size", config.HeadingSize);
        body.AddChild(title);
        summary = MenuLayout.Label("", muted: true);
        body.AddChild(summary);

        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", config.Spacing);
        body.AddChild(tabs);
        levelsTab = UIButtonFactory.Create("Campaign Levels", () => SelectPage(false));
        recruitmentTab = UIButtonFactory.Create("Recruitment", () => SelectPage(true));
        var tabGroup = new ButtonGroup();
        foreach (var tab in new[] { levelsTab, recruitmentTab })
        {
            tab.ToggleMode = true;
            tab.ButtonGroup = tabGroup;
            tab.Disabled = true;
            tabs.AddChild(tab);
        }

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            FollowFocus = true
        };
        body.AddChild(scroll);
        var pages = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(pages);

        status = MenuLayout.Label("", muted: true);
        body.AddChild(status);
        status.Hide();
        var back = UIButtonFactory.Create("Back to Campaign", Back);
        body.AddChild(back);
        back.GrabFocus();

        Run(() =>
        {
            profile = ProfileStore.Selected ?? throw new InvalidOperationException("Choose a profile to open Headquarters.");
            recruitment = new AutoBattlerRecruitment();
            state = AutoBattlerCampaignStore.Open(profile.Id, out bool recovered);
            var legacy = CampaignProgressStore.Load(profile.Id, out bool legacyRecovered);
            levelsPage = new AutoBattlerCampaignLevelPicker(Campaign, profile,
                id => state.CompletedLevelIds.Contains(id) || legacy.IsComplete(Campaign.Id, id),
                id => Run(() => LevelLauncher.Brief(this, Campaign.Id, id)));
            recruitmentPage = new AutoBattlerRecruitmentPage(recruitment, Recruit);
            pages.AddChild(levelsPage);
            pages.AddChild(recruitmentPage);
            Refresh();
            levelsTab.Disabled = recruitmentTab.Disabled = false;
            SelectPage(OpenRecruitment);
            (OpenRecruitment ? recruitmentTab : levelsTab).GrabFocus();
            if (recovered || legacyRecovered) Report("Recovered campaign data from the previous backup.");
        });
    }

    private void SelectPage(bool recruit)
    {
        levelsPage.Visible = !recruit;
        recruitmentPage.Visible = recruit;
        levelsTab.SetPressedNoSignal(!recruit);
        recruitmentTab.SetPressedNoSignal(recruit);
    }

    private void Recruit(string unitId, int quantity) => Run(() =>
    {
        state = AutoBattlerCampaignStore.Recruit(profile.Id, recruitment, unitId, quantity);
        Refresh();
        Report($"Recruited {quantity:N0} × {(unitId == DisruptorGrid.Id ? DisruptorGrid.Name : recruitment.Name(unitId))}.");
    });

    private void Refresh()
    {
        long owned = state.OwnedUnits.Values.Sum(count => (long)count);
        long rating = state.OwnedUnits.Sum(entry => (long)entry.Value * recruitment.Rating(entry.Key));
        summary.Text = $"{profile.Name}   ·   Credits {state.Credits:N0}   ·   Owned soldiers {owned:N0}   ·   Estimated strength {rating:N0}";
        recruitmentPage.Refresh(state);
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (!input.IsActionPressed("ui_cancel")) return;
        GetViewport().SetInputAsHandled();
        Back();
    }

    private void Back() => Run(() => MenuNavigation.OpenMode(this, FrontMenu.MenuScreen.Missions));

    private void Run(Action action)
    {
        try { action(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            Report(error.Message);
        }
    }

    private void Report(string message)
    {
        status.Text = message;
        status.Show();
    }
}
