// Runs fixed-step V-wing combat and owns the aircraft renderer for one battle.
using Godot;

namespace MassiveRts;

public partial class AirSquadron : Node3D
{
    internal const int MaxPerFaction = 64;
    private const float CruiseAltitude = 12f;
    private const float AcquireRange = 96f;
    private const float FiringAlignment = 0.978f;
    private const int BurstShots = 4;

    internal readonly record struct Flight(UnitDefinition Unit, byte Faction, int Count, Vector2[] Starts, Vector2? BombOrder = null);

    internal struct Fighter
    {
        internal byte Faction;
        internal UnitDefinition Unit;
        internal float Speed, TurnRate, FireRangeSquared, ShotInterval, Damage, DamageTaken;
        internal int DamageType, Armour;
        internal int BombType, BombsRemaining;
        internal Vector2? BombOrder, LockedBombPoint;
        internal Vector2 BombCenter, BombAxis;
        internal int Target, Shots;
        internal Vector3 CrashVelocity;
        internal float CrashSpin, CrashSmokeDelay;
        internal bool Crashing;
        internal Vector3 Position, Forward, TracerEnd;
        internal float Health, Heading, Bank, AltitudeOffset, TurnAway, Reacquire;
        internal float ShotDelay, TracerLife, Evade, EvadeCooldown;
    }

    private Fighter[] fighters;
    private AirSpatialGrid grid;
    private AirSquadronVisuals visuals;
    private InterceptorLauncher interceptors;
    internal bool HasInterceptorEffects => interceptors?.HasEffects == true;
    private TerrainHeightField terrain;
    private float boundary, clearance;
    private float elapsed;
    private DamageRules damageRules;
    private int blueAlive, redAlive, crashingCount;
    private MuzzleSmoke crashSmoke;
    private float crashAftermath;
    internal bool HasCrashes => crashingCount > 0;
    internal bool HasCrashAftermath => crashAftermath > 0f;
    internal bool DogfightActive => blueAlive > 0 && redAlive > 0;
    internal int Alive(byte faction) => faction == Factions.Blue ? blueAlive : faction == Factions.Red ? redAlive : 0;

    internal void Initialize(Flight[] flights, float mapHalfWidth, TerrainHeightField heightField,
        float minimumAltitude, DamageRules damage, ProjectileCatalog projectiles)
    {
        terrain = heightField;
        boundary = mapHalfWidth - 2f;
        clearance = minimumAltitude;
        damageRules = damage;
        foreach (var flight in flights)
            if (flight.Faction == Factions.Blue) blueAlive += flight.Count;
            else redAlive += flight.Count;
        fighters = new Fighter[blueAlive + redAlive];
        grid = new AirSpatialGrid(fighters.Length);
        int start = 0;
        foreach (var flight in flights)
        {
            SpawnFlight(start, flight, projectiles);
            start += flight.Count;
        }
        visuals = new AirSquadronVisuals(fighters);
        AddChild(visuals);
        visuals.Sync(fighters, terrain);
    }

    private void SpawnFlight(int start, Flight flight, ProjectileCatalog projectiles)
    {
        var (unit, faction, count, starts, bombOrder) = flight;
        int bombType = unit.CanBombGround && unit.BombProjectile != null ? projectiles.Register(unit.BombProjectile) : -1;
        float direction = faction == Factions.Blue ? 1f : -1f;
        int columns = Mathf.Min(8, count);
        float spacing = Mathf.Min(3.6f, boundary / 10f);
        for (int i = 0; i < count; i++)
        {
            int id = start + i;
            float offset = (id * 0.618034f % 1f) * 2f - 1f;
            float x = (i % columns - (columns - 1) * 0.5f) * spacing;
            float z = -direction * Mathf.Min(boundary - 3f, Mathf.Min(24f, boundary * 0.53f) + i / columns * spacing);
            if (i < starts.Length)
            {
                x = Mathf.Clamp(starts[i].X, -boundary, boundary);
                z = Mathf.Clamp(starts[i].Y, -boundary, boundary);
            }
            float heading = direction > 0f ? 0f : Mathf.Pi;
            if (i < starts.Length && x * x + z * z > 1f)
                heading = Mathf.Atan2(-x, -z);
            fighters[id] = new Fighter
            {
                Unit = unit, Faction = faction, BombType = bombType, BombOrder = bombOrder, Health = Mathf.Max(1f, unit.Health), Target = -1,
                Speed = Mathf.Max(2f, unit.Speed), TurnRate = Mathf.DegToRad(Mathf.Max(1f, unit.TurnDegreesPerSecond)),
                FireRangeSquared = unit.Reach * unit.Reach, ShotInterval = Mathf.Max(0.001f, unit.Interval),
                Damage = unit.Damage, DamageType = damageRules.DamageType(unit.DamageType),
                Armour = damageRules.ArmourClass(unit.ArmourClass), DamageTaken = 1f - Mathf.Clamp(unit.Armour, 0f, 1f),
                Position = new Vector3(x, CruiseHeight(x, z) + offset, z),
                Heading = heading, Forward = new Vector3(Mathf.Sin(heading), 0f, Mathf.Cos(heading)),
                AltitudeOffset = offset, Reacquire = (id % 12) * 0.015f
            };
        }
    }

