// Validates campaign launch requests and attaches session controls to that scene instance.
using System;
using System.IO;
using System.Linq;
using Godot;

namespace MassiveRts;

internal static class LevelLauncher
{
    // =========================================================
    // Re-resolve IDs and progress at launch so the menu is never the authority on unlocks.
    public static void Launch(Node source, string campaignId, string levelId)
    {
        if (!source.IsInsideTree()) return;
        string profileId = ProfileStore.Selected?.Id
            ?? throw new InvalidOperationException("Choose a profile before starting a campaign.");
        CampaignCatalog catalog = CampaignCatalog.Load();
        var campaign = catalog.Campaigns.FirstOrDefault(entry => entry.Id == campaignId)
            ?? throw new InvalidOperationException("Campaign unavailable.");
        var level = campaign.Levels.FirstOrDefault(entry => entry.Id == levelId);
        if (level == null || !level.Enabled ||
            !CampaignCatalog.IsUnlocked(campaign, level, CampaignProgressStore.Load(profileId, out _)))
            throw new InvalidOperationException("This level is unavailable or locked.");

        var scene = MenuNavigation.LoadScene(level.ScenePath).Instantiate();
        var session = new LevelSession(profileId, campaignId, levelId);
        scene.AddChild(CreateControls(level, session, catalog.AllowDevelopmentCompletion && OS.IsDebugBuild()));
        MenuNavigation.Change(source, scene);
    }

    // =========================================================
    // Direct editor launches never instantiate this layer or touch campaign data.
    private static CanvasLayer CreateControls(LevelDefinition level, LevelSession session, bool allowTestCompletion)
    {
        var layer = new CanvasLayer { Layer = 10 };
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", MenuLayout.PanelStyle);
        layer.AddChild(panel);


        var buttons = new VBoxContainer();
        buttons.AddThemeConstantOverride("separation", UIMasterConfig.Current.Spacing);
        panel.AddChild(buttons);
        var status = MenuLayout.Label("", muted: true);
        status.Hide();

        void Return(bool complete)
        {
            if (!panel.IsInsideTree()) return;
            try
            {
                if (complete) session.CompleteCurrentLevel(victory: true);
                MenuNavigation.Open(panel, MenuNavigation.Campaign);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                status.Text = error.Message;
                status.Show();
            }
        }

        buttons.AddChild(MenuLayout.Label(level.DisplayName));
        var back = UIButtonFactory.Create("Return to Campaign", () => Return(false));
        buttons.AddChild(back);
        if (allowTestCompletion)
            buttons.AddChild(UIButtonFactory.Create("Mark Test Level Complete (Dev)", () => Return(true)));
        buttons.AddChild(status);
        panel.Ready += () =>
        {
            panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
            panel.GrowHorizontal = Control.GrowDirection.Begin;
            panel.Position += new Vector2(-24, 24);
            back.GrabFocus();
        };
        return layer;
    }
}
