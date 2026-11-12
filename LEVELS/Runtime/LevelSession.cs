// Owns the campaign context for one scene instance and records explicit successful completion.
using System;

namespace MassiveRts;

internal sealed record LevelSession(string ProfileId, string CampaignId, string LevelId)
{
    public void CompleteCurrentLevel(bool victory)
    {
        if (!victory) return;
        if (ProfileStore.Selected?.Id != ProfileId)
            throw new InvalidOperationException("The active profile has changed.");
        CampaignProgressStore.MarkComplete(ProfileId, CampaignId, LevelId);
    }
}
