// Displays the benchmark's soldier and defense brushes as selectable silhouette cards.
using System;
using Godot;

namespace MassiveRts;

internal partial class UnitPlacementBar : PanelContainer
{
    internal int BarHeight { get; private set; }
    internal event Action<byte> UnitSelected;
    internal event Action<int> DefenseSelected;
    private Label heading;
    private Control unitCards, defenseCards;

    internal void ShowDefenses(bool enabled)
    {
        heading.Text = enabled ? "DEFENSE PLACEMENT" : "UNIT PLACEMENT";
        unitCards.Visible = !enabled;
        defenseCards.Visible = enabled;
    }

    internal void Build(RuntimeUnitCatalog catalog, RuntimeBuildingCatalog buildings, OptionButton faction, bool compact)
    {
        BarHeight = compact ? 118 : 150;
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
            faction.AccessibilityName = "Paint faction";
            faction.TooltipText = "Paint faction";
            faction.CustomMinimumSize = new Vector2(104, 0);
        }
        faction.Visible = !compact;
        header.AddChild(faction);
        body.AddChild(header);
        unitCards = BuildCards(body, catalog.Count, compact, i => catalog[i].Name,
            (icon, i) => icon.SetOutline(catalog[i].Outline, compact), i => UnitSelected?.Invoke((byte)i));
        defenseCards = BuildCards(body, buildings?.Count ?? 0, false, i => buildings[i].Name,
            (icon, i) => icon.SetDefense(buildings[i]), i => DefenseSelected?.Invoke(i));
        ShowDefenses(false);
    }

    private static Control BuildCards(VBoxContainer body, int count, bool compact, Func<int, string> nameFor,
        Action<UnitSilhouette, int> setIcon, Action<int> select)
    {
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        body.AddChild(scroll);
        Container cards = compact
            ? new GridContainer { Columns = Math.Max(1, (count + 1) / 2) }
            : new HBoxContainer();
        cards.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        if (compact)
        {
            cards.AddThemeConstantOverride("h_separation", 5);
            cards.AddThemeConstantOverride("v_separation", 4);
        }
        else cards.AddThemeConstantOverride("separation", 6);
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
            var card = new Button { ToggleMode = true, ButtonGroup = group, ButtonPressed = i == 0,
                CustomMinimumSize = compact ? new Vector2(134, 36) : new Vector2(104, 82), SizeFlagsHorizontal = SizeFlags.ExpandFill,
                TooltipText = title,
                AccessibilityName = title };
            card.AddThemeStyleboxOverride("normal", normal);
            card.AddThemeStyleboxOverride("hover", hover);
            card.AddThemeStyleboxOverride("pressed", pressed);
            card.AddThemeStyleboxOverride("hover_pressed", pressed);
            card.AddThemeStyleboxOverride("focus", focus);
            cards.AddChild(card);
            BoxContainer face = compact ? new HBoxContainer() : new VBoxContainer();
            face.MouseFilter = MouseFilterEnum.Ignore;
            face.AddThemeConstantOverride("separation", compact ? 5 : 0);
            card.AddChild(face);
            face.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            face.OffsetTop = compact ? 2 : 4;
            face.OffsetLeft = 5;
            face.OffsetRight = -5;
            face.OffsetBottom = compact ? -2 : -4;
            var icon = new UnitSilhouette { MouseFilter = MouseFilterEnum.Ignore };
            setIcon(icon, type);
            face.AddChild(icon);
            var name = new Label { Text = title,
                HorizontalAlignment = compact ? HorizontalAlignment.Left : HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                SizeFlagsHorizontal = compact ? SizeFlags.ExpandFill : SizeFlags.Fill,
                AutowrapMode = TextServer.AutowrapMode.WordSmart, MaxLinesVisible = 2,
                MouseFilter = MouseFilterEnum.Ignore };
            name.AddThemeFontSizeOverride("font_size", 12);
            name.AddThemeColorOverride("font_color", new Color("#d5efff"));
            face.AddChild(name);
            card.Pressed += () => select(type);
        }
        return scroll;
    }

    private partial class UnitSilhouette : Control
    {
        private Vector2[] points = Array.Empty<Vector2>();

        private BuildingStats? defense;

        internal void SetDefense(BuildingStats stats)
        {
            defense = stats;
            CustomMinimumSize = new Vector2(0, 43);
        }

        internal void SetOutline(UnitOutline outline, bool compact)
        {
            CustomMinimumSize = compact ? new Vector2(26, 0) : new Vector2(0, 43);
            points = new Vector2[outline.Sides];
            float extent = 0.001f;
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = outline.Vertex(i);
                extent = Mathf.Max(extent, Mathf.Max(Mathf.Abs(points[i].X), Mathf.Abs(points[i].Y)));
            }
            for (int i = 0; i < points.Length; i++) points[i] *= (compact ? 10f : 17f) / extent;
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (defense is BuildingStats stats)
            {
                Vector2 center = Size * 0.5f;
                var baseRect = new Rect2(center - new Vector2(15, 7), new Vector2(30, 22));
                DrawRect(baseRect, new Color("#329bde"));
                DrawRect(baseRect, new Color("#b3efff"), false, 1.6f);
                if (stats.ShieldEmitter.Enabled)
                    DrawArc(center, 12, Mathf.Pi, Mathf.Tau, 16, new Color("#b3efff"), 2f, true);
                else if (stats.Weapon == TowerWeapon.Projectile)
                {
                    DrawLine(center + new Vector2(-4, 3), center + new Vector2(-4, -17), new Color("#b3efff"), 4);
                    DrawLine(center + new Vector2(4, 3), center + new Vector2(4, -17), new Color("#b3efff"), 4);
                }
                else DrawLine(center, center + new Vector2(0, -17), new Color("#b3efff"), 5);
                return;
            }
            if (points.Length < 3) return;
            var vertices = new Vector2[points.Length];
            for (int i = 0; i < vertices.Length; i++) vertices[i] = points[i] + Size * 0.5f;
            DrawColoredPolygon(vertices, new Color("#329bde"));
            for (int i = 0; i < vertices.Length; i++)
                DrawLine(vertices[i], vertices[(i + 1) % vertices.Length], new Color("#b3efff"), 1.6f, true);
        }
    }
}
