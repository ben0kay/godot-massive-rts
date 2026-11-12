// Persists global display preferences and applies them on Boot or after a successful save.
using System.Text.Json.Serialization;
using Godot;

namespace MassiveRts;

internal sealed record GameOptions(
    [property: JsonRequired] int Version,
    [property: JsonRequired] bool Fullscreen,
    [property: JsonRequired] bool VSync);

internal static class OptionsStore
{
    private static string Path => ProjectSettings.GlobalizePath("user://Settings/options.json");
    public static GameOptions Load(out bool recovered) =>
        AtomicJsonFile.Load(Path, Valid, () => new GameOptions(1, false, true), out recovered);

    public static void Save(GameOptions options)
    {
        AtomicJsonFile.Save(Path, options, Valid);
        Apply(options);
    }

    public static void Apply(GameOptions options)
    {
        DisplayServer.WindowSetMode(options.Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
        DisplayServer.WindowSetVsyncMode(options.VSync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
    }

    private static bool Valid(GameOptions options) => options.Version == 1;
}
