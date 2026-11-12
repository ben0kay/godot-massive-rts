// Holds versioned per-profile completion IDs independently of authored content and live battles.
using System.Collections.Generic;

namespace MassiveRts;

internal sealed class CampaignProgress
{
    public int Version { get; set; }
    public string ProfileId { get; set; }
    public Dictionary<string, List<string>> CompletedLevelIds { get; set; }

    public bool IsComplete(string campaignId, string levelId) =>
        CompletedLevelIds.TryGetValue(campaignId, out var levels) && levels.Contains(levelId);
}
