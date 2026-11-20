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
    private bool[] nextRocketRight;

    internal float RocketMuzzleSide(int id) => nextRocketRight?[id] == true ? 1f : -1f;

    internal void AdvanceRocketMuzzle(int id) => nextRocketRight[id] = !nextRocketRight[id];
    private Vector2[] turnSteps;
    private float turnStepSeconds;

    // A fixed tick shares one sine/cosine pair per directional unit type.
    internal Vector2[] TurnSteps(float dt)
    {
        turnSteps ??= new Vector2[Catalog.Count];
        if (turnStepSeconds == dt) return turnSteps;
        turnStepSeconds = dt;
        for (int i = 0; i < turnSteps.Length; i++)
        {
            ref readonly var type = ref Catalog[i];
            if (type.Shape != UnitShape.Circle)
                turnSteps[i] = SoldierFacing.TurnStep(dt, type.TurnDegreesPerSecond);
        }
        return turnSteps;
    }
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
    internal ForestCover Forest { get; set; }

    internal float RangedReach(Vector3 position, in UnitStats stats) =>
        Forest?.Contains(position) == true ? stats.Reach * 0.5f : stats.Reach;

    internal float RangedReach(int id, in UnitStats stats) => RangedReach(Position[id], stats);

    internal float TravelSpeed(int id, in UnitStats stats) =>
        stats.SizeTier == 2f && Forest?.Contains(Position[id]) == true ? stats.Speed * 0.75f : stats.Speed;

    public float MaxFootprint { get; private set; }
    public int Alive { get; private set; }
    internal bool HasStructureAttackers { get; private set; }
    internal bool HasMeleeLunge { get; private set; }

    // Appends a stable soldier ID when the preallocated state has room.
    public int TrySpawn(byte type, byte faction, Vector3 position)
    {
        if (Count == Position.Length) return -1;
        int id = Count++;
        Type[id] = type;
        Faction[id] = faction;
        Position[id] = Goal[id] = OnTerrain(position);
        Health[id] = Catalog[type].Health;
        Target[id] = -1;
        Dirty[id] = true;
        RegisterSpawn(faction, type);
        return id;
    }

    public Vector3 OnTerrain(Vector3 point)
    {
        if (Grounding != null) point.Y = Grounding.Field.At(point.X, point.Z);
        return point;
    }

    public void RegisterSpawn(byte faction, byte type)
    {
        FactionAlive[faction]++;
        Alive++;
        ref readonly var stats = ref Catalog[type];
        if (stats.Outline.IsDeepFork) nextRocketRight ??= new bool[Health.Length];
        HasStructureAttackers |= stats.TargetsStructures;
        HasMeleeLunge |= stats.MeleeLungeDistance > 0f;
        MaxFootprint = Mathf.Max(MaxFootprint, stats.Footprint);
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

    public void Damage(int id, float damage, int damageType = 0)
    {
        if (id < 0 || id >= Count || Health[id] <= 0) return;
        float previous = Health[id];
        ref readonly var defender = ref Catalog[Type[id]];
        Health[id] = Mathf.Max(0, previous - Catalog.DamageRules.Resolve(damage, damageType, defender.ArmourClass, defender.DamageTaken));
        if (Health[id] == 0) RecordDeath(id);
        else if (Health[id] < previous && HitFeedback != null)
        {
            HitFeedback.OnHit(id);
            Dirty[id] = true;
        }
    }

    internal void Kill(int id)
    {
        if (Health[id] <= 0) return;
        Health[id] = 0;
        RecordDeath(id);
    }

    private void RecordDeath(int id)
    {
        FactionAlive[Faction[id]]--;
        Alive--;
        DeathFeedback?.Emit(Position[id], Type[id], Faction[id], Facing[id]);
        Dirty[id] = true;
    }
}
