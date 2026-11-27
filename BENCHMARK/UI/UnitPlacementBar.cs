// Displays shared placement cards for the Benchmark and AutoBattler Level Editor.
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
    internal event Action<UnitDefinition> AircraftSelected;
    private Label heading;
    private Control unitCards, defenseCards;

    internal void ShowDefenses(bool enabled)
    {
        heading.Text = enabled ? "DEFENSE PLACEMENT" : "UNIT PLACEMENT";
        unitCards.Visible = !enabled;
        defenseCards.Visible = enabled;
    }

    internal void Build(RuntimeUnitCatalog catalog, RuntimeBuildingCatalog buildings, bool compact, Control brushControls, UnitDefinition[] aircraft = null)
    {
        var shell = BuildShell(compact ? 148 : 154, compact);
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 12);
        shell.AddChild(columns);
        columns.AddChild(brushControls);
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        columns.AddChild(body);
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
        aircraft ??= Array.Empty<UnitDefinition>();
        int count = catalog.Count + aircraft.Length;
        unitCards = BuildCards(body, count, (count + 1) / 2,
            i => i < catalog.Count ? catalog[i].Name : aircraft[i - catalog.Count].DisplayName,
            i => i < catalog.Count ? Stats(catalog[i].Health, catalog[i].Damage) :
                Stats(aircraft[i - catalog.Count].Health, aircraft[i - catalog.Count].Damage),
            (icon, i) =>
            {
                if (i < catalog.Count) icon.SetOutline(catalog[i].Outline);
                else
                {
                    var unit = aircraft[i - catalog.Count];
                    icon.SetAircraft(unit);
                }
            }, i =>
            {
                if (i < catalog.Count) UnitSelected?.Invoke((byte)i);
                else AircraftSelected?.Invoke(aircraft[i - catalog.Count]);
            });
        int defenseCount = buildings?.Count ?? 0;
        defenseCards = BuildCards(body, defenseCount, defenseCount, i => buildings[i].Name,
            i => Stats(buildings[i].Health, (buildings[i].AttacksSoldiers || buildings[i].AirDefense.Enabled) ? buildings[i].Damage : 0f),
            (icon, i) => icon.SetDefense(buildings[i]), i => DefenseSelected?.Invoke(i));
        ShowDefenses(false);
    }

    internal VBoxContainer BuildShell(int height, bool compact = false)
    {
        BarHeight = height;
        AnchorTop = AnchorBottom = AnchorRight = 1;
        OffsetLeft = 12;
        OffsetRight = -12;
        OffsetTop = -BarHeight - 12;
        OffsetBottom = -12;
        AddThemeStyleboxOverride("panel", UIButtonFactory.Box(new Color("#091a2d"), new Color("#327fa3"), compact ? 6 : 10));
        var body = new VBoxContainer();
        AddChild(body);
        return body;
    }

    internal static HBoxContainer AddBrushSettings(VBoxContainer brushControls, Action<int> brush,
        Action<float> densityChanged, float density = 1f)
    {
        var brushRow = new HBoxContainer();
        brushControls.AddChild(brushRow);
        brushRow.AddChild(new Label { Text = "Brush" });
        var brushes = new ButtonGroup();
        foreach (int size in new[] { 1, 3, 6, 10 })
        {
            var button = UIButtonFactory.Create($"{size}×{size}", () => brush(size), UIButtonStyle.Compact);
            button.ToggleMode = true;
            button.ButtonGroup = brushes;
            if (size == 1) button.SetPressedNoSignal(true);
            brushRow.AddChild(button);
        }
        var densityRow = new HBoxContainer();
        brushControls.AddChild(densityRow);
        densityRow.AddThemeConstantOverride("separation", 8);
        densityRow.AddChild(new Label { Text = "Density", VerticalAlignment = VerticalAlignment.Center });
        var spread = new HSlider { MinValue = 0, MaxValue = 3, Step = 0.1, Value = density,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        densityRow.AddChild(spread);
        var valueLabel = new Label
        {
            Text = density == 0 ? "Touching" : $"{density:F1}×",
            CustomMinimumSize = new Vector2(68, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        densityRow.AddChild(valueLabel);
        spread.ValueChanged += value =>
        {
            valueLabel.Text = value == 0 ? "Touching" : $"{value:F1}×";
            densityChanged((float)value);
        };
        return brushRow;
    }

    internal static string Stats(float hp, float damage) => $"HP {hp:0.#}   DMG {damage:0.#}";

    internal Control BuildCards(VBoxContainer body, int count, int columns, Func<int, string> nameFor,
        Func<int, string> statsFor, Action<UnitSilhouette, int> setIcon, Action<int> select,
        ButtonGroup group = null, bool selectFirst = true, Action<Button, int> configureCard = null)
    {
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        body.AddChild(scroll);
        var cards = new GridContainer { Columns = Math.Max(1, columns), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        cards.AddThemeConstantOverride("h_separation", 5);
        cards.AddThemeConstantOverride("v_separation", 4);
        scroll.AddChild(cards);

        group ??= new ButtonGroup();
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
            var card = new Button { ToggleMode = true, ButtonGroup = group, ButtonPressed = selectFirst && i == 0,
                CustomMinimumSize = new Vector2(144, 46), SizeFlagsHorizontal = SizeFlags.ExpandFill,
                TooltipText = stats == null ? title : $"{title}\n{stats} (base stats)",
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
            if (setIcon != null)
            {
                var icon = new UnitSilhouette { MouseFilter = MouseFilterEnum.Ignore };
                setIcon(icon, type);
                if (icon.CustomMinimumSize.X > 0)
                {
                    silhouettes.Add(icon);
                    face.AddChild(icon);
                }
                else icon.Free();
            }
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
            if (stats != null)
            {
                var values = new Label { Text = stats, MouseFilter = MouseFilterEnum.Ignore };
                values.AddThemeFontSizeOverride("font_size", 10);
                values.AddThemeColorOverride("font_color", new Color("#b3cde0"));
                details.AddChild(values);
            }
            configureCard?.Invoke(card, type);
            card.Pressed += () => select(type);
        }
        return scroll;
    }

    internal partial class UnitSilhouette : Control
    {
        private Vector2[] points = Array.Empty<Vector2>();

        private (bool Projectile, bool Shield, bool Minigun, int AntiAir)? defense;
        private Texture2D texture;
        private bool textureUsesFactionColor;
        private bool wall;

        internal void SetTexture(Texture2D value, bool useFactionColor = false)
        {
            texture = value;
            textureUsesFactionColor = useFactionColor;
            CustomMinimumSize = new Vector2(28, 0);
            QueueRedraw();
        }

        internal void SetAircraft(UnitDefinition unit)
        {
            if (unit.AircraftIcon != null) SetTexture(unit.AircraftIcon, useFactionColor: true);
            else SetOutline(UnitOutline.FromDefinition(unit, UnitOutline.EffectiveSize(unit)));
        }

        internal void SetWall()
        {
            wall = true;
            CustomMinimumSize = new Vector2(28, 0);
            QueueRedraw();
        }
        private Color tint = Factions.Color(Factions.Blue);

        internal void SetColor(Color color)
        {
            tint = color;
            QueueRedraw();
        }

        internal void SetDefense(BuildingStats stats)
        {
            if (stats.Id == PrefabShelterMeshes.Id) SetWall();
            else SetDefense(stats.Weapon == TowerWeapon.Projectile, stats.ShieldEmitter.Enabled, stats.Id == TwinMinigunTowerMeshes.Id, AntiAirTowerMeshes.Variant(stats.Id));
        }

        internal void SetDefense(bool projectile, bool shield, bool minigun = false, int antiAir = 0)
        {
            defense = (projectile, shield, minigun, antiAir);
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
            if (texture != null)
            {
                DrawTextureRect(texture, new Rect2(Size * 0.5f - Vector2.One * 11f, Vector2.One * 22f), false, textureUsesFactionColor ? tint : Colors.White);
                return;
            }
            if (wall)
            {
                Vector2 center = Size * 0.5f;
                var block = new Rect2(center - new Vector2(11, 7), new Vector2(22, 14));
                DrawRect(block, tint);
                DrawRect(block, edge, false, 1.2f);
                DrawLine(center - new Vector2(11, 2), center + new Vector2(11, -2), edge, 1.2f);
                return;
            }
            if (defense is { } stats)
            {
                Vector2 center = Size * 0.5f;
                var baseRect = new Rect2(center - new Vector2(12, 5), new Vector2(24, 17));
                DrawRect(baseRect, tint);
                DrawRect(baseRect, edge, false, 1.6f);
                if (stats.AntiAir > 0)
                {
                    int count = stats.AntiAir == 1 ? 12 : 1;
                    for (int i = 0; i < count; i++)
                    {
                        Vector2 cell = count == 12 ? new Vector2((i % 4 - 1.5f) * 5f, (i / 4 - 1f) * 4f + 3f) : new Vector2(0, 3);
                        float w = count == 12 ? 3f : 12f;
                        DrawRect(new Rect2(center + cell - Vector2.One * w * 0.5f, Vector2.One * w), edge);
                    }
                }
                else if (stats.Shield)
                    DrawArc(center, 12, Mathf.Pi, Mathf.Tau, 16, edge, 2f, true);
                else if (stats.Minigun)
                {
                    DrawLine(center + new Vector2(-5, 3), center + new Vector2(-5, -8), edge, 4);
                    DrawLine(center + new Vector2(5, 3), center + new Vector2(5, -8), edge, 4);
                }
                else if (stats.Projectile)
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
