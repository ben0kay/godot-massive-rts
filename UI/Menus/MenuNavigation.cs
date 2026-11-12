// Centralizes scene changes, including pause restoration on a failed transition.
using System;
using Godot;

namespace MassiveRts;

internal static class MenuNavigation
{
    public const string Boot = "res://UI/Menus/BootScreen.tscn";
    public const string Main = "res://UI/Menus/MainMenu.tscn";
    public const string Campaign = "res://UI/Menus/CampaignMenu.tscn";
    public const string Options = "res://UI/Menus/OptionsMenu.tscn";

    public static void Open(Node source, string path)
    {
        if (source.IsInsideTree()) Change(source, LoadScene(path).Instantiate());
    }

    // =========================================================
    // Restore the mode from the level session rather than a global last selection.
    public static void ReturnToCampaign(Node source, string campaignId)
    {
        if (!source.IsInsideTree()) return;
        var menu = LoadScene(Campaign).Instantiate<FrontMenu>();
        menu.ReturnCampaignId = campaignId;
        Change(source, menu);
    }

    // =========================================================
    // Surface missing or invalid references before touching the active scene.
    public static PackedScene LoadScene(string path)
    {
        if (!SceneExists(path))
            throw new InvalidOperationException($"Scene unavailable: {path}");

        var scene = GD.Load<PackedScene>(path);
        return scene != null && scene.CanInstantiate() ? scene
            : throw new InvalidOperationException($"Could not load scene: {path}");
    }

    public static bool SceneExists(string path) => !string.IsNullOrWhiteSpace(path) &&
        path.StartsWith("res://", StringComparison.Ordinal) && ResourceLoader.Exists(path, "PackedScene");

    // =========================================================
    // A successful scene change immediately removes the old scene from the tree.
    public static void Change(Node source, Node destination)
    {
        if (!source.IsInsideTree())
        {
            destination.Free();
            return;
        }

        SceneTree tree = source.GetTree();
        bool wasPaused = tree.Paused;
        tree.Paused = false;
        Error result = tree.ChangeSceneToNode(destination);
        if (result == Error.Ok) return;
        tree.Paused = wasPaused;
        destination.Free();
        throw new InvalidOperationException($"Could not change scenes ({result}).");
    }
}
