// Registers the editor workspace and preserves an unsaved draft across plugin toggles.
#if TOOLS
using Godot;

namespace MassiveRts;

[Tool]
public partial class UnitEditorPlugin : EditorPlugin
{
    private UnitEditorPanel panel;
    private static UnitEditorPanel.Draft suspendedDraft;

    public override void _EnterTree()
    {
        panel = new UnitEditorPanel
        {
            Name = "UnitEditorWorkspace",
            Visible = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        EditorInterface.Singleton.GetEditorMainScreen().AddChild(panel);
        panel.Initialize(suspendedDraft);
        suspendedDraft = null;
    }

    public override void _ExitTree()
    {
        if (!GodotObject.IsInstanceValid(panel)) return;
        suspendedDraft = panel.PendingDraft;
        panel.GetParent()?.RemoveChild(panel);
        panel.QueueFree();
        panel = null;
    }

    public override bool _HasMainScreen() => true;
    public override string _GetPluginName() => "Unit Editor";
    public override Texture2D _GetPluginIcon() =>
        EditorInterface.Singleton.GetEditorTheme().GetIcon("Animation", "EditorIcons");

    public override void _MakeVisible(bool visible)
    {
        if (GodotObject.IsInstanceValid(panel)) panel.Visible = visible;
    }

    public override string _GetUnsavedStatus(string forScene) =>
        string.IsNullOrEmpty(forScene) && panel?.PendingDraft != null
            ? "Save the Unit Editor draft before closing?" : "";

    public override void _SaveExternalData() => panel?.Save();
}
#endif
