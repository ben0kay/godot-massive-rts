// Authored enemy counts by stable soldier ID for one numbered wave.
using Godot;

namespace MassiveRts;

public partial class WaveDefinition : Resource
{
    [Export] public Godot.Collections.Dictionary<string, int> Enemies { get; set; } = new();

    internal byte[] Compile(RuntimeUnitCatalog catalog, int capacity)
    {
        var types = new System.Collections.Generic.List<byte>();
        foreach (var entry in Enemies)
        {
            if (!catalog.TryResolve(entry.Key, out byte type) || entry.Value < 1 || entry.Value > capacity - types.Count)
                throw new System.InvalidOperationException($"Invalid wave entry for {entry.Key}.");
            for (int i = 0; i < entry.Value; i++) types.Add(type);
        }
        if (types.Count == 0) throw new System.InvalidOperationException("A wave needs at least one enemy.");
        return types.ToArray();
    }
}
