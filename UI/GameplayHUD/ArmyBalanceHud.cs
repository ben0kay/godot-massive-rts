// Shared, presentation-only army balance HUD; reads faction totals without scanning soldiers.
using System;
using Godot;

namespace MassiveRts;

public partial class ArmyBalanceHud : Control
{
    [ExportGroup("Bar appearance")]
    [Export(PropertyHint.Range, "250,1100,10")] public float MaximumWidth { get; set; } = 820f;
    [Export(PropertyHint.Range, "6,24,1")] public float BarThickness { get; set; } = 12f;
    [Export(PropertyHint.Range, "4,50,1")] public float TopOffset { get; set; } = 12f;
    [Export(PropertyHint.Range, "1,20,0.5")] public float AnimationSpeed { get; set; } = 7f;

    private static readonly Color Backing = new("#111821");
    private static readonly Color Outline = new("#748396");
    private readonly Label[] labels = new Label[Factions.Count];
    private readonly int[] previousCounts = new int[Factions.Count];
    private readonly float[] shares = new float[Factions.Count];
    private readonly float[] targets = new float[Factions.Count];
    private UnitState source;
    private StructureHealth objective;
    private Label objectiveLabel;
    private float objectiveY, objectiveFraction, previousObjectiveHealth = -1f;
    private float barX, barWidth;
    private bool hasInitialShares;

