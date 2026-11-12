// Draws the actual compiled unit silhouette and combat overlays on editor changes.
#if TOOLS
using System;
using Godot;

namespace MassiveRts;

[Tool]
public partial class UnitEditorPreview : Control
{
    private static readonly Color Body = new(0.32f, 0.60f, 0.85f);
    private static readonly Color Footprint = new(0.83f, 0.89f, 0.96f, 0.85f);
    private static readonly Color Cleave = new(0.99f, 0.70f, 0.36f, 0.92f);
    private static readonly Color Wave = new(0.73f, 0.62f, 0.92f, 0.9f);
    private UnitDefinition unit;
    public bool ShowFootprint { get; set; } = true;
    public bool ShowCleave { get; set; } = true;
    public bool ShowShockwave { get; set; } = true;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(0, 240);
        SizeFlagsVertical = SizeFlags.ExpandFill;
        Resized += QueueRedraw;
    }

    public void SetUnit(UnitDefinition value)
    {
        unit = value;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.10f, 0.12f, 0.15f));
        if (unit == null || !Enum.IsDefined(unit.Shape)) return;

        float scale = UnitOutline.EffectiveSize(unit);
        UnitOutline outline = UnitOutline.FromDefinition(unit, scale);
        bool melee = unit.Style == AttackStyle.Melee;
        bool drawCleave = ShowCleave && melee && unit.CleaveEnabled;
        bool drawWave = ShowShockwave && melee && unit.ShockwaveEveryAttacks > 0;
        float authoredRadius = float.IsFinite(unit.ShockwaveBaseRadius) && unit.ShockwaveBaseRadius > 0f ? unit.ShockwaveBaseRadius : 4f;
        float waveRadius = drawWave ? Mathf.Clamp(authoredRadius, 0.1f, 64f) * scale : 0f;
        float coneRadius = outline.Footprint * 1.55f;
        float extent = Mathf.Max(Mathf.Max(waveRadius, drawCleave ? coneRadius : 0f), outline.Footprint * 1.3f);
        float pixels = Mathf.Max(1f, Mathf.Min(Size.X, Size.Y) * 0.43f) / extent;
        Vector2 origin = Size * 0.5f;
        float forward = unit.Shape == UnitShape.Diamond ? 0f : -Mathf.Pi * 0.5f;
        var polygon = new Vector2[outline.Sides];
        for (int i = 0; i < polygon.Length; i++) polygon[i] = origin + outline.Vertex(i) * pixels;
        DrawColoredPolygon(polygon, Body);
        if (outline.IsChevron)
        {
            var barrel = new Rect2(origin + new Vector2(-outline.BarrelWidth * 0.5f, -outline.BarrelFront) * pixels,
                new Vector2(outline.BarrelWidth, outline.BarrelFront - outline.BarrelBack) * pixels);
            DrawRect(barrel, Body.Lightened(0.15f));
        }
        if (ShowFootprint) DrawArc(origin, outline.Footprint * pixels, 0f, Mathf.Tau, 64, Footprint, 1f, true);
        if (drawWave) DrawArc(origin, waveRadius * pixels, 0f, Mathf.Tau, 96, Wave, 1.5f, true);
        if (drawCleave)
        {
            float angle = float.IsFinite(unit.CleaveAngleDegrees) ? Mathf.Clamp(unit.CleaveAngleDegrees, 1f, 180f) : 100f;
            float half = Mathf.DegToRad(angle * 0.5f);
            DrawArc(origin, coneRadius * pixels, forward - half, forward + half, 32, Cleave, 1.5f, true);
            DrawLine(origin, origin + Vector2.FromAngle(forward - half) * coneRadius * pixels, Cleave, 1f, true);
            DrawLine(origin, origin + Vector2.FromAngle(forward + half) * coneRadius * pixels, Cleave, 1f, true);
        }

        if (outline.IsChevron) return;
        Vector2 heading = Vector2.FromAngle(forward);
        Vector2 tip = origin + heading * outline.Footprint * pixels * 1.16f;
        Vector2 tail = tip - heading * 7f;
        Vector2 wing = heading.Orthogonal() * 4f;
        DrawLine(origin, tip, Colors.White, 1.5f, true);
        DrawLine(tip, tail + wing, Colors.White, 1.5f, true);
        DrawLine(tip, tail - wing, Colors.White, 1.5f, true);
    }
}
#endif
