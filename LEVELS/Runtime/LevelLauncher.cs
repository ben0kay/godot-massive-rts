// Validates campaign launch requests and attaches session controls to that scene instance.
using System;
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
        scene.AddChild(new LevelExitInput(session, catalog.AllowDevelopmentCompletion && OS.IsDebugBuild()));
        MenuNavigation.Change(source, scene);
    }

}
