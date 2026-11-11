// Compact authoritative soldier arrays; stable IDs survive deaths until the next reset.
using Godot;
namespace MassiveRts;

public sealed class UnitState
{
    public readonly Vector3[] Position;
    public readonly Vector3[] Goal;
    public readonly float[] Health, Cooldown, MoveSpeed;
    public readonly int[] Target;
    public readonly byte[] Faction, Type;
    public readonly bool[] Dirty;
    public readonly int[] FactionAlive = new int[Factions.Count];
    public int Count;
    public int Alive { get; private set; }

    public void RegisterSpawn(byte faction)
    {
        FactionAlive[faction]++;
        Alive++;
    }
    public RuntimeUnitCatalog Catalog { get; }
    public HitFeedbackSystem HitFeedback { get; }
    public UnitState(int capacity, RuntimeUnitCatalog catalog, HitFeedbackSystem hitFeedback = null)
    {
        Catalog = catalog;
        HitFeedback = hitFeedback;
        Position = new Vector3[capacity]; Goal = new Vector3[capacity];
        Health = new float[capacity]; Cooldown = new float[capacity]; MoveSpeed = new float[capacity];
        Target = new int[capacity]; Faction = new byte[capacity]; Type = new byte[capacity];
        Dirty = new bool[capacity];
    }
    // =========================================================
    public void Damage(int id, float damage)
    {
        if (id < 0 || id >= Count || Health[id] <= 0) return;
        float previous = Health[id];
        Health[id] = Mathf.Max(0, previous - Mathf.Max(0, damage * Catalog[Type[id]].DamageTaken));
        if (Health[id] == 0)
        {
            FactionAlive[Faction[id]]--;
            Alive--;
            Dirty[id] = true;
        }
        else if (Health[id] < previous && HitFeedback != null)
        {
            HitFeedback.OnHit(id);
            Dirty[id] = true;
        }
    }
}
// Single flat-ground seam for later elevation; simulation owns world-space positions.
public static class BattlefieldHeight
{
    public static float At(float x, float z) => 0f;
}