    internal void Step(float delta, UnitState ground, SpatialGrid groundGrid, ProjectileSystem projectiles, ShockwaveRenderer impacts)
    {
        elapsed += delta;
        if (HasCrashes) AdvanceCrashes(delta, ground, groundGrid, impacts);
        grid.Rebuild(fighters);
        if (HasInterceptorEffects || (ground?.InterceptorIds != null && blueAlive + redAlive > 0))
        {
            interceptors ??= new InterceptorLauncher(this);
            interceptors.Step(delta, ground, fighters, grid, damageRules, terrain);
        }
        for (int id = 0; id < fighters.Length; id++)
        {
            ref Fighter fighter = ref fighters[id];
            if (fighter.Health <= 0f) continue;
            fighter.TracerLife -= delta;
            fighter.Reacquire -= delta;
            fighter.EvadeCooldown -= delta;
            fighter.Evade -= delta;
            int target = fighter.Target;
            bool valid = target >= 0 && fighters[target].Health > 0f &&
                fighter.Position.DistanceSquaredTo(fighters[target].Position) <= AcquireRange * AcquireRange * 1.5f;
            if (!valid && fighter.Reacquire <= 0f && DogfightActive && fighter.Unit.CanTargetAircraft)
            {
                target = fighter.Target = grid.FindEnemy(fighters, id, AcquireRange);
                fighter.Reacquire = 0.21f + (id % 7) * 0.013f;
                valid = target >= 0;
            }

            Vector3 goal = valid ? fighters[target].Position + fighters[target].Forward * 5f : Vector3.Zero;
            if (fighter.BombType >= 0)
                valid = TryBombGoal(ref fighter, id, ground, groundGrid, out goal);
            float distanceToEdge = boundary - Mathf.Max(Mathf.Abs(fighter.Position.X), Mathf.Abs(fighter.Position.Z));
            bool nearEdge = distanceToEdge < 18f;
            fighter.TurnAway -= delta;
            Vector3 aim = goal - fighter.Position;
            float wanted = Mathf.Atan2(aim.X, aim.Z);
            if (nearEdge && (fighter.BombType < 0 ||
                fighter.Position.X * Mathf.Sin(fighter.Heading) + fighter.Position.Z * Mathf.Cos(fighter.Heading) > 0f))
                wanted = Mathf.Atan2(-fighter.Position.X, -fighter.Position.Z);
            else if (fighter.TurnAway > 0f || fighter.BombsRemaining > 0)
                wanted = fighter.Heading;
            else if (!valid)
                wanted += 0.9f;
            else if (fighter.Evade > 0f)
                wanted += id % 2 == 0 ? 0.65f : -0.65f;

            float difference = Mathf.Wrap(wanted - fighter.Heading, -Mathf.Pi, Mathf.Pi);
            float turn = Mathf.Clamp(difference, -fighter.TurnRate * delta, fighter.TurnRate * delta);
            fighter.Heading = Mathf.Wrap(fighter.Heading + turn, -Mathf.Pi, Mathf.Pi);
            fighter.Bank = Mathf.MoveToward(fighter.Bank, -turn / (fighter.TurnRate * delta) * 0.52f, delta * 2.4f);
            Vector3 forward = new Vector3(Mathf.Sin(fighter.Heading), 0f, Mathf.Cos(fighter.Heading));

            float dip = fighter.TurnAway > 0f ? 0.8f : valid ? -0.55f : 0f;
            float wave = Mathf.Sin(elapsed * 1.2f + id * 1.9f) * 0.4f;
            Vector3 next = fighter.Position + forward * ((fighter.Speed + (id % 5 - 2) * 0.45f) * delta);
            next.X = Mathf.Clamp(next.X, -boundary, boundary);
            next.Z = Mathf.Clamp(next.Z, -boundary, boundary);
            float height = CruiseHeight(next.X, next.Z);
            float ahead = CruiseHeight(next.X + forward.X * 10f, next.Z + forward.Z * 10f);
            next.Y = Mathf.Max(height - 2f, Mathf.MoveToward(fighter.Position.Y,
                Mathf.Max(height, ahead) + fighter.AltitudeOffset + wave + dip, delta * 6f));
            fighter.Forward = (next - fighter.Position).Normalized();
            if (fighter.Forward.LengthSquared() < 0.01f) fighter.Forward = forward;
            fighter.Position = next;

            fighter.ShotDelay -= delta;
            if (fighter.BombType >= 0)
            {
                UpdateBombRun(ref fighter, goal, valid, forward, projectiles);
                continue;
            }
            if (!valid || nearEdge || fighter.TurnAway > 0f)
            {
                fighter.Shots = 0;
                continue;
            }
            ref Fighter enemy = ref fighters[target];
            Vector3 toEnemy = enemy.Position - fighter.Position;
            float distance = toEnemy.LengthSquared();
            if (distance < 24f || (distance < 100f && forward.Dot(toEnemy) < 0f))
            {
                fighter.TurnAway = 0.85f;
                fighter.Shots = 0;
                continue;
            }
            if (distance > fighter.FireRangeSquared || fighter.Forward.Dot(toEnemy.Normalized()) < FiringAlignment)
            {
                fighter.Shots = 0;
                continue;
            }
            if (fighter.ShotDelay > 0f) continue;

            fighter.TracerEnd = enemy.Position;
            fighter.TracerLife = 0.075f;
            ApplyDamage(target, fighter.Damage, fighter.DamageType);
            if (enemy.Health > 0f && enemy.EvadeCooldown <= 0f)
            {
                enemy.Evade = 0.22f;
                enemy.EvadeCooldown = 2.8f;
            }
            fighter.ShotDelay = fighter.ShotInterval;
            if (++fighter.Shots >= BurstShots || enemy.Health <= 0f)
            {
                fighter.Shots = 0;
                fighter.TurnAway = 0.92f;
            }
        }
        visuals.RecordTrails(fighters, delta);
    }

