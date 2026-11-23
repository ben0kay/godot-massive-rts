// Displays permanent ownership and quotes batch recruitment from the campaign catalog.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed partial class AutoBattlerRecruitmentPage : VBoxContainer
{
    private const int CardWidth = 196;
    private const int MaxColumns = 7;
    private readonly AutoBattlerRecruitment recruitment;
    private readonly List<(AutoBattlerRecruitment.Offer Offer, Label Owned, Button Buy)> cards = new();
    private readonly SpinBox quantity;
    private AutoBattlerCampaignState state;

    public AutoBattlerRecruitmentPage(AutoBattlerRecruitment recruitment, Action<string, int> buy)
    {
        this.recruitment = recruitment;
        int spacing = UIMasterConfig.Current.Spacing;
        AddThemeConstantOverride("separation", spacing);
        var batch = new HFlowContainer();
        batch.AddThemeConstantOverride("h_separation", 8);
        batch.AddThemeConstantOverride("v_separation", 6);
        AddChild(batch);
        var caption = CardLabel("Quantity", 18);
        caption.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        batch.AddChild(caption);
        quantity = new SpinBox
        {
            MinValue = 1, MaxValue = int.MaxValue, Step = 1, Value = 1,
            CustomMinimumSize = new Vector2(112, 32), SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        batch.AddChild(quantity);
        foreach (int size in new[] { 1, 10, 50, 100 })
        {
            var shortcut = UIButtonFactory.Create(size.ToString(), () => quantity.Value = size, UIButtonStyle.Compact);
            CompactButton(shortcut);
            batch.AddChild(shortcut);
        }
        quantity.ValueChanged += _ => RefreshOffers();

        var grid = new GridContainer();
        grid.AddThemeConstantOverride("h_separation", spacing);
        grid.AddThemeConstantOverride("v_separation", spacing);
        AddChild(grid);
        Resized += () =>
        {
            int columns = Mathf.Clamp(Mathf.FloorToInt((Size.X + spacing) / (CardWidth + spacing)), 1, MaxColumns);
            if (grid.Columns != columns) grid.Columns = columns;
        };

        var config = UIMasterConfig.Current;
        var cardStyle = UIButtonFactory.Box(config.Panel, config.Accent.Darkened(0.72f), 10);
        foreach (var offer in recruitment.Offers)
        {
            var unit = offer.Unit;
            var panel = new PanelContainer
            {
                CustomMinimumSize = new Vector2(CardWidth, 0),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                TooltipText = $"{unit.DisplayName}\n{unit.Role}"
            };
            panel.AddThemeStyleboxOverride("panel", cardStyle);
            grid.AddChild(panel);
            var details = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            details.AddThemeConstantOverride("separation", 3);
            panel.AddChild(details);
            var name = CardLabel(unit.DisplayName, 18);
            name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            details.AddChild(name);
            details.AddChild(CardLabel($"Tier {unit.SizeTier:0.#}  ·  BR {recruitment.Rating(unit.Id):N0}", 14, muted: true));
            var owned = CardLabel("", 15);
            details.AddChild(owned);
            var button = UIButtonFactory.Create("", () => buy(unit.Id, (int)quantity.Value), UIButtonStyle.Primary);
            CompactButton(button);
            button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            details.AddChild(button);
            cards.Add((offer, owned, button));
        }
    }

    private static Label CardLabel(string text, int size, bool muted = false)
    {
        var label = MenuLayout.Label(text, muted);
        label.AutowrapMode = TextServer.AutowrapMode.Off;
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }

    private static void CompactButton(Button button)
    {
        button.CustomMinimumSize = new Vector2(0, 34);
        button.AddThemeFontSizeOverride("font_size", 16);
    }

    internal void Refresh(AutoBattlerCampaignState saved)
    {
        state = saved;
        foreach (var card in cards)
            card.Owned.Text = $"Owned {state.Owned(card.Offer.Unit.Id):N0}  ·  {card.Offer.Price:N0} cr each";
        RefreshOffers();
    }

    private void RefreshOffers()
    {
        if (state == null) return;
        foreach (var card in cards)
        {
            bool affordable = recruitment.TryQuote(state, card.Offer.Unit.Id, (int)quantity.Value,
                out long cost, out string reason);
            card.Buy.Text = card.Offer.Available ? $"Recruit · {cost:N0} cr" : "Locked";
            card.Buy.Disabled = !affordable;
            card.Buy.TooltipText = affordable ? $"Recruit {quantity.Value:N0} for {cost:N0} Credits" : reason;
        }
    }
}
