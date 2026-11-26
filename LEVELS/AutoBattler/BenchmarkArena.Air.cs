// Starts authored Clash flights on the shared battle clock, outside the ground roster.
using Godot;

namespace MassiveRts;

public partial class BenchmarkArena
{
    private AirSquadron airSquadron;

    private int BlueVWings => !IsOffense && autoBattlerPlaytestMap?.AutoBattlerRules is { } rules
        ? Mathf.Clamp(rules.BlueVWings, 0, AirSquadron.MaxPerFaction) : 0;

    private int RedVWings => !IsOffense && autoBattlerPlaytestMap?.AutoBattlerRules is { } rules
        ? Mathf.Clamp(rules.RedVWings, 0, AirSquadron.MaxPerFaction) : 0;

    private void StartAirSquadron()
    {
        int blue = BlueVWings, red = RedVWings;
        if (blue + red == 0) return;

        // Bound vertical clearance once for authored cover and defense footprints.
        float clearance = 0f;
        float obstacleHeight = Mathf.Max(Config.ObstacleHeight, 9f);
        foreach (var area in autoBattlerCover)
        {
            float ground = terrain?.FootprintHeight(area.Min, area.Max, out _) ?? 0f;
            float top = area.AirborneHeightRange?.Y ?? ground + obstacleHeight;
            clearance = Mathf.Max(clearance, top + 3f);
        }
        foreach (var defense in autoBattlerDefenses)
        {
            var area = defense.Footprint(buildings);
            float ground = terrain?.FootprintHeight(area.Min, area.Max, out _) ?? 0f;
            clearance = Mathf.Max(clearance, ground + obstacleHeight + 3f);
        }

        var scene = GD.Load<PackedScene>("res://GAMEPLAY/COMBAT/AIR/AirSquadron.tscn");
        airSquadron = scene.Instantiate<AirSquadron>();
        AddChild(airSquadron);
        airSquadron.Initialize(blue, red, autoBattlerPlaytestMap.MapSize * 0.5f, terrain, clearance, catalog.DamageRules);
    }
}
