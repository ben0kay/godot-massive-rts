// Compact authoritative soldier arrays; stable IDs survive deaths until the next reset.
using System.Collections.Generic;
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
    private Dictionary<int, HashSet<int>> firstHitTargets;
    private int[] nextRocketMount;

    internal int RocketMountIndex(int id) => nextRocketMount?[id] ?? 0;

    internal void AdvanceRocketMount(int id, int count) => nextRocketMount[id] = (nextRocketMount[id] + 1) % count;
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

    private const float DownhillBonus = 0.10f;
    internal float MaximumTravelSpeed => Catalog.MaxSpeed * (Grounding == null ? 1f : 1f + DownhillBonus);

    // Ramps between a 2% dead zone and a 35% grade, measured in the travel direction.
    internal float SlopeSpeedFactor(int id, in UnitStats stats, Vector2 direction)
    {
        if (Grounding == null) return 1f;
        float grade = Grounding.Field.Grade(Position[id], direction);
        float steepness = Mathf.Abs(grade);
        if (steepness <= 0.02f) return 1f;
        float amount = Mathf.Min(1f, (steepness - 0.02f) / 0.33f);
        if (grade <= 0f) return 1f + amount * DownhillBonus;

        float tier = stats.SizeTier;
        float cap = tier <= 2f ? Mathf.Lerp(0.03f, 0.08f, Mathf.Clamp(tier - 1f, 0f, 1f))
            : tier <= 3f ? Mathf.Lerp(0.08f, 0.15f, tier - 2f)
            : Mathf.Lerp(0.15f, 0.22f, Mathf.Min(1f, (tier - 3f) * 0.5f));
        return 1f - amount * cap;
    }

    private float[] prismCharge;
    private int[] chargingIds, chargingSlots;
    internal int ChargingCount { get; private set; }
    internal int ChargingId(int slot) => chargingIds[slot];
    internal float PrismCharge(int id) => prismCharge?[id] ?? 0f;

    internal bool SetPrismCharge(int id, float seconds)
    {
        if (PrismCharge(id) == seconds) return false;
        if (prismCharge == null)
        {
            prismCharge = new float[Health.Length];
            chargingIds = new int[Health.Length];
            chargingSlots = new int[Health.Length];
        }
        if (seconds == 0f)
        {
            int slot = chargingSlots[id];
            int moved = chargingIds[--ChargingCount];
            chargingIds[slot] = moved;
            chargingSlots[moved] = slot;
        }
        else if (prismCharge[id] == 0f)
        {
            chargingSlots[id] = ChargingCount;
            chargingIds[ChargingCount++] = id;
        }
        prismCharge[id] = seconds;
        return Dirty[id] = true;
    }

    public float MaxFootprint { get; private set; }
    public int Alive { get; private set; }
    internal bool HasStructureAttackers { get; private set; }
    internal bool HasMeleeLunge { get; private set; }
    private int prismAlive;
    internal bool HasPrism => prismAlive > 0;

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
        RegisterSpawn(id, faction, type);
        return id;
    }

    public Vector3 OnTerrain(Vector3 point)
    {
        if (Grounding != null) point.Y = Grounding.Field.At(point.X, point.Z);
        return point;
    }

    public void RegisterSpawn(int id, byte faction, byte type)
    {
        FactionAlive[faction]++;
        Alive++;
        ref readonly var stats = ref Catalog[type];
        if (Catalog.PrimaryRockets(type).Length > 1) nextRocketMount ??= new int[Health.Length];
        HasStructureAttackers |= stats.TargetsStructures;
        HasMeleeLunge |= stats.MeleeLungeDistance > 0f;
        if (stats.PrismBeam.Enabled) prismAlive++;
        if (stats.ShieldEmitter.Enabled)
        {
            Shields ??= new ShieldSystem(Health.Length);
            Shields.AddMobile(id, stats.ShieldEmitter);
        }
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
    internal ShieldSystem Shields { get; private set; }

    internal void AddShieldTower(StructureHealth tower, ShieldEmitterStats stats)
    {
        Shields ??= new ShieldSystem(Health.Length);
        Shields.AddTower(tower, stats);
    }

    // Stable soldier IDs let an attacker remember each defender for its lifetime.
    internal bool RecordFirstHit(int attacker, int defender)
    {
        firstHitTargets ??= new Dictionary<int, HashSet<int>>();
        if (!firstHitTargets.TryGetValue(attacker, out var targets))
        {
            targets = new HashSet<int>();
            firstHitTargets.Add(attacker, targets);
        }
        return targets.Add(defender);
    }

    public void Damage(int id, float damage, int damageType = 0)
    {
        if (id < 0 || id >= Count || Health[id] <= 0) return;
        float previous = Health[id];
        ref readonly var defender = ref Catalog[Type[id]];
        float resolved = Catalog.DamageRules.Resolve(damage, damageType, defender.ArmourClass, defender.DamageTaken);
        resolved = Shields?.Absorb(id, resolved) ?? resolved;
        Health[id] = Mathf.Max(0, previous - resolved);
        if (Health[id] == 0) RecordDeath(id);
        else if (Health[id] < previous)
        {
            HitFeedback?.OnHit(id);
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
        Shields?.RemoveRecipient(id);
        firstHitTargets?.Remove(id);
        if (Catalog[Type[id]].PrismBeam.Enabled)
        {
            prismAlive--;
            SetPrismCharge(id, 0f);
        }
        DeathFeedback?.Emit(Position[id], Type[id], Faction[id], Facing[id], Catalog[Type[id]], Grounding?.Field);
        Dirty[id] = true;
    }
}
