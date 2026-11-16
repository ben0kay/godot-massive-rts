// Selection content showing unit types and pooled health for the current selection.
using System;
using System.Text;
using Godot;

namespace MassiveRts;

public partial class SelectionHud : VBoxContainer
{
    private readonly Label counts = new() { MouseFilter = MouseFilterEnum.Ignore,
        AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label heading = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly ProgressBar health = new()
    {
        CustomMinimumSize = new Vector2(0, 6), ShowPercentage = false, MouseFilter = MouseFilterEnum.Ignore
    };
    private readonly StringBuilder description = new();
    private RuntimeUnitCatalog infoCatalog;
    private string[] combatInfo;
    private int[] typeCounts = Array.Empty<int>();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddChild(heading);
        AddChild(health);
        AddChild(counts);
        health.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color("#60b891") });
    }

    public void Refresh(UnitState units, ReadOnlySpan<int> selected)
    {
        if (infoCatalog != units.Catalog)
        {
            infoCatalog = units.Catalog;
            typeCounts = new int[infoCatalog.Count];
            combatInfo = new string[infoCatalog.Count];
            for (int i = 0; i < combatInfo.Length; i++)
            {
                ref readonly var stats = ref infoCatalog[i];
                combatInfo[i] = stats.AllowedTargets == TargetCategory.None ? "Noncombat · " + infoCatalog.DamageRules.ArmourName(stats.ArmourClass) + " armour" :
                    stats.UnitDamagePercent >= 0f && stats.StructureDamagePercent >= 0f
                    ? $"{infoCatalog.DamageRules.ArmourName(stats.ArmourClass)} armour · {stats.AgainstUnits(stats.Damage):0.##} damage vs units · {stats.AgainstStructures(stats.Damage):0.##} vs structures"
                    : infoCatalog.DamageRules.Describe(stats.DamageType, stats.ArmourClass);
            }
        }
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
        int onlyType = -1;
        for (int type = 0; type < typeCounts.Length; type++)
        {
            if (typeCounts[type] == 0) continue;
            onlyType = onlyType == -1 ? type : -2;
            if (description.Length > 0) description.Append("   ·   ");
            description.Append(typeCounts[type]).Append(" × ").Append(units.Catalog[type].Name);
        }
        if (onlyType >= 0) description.Append('\n').Append(combatInfo[onlyType]);
        counts.Text = description.ToString();
    }
}
