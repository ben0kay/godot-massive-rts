// Presents registered Preset Battles levels as cards and delegates opening their briefings.
using System;
using Godot;

namespace MassiveRts;

internal sealed partial class PresetCampaignLevelPicker : GridContainer
{
    public PresetCampaignLevelPicker(CampaignDefinition campaign, PlayerProfile profile,
        Func<string, bool> isComplete, Action<string> brief)
    {
        Columns = 3;
        int spacing = UIMasterConfig.Current.Spacing;
        AddThemeConstantOverride("h_separation", spacing);
        AddThemeConstantOverride("v_separation", spacing);
        Resized += () => Columns = Size.X >= 850f ? 3 : Size.X >= 550f ? 2 : 1;
        foreach (var level in CampaignCatalog.OrderedLevels(campaign))
        {
            bool missing = level.Enabled && !MenuNavigation.SceneExists(level.ScenePath);
            bool playable = level.Enabled && !missing && CampaignCatalog.IsUnlocked(level, profile);
            string state = !level.Enabled ? "Coming soon" : missing ? "Scene unavailable" :
                !playable ? "Locked" : isComplete(level.Id) ? "Complete" : "Available";

            var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            panel.AddThemeStyleboxOverride("panel", MenuLayout.PanelStyle);
            AddChild(panel);
            var card = new VBoxContainer();
            panel.AddChild(card);
            var button = UIButtonFactory.Create(level.MenuName, () => brief(level.Id), UIButtonStyle.Primary);
            button.Disabled = !playable;
            if (missing) button.TooltipText = level.ScenePath;
            card.AddChild(button);
            card.AddChild(MenuLayout.Label(state, muted: true));
            if (!string.IsNullOrWhiteSpace(level.Description))
                card.AddChild(MenuLayout.Label(level.Description, muted: true));
        }
    }
}
