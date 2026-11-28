// Shared chamfered frames and typography for tactical battle panels.
using Godot;

namespace MassiveRts;

internal static class TacticalHudStyle
{
    internal static readonly Color Silver = new("8ba9bf");
    internal static readonly Color Text = new("e4f3ff");

    internal static Label Label(string text, int size, Color color, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = new Label { Text = text, HorizontalAlignment = align, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    internal static Button Button(string text, string tooltip, System.Action action)
    {
        var button = new Button { Text = text, TooltipText = tooltip, FocusMode = Control.FocusModeEnum.None };
        button.AddThemeFontSizeOverride("font_size", 14);
        button.AddThemeStyleboxOverride("normal", UIButtonFactory.Box(new Color("142331"), Silver.Darkened(.5f), 4));
        button.AddThemeStyleboxOverride("hover", UIButtonFactory.Box(new Color("243e53"), Silver, 4));
        button.Pressed += action;
        return button;
    }

    internal static VBoxContainer Panel(Container parent, Color accent)
    {
        var frame = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var frameStyle = new StyleBoxFlat
        {
            BgColor = new Color(.025f + accent.R * .06f, .05f + accent.G * .05f, .075f + accent.B * .06f, .91f),
            BorderColor = new Color(accent, .56f),
            BorderWidthBottom = 2, BorderWidthTop = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
            CornerRadiusTopLeft = 9, CornerRadiusBottomRight = 9, CornerDetail = 1,
            ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 5, ContentMarginBottom = 6,
            AntiAliasing = true
        };
        frame.AddThemeStyleboxOverride("panel", frameStyle);
        var contents = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        contents.AddThemeConstantOverride("separation", 1);
        frame.AddChild(contents);
        parent.AddChild(frame);
        return contents;
    }

    internal static void Set(Label label, string value)
    {
        if (label.Text != value) label.Text = value;
    }
}
