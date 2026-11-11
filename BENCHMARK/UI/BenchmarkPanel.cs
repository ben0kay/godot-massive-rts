// Benchmark-only controls and throttled, permanent performance readout.
using System;
using Godot;
namespace MassiveRts;

public partial class BenchmarkPanel : CanvasLayer
{
    public event Action SpawnRequested, ClearRequested, PauseRequested;
    public readonly OptionButton CountChoice = new(), ModeChoice = new(), LayoutChoice = new();
    public readonly SpinBox Seed = new() { MinValue = 0, MaxValue = 2147483647, Value = 12345, Step = 1 };
    public readonly SpinBox FireRate = new() { MinValue = 0.2, MaxValue = 20, Step = 0.2, Value = 2 };
    public readonly SpinBox RangedPercent = new() { MinValue = 0, MaxValue = 100, Step = 5, Value = 40 };
    public readonly Label Status = new(), Metrics = new();
    public readonly Button PauseButton = new() { Text = "Pause" };
    private PanelContainer overlay;
    public static readonly int[] Counts = { 1000, 5000, 10000, 25000, 50000, 100000 };
    public int RequestedCount => Counts[CountChoice.Selected];
    // =========================================================
    public override void _Ready()
    {
        var left = new PanelContainer { Position = new Vector2(12, 12), CustomMinimumSize = new Vector2(245, 0) };
        AddChild(left); var column = new VBoxContainer(); left.AddChild(column);
        column.AddChild(new Label { Text = "BENCH0 • MASSIVE RTS" });
        foreach (int count in Counts) CountChoice.AddItem(count.ToString("N0"));
        foreach (string mode in new[] { "A — Rendering only", "B — Movement", "C — Melee", "D — Ranged" }) ModeChoice.AddItem(mode);
        foreach (string layout in new[] { "Opposing lines", "Dense clusters", "Distributed battlefield" }) LayoutChoice.AddItem(layout);
        Field(column, "Total soldiers", CountChoice); Field(column, "Mode (applies on spawn)", ModeChoice);
        Field(column, "Layout (applies on spawn)", LayoutChoice); Field(column, "Seed (applies on spawn)", Seed);
        Field(column, "Shots/sec per ranged unit (live)", FireRate); Field(column, "Ranged % (applies on spawn)", RangedPercent);
        var spawn = new Button { Text = "Spawn / Reset selected" }; spawn.Pressed += () => SpawnRequested?.Invoke(); column.AddChild(spawn);
        var clear = new Button { Text = "Clear all" }; clear.Pressed += () => ClearRequested?.Invoke(); column.AddChild(clear);
        PauseButton.Pressed += () => PauseRequested?.Invoke(); column.AddChild(PauseButton);
        var toggle = new CheckButton { Text = "Performance overlay (F4)", ButtonPressed = true };
        toggle.Toggled += shown => overlay.Visible = shown; column.AddChild(toggle);
        Status.AutowrapMode = TextServer.AutowrapMode.WordSmart; column.AddChild(Status);
        column.AddChild(new Label { Text = "WASD / arrows: pan\nMouse wheel: zoom\nSpace: pause • F4: overlay" });
        overlay = new PanelContainer { AnchorLeft = 1, AnchorRight = 1, OffsetLeft = -415, OffsetRight = -12, OffsetTop = 12 };
        AddChild(overlay); overlay.AddChild(Metrics); Metrics.AddThemeFontSizeOverride("font_size", 15);
    }
    private static void Field(VBoxContainer column, string title, Control input)
    { column.AddChild(new Label { Text = title }); column.AddChild(input); }
    // =========================================================
    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (input is not InputEventKey key || !key.Pressed || key.Echo) return;
        if (key.PhysicalKeycode == Key.F4) overlay.Visible = !overlay.Visible;
        if (key.PhysicalKeycode == Key.Space) PauseRequested?.Invoke();
    }
}
