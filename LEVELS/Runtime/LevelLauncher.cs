// Validates campaign launch requests and attaches session controls to that scene instance.
using System;
using System.Linq;
using System.IO;
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
        if (CampaignCatalog.IsTactical(campaignId))
        {
            LaunchAutoBattler(source, level, profile.Id, campaignId);
            return;
        }

        var scene = MenuNavigation.LoadScene(level.ScenePath).Instantiate();
        var session = new LevelSession(profile.Id, campaignId, levelId);
        scene.AddChild(new LevelExitInput(session, catalog.AllowDevelopmentCompletion && OS.IsDebugBuild()));
        MenuNavigation.Change(source, scene);
    }

    private static void LaunchAutoBattler(Node source, LevelDefinition level, string profileId, string campaignId)
    {
        AutoBattlerCampaignState state;
        try { state = CampaignCatalog.IsSkirmish(campaignId) ? null : AutoBattlerCampaignStore.Open(profileId, out _); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(error.Message, error);
        }

        var arenaScene = MenuNavigation.LoadScene("res://BENCHMARK/BenchmarkArena.tscn");
        var instance = MenuNavigation.LoadScene(level.ScenePath).Instantiate();
        if (instance is not CommandSandbox { AutoBattlerRules: not null } map)
        {
            instance.Free();
            throw new InvalidOperationException("This level needs an authored Clash map.");
        }

        var arena = arenaScene.Instantiate<BenchmarkArena>();
        arena.Config = (BenchmarkConfig)arena.Config.Duplicate();
        map.LevelSettings = level;
        if (CampaignCatalog.IsOffense(campaignId)) map.AutoBattlerRules.Objective = AutoBattlerRules.ObjectiveType.DestroyCitadel;
        if (CampaignCatalog.IsDefense(campaignId)) map.AutoBattlerRules.Objective = AutoBattlerRules.ObjectiveType.ProtectCitadel;
        if (CampaignCatalog.IsSkirmish(campaignId))
            state = new AutoBattlerCampaignState
            {
                OwnedUnits = new System.Collections.Generic.Dictionary<string, int>(map.AutoBattlerRules.SkirmishArmy)
            };
        arena.ConfigureCampaignBattle(map, state, campaignId);
        MenuNavigation.Change(source, arena);
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
        if (level == null || !level.Enabled || (!CampaignCatalog.IsSkirmish(campaignId) && !CampaignCatalog.IsUnlocked(level, profile)))
            throw new InvalidOperationException("This level is unavailable or locked.");
        return (catalog, campaign, level, profile);
    }
}
