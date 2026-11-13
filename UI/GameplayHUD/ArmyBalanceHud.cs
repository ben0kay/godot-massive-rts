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
    private static readonly Color Blue = ArmyRenderer.FactionColor(Factions.Blue);
    private static readonly Color Red = ArmyRenderer.FactionColor(Factions.Red);
    private static readonly Color Green = ArmyRenderer.FactionColor(Factions.Green);

    private readonly Label blueCount = MakeLabel(Blue, HorizontalAlignment.Right);
    private readonly Label redCount = MakeLabel(Red, HorizontalAlignment.Left);
    private readonly Label greenCount = MakeLabel(Green, HorizontalAlignment.Center);
    private UnitState source;
    private int previousBlue = -1, previousRed = -1, previousGreen = -1;
    private float blueShare, redShare, greenShare;
    private float targetBlue, targetRed, targetGreen;
    private float barX, barWidth;
    private bool hasInitialShares;

    // =========================================================
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddChild(blueCount);
        AddChild(redCount);
        AddChild(greenCount);
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
            CustomMinimumSize = new Vector2(100f, 18f)
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
        blueCount.Position = new Vector2(barX - 108f, TopOffset - 3f);
        blueCount.Size = new Vector2(100f, 19f);
        redCount.Position = new Vector2(barX + barWidth + 8f, TopOffset - 3f);
        redCount.Size = new Vector2(100f, 19f);
        greenCount.Position = new Vector2((Size.X - 110f) * 0.5f, TopOffset + BarThickness + 3f);
        greenCount.Size = new Vector2(110f, 19f);
        // For narrow windows, retain the graphic without overlapping the side panels.
        blueCount.Visible = redCount.Visible = Size.X > 1000f;
        QueueRedraw();
    }

    // =========================================================
    public void BeginBattle(UnitState state)
    {
        Reset();
        source = state;
        if (source == null || source.Count <= 0) return;
        RefreshCounts();
        Visible = true;
    }

    // =========================================================
    public void Reset()
    {
        source = null;
        previousBlue = previousRed = previousGreen = -1;
        blueShare = redShare = greenShare = 0f;
        targetBlue = targetRed = targetGreen = 0f;
        hasInitialShares = false;
        Visible = false;
        SetProcess(false);
    }

    // =========================================================
    // Invoked by the owning arena at its existing HUD refresh cadence (~0.3s).
    // UnitState already maintains death-adjusted totals, so this is O(1).
    public void RefreshCounts()
    {
        if (source == null) return;
        int blue = Math.Max(0, source.FactionAlive[Factions.Blue]);
        int red = Math.Max(0, source.FactionAlive[Factions.Red]);
        int green = Math.Max(0, source.FactionAlive[Factions.Green]);
        if (blue == previousBlue && red == previousRed && green == previousGreen) return;
        previousBlue = blue; previousRed = red; previousGreen = green;
        int alive = blue + red + green;
        float denominator = alive > 0 ? 1f / alive : 0f;
        targetBlue = blue * denominator;
        targetGreen = green * denominator;
        targetRed = red * denominator;
        blueCount.Text = $"BLUE {blue:N0}";
        redCount.Text = $"RED {red:N0}";
        greenCount.Text = $"GREEN {green:N0}";
        greenCount.Visible = green > 0;

        if (!hasInitialShares)
        {
            blueShare = targetBlue;
            redShare = targetRed;
            greenShare = targetGreen;
            hasInitialShares = true;
            QueueRedraw();
            return;
        }
        SetProcess(true);
    }

    // =========================================================
    // Animate just three floats; stop processing completely once they settle.
    public override void _Process(double delta)
    {
        float step = 1f - Mathf.Exp(-AnimationSpeed * Mathf.Min((float)delta, 0.25f));
        blueShare = Mathf.Lerp(blueShare, targetBlue, step);
        redShare = Mathf.Lerp(redShare, targetRed, step);
        greenShare = Mathf.Lerp(greenShare, targetGreen, step);
        if (Mathf.Abs(blueShare - targetBlue) < 0.0005f &&
            Mathf.Abs(redShare - targetRed) < 0.0005f &&
            Mathf.Abs(greenShare - targetGreen) < 0.0005f)
        {
            blueShare = targetBlue;
            redShare = targetRed;
            greenShare = targetGreen;
            SetProcess(false);
        }
        QueueRedraw();
    }

    // =========================================================
    // Order: Blue at left, optional Green in the middle, Red at right.
    public override void _Draw()
    {
        if (barWidth <= 0f) return;
        var border = new Rect2(barX, TopOffset, barWidth, BarThickness);
        DrawRect(border, Backing);
        float x = barX + 1f, usable = Mathf.Max(0f, barWidth - 2f);
        float blueWidth = usable * blueShare;
        float greenWidth = usable * greenShare;
        float redWidth = usable * redShare;
        if (blueWidth > 0f) DrawRect(new Rect2(x, TopOffset + 1f, blueWidth, BarThickness - 2f), Blue);
        x += blueWidth;
        if (greenWidth > 0f) DrawRect(new Rect2(x, TopOffset + 1f, greenWidth, BarThickness - 2f), Green);
        x += greenWidth;
        if (redWidth > 0f) DrawRect(new Rect2(x, TopOffset + 1f, redWidth, BarThickness - 2f), Red);
        DrawRect(border, Outline, filled: false, width: 1f);
    }
}
