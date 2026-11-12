// Builds the shared menu panel, labels and keyboard-following scroll lists.
using Godot;

namespace MassiveRts;

internal static class MenuLayout
{
    private static UIMasterConfig Config => UIMasterConfig.Current;
    public static StyleBoxFlat PanelStyle { get; } = UIButtonFactory.Box(
        Config.Panel, Config.Accent.Darkened(0.7f), Config.Margin);

    // =========================================================
    // Center the card while allowing the screen to scroll if its height is constrained.
    public static VBoxContainer Create(Control root, string heading, string subtitle = "")
    {
        var background = new ColorRect { Color = Config.Background, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.AddChild(background);
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        root.AddChild(scroll);
        scroll.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var center = new CenterContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        scroll.AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(Config.PanelWidth, 0) };
        panel.AddThemeStyleboxOverride("panel", PanelStyle);
        center.AddChild(panel);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", Config.Spacing);
        panel.AddChild(content);

        var title = Label(heading);
        title.AddThemeFontSizeOverride("font_size", Config.HeadingSize);
        content.AddChild(title);
        if (subtitle.Length > 0) content.AddChild(Label(subtitle, muted: true));
        return content;
    }

    public static Label Label(string text, bool muted = false)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontSizeOverride("font_size", Config.TextSize);
        label.AddThemeColorOverride("font_color", muted ? Config.MutedText : Config.Text);
        return label;
    }

    public static VBoxContainer ScrollList(VBoxContainer parent)
    {
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 240),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true
        };
        parent.AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", Config.Spacing);
        scroll.AddChild(list);
        return list;
    }
}
