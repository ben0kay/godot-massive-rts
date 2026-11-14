// Owns one paid batch per Fabricator and retries blocked releases without losing units.
using System;

namespace MassiveRts;

internal sealed class CarbonBalance
{
    public int Amount { get; private set; } = 10000;
    public bool TrySpend(int cost)
    {
        if (Amount < cost) return false;
        Amount -= cost;
        return true;
    }
}

internal sealed class FabricatorProduction
{
    private BatchRecipe paid;
    private float remainingTime, releaseDelay;
    private int remainingUnits;

    public int RecipeIndex { get; set; } = -1;
    public bool Automatic { get; set; } = true;
    public string Status { get; private set; } = "Idle";
    public float Progress => remainingUnits == 0 ? 0 : 1f - remainingTime / paid.Seconds;

    public void Step(float delta, BatchRecipe[] recipes, CarbonBalance carbon, Func<byte, bool> release)
    {
        if (remainingUnits == 0)
        {
            if (!Automatic || RecipeIndex < 0)
            {
                Status = "Idle";
                return;
            }
            var next = recipes[RecipeIndex];
            if (!carbon.TrySpend(next.Cost))
            {
                Status = "Insufficient Carbon";
                return;
            }
            paid = next;
            remainingTime = paid.Seconds;
            remainingUnits = paid.Size;
        }

        if (remainingTime > 0)
        {
            remainingTime = Math.Max(0, remainingTime - delta);
            Status = "Producing";
            return;
        }
        releaseDelay -= delta;
        if (releaseDelay > 0) return;
        releaseDelay = 0.15f;
        if (release(paid.Type))
        {
            remainingUnits--;
            Status = "Releasing";
        }
        else Status = "Exit Blocked";
    }
}
