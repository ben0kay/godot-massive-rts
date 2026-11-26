// Runs fixed-step V-wing combat and owns the aircraft renderer for one battle.
using Godot;

namespace MassiveRts;

public partial class AirSquadron : Node3D
{
    internal const int MaxPerFaction = 64;
    private const float CruiseAltitude = 12f;
    private const float Speed = 19f;
    private const float TurnRate = 1.55f;
    private const float AcquireRange = 96f;
    private const float FireRange = 27f;
    private const float FiringAlignment = 0.978f;
    private const float ShotInterval = 0.085f;
    private const int BurstShots = 4;

    internal struct Fighter
    {
        internal byte Faction;
        internal int Target, Shots;
        internal Vector3 Position, Forward, TracerEnd;
        internal float Health, Heading, Bank, AltitudeOffset, TurnAway, Reacquire;
        internal float ShotDelay, TracerLife, Evade, EvadeCooldown;
    }

    private Fighter[] fighters;
    private AirSpatialGrid grid;
    private AirSquadronVisuals visuals;
    private TerrainHeightField terrain;
    private float boundary, clearance;
    private float burstDamage, elapsed;
    private int blueAlive, redAlive;
    internal bool DogfightActive => blueAlive > 0 && redAlive > 0;
    internal int Alive(byte faction) => faction == Factions.Blue ? blueAlive : redAlive;

    internal void Initialize(int blue, int red, float mapHalfWidth, TerrainHeightField heightField,
        float minimumAltitude, DamageRules damage)
    {
        blueAlive = blue;
        redAlive = red;
        terrain = heightField;
        boundary = mapHalfWidth - 2f;
        clearance = minimumAltitude;
        burstDamage = damage.Resolve(9f, damage.DamageType("Standard"), damage.ArmourClass("Light"), 1f);
        fighters = new Fighter[blue + red];
        grid = new AirSpatialGrid(fighters.Length);
        SpawnFlight(0, blue, Factions.Blue, 1f);
        SpawnFlight(blue, red, Factions.Red, -1f);
        visuals = new AirSquadronVisuals(fighters.Length);
        AddChild(visuals);
        visuals.Sync(fighters, terrain);
    }

    private void SpawnFlight(int start, int count, byte faction, float direction)
    {
        int columns = Mathf.Min(8, count);
        float spacing = Mathf.Min(3.6f, boundary / 10f);
        for (int i = 0; i < count; i++)
        {
            int id = start + i;
            float offset = (id * 0.618034f % 1f) * 2f - 1f;
            float x = (i % columns - (columns - 1) * 0.5f) * spacing;
            float z = -direction * Mathf.Min(boundary - 3f, Mathf.Min(24f, boundary * 0.53f) + i / columns * spacing);
            fighters[id] = new Fighter
            {
                Faction = faction, Health = 75f, Target = -1,
                Position = new Vector3(x, CruiseHeight(x, z) + offset, z),
                Heading = direction > 0f ? 0f : Mathf.Pi, Forward = Vector3.Back * direction,
                AltitudeOffset = offset, Reacquire = (id % 12) * 0.015f
            };
        }
    }

    internal void Step(float delta)
    {
        elapsed += delta;
        grid.Rebuild(fighters);
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
            if (!valid && fighter.Reacquire <= 0f && DogfightActive)
            {
                target = fighter.Target = grid.FindEnemy(fighters, id, AcquireRange);
                fighter.Reacquire = 0.21f + (id % 7) * 0.013f;
                valid = target >= 0;
            }

            Vector3 goal = valid ? fighters[target].Position + fighters[target].Forward * 5f : Vector3.Zero;
            float distanceToEdge = boundary - Mathf.Max(Mathf.Abs(fighter.Position.X), Mathf.Abs(fighter.Position.Z));
            bool nearEdge = distanceToEdge < 18f;
            fighter.TurnAway -= delta;
            Vector3 aim = goal - fighter.Position;
            float wanted = Mathf.Atan2(aim.X, aim.Z);
            if (nearEdge)
                wanted = Mathf.Atan2(-fighter.Position.X, -fighter.Position.Z);
            else if (fighter.TurnAway > 0f)
                wanted = fighter.Heading;
            else if (!valid)
                wanted += 0.9f;
            else if (fighter.Evade > 0f)
                wanted += id % 2 == 0 ? 0.65f : -0.65f;

            float difference = Mathf.Wrap(wanted - fighter.Heading, -Mathf.Pi, Mathf.Pi);
            float turn = Mathf.Clamp(difference, -TurnRate * delta, TurnRate * delta);
            fighter.Heading = Mathf.Wrap(fighter.Heading + turn, -Mathf.Pi, Mathf.Pi);
            fighter.Bank = Mathf.MoveToward(fighter.Bank, -turn / (TurnRate * delta) * 0.52f, delta * 2.4f);
            Vector3 forward = new Vector3(Mathf.Sin(fighter.Heading), 0f, Mathf.Cos(fighter.Heading));

            float dip = fighter.TurnAway > 0f ? 0.8f : valid ? -0.55f : 0f;
            float wave = Mathf.Sin(elapsed * 1.2f + id * 1.9f) * 0.4f;
            Vector3 next = fighter.Position + forward * ((Speed + (id % 5 - 2) * 0.45f) * delta);
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
            if (distance > FireRange * FireRange || fighter.Forward.Dot(toEnemy.Normalized()) < FiringAlignment)
            {
                fighter.Shots = 0;
                continue;
            }
            if (fighter.ShotDelay > 0f) continue;

            fighter.TracerEnd = enemy.Position;
            fighter.TracerLife = 0.075f;
            enemy.Health -= burstDamage;
            if (enemy.Health <= 0f)
            {
                if (enemy.Faction == Factions.Blue) blueAlive--;
                else redAlive--;
            }
            else if (enemy.EvadeCooldown <= 0f)
            {
                enemy.Evade = 0.22f;
                enemy.EvadeCooldown = 2.8f;
            }
            fighter.ShotDelay = ShotInterval;
            if (++fighter.Shots >= BurstShots || enemy.Health <= 0f)
            {
                fighter.Shots = 0;
                fighter.TurnAway = 0.92f;
            }
        }
        visuals.RecordTrails(fighters, delta);
    }

    private float CruiseHeight(float x, float z) => Mathf.Max(clearance, (terrain?.At(x, z) ?? 0f) + CruiseAltitude);

    internal void SyncVisuals() => visuals.Sync(fighters, terrain);
}
