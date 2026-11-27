// Fixed-capacity projectile pool with swept arrow and explosive shell impacts.
using Godot;

namespace MassiveRts;

public sealed class ProjectileSystem
{
    public readonly Vector3[] Position, Direction;
    public readonly int[] Type;
    private readonly Vector3[] origin, displacement;
    private readonly float[] elapsed, duration, arc, damage;
    private readonly byte[] sourceFaction;
    private readonly int[] damageTypes;
    private readonly float[] forestDeflection;
    private uint shotRandom = 0x9E3779B9u;
    private StructureHealth[] structureTargets, firingTowers;
    // Allocated only when a short-barrel static weapon fires.
    private ObstacleRectangle?[] firingFootprints;
    private readonly ProjectileCatalog catalog;
    private readonly float lifetime;
    public int Count { get; private set; }
    public int Dropped { get; private set; }
    public bool Dirty { get; set; }
    internal event System.Action<Vector3, int> ShotLaunched;
    internal event System.Action<Vector3, float> MuzzleSmokeRequested;
    internal void EmitMuzzleSmoke(Vector3 muzzle, float scale) => MuzzleSmokeRequested?.Invoke(muzzle, scale);
    internal event System.Action<Vector3, Vector3, byte, float> PrismBeamFired;
    internal void EmitPrismBeam(Vector3 from, Vector3 to, byte faction, float seconds) =>
        PrismBeamFired?.Invoke(from, to, faction, seconds);
    // Visuals read the source team without exposing mutable projectile pool arrays.
    public byte FactionAt(int index) => sourceFaction[index];

    // The trail uses the same parabola as flight, evaluated backward from the current pose.
    internal Color TrailCurveAt(int index, float seconds)
    {
        float fraction = Mathf.Min(elapsed[index], seconds) / duration[index];
        Vector3 backward = -Direction[index] * fraction;
        return new Color(backward.X, backward.Y, backward.Z, 4f * arc[index] * fraction * fraction);
    }

    internal float AgeAt(int index) => elapsed[index];
    internal Vector3 MuzzleAt(int index) => origin[index];

    public ProjectileSystem(int capacity, ProjectileCatalog catalog, float lifetime)
    {
        this.catalog = catalog;
        this.lifetime = lifetime;
        Position = new Vector3[capacity]; Direction = new Vector3[capacity];
        origin = new Vector3[capacity]; displacement = new Vector3[capacity];
        elapsed = new float[capacity]; duration = new float[capacity]; arc = new float[capacity];
        damageTypes = new int[capacity];
        forestDeflection = new float[capacity];
        damage = new float[capacity]; Type = new int[capacity]; sourceFaction = new byte[capacity];
    }

    public void Fire(int shooter, int victim, UnitState units)
    {
        ref readonly UnitStats target = ref units.Catalog[units.Type[victim]];
        ref readonly UnitStats type = ref units.Catalog[units.Type[shooter]];
        var mounts = units.Catalog.PrimaryRockets(units.Type[shooter]);
        int projectile = mounts.Length == 0 ? type.ProjectileType : mounts[units.RocketMountIndex(shooter)].Projectile;
        Vector3 aim = units.Position[victim];
        if (catalog.Flight(projectile).ExplosionRadius <= 0f) aim.Y += target.Height * 0.5f;
        Launch(shooter, units, aim, type, againstStructure: false);
    }

    internal bool FireAtStructure(int shooter, UnitState units, Vector3 aim, StructureHealth target)
    {
        int index = Count;
        if (!Launch(shooter, units, aim, units.Catalog[units.Type[shooter]], againstStructure: true)) return false;
        structureTargets ??= new StructureHealth[Position.Length];
        structureTargets[index] = target;
        return true;
    }

