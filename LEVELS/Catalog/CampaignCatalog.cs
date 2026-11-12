// Validates authored campaign metadata and resolves unlocks from profile completion.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MassiveRts;

[GlobalClass]
public partial class CampaignCatalog : Resource
{
    private const string CatalogPath = "res://LEVELS/Catalog/DefaultCampaignCatalog.tres";
    [Export] public Godot.Collections.Array<CampaignDefinition> Campaigns { get; set; } = new();
    [Export] public bool AllowDevelopmentCompletion { get; set; }

    // =========================================================
    // Load metadata only. Packed scenes are opened when the player launches a level.
    public static CampaignCatalog Load()
    {
        var catalog = GD.Load<CampaignCatalog>(CatalogPath);
        if (catalog == null || catalog.Campaigns == null || catalog.Campaigns.Count == 0)
            throw new InvalidOperationException("The campaign catalog is missing or empty.");

        var campaigns = new HashSet<string>(StringComparer.Ordinal);
        foreach (var campaign in catalog.Campaigns)
        {
            if (campaign == null || !ValidId(campaign.Id) || !campaigns.Add(campaign.Id) ||
                string.IsNullOrWhiteSpace(campaign.DisplayName) || campaign.Levels == null || campaign.Levels.Count == 0)
                throw new InvalidOperationException("The campaign catalog has an invalid or duplicate campaign.");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var orders = new HashSet<int>();
            foreach (var level in campaign.Levels)
            {
                if (level == null || !ValidId(level.Id) || !ids.Add(level.Id) ||
                    level.Order < 1 || !orders.Add(level.Order) || string.IsNullOrWhiteSpace(level.DisplayName))
                    throw new InvalidOperationException($"Campaign {campaign.DisplayName} has an invalid or duplicate level entry.");
            }
        }

        return catalog;
    }

    public static bool ValidId(string id) =>
        id != null && id.Length is >= 1 and <= 64 &&
        id.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');

    public static LevelDefinition[] OrderedLevels(CampaignDefinition campaign) =>
        campaign.Levels.OrderBy(level => level.Order).ToArray();

    internal static bool IsUnlocked(CampaignDefinition campaign, LevelDefinition level, CampaignProgress progress)
    {
        LevelDefinition previous = campaign.Levels.Where(entry => entry.Order < level.Order)
            .OrderByDescending(entry => entry.Order).FirstOrDefault();
        return previous == null || progress.IsComplete(campaign.Id, previous.Id);
    }
}
