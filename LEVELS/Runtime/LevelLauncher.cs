// Validates campaign launch requests and attaches session controls to that scene instance.
using System;
using System.Linq;
using Godot;

namespace MassiveRts;

internal static class LevelLauncher
{
    public static void Brief(Node source, string campaignId, string levelId)
    {
        var (_, campaign, level, _) = Resolve(campaignId, levelId);
        var scene = MenuNavigation.LoadScene(level.ScenePath);
        MenuNavigation.Change(source, new LevelBriefing { Campaign = campaign, Level = level, MapScene = scene });
    }

    public static void Launch(Node source, string campaignId, string levelId)
    {
        if (!source.IsInsideTree()) return;
        var (catalog, _, level, profile) = Resolve(campaignId, levelId);
        var scene = MenuNavigation.LoadScene(level.ScenePath).Instantiate();
        var session = new LevelSession(profile.Id, campaignId, levelId);
        scene.AddChild(new LevelExitInput(session, catalog.AllowDevelopmentCompletion && OS.IsDebugBuild()));
        MenuNavigation.Change(source, scene);
    }

    // =========================================================
    // Check the selected profile at both briefing and deploy entry points.
    private static (CampaignCatalog Catalog, CampaignDefinition Campaign, LevelDefinition Level, PlayerProfile Profile)
        Resolve(string campaignId, string levelId)
    {
        var profile = ProfileStore.Selected
            ?? throw new InvalidOperationException("Choose a profile before starting a campaign.");
        var catalog = CampaignCatalog.Load();
        var campaign = catalog.Campaigns.FirstOrDefault(entry => entry.Id == campaignId)
            ?? throw new InvalidOperationException("Campaign unavailable.");
        var level = campaign.Levels.FirstOrDefault(entry => entry.Id == levelId);
        if (level == null || !level.Enabled || !CampaignCatalog.IsUnlocked(level, profile))
            throw new InvalidOperationException("This level is unavailable or locked.");
        return (catalog, campaign, level, profile);
    }
}