    private void BeginCrash(ref Fighter fighter, int id)
    {
        if (fighter.Faction == Factions.Blue) blueAlive--;
        else redAlive--;
        fighter.Crashing = true;
        fighter.CrashVelocity = new Vector3(Mathf.Sin(fighter.Heading) * fighter.Speed * 0.78f,
            -2.8f, Mathf.Cos(fighter.Heading) * fighter.Speed * 0.78f);
        fighter.CrashSpin = id % 3 == 0 ? (id % 2 == 0 ? 5f : -5f) : 0f;
        fighter.TracerLife = 0f;
        fighter.CrashSmokeDelay = 0f;
        crashingCount++;
        crashSmoke ??= new MuzzleSmoke(this, MuzzleSmoke.DefaultWind);
    }

    private void AdvanceCrashes(float delta, UnitState ground, SpatialGrid groundGrid, ShockwaveRenderer impacts)
    {
        for (int i = 0; i < fighters.Length; i++)
        {
            ref var fighter = ref fighters[i];
            if (!fighter.Crashing) continue;
            fighter.CrashVelocity.Y -= 17f * delta;
            Vector3 from = fighter.Position;
            Vector3 next = from + fighter.CrashVelocity * delta;
            next.X = Mathf.Clamp(next.X, -boundary, boundary);
            next.Z = Mathf.Clamp(next.Z, -boundary, boundary);
            Vector3 travel = next - from;
            float groundY = terrain?.At(next.X, next.Z) ?? 0f;
            float hit = terrain?.SurfaceHit(from, travel, 1f) ?? float.PositiveInfinity;
            if (hit <= 1f || next.Y <= groundY)
            {
                Vector3 impact = hit <= 1f ? from + travel * Mathf.Clamp(hit, 0f, 1f) : next;
                impact.Y = terrain?.At(impact.X, impact.Z) ?? 0f;
                bool heavy = fighter.Unit.CanBombGround;
                float radius = heavy ? 5f : 3f;
                if (groundGrid != null)
                    AreaDamage.Radial(ground, groundGrid, fighter.Faction, impact, radius,
                        heavy ? 38f : 22f, 0.35f, ground.MaximumTravelSpeed * delta,
                        damageType: damageRules.DamageType("Standard"));
                impacts?.Emit(impact, radius, impact + Vector3.Up * 0.25f, fighter.Faction, structureChips: true);
                crashSmoke.Emit(impact);
                crashAftermath = 2.6f;
                fighter.Crashing = false;
                crashingCount--;
                continue;
            }

            fighter.Position = next;
            fighter.Forward = travel.Normalized();
            fighter.Bank = fighter.CrashSpin == 0f ? Mathf.MoveToward(fighter.Bank, 0.2f, delta) :
                fighter.Bank + fighter.CrashSpin * delta;
            fighter.CrashSmokeDelay -= delta;
            if (fighter.CrashSmokeDelay > 0f) continue;
            crashSmoke.Emit(next - fighter.Forward);
            fighter.CrashSmokeDelay = 0.075f;
        }
    }

