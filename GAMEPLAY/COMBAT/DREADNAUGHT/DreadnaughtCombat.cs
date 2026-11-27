// Updates the rare Dreadnaught hulls and their independently aiming weapons.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal partial class DreadnaughtCombat : Node3D
{
    private const int GunsPerHull = 4;
    private const int CandidateLimit = 4096;
    private const float BarrelLength = 1.35f;
    private const float TargetRefresh = 0.2f;

    private readonly UnitState units;
    private readonly Hull[] hulls;
    private float time;

    private sealed class Hull
    {
        public readonly int Id;
        public readonly Dictionary<int, float> NextContact = new();
        public readonly Gun[] Guns;
        public readonly int Miniguns;
        public Hull(int id, int miniguns, int rockets)
        {
            Id = id;
            Miniguns = miniguns;
            Guns = new Gun[miniguns + rockets];
        }
    }

    private struct Gun
    {
        public int Target;
        public float RefreshAt, Cooldown, Yaw, VisualScale;
        public MeshInstance3D Model;
        public MountedRocket? Rocket;
    }

    // =========================================================
    public static DreadnaughtCombat Create(UnitState units)
    {
        var ids = new List<int>();
        for (int i = 0; i < units.Count; i++)
            if (units.Type[i] == RuntimeUnitCatalog.Dreadnaught ||
                units.Catalog.IndependentRockets(units.Type[i]).Length > 0) ids.Add(i);
        return ids.Count == 0 ? null : new DreadnaughtCombat(units, ids);
    }

    public DreadnaughtCombat() { }

    private DreadnaughtCombat(UnitState units, List<int> ids)
    {
        this.units = units;
        hulls = new Hull[ids.Count];
        var housingMaterial = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color("#344353") };
        var baseMaterial = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color("#243747") };
        var capMaterial = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color("#657B8D") };
        // All hulls reuse the same turret meshes and materials.
        var housing = new BoxMesh { Material = housingMaterial, Size = new Vector3(0.88f, 0.38f, 0.88f) };
        var basePlate = new BoxMesh { Material = baseMaterial, Size = new Vector3(1.08f, 0.13f, 1.08f) };
        var topCap = new BoxMesh { Material = capMaterial, Size = new Vector3(0.57f, 0.055f, 0.55f) };
        var barrel = new BoxMesh { Material = baseMaterial, Size = new Vector3(0.16f, 0.16f, BarrelLength) };
        var rocketHousing = new BoxMesh { Material = housingMaterial, Size = new Vector3(1.36f, 0.68f, 1.30f) };
        var rocketPedestal = new BoxMesh { Material = baseMaterial, Size = new Vector3(1.6f, 0.68f, 1.5f) };
        using var rocketSurface = new SurfaceTool();
        rocketSurface.Begin(Mesh.PrimitiveType.Triangles);
        rocketSurface.SetMaterial(capMaterial);
        FacetedSoldierMeshes.AppendRocketBarrel(rocketSurface, Transform3D.Identity);
        var rocketBarrel = rocketSurface.Commit();
        for (int i = 0; i < ids.Count; i++)
        {
            byte type = units.Type[ids[i]];
            var rockets = units.Catalog.IndependentRockets(type);
            bool legacy = type == RuntimeUnitCatalog.Dreadnaught && units.Catalog.Definition(type).UseLegacyMiniguns;
            var hull = hulls[i] = new Hull(ids[i], legacy ? GunsPerHull : 0, rockets.Length);
            // The reference half-width is 4 for an 8-unit authored hull.
            // Using the compiled outline keeps both the guns and their muzzles
            // in proportion when the Dreadnaught SizeMultiplier changes.
            float visualScale = units.Catalog[type].Outline.RadiusX / 4f;
            for (int slot = 0; slot < hull.Guns.Length; slot++)
            {
                MountedRocket? mount = slot >= hull.Miniguns ? rockets[slot - hull.Miniguns] : null;
                bool rocket = mount is { TwinMinigunModel: false };
                var model = new MeshInstance3D { Mesh = rocket ? rocketHousing : housing, Scale = Vector3.One * visualScale };
                model.AddChild(new MeshInstance3D { Mesh = rocket ? rocketPedestal : basePlate,
                    Position = Vector3.Down * (rocket ? 0.55f : 0.25f) });
                if (rocket)
                {
                    var rocketPose = mount.Value.Pose;
                    Vector3 size = new(rocketPose.Basis.X.Length(), rocketPose.Basis.Y.Length(), rocketPose.Basis.Z.Length());
                    model.AddChild(new MeshInstance3D { Mesh = rocketBarrel, Scale = size / visualScale });
                }
                else
                {
                    if (mount is MountedRocket minigun)
                    {
                        Basis pose = minigun.Pose.Basis;
                        model.Scale = new Vector3(pose.X.Length() / 0.16f, pose.Y.Length() / 0.16f,
                            pose.Z.Length() / BarrelLength);
                    }
                    model.AddChild(new MeshInstance3D { Mesh = topCap, Position = new Vector3(0f, 0.22f, 0.03f) });
                    for (int side = -1; side <= 1; side += 2)
                        model.AddChild(new MeshInstance3D { Mesh = barrel,
                            Position = new Vector3(side * 0.17f, 0.025f, -BarrelLength * 0.5f) });
                }
                AddChild(model);
                hull.Guns[slot] = new Gun { Model = model, VisualScale = visualScale, Rocket = mount,
                    Yaw = mount.HasValue ? InitialYaw(ids[i], mount.Value) : 0f,
                    Target = -1, RefreshAt = ((i + slot) % GunsPerHull) * TargetRefresh / GunsPerHull };
            }
        }
    }

    public void SyncVisuals()
    {
        foreach (var hull in hulls)
        {
            int id = hull.Id;
            bool alive = units.Health[id] > 0;
            for (int slot = 0; slot < hull.Guns.Length; slot++)
            {
                ref var gun = ref hull.Guns[slot];
                gun.Model.Visible = alive;
                if (!alive) continue;
                gun.Model.Position = Mount(id, slot, gun.Rocket);
                gun.Model.Rotation = new Vector3(0, gun.Yaw, 0);
            }
        }
    }

    private Vector3 Mount(int id, int slot, MountedRocket? rocket)
    {
        ref readonly var stats = ref units.Catalog[units.Type[id]];
        if (rocket is MountedRocket cannon)
            return units.Position[id] + Vector3.Up * (stats.Height * 0.5f) +
                MountedRocket.HullBasis(units.Facing[id], stats.Shape) * cannon.Pose.Origin;

        Vector2 forward = Forward(units, id);
        Vector2 right = new(-forward.Y, forward.X);
        Vector2 offset = right * (stats.Outline.RadiusX * (slot % 2 == 0 ? -0.68f : 0.68f)) +
            forward * (stats.Outline.RadiusZ * (slot < 2 ? 0.36f : -0.64f));
        return units.Position[id] + new Vector3(offset.X, stats.Height + 0.2f, offset.Y);
    }

    private float InitialYaw(int id, MountedRocket rocket)
    {
        ref readonly var type = ref units.Catalog[units.Type[id]];
        Vector3 forward = MountedRocket.HullBasis(units.Facing[id], type.Shape) * (rocket.Pose.Basis * Vector3.Forward);
        return Mathf.Atan2(-forward.X, -forward.Z);
    }

    // =========================================================
    public void Step(SpatialGrid grid, ProjectileSystem projectiles, ObstacleField obstacles, float dt, float padding, float fireRate, ObstacleField movementObstacles = null)
    {
        time += dt;
        foreach (var hull in hulls)
        {
            if (units.Health[hull.Id] <= 0) continue;
            if (units.Type[hull.Id] == RuntimeUnitCatalog.Dreadnaught) Contact(hull, grid, obstacles, movementObstacles ?? obstacles, padding);
            for (int slot = 0; slot < hull.Guns.Length; slot++)
                StepGun(hull.Id, slot, ref hull.Guns[slot], grid, projectiles, obstacles, dt, padding, fireRate);
        }
    }

    private void StepGun(int id, int slot, ref Gun gun, SpatialGrid grid, ProjectileSystem projectiles,
        ObstacleField obstacles, float dt, float padding, float fireRate)
    {
        bool rocket = gun.Rocket.HasValue;
        var settings = units.Catalog.DreadnaughtGuns;
        var cannon = gun.Rocket.GetValueOrDefault();
        int projectile = rocket ? cannon.Projectile : settings.Projectile;
        float max = rocket ? cannon.MaximumRange :
            float.IsFinite(settings.Max[slot]) ? Mathf.Clamp(settings.Max[slot], 1f, 100f) : 30f;
        float min = rocket ? cannon.MinimumRange :
            float.IsFinite(settings.Min[slot]) ? Mathf.Clamp(settings.Min[slot], 0f, max) : Mathf.Min(3f, max);
        float boost = units.AttackMultiplier(id);
        max *= boost * units.ForestRangeMultiplier(units.Position[id]);
        var flight = rocket ? units.Catalog.Projectiles.Flight(projectile) : default;
        bool indirect = rocket && flight.ObstacleFire == ObstacleFireMode.IndirectFire;
        bool groundAim = rocket && flight.ExplosionRadius > 0f;
        Vector3 mount = Mount(id, slot, gun.Rocket);
        Vector3 Aim(int target) => groundAim
            ? units.Position[target]
            : units.Position[target] + Vector3.Up * (units.Catalog[units.Type[target]].Height * 0.5f);
        gun.Cooldown = Mathf.Max(0, gun.Cooldown - dt);
        if (time >= gun.RefreshAt)
        {
            gun.RefreshAt += TargetRefresh;
            gun.Target = -1;
            float best = max * max;
            var candidates = grid.GetCandidates(mount, max + padding, units.Faction[id]);
            int checkedCount = 0;
            while (checkedCount++ < CandidateLimit && candidates.MoveNext())
            {
                int candidate = candidates.Current;
                if (units.Health[candidate] <= 0) continue;
                float distance = ObstacleField.Xz(units.Position[candidate] - mount).LengthSquared();
                if (distance < min * min || distance > best ||
                    (distance == best && gun.Target >= 0 && candidate >= gun.Target) ||
                    (!indirect && obstacles?.BlocksSight(mount, Aim(candidate)) == true)) continue;
                best = distance;
                gun.Target = candidate;
            }
        }

        int target = gun.Target;
        if (target < 0 || units.Health[target] <= 0) return;
        Vector3 aim = Aim(target);
        Vector2 horizontal = ObstacleField.Xz(aim - mount);
        float distanceSquared = horizontal.LengthSquared();
        if (distanceSquared < min * min || distanceSquared > max * max || distanceSquared < 0.00001f) return;
        float desired = Mathf.Atan2(-horizontal.X, -horizontal.Y);
        float error = Mathf.Wrap(desired - gun.Yaw, -Mathf.Pi, Mathf.Pi);
        float turn = Mathf.Pi * 2f * dt;
        gun.Yaw = Mathf.Wrap(gun.Yaw + Mathf.Clamp(error, -turn, turn), -Mathf.Pi, Mathf.Pi);
        if (gun.Cooldown > 0 || Mathf.Abs(error) > turn) return;
        float barrelLength = rocket ? cannon.Pose.Basis.Z.Length() : BarrelLength * gun.VisualScale;
        Vector3 muzzle = mount + Vector3.Forward.Rotated(Vector3.Up, gun.Yaw) * barrelLength;
        if (!indirect && obstacles?.BlocksSight(muzzle, aim) == true) return;
        if (projectiles.LaunchFrom(muzzle, aim, units.Faction[id], projectile,
            (rocket ? cannon.Damage : settings.Damage) * boost, DamageRules.NeutralType))
            units.HitFeedback?.OnAttack(units, id);
        gun.Cooldown = (rocket ? cannon.Interval : settings.Interval) / Mathf.Max(0.01f, fireRate);
    }

    // =========================================================
    private void Contact(Hull hull, SpatialGrid grid, ObstacleField obstacles, ObstacleField movementObstacles, float padding)
    {
        int id = hull.Id;
        ref readonly var stats = ref units.Catalog[units.Type[id]];
        Vector3 center = units.Position[id];
        Vector2 forward = Forward(units, id), right = new(-forward.Y, forward.X);
        var candidates = grid.GetNearbySoldiers(center,
            new Vector2(stats.Outline.RadiusX, stats.Outline.RadiusZ).Length() + units.MaxFootprint + padding);
        int checkedCount = 0;
        while (checkedCount++ < CandidateLimit && candidates.MoveNext())
        {
            int victim = candidates.Current;
            if (units.Health[victim] <= 0 || units.Type[victim] == RuntimeUnitCatalog.Dreadnaught) continue;
            Vector3 position = units.Position[victim];
            Vector2 offset = ObstacleField.Xz(position - center);
            Vector2 local = new(offset.Dot(right), -offset.Dot(forward));
            float radius = units.Catalog[units.Type[victim]].Footprint;
            if (Mathf.Abs(local.X) > stats.Outline.RadiusX + radius + 0.08f ||
                Mathf.Abs(local.Y) > stats.Outline.RadiusZ + radius + 0.08f) continue;
            float clearance = HullClearance(stats.Outline, local, out var outward, out bool prow);
            if (clearance > radius + 0.08f) continue;
            if (prow && units.Faction[victim] != units.Faction[id] &&
                (!hull.NextContact.TryGetValue(victim, out float next) || time >= next) &&
                obstacles?.BlocksSight(center, position) != true)
            {
                hull.NextContact[victim] = time + stats.Interval;
                units.Damage(victim, stats.AgainstUnits(stats.Damage) * units.AttackMultiplier(id), stats.UnitDamageType, melee: true);
                units.HitFeedback?.OnAttack(units, id);
            }

            if (units.Health[victim] <= 0 || clearance >= radius) continue;
            Vector2 from = ObstacleField.Xz(position);
            Vector2 desired = from + (right * outward.X - forward * outward.Y) * Mathf.Min(0.16f, radius - clearance);
            ref readonly var victimType = ref units.Catalog[units.Type[victim]];
            Vector2 safe = movementObstacles?.ForTier(victimType.SizeTier).ClipMove(from, desired, radius) ?? desired;
            units.Wrecks?.CrushAlong(from, safe, victimType);
            units.Position[victim] = new Vector3(safe.X, position.Y, safe.Y);
            units.Grounding?.MarkMoved(victim);
            units.Dirty[victim] = true;
        }
    }

    internal static float DistanceToHull(UnitState units, int hull, Vector3 position)
    {
        Vector2 forward = Forward(units, hull), right = new(-forward.Y, forward.X);
        Vector2 delta = ObstacleField.Xz(position - units.Position[hull]);
        return Mathf.Max(0f, HullClearance(units.Catalog[units.Type[hull]].Outline,
            new Vector2(delta.Dot(right), -delta.Dot(forward)), out _, out _));
    }

    private static Vector2 Forward(UnitState units, int id) =>
        units.Facing[id] == Vector2.Zero ? Vector2.Up : units.Facing[id];

    private static float HullClearance(UnitOutline outline, Vector2 point, out Vector2 outward, out bool prow)
    {
        bool inside = true;
        float best = float.PositiveInfinity;
        outward = Vector2.Zero;
        prow = false;
        for (int i = 0; i < outline.Sides; i++)
        {
            Vector2 start = outline.Vertex(i), edge = outline.Vertex((i + 1) % outline.Sides) - start;
            Vector2 normal = new Vector2(edge.Y, -edge.X).Normalized();
            Vector2 offset = point - start;
            inside &= offset.Dot(normal) <= 0f;
            Vector2 delta = offset - edge * Mathf.Clamp(offset.Dot(edge) / edge.LengthSquared(), 0f, 1f);
            float distance = delta.LengthSquared();
            if (distance >= best) continue;
            best = distance;
            outward = normal;
            prow = i == 0 || i == outline.Sides - 1;
        }
        float length = Mathf.Sqrt(best);
        return inside ? -length : length;
    }
}
