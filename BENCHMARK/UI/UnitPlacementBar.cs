// Displays the benchmark's soldier brushes as selectable silhouette cards.
using System;
using Godot;

namespace MassiveRts;

internal partial class UnitPlacementBar : PanelContainer
{
    internal const int Height = 142;
    internal event Action<byte> UnitSelected;

    internal void Build(RuntimeUnitCatalog catalog)
    {
        AnchorTop = AnchorBottom = AnchorRight = 1;
        OffsetLeft = 12;
        OffsetRight = -12;
        OffsetTop = -Height - 12;
        OffsetBottom = -12;
        AddThemeStyleboxOverride("panel", UIButtonFactory.Box(new Color("#091a2d"), new Color("#327fa3"), 10));
        var body = new VBoxContainer();
        AddChild(body);
        var heading = new Label { Text = "UNIT PLACEMENT" };
        heading.AddThemeFontSizeOverride("font_size", 14);
        heading.AddThemeColorOverride("font_color", new Color("#bde9ff"));
        body.AddChild(heading);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        body.AddChild(scroll);
        var cards = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        cards.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(cards);

        var group = new ButtonGroup();
        var normal = UIButtonFactory.Box(new Color("#122d4a"), new Color("#326582"), 3);
        var hover = UIButtonFactory.Box(new Color("#174161"), new Color("#75cbe9"), 3);
        var pressed = UIButtonFactory.Box(new Color("#14506a"), new Color("#56e5ff"), 3);
        var focus = UIButtonFactory.Box(Colors.Transparent, new Color("#c4f3ff"), 3);
        focus.DrawCenter = false;
        for (int i = 0; i < catalog.Count; i++)
        {
            byte type = (byte)i;
            ref readonly var unit = ref catalog[type];
            var card = new Button { ToggleMode = true, ButtonGroup = group, ButtonPressed = i == 0,
                CustomMinimumSize = new Vector2(104, 82), SizeFlagsHorizontal = SizeFlags.ExpandFill,
                TooltipText = unit.Name,
                AccessibilityName = unit.Name };
            card.AddThemeStyleboxOverride("normal", normal);
            card.AddThemeStyleboxOverride("hover", hover);
            card.AddThemeStyleboxOverride("pressed", pressed);
            card.AddThemeStyleboxOverride("hover_pressed", pressed);
            card.AddThemeStyleboxOverride("focus", focus);
            cards.AddChild(card);
            var face = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            face.AddThemeConstantOverride("separation", 0);
            card.AddChild(face);
            face.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            face.OffsetTop = 4;
            face.OffsetLeft = 5;
            face.OffsetRight = -5;
            face.OffsetBottom = -4;
            var icon = new UnitSilhouette { MouseFilter = MouseFilterEnum.Ignore };
            icon.SetOutline(unit.Outline);
            face.AddChild(icon);
            var name = new Label { Text = unit.Name, HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart, MaxLinesVisible = 2,
                MouseFilter = MouseFilterEnum.Ignore };
            name.AddThemeFontSizeOverride("font_size", 12);
            name.AddThemeColorOverride("font_color", new Color("#d5efff"));
            face.AddChild(name);
            card.Pressed += () => UnitSelected?.Invoke(type);
        }
    }

    private partial class UnitSilhouette : Control
    {
        private Vector2[] points = Array.Empty<Vector2>();

        internal void SetOutline(UnitOutline outline)
        {
            CustomMinimumSize = new Vector2(0, 43);
            points = new Vector2[outline.Sides];
            float extent = 0.001f;
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = outline.Vertex(i);
                extent = Mathf.Max(extent, Mathf.Max(Mathf.Abs(points[i].X), Mathf.Abs(points[i].Y)));
            }
            for (int i = 0; i < points.Length; i++) points[i] *= 17f / extent;
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (points.Length < 3) return;
            var vertices = new Vector2[points.Length];
            for (int i = 0; i < vertices.Length; i++) vertices[i] = points[i] + Size * 0.5f;
            DrawColoredPolygon(vertices, new Color("#329bde"));
            for (int i = 0; i < vertices.Length; i++)
                DrawLine(vertices[i], vertices[(i + 1) % vertices.Length], new Color("#b3efff"), 1.6f, true);
        }
    }
}