    private bool Launch(int shooter, UnitState units, Vector3 aim, in UnitStats type, bool againstStructure)
    {
        var mounts = units.Catalog.PrimaryRockets(units.Type[shooter]);
        int projectile = type.ProjectileType;
        float shotDamage = type.Damage;
        Vector3 muzzle = units.Position[shooter] + Vector3.Up * (type.Height * 0.5f);
        if (mounts.Length > 0)
        {
            var mount = mounts[units.RocketMountIndex(shooter)];
            var hull = MountedRocket.HullBasis(units.Facing[shooter], type.Shape);
            muzzle += hull * (mount.Pose * MountedRocket.Muzzle);
            projectile = mount.Projectile;
            shotDamage = mount.Damage;
        }
        else if (type.Outline.IsChevron)
        {
            Vector2 heading = units.Facing[shooter];
            Vector3 tip = type.Outline.ArtilleryBarrelTip(type.Height);
            muzzle = units.Position[shooter] +
                new Vector3(-heading.X * tip.Z, type.Height * 0.5f + tip.Y, -heading.Y * tip.Z);
        }
        var definition = units.Catalog.Definition(units.Type[shooter]);
        float spread = Mathf.Clamp(definition.AimSpreadDegrees, 0f, 15f);
        if (spread > 0f && !againstStructure)
            aim = muzzle + (aim - muzzle).Rotated(Vector3.Up, Mathf.DegToRad(spread * (NextShotRoll() * 2f - 1f)));
        int slot = Count;
        bool launched = LaunchFrom(muzzle, aim, units.Faction[shooter], projectile,
            (againstStructure ? type.AgainstStructures(shotDamage) : type.AgainstUnits(shotDamage)) * units.AttackMultiplier(shooter),
            againstStructure ? type.StructureDamageType : type.UnitDamageType);
        if (launched && !againstStructure)
            forestDeflection[slot] = Mathf.Clamp(definition.ForestDeflectionChance, 0f, 1f);
        if (launched && mounts.Length > 1) units.AdvanceRocketMount(shooter, mounts.Length);
        return launched;
    }

    internal bool LaunchFrom(Vector3 muzzle, Vector3 aim, byte faction, int projectileType, float amount, int damageType, ObstacleRectangle? firingFootprint = null, StructureHealth firingTower = null)
    {
        if (Count == Position.Length) { Dropped++; return false; }
        ProjectileFlight flight = catalog.Flight(projectileType);
        int i = Count++;
        if (firingFootprint.HasValue) firingFootprints ??= new ObstacleRectangle?[Position.Length];
        if (firingFootprints != null) firingFootprints[i] = firingFootprint;
        if (firingTower != null) firingTowers ??= new StructureHealth[Position.Length];
        if (firingTowers != null) firingTowers[i] = firingTower;
        origin[i] = Position[i] = muzzle;
        Vector3 path = aim - origin[i];
        displacement[i] = path;
        float distance = path.Length();
        float horizontal = new Vector2(path.X, path.Z).Length();
        arc[i] = Mathf.Min(flight.ArcHeight, horizontal * flight.ArcHeightPerDistance);
        duration[i] = Mathf.Max(0.0001f, distance / flight.Speed);
        elapsed[i] = 0;
        Direction[i] = path + Vector3.Up * (4f * arc[i]);
        damage[i] = amount;
        forestDeflection[i] = 0f;
        damageTypes[i] = damageType;
        Type[i] = projectileType;
        sourceFaction[i] = faction;
        Dirty = true;
        ShotLaunched?.Invoke(muzzle, projectileType);
        return true;
    }

    public void Step(UnitState units, SpatialGrid grid, BenchmarkConfig config, float dt, float separationPadding, ObstacleField obstacles = null, ShockwaveRenderer rings = null)
    {
        if (Count == 0) return;
        Dirty = true;
        float movementPadding = units.MaximumTravelSpeed * dt + separationPadding;
        for (int i = Count - 1; i >= 0; i--)
        {
            float endTime = Mathf.Min(duration[i], lifetime);
            elapsed[i] = Mathf.Min(elapsed[i] + dt, endTime);
            float t = elapsed[i] / duration[i];
            Vector3 next = origin[i] + displacement[i] * t + Vector3.Up * (4f * arc[i] * t * (1f - t));
            Vector3 segment = next - Position[i];
            ProjectileFlight flight = catalog.Flight(Type[i]);
            if (structureTargets?[i] is StructureHealth structure)
            {
                if (structure.Destroyed || (flight.BlocksOnObstacles &&
                    obstacles?.BlocksSightExcept(Position[i], next, structure.Area) == true))
                {
                    Remove(i);
                    continue;
                }
                if (elapsed[i] >= duration[i])
                {
                    float before = structure.Health;
                    structure.Damage(damage[i], damageTypes[i]);
                    Vector3 ground = next;
                    ground.Y = units.Grounding?.Field?.At(next.X, next.Z) ?? 0f;
                    rings?.Emit(ground, flight.ExplosionRadius, next, sourceFaction[i],
                        structureChips: structure.Health < before);
                    rings?.EmitScorch(ground, flight.ExplosionRadius, units.Grounding?.Field);
                }
            }
            else if (flight.ExplosionRadius > 0f)
            {
                if (ImpactShell(i, units, grid, config, movementPadding, obstacles, rings, flight, segment))
                {
                    Remove(i);
                    continue;
                }
            }
            else
            {
                float wallHit = float.PositiveInfinity;
                bool blocked;
                if (firingFootprints?[i] is ObstacleRectangle own)
                {
                    float end = flight.BlocksOnObstacles
                        ? obstacles?.FirstSightHitExcept(Position[i], segment, own) ?? 1f : 1f;
                    end = PiercingBeam.ClipTerrain(units.Grounding?.Field, Position[i], segment, end);
                    blocked = end < 1f;
                    if (blocked) wallHit = end;
                }
                else
                    blocked = obstacles != null && flight.BlocksOnObstacles &&
                        obstacles.FirstHit(ObstacleField.Xz(Position[i]), ObstacleField.Xz(next), 0f, out wallHit);
                int victim = wallHit > 0f
                    ? grid.FirstProjectileHit(units, sourceFaction[i], Position[i], segment, config.ProjectileHitRadius, movementPadding, out _, wallHit)
                    : -1;
                if (victim >= 0 || blocked)
                {
                    if (victim >= 0)
                    {
                        var forest = units.Forest;
                        if (forestDeflection[i] > 0f && forest?.Contains(units.Position[victim]) == true &&
                            NextShotRoll() < forestDeflection[i])
                            forest.DropLeaves(units.Position[victim] + Vector3.Up * units.Catalog[units.Type[victim]].Height * 0.65f);
                        else
                        {
                            units.Damage(victim, damage[i], damageTypes[i]);
                            if (units.Health[victim] <= 0f && firingTowers?[i] is { } tower) tower.Kills++;
                        }
                    }
                    Remove(i);
                    continue;
                }
            }
            if (elapsed[i] >= endTime)
            {
                Remove(i);
                continue;
            }
            Position[i] = next;
            Direction[i] = displacement[i] + Vector3.Up * (4f * arc[i] * (1f - 2f * t));
        }
    }

