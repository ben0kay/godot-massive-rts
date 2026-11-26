// Resolves authored starts by unit ID and preserves older V-wing maps.
using System;
using Godot;
using StartsByUnit = Godot.Collections.Dictionary<string, Godot.Vector2[]>;

namespace MassiveRts;

public partial class AutoBattlerRules
{
    internal const string LegacyVWingId = "v_wing";

    internal static bool SupportsAircraft(string campaignId) => campaignId == AutoBattlerCampaignStore.CampaignId;
    internal static bool AllowsAircraftFaction(byte faction) => faction is Factions.Blue or Factions.Red;

    private StartsByUnit AircraftStarts(byte faction) => faction == Factions.Blue ? BlueAircraftStarts : RedAircraftStarts;

    internal Vector2[] ReadAircraftStarts(byte faction, string unitId)
    {
        if (AircraftStarts(faction).TryGetValue(unitId, out var points)) return points;
        if (unitId != LegacyVWingId) return Array.Empty<Vector2>();
        return faction == Factions.Blue ? BlueVWingStarts : RedVWingStarts;
    }

    internal int ReadLegacyFlightCount(byte faction, string unitId)
    {
        if (unitId != LegacyVWingId || AircraftStarts(faction).ContainsKey(unitId)) return 0;
        return faction == Factions.Blue ? BlueVWings : RedVWings;
    }

    internal void SetAircraftStarts(byte faction, string unitId, Vector2[] points)
    {
        AircraftStarts(faction)[unitId] = points;
    }

    internal bool HasAircraftStarts(byte faction, string unitId) => AircraftStarts(faction).ContainsKey(unitId);

    internal void RestoreLegacyAircraftStarts(byte faction, string unitId) => AircraftStarts(faction).Remove(unitId);
}
