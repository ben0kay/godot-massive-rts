// Displays estimated painted army strength from the deployment's incremental totals.
using System.Globalization;
using Godot;

namespace MassiveRts;

internal partial class DeploymentStrengthBar : Control
{
    private const float LeftPanelEnd = 452f, RightPanelWidth = 296f, Margin = 12f;
    private const float MaximumWidth = 620f;
    private readonly Label heading = MakeLabel(Colors.White, "Estimated Army Strength");
    private readonly Label[] labels = new Label[Factions.Count];
    private readonly long[] strengths = new long[Factions.Count];
    private readonly bool[] occupied = new bool[Factions.Count];
    private Rect2 bar;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(heading);
        for (int faction = 0; faction < Factions.Count; faction++)
        {
            labels[faction] = MakeLabel(Factions.Color(faction), $"{Factions.Name(faction)} 0");
            AddChild(labels[faction]);
        }
        Resized += LayoutBar;
        LayoutBar();
    }

    public void Refresh(DeploymentLayout layout)
    {
        bool changed = false;
        for (int faction = 0; faction < Factions.Count; faction++)
        {
            bool hasUnits = layout.CountForFaction(faction) > 0;
            changed |= occupied[faction] != hasUnits;
            occupied[faction] = hasUnits;
            long strength = layout.StrengthOf(faction);
            if (strengths[faction] == strength) continue;
            strengths[faction] = strength;
            labels[faction].Text = $"{Factions.Name(faction)} {Format(strength)}";
            changed = true;
        }
        if (changed) LayoutBar();
    }

    private void LayoutBar()
    {
        float available = Size.X - LeftPanelEnd - RightPanelWidth - Margin * 2f;
        bool betweenPanels = available >= 190f;
        float width = Mathf.Max(0f, Mathf.Min(MaximumWidth, betweenPanels ? available : Size.X - Margin * 2f));
        float left = (Size.X - width) * 0.5f;
        if (betweenPanels) left = Mathf.Clamp(left, LeftPanelEnd + Margin, Size.X - RightPanelWidth - Margin - width);
        heading.Position = new Vector2(left, 8f);
        heading.Size = new Vector2(width, 18f);
        bar = new Rect2(left, 27f, width, 8f);

        int visibleCount = 0;
        foreach (byte faction in Factions.DisplayOrder)
        {
            labels[faction].Visible = occupied[faction] || Factions.ShowWhenEmpty(faction);
            if (labels[faction].Visible) visibleCount++;
        }
        int columns = Mathf.Clamp((int)(width / 90f), 1, Mathf.Max(1, visibleCount));
        float columnWidth = width / columns;
        int slot = 0;
        foreach (byte faction in Factions.DisplayOrder)
        {
            Label label = labels[faction];
            if (!label.Visible) continue;
            label.Position = new Vector2(left + columnWidth * (slot % columns), 38f + 18f * (slot / columns));
            label.Size = new Vector2(columnWidth, 18f);
            slot++;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(bar, new Color("#17202b"));
        long total = 0;
        foreach (long strength in strengths) total += strength;
        if (total == 0) return;
        long cumulative = 0;
        foreach (byte faction in Factions.DisplayOrder)
        {
            if (strengths[faction] == 0) continue;
            float start = bar.Position.X + bar.Size.X * (float)(cumulative / (double)total);
            cumulative += strengths[faction];
            float end = bar.Position.X + bar.Size.X * (float)(cumulative / (double)total);
            DrawRect(new Rect2(start, bar.Position.Y, end - start, bar.Size.Y), Factions.Color(faction));
        }
    }

    private static Label MakeLabel(Color color, string text)
    {
        var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center, ClipText = true };
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeConstantOverride("outline_size", 4);
        return label;
    }

    private static string Format(long value) => value switch
    {
        >= 1000000000 => (value / 1000000000d).ToString("0.#", CultureInfo.InvariantCulture) + "B",
        >= 1000000 => (value / 1000000d).ToString("0.#", CultureInfo.InvariantCulture) + "M",
        >= 1000 => (value / 1000d).ToString("0.#", CultureInfo.InvariantCulture) + "K",
        _ => value.ToString(CultureInfo.InvariantCulture)
    };
}
