// Bottom gameplay bar showing unit types and pooled health for the current selection.
using System;
using System.Text;
using Godot;

namespace MassiveRts;

public partial class SelectionHud : PanelContainer
{
    private readonly Label counts = new() { MouseFilter = MouseFilterEnum.Ignore,
        AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label heading = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly ProgressBar health = new()
    {
        CustomMinimumSize = new Vector2(0, 6), ShowPercentage = false, MouseFilter = MouseFilterEnum.Ignore
    };
    private readonly StringBuilder description = new();
    private int[] typeCounts = Array.Empty<int>();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color("#17212d"),
            ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 10, ContentMarginBottom = 10 });
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(270, 58),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin, MouseFilter = MouseFilterEnum.Ignore };
        // Keep selection details on the left, leaving the rest of the bar available.
        var left = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(left);
        left.AddChild(column);
        column.AddChild(heading);
        column.AddChild(health);
        column.AddChild(counts);
        health.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color("#60b891") });
    }

    public void Refresh(UnitState units, ReadOnlySpan<int> selected)
    {
        if (typeCounts.Length != units.Catalog.Count) typeCounts = new int[units.Catalog.Count];
        Array.Clear(typeCounts);
        int living = 0;
        double current = 0, maximum = 0;
        foreach (int id in selected)
        {
            if ((uint)id >= (uint)units.Count || units.Health[id] <= 0f) continue;
            int type = units.Type[id];
            float maxHealth = units.Catalog[type].Health;
            current += Mathf.Min(units.Health[id], maxHealth);
            maximum += maxHealth;
            typeCounts[type]++;
            living++;
        }

        health.Visible = counts.Visible = living > 0;
        if (living == 0)
        {
            heading.Text = "No units selected";
            return;
        }

        double percentage = maximum > 0 ? current / maximum * 100 : 0;
        heading.Text = $"{living:N0} selected   ·   {percentage:0.#}% HP";
        health.Value = percentage;
        description.Clear();
        for (int type = 0; type < typeCounts.Length; type++)
        {
            if (typeCounts[type] == 0) continue;
            if (description.Length > 0) description.Append("   ·   ");
            description.Append(typeCounts[type]).Append(" × ").Append(units.Catalog[type].Name);
        }
        counts.Text = description.ToString();
    }
}
