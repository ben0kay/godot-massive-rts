// Displays permanent ownership and quotes batch recruitment from the campaign catalog.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed partial class AutoBattlerRecruitmentPage : VBoxContainer
{
    private readonly AutoBattlerRecruitment recruitment;
    private readonly List<(AutoBattlerRecruitment.Offer Offer, Label Owned, Button Buy)> rows = new();
    private readonly SpinBox quantity;
    private AutoBattlerCampaignState state;

    public AutoBattlerRecruitmentPage(AutoBattlerRecruitment recruitment, Action<string, int> buy)
    {
        this.recruitment = recruitment;
        int spacing = UIMasterConfig.Current.Spacing;
        AddThemeConstantOverride("separation", spacing);
        var batch = new HFlowContainer();
        AddChild(batch);
        batch.AddChild(MenuLayout.Label("Recruit quantity"));
        quantity = new SpinBox { MinValue = 1, MaxValue = int.MaxValue, Step = 1, Value = 1, CustomMinimumSize = new Vector2(120, 0) };
        batch.AddChild(quantity);
        foreach (int size in new[] { 1, 10, 50, 100 })
            batch.AddChild(UIButtonFactory.Create(size.ToString(), () => quantity.Value = size, UIButtonStyle.Compact));
        quantity.ValueChanged += _ => RefreshOffers();

        foreach (var offer in recruitment.Offers)
        {
            var unit = offer.Unit;
            var panel = new PanelContainer();
            panel.AddThemeStyleboxOverride("panel", MenuLayout.PanelStyle);
            AddChild(panel);
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", spacing);
            panel.AddChild(row);
            var details = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddChild(details);
            details.AddChild(MenuLayout.Label(unit.DisplayName));
            details.AddChild(MenuLayout.Label(unit.Role, muted: true));
            details.AddChild(MenuLayout.Label(
                $"Tier {unit.SizeTier:0.#}   ·   Battle Rating {recruitment.Rating(unit.Id):N0}   ·   {offer.Price:N0} Credits each",
                muted: true));
            var owned = MenuLayout.Label("");
            details.AddChild(owned);
            var button = UIButtonFactory.Create("", () => buy(unit.Id, (int)quantity.Value), UIButtonStyle.Primary);
            button.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            row.AddChild(button);
            rows.Add((offer, owned, button));
        }
    }

    internal void Refresh(AutoBattlerCampaignState saved)
    {
        state = saved;
        foreach (var row in rows) row.Owned.Text = $"Owned {state.Owned(row.Offer.Unit.Id):N0}";
        RefreshOffers();
    }

    private void RefreshOffers()
    {
        if (state == null) return;
        foreach (var row in rows)
        {
            bool affordable = recruitment.TryQuote(state, row.Offer.Unit.Id, (int)quantity.Value,
                out long cost, out string reason);
            row.Buy.Text = row.Offer.Available ? $"Recruit · {cost:N0} Credits" : "Locked";
            row.Buy.Disabled = !affordable;
            row.Buy.TooltipText = reason;
        }
    }
}
