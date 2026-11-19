// Opens the project authoring tools from a single F6 scene.
using Godot;

namespace MassiveRts;

public partial class GameEditor : Control
{
    internal const string ScenePath = "res://UI/Editors/GameEditor.tscn";

    public override void _Ready()
    {
        var content = MenuLayout.Create(this, "Game Editor");
        if (!LevelEditorSave.CanWrite)
        {
            content.AddChild(MenuLayout.Label("Open GameEditor.tscn in the Godot editor and press F6.", muted: true));
            return;
        }
        var levels = UIButtonFactory.Create("Level Editor", () =>
            MenuNavigation.Open(this, "res://LEVELS/EDITOR/LevelEditor.tscn"), UIButtonStyle.Primary);
        content.AddChild(levels);
        content.AddChild(UIButtonFactory.Create("Entity Editor", () =>
            MenuNavigation.Change(this, new RuntimeEntityEditor()), UIButtonStyle.Primary));
        levels.GrabFocus();
    }
}
