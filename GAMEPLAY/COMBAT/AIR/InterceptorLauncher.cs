// Updates pooled guided AA missiles independently of ground melee and draws a shared exhaust batch.
using Godot;

namespace MassiveRts;

internal sealed class InterceptorLauncher
{
    private const int Capacity = 512;
    private const int ScanSlices = 16;
    private const float FlightTime = 6f;
    private const float Range = 65f;
    private const float Reload = 3.5f;
    private const float Speed = 34f;
    private const float TurnRate = 3.8f;
    private const float HitRadius = 1.15f;
    private const float BurstTime = 0.22f;

    private struct Missile
    {
        internal Vector3 Position, Direction, TrailNear, TrailFar;
        internal int Target;
        internal byte Faction;
        internal float Age, BurstAge, TrailAge, Damage, Scale;
        internal int DamageType;
        internal StructureHealth Source;
        internal bool Bursting;
    }

    private readonly AirSquadron owner;
    private readonly Missile[] missiles = new Missile[Capacity];
    private MultiMesh bodies;
    private ImmediateMesh streaks;
    private float[] readyAt;
    private int count, slice, damageType = -1;
    private float clock;
    private bool uploaded;
    internal bool HasEffects => count > 0;
    internal bool HasCapacity => count < Capacity;

    internal bool TryLaunch(Vector3 muzzle, Vector3 direction, int target, byte faction,
        float damage, int type, float scale = 1f, StructureHealth source = null)
    {
        if (!HasCapacity || target < 0) return false;
        missiles[count++] = new Missile { Position = muzzle, Direction = direction, Target = target,
            Faction = faction, Damage = damage, DamageType = type, Scale = scale, Source = source,
            TrailNear = muzzle, TrailFar = muzzle };
        return true;
    }

    internal InterceptorLauncher(AirSquadron owner) => this.owner = owner;

    internal static Transform3D LocalPose(float height) =>
        new(new Basis(Vector3.Right, Mathf.DegToRad(64f)), new Vector3(0f, height * 0.22f, height * 0.5f));

    internal void Step(float delta, UnitState ground, AirSquadron.Fighter[] fighters,
        AirSpatialGrid grid, DamageRules damageRules, TerrainHeightField terrain)
    {
        clock += delta;
        for (int i = count - 1; i >= 0; i--)
        {
            ref var missile = ref missiles[i];
            if (missile.Bursting)
            {
                missile.BurstAge += delta;
                if (missile.BurstAge >= BurstTime) missiles[i] = missiles[--count];
                continue;
            }

            float remaining = FlightTime - missile.Age;
            float step = Mathf.Min(delta, remaining);
            missile.Age += step;
            ref readonly var target = ref fighters[missile.Target];
            if (missile.Age > 0.12f && target.Health > 0f)
            {
                float lead = Mathf.Min(0.45f, missile.Position.DistanceTo(target.Position) / Speed * 0.7f);
                Vector3 aim = target.Position + target.Forward * target.Speed * lead - missile.Position;
                Vector3 axis = missile.Direction.Cross(aim.Normalized());
                if (axis.LengthSquared() > 0.000001f)
                    missile.Direction = missile.Direction.Rotated(axis.Normalized(),
                        Mathf.Min(missile.Direction.AngleTo(aim), TurnRate * step)).Normalized();
                else if (missile.Direction.Dot(aim) < 0f)
                {
                    Vector3 side = Mathf.Abs(missile.Direction.Y) > 0.9f ? Vector3.Right : Vector3.Up;
                    missile.Direction = missile.Direction.Rotated(missile.Direction.Cross(side).Normalized(), TurnRate * step);
                }
            }

            Vector3 from = missile.Position;
            Vector3 travel = missile.Direction * (Speed * step);
            int contact = target.Health > 0f ? missile.Target :
                grid.FindEnemy(fighters, from + travel * 0.5f, missile.Faction, HitRadius + travel.Length() * 0.5f);
            float hit = float.PositiveInfinity;
            if (contact >= 0)
            {
                Vector3 offset = fighters[contact].Position - from;
                float along = Mathf.Clamp(offset.Dot(missile.Direction), 0f, travel.Length());
                if ((offset - missile.Direction * along).LengthSquared() <= HitRadius * HitRadius)
                    hit = along / Mathf.Max(0.0001f, travel.Length());
            }
            float surface = terrain?.SurfaceHit(from, travel, 1f) ?? float.PositiveInfinity;
            if (hit <= 1f && hit <= surface)
            {
                missile.Position += travel * hit;
                owner.ApplyDamage(contact, missile.Damage, missile.DamageType);
                if (fighters[contact].Health <= 0f && missile.Source != null) missile.Source.Kills++;
                missile.Bursting = true;
            }
            else if (surface <= 1f)
            {
                missile.Position += travel * surface;
                missile.Bursting = true;
            }
            else
            {
                missile.Position += travel;
                missile.Bursting = step >= remaining;
            }

            missile.TrailAge += step;
            if (missile.TrailAge >= 0.048f)
            {
                missile.TrailFar = missile.TrailNear;
                missile.TrailNear = from;
                missile.TrailAge %= 0.048f;
            }
        }

        bool blueTargets = owner.Alive(Factions.Red) > 0;
        bool redTargets = owner.Alive(Factions.Blue) > 0;
        if ((!blueTargets && !redTargets) || count == Capacity || ground?.InterceptorIds == null || ground.InterceptorIds.Count == 0) return;
        var launchers = ground.InterceptorIds;
        int slices = Mathf.Min(ScanSlices, (launchers.Count + 3) / 4);
        slice = (slice + 1) % slices;
        for (int index = slice; index < launchers.Count && count < Capacity; index += slices)
        {
            int id = launchers[index];
            if (ground.Health[id] <= 0f) continue;
            byte faction = ground.Faction[id];
            if ((faction == Factions.Blue && !blueTargets) || (faction == Factions.Red && !redTargets)) continue;
            if (readyAt != null && clock < readyAt[id]) continue;
            float boost = ground.AttackMultiplier(id);
            int target = grid.FindEnemy(fighters, ground.Position[id], faction, Range * boost);
            if (target < 0) continue;

            readyAt ??= new float[ground.Health.Length];
            readyAt[id] = clock + Reload;
            if (damageType < 0) damageType = damageRules.DamageType("Standard");
            float height = ground.Catalog[RuntimeUnitCatalog.Interceptor].Height;
            Vector2 facing = ground.Facing[id];
            var basis = facing == Vector2.Zero ? Basis.Identity :
                new Basis(Vector3.Up, Mathf.Atan2(-facing.X, -facing.Y));
            var pose = LocalPose(height);
            Vector3 muzzle = ground.Position[id] + Vector3.Up * (height * 0.5f) +
                basis * (pose * new Vector3(0f, 0f, -height));
            TryLaunch(muzzle, -(basis * pose.Basis.Z), target, faction, 26f * boost, damageType);
        }
    }

