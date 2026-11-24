// Displays the benchmark's soldier and defense brushes as selectable silhouette cards.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal partial class UnitPlacementBar : PanelContainer
{
    internal int BarHeight { get; private set; }
    internal event Action<byte> UnitSelected, FactionSelected;
    private readonly List<UnitSilhouette> silhouettes = new();
    internal event Action<int> DefenseSelected;
    private Label heading;
    private Control unitCards, defenseCards;

    internal void ShowDefenses(bool enabled)
    {
        heading.Text = enabled ? "DEFENSE PLACEMENT" : "UNIT PLACEMENT";
        unitCards.Visible = !enabled;
        defenseCards.Visible = enabled;
    }

    internal void Build(RuntimeUnitCatalog catalog, RuntimeBuildingCatalog buildings, bool compact)
    {
        BarHeight = compact ? 148 : 154;
        AnchorTop = AnchorBottom = AnchorRight = 1;
        OffsetLeft = 12;
        OffsetRight = -12;
        OffsetTop = -BarHeight - 12;
        OffsetBottom = -12;
        AddThemeStyleboxOverride("panel", UIButtonFactory.Box(new Color("#091a2d"), new Color("#327fa3"), compact ? 6 : 10));
        var body = new VBoxContainer();
        AddChild(body);
        heading = new Label { Text = "UNIT PLACEMENT" };
        heading.AddThemeFontSizeOverride("font_size", 14);
        heading.AddThemeColorOverride("font_color", new Color("#bde9ff"));
        var header = new HBoxContainer();
        header.AddChild(heading);
        if (!compact)
        {
            var group = new ButtonGroup();
            foreach (byte faction in Factions.DisplayOrder)
            {
                byte selectedFaction = faction;
                Color color = Factions.Color(faction);
                string name = Factions.Name(faction);
                var swatch = new Button { ToggleMode = true, ButtonGroup = group,
                    ButtonPressed = faction == Factions.Blue, CustomMinimumSize = new Vector2(27, 22),
                    TooltipText = name, AccessibilityName = $"Paint {name}" };
                var selected = UIButtonFactory.Box(color, Colors.White, 1);
                selected.SetBorderWidthAll(2);
                var focus = UIButtonFactory.Box(Colors.Transparent, Colors.White, 1);
                focus.DrawCenter = false;
                swatch.AddThemeStyleboxOverride("normal", UIButtonFactory.Box(color.Darkened(0.3f), color, 1));
                swatch.AddThemeStyleboxOverride("hover", UIButtonFactory.Box(color.Lightened(0.18f), Colors.White, 1));
                swatch.AddThemeStyleboxOverride("pressed", selected);
                swatch.AddThemeStyleboxOverride("hover_pressed", selected);
                swatch.AddThemeStyleboxOverride("focus", focus);
                swatch.Pressed += () =>
                {
                    foreach (var icon in silhouettes) icon.SetColor(color);
                    FactionSelected?.Invoke(selectedFaction);
                };
                header.AddChild(swatch);
            }
        }
        body.AddChild(header);
        unitCards = BuildCards(body, catalog.Count, (catalog.Count + 1) / 2, i => catalog[i].Name,
            i => Stats(catalog[i].Health, catalog[i].Damage),
            (icon, i) => icon.SetOutline(catalog[i].Outline), i => UnitSelected?.Invoke((byte)i));
        int defenseCount = buildings?.Count ?? 0;
        defenseCards = BuildCards(body, defenseCount, defenseCount, i => buildings[i].Name,
            i => Stats(buildings[i].Health, buildings[i].AttacksSoldiers ? buildings[i].Damage : 0f),
            (icon, i) => icon.SetDefense(buildings[i]), i => DefenseSelected?.Invoke(i));
        ShowDefenses(false);
    }

    private static string Stats(float hp, float damage) => $"HP {hp:0.#}   DMG {damage:0.#}";

    private Control BuildCards(VBoxContainer body, int count, int columns, Func<int, string> nameFor,
        Func<int, string> statsFor, Action<UnitSilhouette, int> setIcon, Action<int> select)
    {
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        body.AddChild(scroll);
        var cards = new GridContainer { Columns = Math.Max(1, columns), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        cards.AddThemeConstantOverride("h_separation", 5);
        cards.AddThemeConstantOverride("v_separation", 4);
        scroll.AddChild(cards);

        var group = new ButtonGroup();
        var normal = UIButtonFactory.Box(new Color("#122d4a"), new Color("#326582"), 3);
        var hover = UIButtonFactory.Box(new Color("#174161"), new Color("#75cbe9"), 3);
        var pressed = UIButtonFactory.Box(new Color("#14506a"), new Color("#56e5ff"), 3);
        var focus = UIButtonFactory.Box(Colors.Transparent, new Color("#c4f3ff"), 3);
        focus.DrawCenter = false;
        for (int i = 0; i < count; i++)
        {
            int type = i;
            string title = nameFor(type);
            string stats = statsFor(type);
            var card = new Button { ToggleMode = true, ButtonGroup = group, ButtonPressed = i == 0,
                CustomMinimumSize = new Vector2(144, 46), SizeFlagsHorizontal = SizeFlags.ExpandFill,
                TooltipText = $"{title}\n{stats} (base stats)",
                AccessibilityName = title };
            card.AddThemeStyleboxOverride("normal", normal);
            card.AddThemeStyleboxOverride("hover", hover);
            card.AddThemeStyleboxOverride("pressed", pressed);
            card.AddThemeStyleboxOverride("hover_pressed", pressed);
            card.AddThemeStyleboxOverride("focus", focus);
            cards.AddChild(card);
            var face = new HBoxContainer();
            face.MouseFilter = MouseFilterEnum.Ignore;
            face.AddThemeConstantOverride("separation", 5);
            card.AddChild(face);
            face.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            face.OffsetTop = 2;
            face.OffsetLeft = 5;
            face.OffsetRight = -5;
            face.OffsetBottom = -2;
            var icon = new UnitSilhouette { MouseFilter = MouseFilterEnum.Ignore };
            setIcon(icon, type);
            silhouettes.Add(icon);
            face.AddChild(icon);
            var details = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill,
                Alignment = BoxContainer.AlignmentMode.Center };
            details.AddThemeConstantOverride("separation", 0);
            face.AddChild(details);
            var name = new Label { Text = title,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart, MaxLinesVisible = 2,
                MouseFilter = MouseFilterEnum.Ignore };
            name.AddThemeFontSizeOverride("font_size", 12);
            name.AddThemeColorOverride("font_color", new Color("#d5efff"));
            details.AddChild(name);
            var values = new Label { Text = stats, MouseFilter = MouseFilterEnum.Ignore };
            values.AddThemeFontSizeOverride("font_size", 10);
            values.AddThemeColorOverride("font_color", new Color("#b3cde0"));
            details.AddChild(values);
            card.Pressed += () => select(type);
        }
        return scroll;
    }

    private partial class UnitSilhouette : Control
    {
        private Vector2[] points = Array.Empty<Vector2>();

        private BuildingStats? defense;
        private Color tint = Factions.Color(Factions.Blue);

        internal void SetColor(Color color)
        {
            tint = color;
            QueueRedraw();
        }

        internal void SetDefense(BuildingStats stats)
        {
            defense = stats;
            CustomMinimumSize = new Vector2(28, 0);
        }

        internal void SetOutline(UnitOutline outline)
        {
            CustomMinimumSize = new Vector2(28, 0);
            points = new Vector2[outline.Sides];
            float extent = 0.001f;
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = outline.Vertex(i);
                extent = Mathf.Max(extent, Mathf.Max(Mathf.Abs(points[i].X), Mathf.Abs(points[i].Y)));
            }
            for (int i = 0; i < points.Length; i++) points[i] *= 11f / extent;
            QueueRedraw();
        }

        public override void _Draw()
        {
            Color edge = tint.Lerp(Colors.White, 0.58f);
            if (defense is BuildingStats stats)
            {
                Vector2 center = Size * 0.5f;
                var baseRect = new Rect2(center - new Vector2(12, 5), new Vector2(24, 17));
                DrawRect(baseRect, tint);
                DrawRect(baseRect, edge, false, 1.6f);
                if (stats.ShieldEmitter.Enabled)
                    DrawArc(center, 12, Mathf.Pi, Mathf.Tau, 16, edge, 2f, true);
                else if (stats.Weapon == TowerWeapon.Projectile)
                {
                    DrawLine(center + new Vector2(-4, 3), center + new Vector2(-4, -17), edge, 4);
                    DrawLine(center + new Vector2(4, 3), center + new Vector2(4, -17), edge, 4);
                }
                else DrawLine(center, center + new Vector2(0, -17), edge, 5);
                return;
            }
            if (points.Length < 3) return;
            var vertices = new Vector2[points.Length];
            for (int i = 0; i < vertices.Length; i++) vertices[i] = points[i] + Size * 0.5f;
            DrawColoredPolygon(vertices, tint);
            for (int i = 0; i < vertices.Length; i++)
                DrawLine(vertices[i], vertices[(i + 1) % vertices.Length], edge, 1.6f, true);
        }
    }
}