    // =========================================================
    // Resolve the first contact before querying the explosion radius.
    private bool ImpactShell(int index, UnitState units, SpatialGrid grid, BenchmarkConfig config,
        float movementPadding, ObstacleField obstacles, ShockwaveRenderer rings, ProjectileFlight flight, Vector3 segment)
    {
        Vector3 from = Position[index];
        var terrain = units.Grounding?.Field;
        float hit = flight.BlocksOnObstacles
            ? obstacles?.AirborneHit(from, segment, config.ObstacleHeight) ?? float.PositiveInfinity
            : float.PositiveInfinity;
        float groundHit = terrain != null
            ? terrain.SurfaceHit(from, segment, Mathf.Min(hit, 1f))
            : (from.Y + segment.Y <= 0f && segment.Y < 0f ? Mathf.Clamp(-from.Y / segment.Y, 0f, 1f) : float.PositiveInfinity);
        hit = Mathf.Min(hit, groundHit);
        grid.FirstProjectileHit(units, sourceFaction[index], from, segment, config.ProjectileHitRadius,
            movementPadding, out float firstHit, hit);
        hit = firstHit;
        // Completing the flight lands at the recorded ground aim point. Expiring in flight does not explode.
        if (hit > 1f && elapsed[index] < duration[index]) return false;
        Vector3 impact = from + segment * Mathf.Min(hit, 1f);
        int kills = AreaDamage.Radial(units, grid, sourceFaction[index], impact, flight.ExplosionRadius,
            damage[index], flight.ExplosionEdgeFraction, movementPadding, damageType: damageTypes[index]);
        if (firingTowers?[index] is { } tower) tower.Kills += kills;
        Vector3 burstOrigin = impact;
        impact.Y = terrain?.At(impact.X, impact.Z) ?? 0f;
        rings?.Emit(impact, flight.ExplosionRadius, burstOrigin, sourceFaction[index]);
        rings?.EmitScorch(impact, flight.ExplosionRadius, terrain);
        return true;
    }

    private float NextShotRoll()
    {
        shotRandom ^= shotRandom << 13;
        shotRandom ^= shotRandom >> 17;
        shotRandom ^= shotRandom << 5;
        return (shotRandom >> 8) * (1f / 16777216f);
    }

    private void Remove(int index)
    {
        int last = --Count;
        if (structureTargets != null)
        {
            structureTargets[index] = structureTargets[last];
            structureTargets[last] = null;
        }
        if (firingFootprints != null)
        {
            firingFootprints[index] = firingFootprints[last];
            firingFootprints[last] = null;
        }
        if (firingTowers != null)
        {
            firingTowers[index] = firingTowers[last];
            firingTowers[last] = null;
        }
        if (index == last) return;
        Position[index] = Position[last]; Direction[index] = Direction[last];
        origin[index] = origin[last]; displacement[index] = displacement[last];
        elapsed[index] = elapsed[last]; duration[index] = duration[last]; arc[index] = arc[last];
        damageTypes[index] = damageTypes[last];
        forestDeflection[index] = forestDeflection[last];
        damage[index] = damage[last]; Type[index] = Type[last]; sourceFaction[index] = sourceFaction[last];
    }
}
