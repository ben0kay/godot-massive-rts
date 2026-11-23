// Displays permanent ownership and quotes batch recruitment from the campaign catalog.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MassiveRts;

internal sealed partial class AutoBattlerRecruitmentPage : VBoxContainer
{
    private const int CardWidth = 224;
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

        var offers = recruitment.Offers.ToArray();
        AddSection("Available to recruit", offers.Where(offer => offer.Available).ToArray(), buy, spacing);
        AddSection("Locked units", offers.Where(offer => !offer.Available).ToArray(), buy, spacing);
    }

    private void AddSection(string title, AutoBattlerRecruitment.Offer[] offers, Action<string, int> buy, int spacing)
    {
        if (offers.Length == 0) return;
        var section = new VBoxContainer();
        section.AddThemeConstantOverride("separation", 6);
        AddChild(section);
        var heading = CardLabel(title, 19);
        heading.AddThemeColorOverride("font_color", UIMasterConfig.Current.Accent);
        section.AddChild(heading);

        var grid = new GridContainer { SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        grid.AddThemeConstantOverride("h_separation", spacing);
        grid.AddThemeConstantOverride("v_separation", spacing);
        section.AddChild(grid);
        Resized += () =>
        {
            int columns = Mathf.Clamp(Mathf.FloorToInt((Size.X + spacing) / (CardWidth + spacing)),
                1, Math.Min(MaxColumns, offers.Length));
            if (grid.Columns != columns) grid.Columns = columns;
        };

        var config = UIMasterConfig.Current;
        var availableStyle = UIButtonFactory.Box(config.Panel.Lightened(0.035f), config.Accent.Darkened(0.48f), 10);
        var lockedStyle = UIButtonFactory.Box(config.Panel.Darkened(0.12f), config.Button, 10);
        foreach (var offer in offers)
        {
            var unit = offer.Unit;
            var panel = new PanelContainer
            {
                CustomMinimumSize = new Vector2(CardWidth, 0),
                TooltipText = $"{unit.DisplayName}\n{unit.Role}\nTier {unit.SizeTier:0.#} · Battle Rating {recruitment.Rating(unit.Id):N0}"
            };
            panel.AddThemeStyleboxOverride("panel", offer.Available ? availableStyle : lockedStyle);
            grid.AddChild(panel);
            var details = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            details.AddThemeConstantOverride("separation", 6);
            panel.AddChild(details);
            var name = CardLabel(unit.DisplayName, 18);
            name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            details.AddChild(name);

            var stats = new HBoxContainer();
            stats.AddThemeConstantOverride("separation", 8);
            details.AddChild(stats);
            var owned = CardLabel("", 16);
            owned.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            owned.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            stats.AddChild(owned);
            var price = CardLabel($"{offer.Price:N0} cr / unit", 15, muted: !offer.Available);
            if (offer.Available) price.AddThemeColorOverride("font_color", new Color("#ffda93"));
            stats.AddChild(price);

            Button button = null;
            if (offer.Available)
            {
                button = UIButtonFactory.Create("", () => buy(unit.Id, (int)quantity.Value), UIButtonStyle.Primary);
                CompactButton(button);
                button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                details.AddChild(button);
            }
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
            card.Owned.Text = $"Owned {state.Owned(card.Offer.Unit.Id):N0}";
        RefreshOffers();
    }

    private void RefreshOffers()
    {
        if (state == null) return;
        foreach (var card in cards)
        {
            if (card.Buy == null) continue;
            bool affordable = recruitment.TryQuote(state, card.Offer.Unit.Id, (int)quantity.Value,
                out long cost, out string reason);
            card.Buy.Text = $"Recruit · {cost:N0} cr";
            card.Buy.Disabled = !affordable;
            card.Buy.TooltipText = affordable ? $"Recruit {quantity.Value:N0} for {cost:N0} Credits" : reason;
        }
    }
}
