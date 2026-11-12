// Loads the local identity index and tracks the explicitly selected commander.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MassiveRts;

internal static class ProfileStore
{
    private const int MaxProfiles = 1024;
    private static string Path => ProjectSettings.GlobalizePath("user://Profiles/profiles.json");
    public static PlayerProfile Selected { get; private set; }

    public sealed class ProfileIndex
    {
        public int Version { get; set; }
        public List<PlayerProfile> Profiles { get; set; }
    }

    // =========================================================
    // Read current disk state on Boot and before identity changes.
    public static IReadOnlyList<PlayerProfile> Load(out bool recovered) => AtomicJsonFile.Load(
        Path, Valid, () => new ProfileIndex { Version = 1, Profiles = new() }, out recovered).Profiles;

    public static void Select(string id) =>
        Selected = Load(out _).FirstOrDefault(profile => profile.Id == id)
            ?? throw new InvalidOperationException("That profile is unavailable.");

    public static void ClearSelection() => Selected = null;

    // =========================================================
    // Publish the new identity only after the index has been saved.
    public static void Create(string input)
    {
        string name = input.Trim();
        if (!ValidName(name)) throw new ArgumentException("Enter a name of 1–32 characters without control characters.");
        var profiles = Load(out _).ToList();
        if (profiles.Count >= MaxProfiles) throw new InvalidOperationException("The local profile limit has been reached.");
        if (profiles.Any(profile => StringComparer.OrdinalIgnoreCase.Equals(profile.Name, name)))
            throw new ArgumentException("A profile with that name already exists.");

        var created = new PlayerProfile(Guid.NewGuid().ToString("N"), name, DateTime.UtcNow);
        profiles.Add(created);
        AtomicJsonFile.Save(Path, new ProfileIndex { Version = 1, Profiles = profiles }, Valid);
        Selected = created;
    }

    public static bool ValidId(string id) =>
        Guid.TryParseExact(id, "N", out Guid guid) && guid != Guid.Empty && id == guid.ToString("N");

    private static bool ValidName(string name) =>
        name != null && name.Length is >= 1 and <= 32 && name == name.Trim() && !name.Any(char.IsControl);

    private static bool Valid(ProfileIndex index)
    {
        if (index.Version != 1 || index.Profiles == null || index.Profiles.Count > MaxProfiles) return false;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return index.Profiles.All(profile => profile != null && ValidId(profile.Id) && ids.Add(profile.Id) &&
            ValidName(profile.Name) && names.Add(profile.Name) &&
            profile.CreatedUtc.Kind == DateTimeKind.Utc && profile.CreatedUtc > DateTime.UnixEpoch);
    }
}
