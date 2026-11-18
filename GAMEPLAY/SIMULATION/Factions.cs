// Loads the shared roster once and keeps stable, dense IDs for simulation arrays and saves.
using System;
using Godot;

namespace MassiveRts;

public static class Factions
{
    public const byte Blue = 0, Red = 1, Green = 2;
    internal const int LegacyCount = 3;
    internal const int MaxCount = 32; // The spatial grid uses a 32-bit faction mask.
    private static readonly FactionDefinition[] definitions;
    private static readonly byte[] displayOrder;
    public static readonly int Count;
    internal static ReadOnlySpan<byte> DisplayOrder => displayOrder;

    static Factions()
    {
        var catalog = GD.Load<FactionCatalog>("res://GAMEPLAY/SIMULATION/FACTIONS/DefaultFactionCatalog.tres");
        Count = catalog?.Definitions?.Count ?? 0;
        if (Count < LegacyCount || Count > MaxCount)
            throw new InvalidOperationException($"Faction catalogue requires {LegacyCount}–{MaxCount} entries.");

        definitions = new FactionDefinition[Count];
        displayOrder = new byte[Count];
        foreach (var definition in catalog.Definitions)
        {
            if (definition == null || (uint)definition.Id >= (uint)Count ||
                definitions[definition.Id] != null || string.IsNullOrWhiteSpace(definition.DisplayName))
                throw new InvalidOperationException("Faction IDs must be unique and fill the range from zero, with a name for each faction.");
            definitions[definition.Id] = definition;
            displayOrder[definition.Id] = (byte)definition.Id;
        }
        Array.Sort(displayOrder, (left, right) =>
        {
            int order = definitions[left].DisplayOrder.CompareTo(definitions[right].DisplayOrder);
            return order != 0 ? order : left.CompareTo(right);
        });
    }

    internal static string Name(int faction) => definitions[faction].DisplayName;
    internal static Color Color(int faction) => definitions[faction].Color;
    internal static bool ShowWhenEmpty(int faction) => definitions[faction].ShowWhenEmpty;
}
