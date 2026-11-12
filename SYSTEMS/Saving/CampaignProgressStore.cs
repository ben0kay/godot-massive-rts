// Loads and saves completion data for a single validated profile ID.
using System;
using System.Linq;
using Godot;

namespace MassiveRts;

internal static class CampaignProgressStore
{
    // =========================================================
    // Reload on completion so other campaigns and retired level IDs survive each write.
    public static CampaignProgress Load(string profileId, out bool recovered) => AtomicJsonFile.Load(
        PathFor(profileId), value => Valid(value, profileId),
        () => new CampaignProgress { Version = 1, ProfileId = profileId, CompletedLevelIds = new() }, out recovered);

    public static void MarkComplete(string profileId, string campaignId, string levelId)
    {
        CampaignProgress progress = Load(profileId, out _);
        if (!progress.CompletedLevelIds.TryGetValue(campaignId, out var levels))
            progress.CompletedLevelIds.Add(campaignId, levels = new());
        if (levels.Contains(levelId)) return;
        levels.Add(levelId);
        AtomicJsonFile.Save(PathFor(profileId), progress, value => Valid(value, profileId));
    }

    private static string PathFor(string profileId)
    {
        if (!ProfileStore.ValidId(profileId)) throw new ArgumentException("Invalid profile ID.");
        return ProjectSettings.GlobalizePath($"user://Profiles/{profileId}/campaign_progress.json");
    }

    private static bool Valid(CampaignProgress value, string profileId) =>
        value.Version == 1 && value.ProfileId == profileId &&
        value.CompletedLevelIds != null && value.CompletedLevelIds.Count <= 128 &&
        value.CompletedLevelIds.All(entry => CampaignCatalog.ValidId(entry.Key) && entry.Value != null &&
            entry.Value.Count is >= 1 and <= 1024 && entry.Value.All(CampaignCatalog.ValidId) &&
            entry.Value.Distinct(StringComparer.Ordinal).Count() == entry.Value.Count);
}
