// Resolves recruitment offers against the current unit catalog and calculates purchase quotes.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MassiveRts;

internal sealed class AutoBattlerRecruitment
{
    private const string ConfigPath = "res://LEVELS/AutoBattler/PROGRESSION/DefaultAutoBattlerRecruitment.tres";
    private const string UnitsPath = "res://GAMEPLAY/UNITS/CATALOG/DefaultUnitCatalog.tres";

    internal sealed record Offer(UnitDefinition Unit, int Price, bool Available);
    private readonly Dictionary<string, Offer> offers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UnitDefinition> units;
    public IEnumerable<Offer> Offers => offers.Values.OrderByDescending(offer => offer.Available)
        .ThenBy(offer => offer.Price);

    public AutoBattlerRecruitment()
    {
        var catalog = GD.Load<UnitCatalog>(UnitsPath);
        var config = GD.Load<AutoBattlerRecruitmentConfig>(ConfigPath);
        if (catalog?.Definitions == null || config?.Prices == null || config.InitiallyAvailable == null)
            throw new InvalidOperationException("AutoBattler recruitment configuration is unavailable.");

        units = catalog.Definitions.Where(unit => unit != null).ToDictionary(unit => unit.Id, StringComparer.Ordinal);
        var available = new HashSet<string>(config.InitiallyAvailable, StringComparer.Ordinal);
        if (available.Any(id => !config.Prices.ContainsKey(id)))
            throw new InvalidOperationException("A starting recruit is missing its price.");

        foreach (var entry in config.Prices)
        {
            if (!CampaignCatalog.ValidId(entry.Key) || entry.Value <= 0 || !units.TryGetValue(entry.Key, out var unit))
                throw new InvalidOperationException($"Invalid recruitment offer for {entry.Key}.");
            offers.Add(entry.Key, new Offer(unit, entry.Value, available.Contains(entry.Key)));
        }
    }

    public int Rating(string unitId) => units.TryGetValue(unitId, out var unit)
        ? Math.Clamp(unit.BattleRating, 0, UnitDefinition.MaxBattleRating) : 0;

    public string Name(string unitId) => units.TryGetValue(unitId, out var unit) ? unit.DisplayName : unitId;

    public bool TryQuote(AutoBattlerCampaignState state, string unitId, int quantity, out long cost, out string reason)
    {
        cost = 0;
        if (unitId == null || !offers.TryGetValue(unitId, out var offer) || !offer.Available)
            reason = "This unit is locked for recruitment.";
        else if (quantity <= 0)
            reason = "Choose a positive recruitment quantity.";
        else
        {
            cost = checked((long)offer.Price * quantity);
            if (cost > state.Credits) reason = "Insufficient Credits.";
            else if ((long)state.Owned(unitId) + quantity > int.MaxValue)
                reason = "That quantity exceeds the roster save limit.";
            else if (!state.OwnedUnits.ContainsKey(unitId) && state.OwnedUnits.Count >= AutoBattlerCampaignState.MaxIds)
                reason = "The roster save has reached its unit-type limit.";
            else
            {
                reason = "";
                return true;
            }
        }
        return false;
    }
}
