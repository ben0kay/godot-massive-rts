// Tracks paid Worker orders and retries finished releases without charging again.
using System;
using Godot;

namespace MassiveRts;

internal sealed class CitadelWorkerProduction
{
    private readonly CarbonBalance carbon;
    private readonly byte workerType;
    private readonly float productionSeconds;
    private float remaining, retryDelay;
    public int Cost { get; }
    public int Limit { get; }
    public int Queued { get; private set; }
    public float Progress => Queued == 0 ? 0f : 1f - remaining / productionSeconds;
    public bool WaitingForExit => Queued > 0 && remaining <= 0f;

    public CitadelWorkerProduction(CitadelWorkerSettings settings, RuntimeUnitCatalog catalog, CarbonBalance carbon)
    {
        if (!catalog.TryResolve(settings.UnitId, out workerType) || catalog[workerType].AllowedTargets != TargetCategory.None)
            throw new InvalidOperationException("Citadel worker production needs a registered noncombat unit.");
        this.carbon = carbon;
        Cost = Math.Max(0, settings.CarbonCost);
        Limit = Math.Clamp(settings.QueueLimit, 1, 64);
        productionSeconds = float.IsFinite(settings.ProductionSeconds) ? Mathf.Max(0.1f, settings.ProductionSeconds) : 3f;
    }

    public string BlockReason(bool operational) =>
        !operational ? "Citadel unavailable" : Queued >= Limit ? "Worker queue full" :
        carbon.Amount < Cost ? "Insufficient Carbon" : null;

    public bool Enqueue(bool operational)
    {
        if (BlockReason(operational) != null || !carbon.TrySpend(Cost)) return false;
        if (Queued++ == 0) remaining = productionSeconds;
        return true;
    }

    public void Step(float delta, Func<byte, bool> release)
    {
        if (Queued == 0) return;
        remaining = Mathf.Max(0f, remaining - delta);
        if (remaining > 0f) return;
        retryDelay -= delta;
        if (retryDelay > 0f) return;
        if (!release(workerType))
        {
            retryDelay = 0.15f;
            return;
        }
        Queued--;
        remaining = productionSeconds;
        retryDelay = 0f;
    }
}
