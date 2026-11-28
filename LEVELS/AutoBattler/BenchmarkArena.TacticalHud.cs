// Connects the tactical HUD to the existing player-battle lifecycle and fixed steps.
using Godot;

namespace MassiveRts;

public partial class BenchmarkArena
{
    private static readonly float[] BattleSpeeds = { 0.5f, 1f, 2f, 4f };
    private TacticalBattleHud tacticalHud;
    private int battleSpeedIndex = 1;
    private double minimapRefreshClock;
    private bool minimapNeedsRefresh;
    private float BattleSpeed => tacticalHud == null ? 1f : BattleSpeeds[battleSpeedIndex];

    // =========================================================
    private void CreateTacticalHud(CanvasLayer layer)
    {
        tacticalHud = new TacticalBattleHud();
        layer.AddChild(tacticalHud);
        string scenario = CampaignCatalog.IsSkirmish(battleCampaignId) ? "SKIRMISH" : "MISSION";
        var (modeName, objectiveName) = autoBattlerPlaytestMap.AutoBattlerRules.Objective switch
        {
            AutoBattlerRules.ObjectiveType.DestroyCitadel => ("OFFENSE", "Destroy the enemy Citadel."),
            AutoBattlerRules.ObjectiveType.ProtectCitadel => ("DEFENSE", "Protect the friendly Citadel."),
            _ => ("CLASH", "Eliminate the opposing army.")
        };
        tacticalHud.Build($"{scenario} — {modeName}", autoBattlerPlaytestMap.LevelSettings.MenuName, objectiveName,
            () =>
            {
                if (!campaignBattleRunning) return;
                ToggleBattlePause();
                UpdateTacticalControls();
            },
            () =>
            {
                if (!campaignBattleRunning) return;
                battleSpeedIndex = (battleSpeedIndex + 1) % BattleSpeeds.Length;
                UpdateTacticalControls();
            }, () => FinishCampaignBattle("Withdrawn"));
    }

    private void ToggleBattlePause()
    {
        if (offenseBattleEnded) return;
        paused = !paused;
        panel.PauseButton.Text = paused ? "Resume" : "Pause";
    }

    private void UpdateTacticalControls() => tacticalHud?.SetControls(paused, BattleSpeed, !campaignBattleRunning);

    // =========================================================
    private void StartTacticalHud()
    {
        if (tacticalHud == null) return;
        var expected = new bool[Factions.Count];
        expected[Factions.Blue] = expected[Factions.Red] = true;
        foreach (var defense in autoBattlerDefenses) expected[defense.Faction] = true;
        float half = BattleBoundary;
        tacticalHud.Minimap.Configure(new Rect2(-half, -half, 2 * half, 2 * half), terrain,
            autoBattlerWater, autoBattlerCover, autoBattlerPaint?.BridgeAreas, camera);
        tacticalHud.Begin(units, airSquadron, HasCitadelObjective ? offenseCitadel : null, IsDefense, expected);
        tacticalHud.SetArrivals(ReserveCountdown());
        UpdateTacticalControls();
        minimapRefreshClock = 0;
        RefreshTacticalMinimap();
    }

    private void RefreshTacticalMinimap()
    {
        if (tacticalHud?.Visible != true || units == null) return;
        tacticalHud.Minimap.Refresh(units, airSquadron, defenseSystem?.Structures, offenseCitadel, Factions.Blue);
        minimapNeedsRefresh = false;
    }

    private void AdvanceTacticalMinimap(double delta, int steps)
    {
        if (tacticalHud?.Visible != true || !campaignBattleRunning) return;
        minimapNeedsRefresh |= steps != 0;
        minimapRefreshClock += delta;
        if (minimapRefreshClock < .25 || !minimapNeedsRefresh) return;
        minimapRefreshClock = 0;
        RefreshTacticalMinimap();
    }

    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (tacticalHud == null || campaignHud == null || units == null || spawning ||
            input is not InputEventKey { Pressed: true, Echo: false } key ||
            key.PhysicalKeycode != Controls.ToggleSidePanels) return;
        tacticalHud.ToggleCinematic();
        RefreshTacticalMinimap();
        GetViewport().SetInputAsHandled();
    }
}