    internal void AdvanceCrashEffects(float delta)
    {
        crashAftermath = Mathf.Max(0f, crashAftermath - delta);
        crashSmoke?.Advance(delta);
    }

    // Snapshot the target once per pass. Moving or dying ground units cannot redirect it.
    private static bool TryBombGoal(ref Fighter fighter, int id, UnitState ground, SpatialGrid groundGrid, out Vector3 goal)
    {
        var point = fighter.BombOrder ?? fighter.LockedBombPoint;
        if (!point.HasValue && fighter.TurnAway <= 0f && groundGrid != null && fighter.Reacquire <= 0f)
        {
            int target = groundGrid.FindNearestGroundEnemy(ground, fighter.Position, fighter.Faction);
            fighter.Reacquire = 0.46f + (id % 9) * 0.021f;
            if (target >= 0)
                point = fighter.LockedBombPoint = ObstacleField.Xz(ground.Position[target]);
        }
        goal = point is { } destination ? new Vector3(destination.X, 0f, destination.Y) : Vector3.Zero;
        return point.HasValue;
    }

    private void UpdateBombRun(ref Fighter fighter, Vector3 goal, bool valid,
        Vector3 forward, ProjectileSystem projectiles)
    {
        if (fighter.ShotDelay > 0f) return;
        if (fighter.BombsRemaining == 0)
        {
            if (!valid || fighter.TurnAway > 0f) return;
            Vector2 route = ObstacleField.Xz(goal - fighter.Position);
            Vector2 heading = ObstacleField.Xz(forward);
            if (route.LengthSquared() > fighter.FireRangeSquared) return;
            if (route.Dot(heading) < 3f || route.Normalized().Dot(heading) < 0.94f)
            {
                // Extend a missed approach before banking back onto the same spot.
                if (route.LengthSquared() < 12f * 12f) fighter.TurnAway = 1.8f;
                return;
            }
            fighter.BombCenter = ObstacleField.Xz(goal);
            fighter.BombAxis = heading;
            fighter.BombsRemaining = 3;
        }

        Vector2 point = fighter.BombCenter + fighter.BombAxis * ((2 - fighter.BombsRemaining) * 3.2f);
        point = point.Clamp(Vector2.One * -boundary, Vector2.One * boundary);
        float groundY = terrain?.At(point.X, point.Y) ?? 0f;
        if (!projectiles.LaunchFrom(fighter.Position - Vector3.Up * 0.35f, new Vector3(point.X, groundY, point.Y),
            fighter.Faction, fighter.BombType, fighter.Damage, fighter.DamageType)) return;
        fighter.ShotDelay = fighter.ShotInterval;
        if (--fighter.BombsRemaining == 0)
        {
            fighter.BombOrder = null;
            fighter.LockedBombPoint = null;
            fighter.Reacquire = 0f;
            fighter.TurnAway = 2.4f;
        }
    }

    private float CruiseHeight(float x, float z) => Mathf.Max(clearance, (terrain?.At(x, z) ?? 0f) + CruiseAltitude);

    internal void ApplyDamage(int target, float damage, int damageType)
    {
        ref var enemy = ref fighters[target];
        if (enemy.Health <= 0f) return;
        enemy.Health -= damageRules.Resolve(damage, damageType, enemy.Armour, enemy.DamageTaken);
        if (enemy.Health <= 0f) BeginCrash(ref enemy, target);
    }

    internal void SyncVisuals()
    {
        visuals.Sync(fighters, terrain);
        interceptors?.Sync();
    }
}