    internal void Sync()
    {
        if (count == 0 && !uploaded) return;
        if (bodies == null)
        {
            bodies = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = BomberPayloadMesh.Get(),
                InstanceCount = Capacity, VisibleInstanceCount = 0
            };
            owner.AddChild(new MultiMeshInstance3D
            {
                Multimesh = bodies, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            });
            streaks = new ImmediateMesh();
            owner.AddChild(new MeshInstance3D
            {
                Mesh = streaks, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                MaterialOverride = new StandardMaterial3D
                {
                    VertexColorUseAsAlbedo = true, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
                }
            });
        }
        streaks.ClearSurfaces();
        if (count > 0) streaks.SurfaceBegin(Mesh.PrimitiveType.Lines);
        int slot = 0;
        for (int i = 0; i < count; i++)
        {
            ref readonly var missile = ref missiles[i];
            Color tint = Factions.Color(missile.Faction);
            if (missile.Bursting)
            {
                float progress = missile.BurstAge / BurstTime;
                float size = (0.15f + progress * 1.25f) * missile.Scale;
                Spark(missile.Position, Vector3.Right * size, tint, 1f - progress);
                Spark(missile.Position, Vector3.Up * size, tint, 1f - progress);
                Spark(missile.Position, Vector3.Back * size, tint.Lightened(0.6f), 1f - progress);
                continue;
            }
            Vector3 up = Mathf.Abs(missile.Direction.Y) > 0.98f ? Vector3.Right : Vector3.Up;
            var pose = Basis.LookingAt(missile.Direction, up) * Basis.FromScale(new Vector3(0.56f, 0.56f, 0.85f) * missile.Scale);
            bodies.SetInstanceTransform(slot++, new Transform3D(pose, missile.Position));
            Vector3 tail = missile.Position - missile.Direction * (0.43f * missile.Scale);
            Line(tail, tail - missile.Direction * (0.62f * missile.Scale), Colors.White, 0.95f, 0.2f);
            Line(tail, missile.TrailNear, tint, 0.92f, 0.36f);
            Line(missile.TrailNear.Lerp(missile.TrailFar, 0.28f), missile.TrailFar, tint, 0.38f, 0f);
        }
        if (count > 0) streaks.SurfaceEnd();
        bodies.VisibleInstanceCount = slot;
        uploaded = count > 0;
    }

    private void Spark(Vector3 center, Vector3 ray, Color tint, float alpha)
    {
        Line(center - ray, center, tint, 0f, alpha);
        Line(center, center + ray, tint, alpha, 0f);
    }

    private void Line(Vector3 start, Vector3 end, Color tint, float near, float far)
    {
        tint.A = near;
        streaks.SurfaceSetColor(tint);
        streaks.SurfaceAddVertex(start);
        tint.A = far;
        streaks.SurfaceSetColor(tint);
        streaks.SurfaceAddVertex(end);
    }
}
