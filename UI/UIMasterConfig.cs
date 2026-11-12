// Exposes shared front-end colors, sizing and typography to the Godot Inspector.
using Godot;

namespace MassiveRts;

[GlobalClass]
public partial class UIMasterConfig : Resource
{
    [Export] public Color Background { get; set; } = new("#101621");
    [Export] public Color Panel { get; set; } = new("#192333");
    [Export] public Color Button { get; set; } = new("#253448");
    [Export] public Color Accent { get; set; } = new("#76d8eb");
    [Export] public Color Text { get; set; } = new("#ecf4ff");
    [Export] public Color MutedText { get; set; } = new("#aabbd0");
    [Export] public Color Danger { get; set; } = new("#693447");
    [Export] public int PanelWidth { get; set; } = 560;
    [Export] public int ButtonHeight { get; set; } = 52;
    [Export] public int Margin { get; set; } = 28;
    [Export] public int Spacing { get; set; } = 12;
    [Export] public int CornerRadius { get; set; } = 5;
    [Export] public int HeadingSize { get; set; } = 34;
    [Export] public int TextSize { get; set; } = 22;

    private static UIMasterConfig current;
    internal static UIMasterConfig Current => current ??=
        GD.Load<UIMasterConfig>("res://UI/DefaultUIConfig.tres") ?? new UIMasterConfig();
}
