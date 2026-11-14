// Compact authoritative soldier arrays; stable IDs survive deaths until the next reset.
using Godot;
namespace MassiveRts;

public sealed class UnitState
{
    public readonly Vector3[] Position;
    public readonly Vector3[] Goal;
    public readonly Vector2[] Facing;
    public readonly float[] Health, Cooldown, MoveSpeed;
    public readonly int[] Target;
    public readonly byte[] Faction, Type;
    public readonly bool[] Dirty;
    public readonly int[] FactionAlive = new int[Factions.Count];
    private byte[] completedAttacks;
    private int[] retargetAfter;

    internal int[] RetargetDeadlines(bool enabled)
    {
        if (!enabled)
        {
            retargetAfter = null;
            return null;
        }
        return retargetAfter ??= new int[Target.Length];
    }
    public int Count;
    public float MaxFootprint { get; private set; }
    public int Alive { get; private set; }

    // Appends a stable soldier ID when the preallocated state has room.
    public int TrySpawn(byte type, byte faction, Vector3 position)
    {
        if (Count == Position.Length) return -1;
        int id = Count++;
        Type[id] = type;
        Faction[id] = faction;
        Position[id] = Goal[id] = position;
        Health[id] = Catalog[type].Health;
        Target[id] = -1;
        Dirty[id] = true;
        RegisterSpawn(faction, type);
        return id;
    }

    public void RegisterSpawn(byte faction, byte type)
    {
        FactionAlive[faction]++;
        Alive++;
        MaxFootprint = Mathf.Max(MaxFootprint, Catalog[type].Footprint);
    }
    public RuntimeUnitCatalog Catalog { get; }
    public HitFeedbackSystem HitFeedback { get; }
    public RangedRecoilSystem RangedRecoil { get; }
    public TerrainGrounding Grounding { get; }
    public UnitState(int capacity, RuntimeUnitCatalog catalog, HitFeedbackSystem hitFeedback = null, TerrainHeightField terrain = null, RangedRecoilSystem rangedRecoil = null)
    {
        Catalog = catalog;
        HitFeedback = hitFeedback;
        RangedRecoil = rangedRecoil;
        if (terrain != null) Grounding = new TerrainGrounding(terrain, capacity);
        Position = new Vector3[capacity]; Goal = new Vector3[capacity];
        Facing = new Vector2[capacity];
        Health = new float[capacity]; Cooldown = new float[capacity]; MoveSpeed = new float[capacity];
        Target = new int[capacity]; Faction = new byte[capacity]; Type = new byte[capacity];
        Dirty = new bool[capacity];
    }
    // =========================================================
    public bool IsShockwaveAttack(int id, int cadence)
    {
        completedAttacks ??= new byte[Health.Length];
        if (++completedAttacks[id] < cadence) return false;
        completedAttacks[id] = 0;
        return true;
    }

    public DeathFeedbackSystem DeathFeedback { get; set; }

    public void Damage(int id, float damage)
    {
        if (id < 0 || id >= Count || Health[id] <= 0) return;
        float previous = Health[id];
        Health[id] = Mathf.Max(0, previous - Mathf.Max(0, damage * Catalog[Type[id]].DamageTaken));
        if (Health[id] == 0)
        {
            FactionAlive[Faction[id]]--;
            Alive--;
            DeathFeedback?.Emit(Position[id], Type[id], Faction[id], Facing[id]);
            Dirty[id] = true;
        }
        else if (Health[id] < previous && HitFeedback != null)
        {
            HitFeedback.OnHit(id);
            Dirty[id] = true;
        }
    }
}
