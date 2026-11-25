// Reads and commits AutoBattler saves independently of Fortress Defense progress.
using System;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace MassiveRts;

internal static class AutoBattlerCampaignStore
{
    public const string CampaignId = "Campaign001";

    public static AutoBattlerCampaignState Open(string profileId, out bool recovered)
    {
        string path = PathFor(profileId);
        using var transaction = BeginTransaction(path);
        return Load(path, profileId, out recovered);
    }

    // =========================================================
    // Read the latest balance and publish the debit and roster quantity together.
    public static AutoBattlerCampaignState Recruit(string profileId, AutoBattlerRecruitment recruitment, string unitId, int quantity)
    {
        string path = PathFor(profileId);
        using var transaction = BeginTransaction(path);
        var state = Load(path, profileId, out _);
        if (unitId == DisruptorGrid.Id)
        {
            long tileCost = (long)quantity * DisruptorGrid.Price;
            if (quantity <= 0 || quantity > DisruptorGrid.MaxTiles - state.OwnedDisruptorTiles || tileCost > state.Credits)
                throw new InvalidOperationException("Not enough credits or inventory space for those tiles.");
            state.Credits -= (int)tileCost;
            state.OwnedDisruptorTiles += quantity;
            AtomicJsonFile.Save(path, state, value => value.IsValid(profileId));
            return state;
        }
        if (!recruitment.TryQuote(state, unitId, quantity, out long cost, out string reason))
            throw new InvalidOperationException(reason);

        state.Credits = checked(state.Credits - (int)cost);
        state.OwnedUnits[unitId] = checked(state.Owned(unitId) + quantity);
        AtomicJsonFile.Save(path, state, value => value.IsValid(profileId));
        return state;
    }

    private static AutoBattlerCampaignState Load(string path, string profileId, out bool recovered) =>
        AtomicJsonFile.Load(path, value => value.IsValid(profileId), () => Initialize(path, profileId), out recovered);

    // Save migration once. Legacy completions are marked claimed without issuing Credits.
    private static AutoBattlerCampaignState Initialize(string path, string profileId)
    {
        var legacy = CampaignProgressStore.Load(profileId, out _);
        var completed = legacy.CompletedLevelIds.GetValueOrDefault(CampaignId) ?? new List<string>();
        var state = new AutoBattlerCampaignState
        {
            Version = 1, ProfileId = profileId, Credits = 500, OwnedUnits = new(),
            PurchasedResearchIds = new(), CompletedLevelIds = new(completed),
            FirstClearClaimedLevelIds = new(completed), BestSurvivalBonuses = new()
        };
        AtomicJsonFile.Save(path, state, value => value.IsValid(profileId));
        return state;
    }

    private static FileStream BeginTransaction(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        return new FileStream(path + ".lock", FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite, FileShare.None);
    }

    private static string PathFor(string profileId)
    {
        if (!ProfileStore.ValidId(profileId) || ProfileStore.Selected?.Id != profileId)
            throw new InvalidOperationException("Select the campaign profile again.");
        // The original filename keeps existing campaign profiles and backups readable.
        return ProjectSettings.GlobalizePath($"user://Profiles/{profileId}/preset_battles.json");
    }
}
