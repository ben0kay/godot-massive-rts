// Stores the persistent AutoBattler economy and progression for one commander.
using System;
using System.Collections.Generic;
using System.Linq;

namespace MassiveRts;

internal sealed class AutoBattlerCampaignState
{
    public const int MaxIds = 1024;

    public int Version { get; set; }
    public string ProfileId { get; set; }
    public int Credits { get; set; } = -1;
    public Dictionary<string, int> OwnedUnits { get; set; }
    public List<string> PurchasedResearchIds { get; set; }
    public List<string> CompletedLevelIds { get; set; }
    public List<string> FirstClearClaimedLevelIds { get; set; }
    public Dictionary<string, int> BestSurvivalBonuses { get; set; }

    public int Owned(string unitId) => OwnedUnits.GetValueOrDefault(unitId);

    internal bool IsValid(string profileId) =>
        Version == 1 && ProfileId == profileId && Credits >= 0 &&
        OwnedUnits != null && OwnedUnits.Count <= MaxIds &&
        OwnedUnits.All(entry => CampaignCatalog.ValidId(entry.Key) && entry.Value > 0) &&
        ValidIds(PurchasedResearchIds) && ValidIds(CompletedLevelIds) &&
        ValidIds(FirstClearClaimedLevelIds) && !FirstClearClaimedLevelIds.Except(CompletedLevelIds).Any() &&
        BestSurvivalBonuses != null && BestSurvivalBonuses.Count <= MaxIds &&
        BestSurvivalBonuses.All(entry => entry.Value >= 0) &&
        !BestSurvivalBonuses.Keys.Except(CompletedLevelIds).Any();

    private static bool ValidIds(List<string> ids) =>
        ids != null && ids.Count <= MaxIds && ids.All(CampaignCatalog.ValidId) &&
        ids.Distinct(StringComparer.Ordinal).Count() == ids.Count;
}
