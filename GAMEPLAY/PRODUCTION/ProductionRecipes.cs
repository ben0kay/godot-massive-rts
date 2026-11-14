// Authored batch sizes, Carbon costs and production times keyed by stable soldier ID.
using Godot;

namespace MassiveRts;

public partial class ProductionRecipes : Resource
{
    [Export] public Godot.Collections.Dictionary<string, Vector3I> Batches { get; set; } = new();

    internal BatchRecipe[] Compile(RuntimeUnitCatalog catalog)
    {
        var recipes = new BatchRecipe[catalog.Count];
        for (byte type = 0; type < recipes.Length; type++)
        {
            if (!Batches.TryGetValue(catalog[type].Id, out Vector3I value) ||
                value.X <= 0 || value.X > 256 || value.Y < 0 || value.Z <= 0)
                throw new System.InvalidOperationException($"Invalid production recipe for {catalog[type].Id}.");
            recipes[type] = new BatchRecipe(type, value.X, value.Y, value.Z);
        }
        return recipes;
    }
}

internal readonly record struct BatchRecipe(byte Type, int Size, int Cost, float Seconds);