    // =========================================================
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        foreach (byte faction in Factions.DisplayOrder)
        {
            labels[faction] = MakeLabel(Factions.Color(faction), HorizontalAlignment.Center);
            AddChild(labels[faction]);
        }
        objectiveLabel = MakeLabel(Factions.Color(Factions.Red), HorizontalAlignment.Center);
        objectiveLabel.AddThemeFontSizeOverride("font_size", 14);
        objectiveLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        objectiveLabel.AddThemeConstantOverride("outline_size", 3);
        AddChild(objectiveLabel);
        Resized += LayoutBar;
        LayoutBar();
        Reset();
    }

    // =========================================================
    private static Label MakeLabel(Color tint, HorizontalAlignment alignment)
    {
        Label label = new()
        {
            HorizontalAlignment = alignment,
            MouseFilter = MouseFilterEnum.Ignore,
            ClipText = true
        };
        label.AddThemeColorOverride("font_color", tint);
        label.AddThemeFontSizeOverride("font_size", 12);
        return label;
    }

    // =========================================================
    // The outer margins leave room for the benchmark's left/right debug panels.
    private void LayoutBar()
    {
        float usable = Mathf.Max(130f, Size.X - 2f * 435f);
        barWidth = Mathf.Min(MaximumWidth, Mathf.Min(usable, Mathf.Max(0f, Size.X - 24f)));
        barX = (Size.X - barWidth) * 0.5f;
        int visibleCount = 0;
        foreach (byte faction in Factions.DisplayOrder)
        {
            labels[faction].Visible = previousCounts[faction] > 0 || Factions.ShowWhenEmpty(faction);
            if (labels[faction].Visible) visibleCount++;
        }
        float labelWidth = Mathf.Min(Mathf.Max(barWidth, 220f), Mathf.Max(0f, Size.X - 24f));
        float labelX = (Size.X - labelWidth) * 0.5f;
        int columns = Mathf.Clamp((int)(labelWidth / 100f), 1, Mathf.Max(1, visibleCount));
        float columnWidth = labelWidth / columns;
        int slot = 0;
        foreach (byte faction in Factions.DisplayOrder)
        {
            Label label = labels[faction];
            if (!label.Visible) continue;
            label.Position = new Vector2(labelX + columnWidth * (slot % columns),
                TopOffset + BarThickness + 3f + 18f * (slot / columns));
            label.Size = new Vector2(columnWidth, 18f);
            slot++;
        }
        int rows = Mathf.CeilToInt(visibleCount / (float)columns);
        objectiveLabel.Position = new Vector2(labelX, TopOffset + BarThickness + 3f + 18f * rows + 8f);
        objectiveLabel.Size = new Vector2(labelWidth, 20f);
        objectiveY = objectiveLabel.Position.Y + 22f;
        QueueRedraw();
    }

    // =========================================================
    public void BeginBattle(UnitState state) => BeginBattle(state, null);

    // Citadel encounters add the objective’s health to the army strip.
    internal void BeginBattle(UnitState state, StructureHealth citadel)
    {
        Reset();
        source = state;
        objective = citadel;
        if (source == null || (source.Count <= 0 && objective == null)) return;
        RefreshCounts();
        Visible = true;
    }

    // =========================================================
    public void Reset()
    {
        source = null;
        objective = null;
        previousObjectiveHealth = -1f;
        objectiveFraction = 0f;
        objectiveLabel?.Hide();
        Array.Fill(previousCounts, -1);
        Array.Clear(shares);
        Array.Clear(targets);
        hasInitialShares = false;
        Visible = false;
        SetProcess(false);
    }

    // =========================================================
    // Invoked by the owning arena at its existing HUD refresh cadence (~0.3s).
    // UnitState already maintains death-adjusted totals, so this scales with the faction count.
    public void RefreshCounts()
    {
        if (source == null) return;
        RefreshObjective();
        bool changed = false;
        int alive = 0;
        for (int faction = 0; faction < Factions.Count; faction++)
        {
            int count = Math.Max(0, source.FactionAlive[faction]);
            alive += count;
            if (previousCounts[faction] == count) continue;
            previousCounts[faction] = count;
            labels[faction].Text = $"{Factions.Name(faction).ToUpperInvariant()} {count:N0}";
            changed = true;
        }
        if (!changed) return;
        float denominator = alive > 0 ? 1f / alive : 0f;
        for (int faction = 0; faction < Factions.Count; faction++)
            targets[faction] = previousCounts[faction] * denominator;
        LayoutBar();

        if (!hasInitialShares)
        {
            targets.CopyTo(shares, 0);
            hasInitialShares = true;
            return;
        }
        SetProcess(true);
    }

    // One direct HP read at the existing HUD cadence; no structure or soldier search.
    private void RefreshObjective()
    {
        if (objective == null || previousObjectiveHealth == objective.Health) return;
        previousObjectiveHealth = objective.Health;
        objectiveFraction = Mathf.Clamp(objective.Health / objective.MaxHealth, 0f, 1f);
        string name = objective.Faction == Factions.Blue ? "BLUE CITADEL" : "ENEMY CITADEL";
        objectiveLabel.AddThemeColorOverride("font_color", Factions.Color(objective.Faction));
        objectiveLabel.Text = objective.Destroyed ? $"{name} · DESTROYED" :
            $"{name}  {Mathf.CeilToInt(objective.Health):N0} / {Mathf.CeilToInt(objective.MaxHealth):N0} HP";
        objectiveLabel.Show();
        QueueRedraw();
    }

    // =========================================================
    // Animate the faction shares and stop processing completely once they settle.
    public override void _Process(double delta)
    {
        float step = 1f - Mathf.Exp(-AnimationSpeed * Mathf.Min((float)delta, 0.25f));
        bool settled = true;
        for (int faction = 0; faction < Factions.Count; faction++)
        {
            shares[faction] = Mathf.Lerp(shares[faction], targets[faction], step);
            settled &= Mathf.Abs(shares[faction] - targets[faction]) < 0.0005f;
        }
        if (settled)
        {
            targets.CopyTo(shares, 0);
            SetProcess(false);
        }
        QueueRedraw();
    }

    // =========================================================
    public override void _Draw()
    {
        if (barWidth <= 0f) return;
        var border = new Rect2(barX, TopOffset, barWidth, BarThickness);
        DrawRect(border, Backing);
        float x = barX + 1f, usable = Mathf.Max(0f, barWidth - 2f);
        foreach (byte faction in Factions.DisplayOrder)
        {
            float width = usable * shares[faction];
            if (width <= 0f) continue;
            DrawRect(new Rect2(x, TopOffset + 1f, width, BarThickness - 2f), Factions.Color(faction));
            x += width;
        }
        DrawRect(border, Outline, filled: false, width: 1f);
        if (objective == null) return;
        var citadelBar = new Rect2(barX, objectiveY, barWidth, BarThickness);
        DrawRect(citadelBar, Backing);
        if (objectiveFraction > 0f)
            DrawRect(new Rect2(barX + 1f, objectiveY + 1f, usable * objectiveFraction, BarThickness - 2f), Factions.Color(objective.Faction));
        DrawRect(citadelBar, Outline, filled: false, width: 1f);
    }
}
