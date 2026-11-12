// Applies the same interactive and disabled button styles across FRONT0.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal enum UIButtonStyle { Primary, Secondary, Compact, Danger }

internal static class UIButtonFactory
{
    private static readonly Dictionary<UIButtonStyle, StyleBoxFlat[]> Styles = new();

    public static Button Create(string text, Action pressed, UIButtonStyle style = UIButtonStyle.Secondary)
    {
        var button = new Button { Text = text };
        Apply(button, style);
        button.Pressed += pressed;
        return button;
    }

    // =========================================================
    // Reuse immutable style boxes across dynamic and authored buttons.
    public static void Apply(Button button, UIButtonStyle style = UIButtonStyle.Secondary)
    {
        UIMasterConfig config = UIMasterConfig.Current;
        if (!Styles.TryGetValue(style, out var boxes))
        {
            Color fill = style switch
            {
                UIButtonStyle.Primary => config.Button.Lerp(config.Accent, 0.24f),
                UIButtonStyle.Danger => config.Danger,
                _ => config.Button
            };
            var focus = Box(Colors.Transparent, config.Accent, 10);
            focus.DrawCenter = false;
            focus.SetBorderWidthAll(2);
            boxes = new[]
            {
                Box(fill, config.Accent.Darkened(0.65f), 10),
                Box(fill.Lightened(0.10f), config.Accent, 10),
                Box(fill.Darkened(0.20f), config.Accent, 10),
                Box(config.Panel, config.Button, 10),
                focus
            };
            Styles.Add(style, boxes);
        }

        button.CustomMinimumSize = new Vector2(0, style == UIButtonStyle.Compact ? config.ButtonHeight * 0.8f : config.ButtonHeight);
        button.FocusMode = Control.FocusModeEnum.All;
        button.AddThemeFontSizeOverride("font_size", config.TextSize);
        button.AddThemeColorOverride("font_color", config.Text);
        button.AddThemeColorOverride("font_hover_color", config.Text);
        button.AddThemeColorOverride("font_pressed_color", config.Text);
        button.AddThemeColorOverride("font_focus_color", config.Text);
        button.AddThemeColorOverride("font_disabled_color", config.MutedText);
        button.AddThemeStyleboxOverride("normal", boxes[0]);
        button.AddThemeStyleboxOverride("hover", boxes[1]);
        button.AddThemeStyleboxOverride("pressed", boxes[2]);
        button.AddThemeStyleboxOverride("disabled", boxes[3]);
        button.AddThemeStyleboxOverride("focus", boxes[4]);
    }

    internal static StyleBoxFlat Box(Color fill, Color edge, int padding)
    {
        var box = new StyleBoxFlat { BgColor = fill, BorderColor = edge };
        box.SetBorderWidthAll(1);
        box.SetCornerRadiusAll(UIMasterConfig.Current.CornerRadius);
        box.ContentMarginLeft = box.ContentMarginRight = padding;
        box.ContentMarginTop = box.ContentMarginBottom = padding;
        return box;
    }
}
