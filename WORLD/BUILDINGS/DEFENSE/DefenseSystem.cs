// Updates stationary defenses against the shared soldier grid and damage pipeline.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed class DefenseSystem
{
    private struct Tower
    {
        public BuildingStats Stats;
        public byte Faction;
        public Vector3 Pivot;
        public ObstacleRectangle Footprint;
        public int Target;
        public float Yaw, Cooldown, BeamRemaining, BeamSlope, LeftRecoil, RightRecoil;
        public byte NextRocketMuzzle;
    }

    private readonly Tower[] towers;
    private readonly DefenseRenderer renderer;
    private readonly StructureHealth[] structures;
    private readonly ObstacleField staticWalls;
    private readonly bool[] removed;
    public IReadOnlyList<StructureHealth> Structures => structures;
    public int ActiveCount { get; private set; }

    // Command mode registers and removes individual towers in bounded, reusable slots.
    public DefenseSystem(DefenseRenderer renderer, int capacity)
    {
        this.renderer = renderer;
        structures = new StructureHealth[capacity];
        towers = new Tower[capacity];
        removed = new bool[capacity];
    }

    public void AddTower(StructureHealth structure, BuildingStats stats, float height, float ground)
    {
        int slot = System.Array.IndexOf(structures, null);
        structures[slot] = structure;
        towers[slot] = CreateTower(structure, stats, height, ground);
        renderer.ShowTower(slot, structure.Area, structure.Faction, height, ground, stats.ProjectileType >= 0, stats.ShieldEmitter.Enabled,
            stats.ShieldEmitter.Radius, stats.Id == PrefabShelterMeshes.Id, stats.Id == TwinMinigunTowerMeshes.Id);
        ActiveCount++;
    }

    internal void RegisterShieldEmitters(UnitState units)
    {
        for (int i = 0; i < towers.Length; i++)
            if (structures[i]?.Destroyed == false && towers[i].Stats.ShieldEmitter.Enabled)
                units.AddShieldTower(structures[i], towers[i].Stats.ShieldEmitter);
    }

    public void RemoveTower(StructureHealth structure)
    {
        int slot = System.Array.IndexOf(structures, structure);
        if (slot < 0) return;
        renderer.HideTower(slot);
        structures[slot] = null;
        ActiveCount--;
    }

    public DefenseSystem(IReadOnlyList<BuildingPlacement> placements, RuntimeBuildingCatalog catalog,
        float height, DefenseRenderer renderer, DamageRules rules, ObstacleField staticWalls, TerrainHeightField terrain = null)
    {
        this.renderer = renderer;
        this.staticWalls = staticWalls;
        structures = new StructureHealth[placements.Count];
        removed = new bool[placements.Count];
        towers = new Tower[placements.Count];
        for (int i = 0; i < towers.Length; i++)
        {
            var placement = placements[i];
            var area = placement.Footprint(catalog);
            var stats = catalog[placement.Type];
            structures[i] = new StructureHealth(area, placement.Faction, stats.Health, rules, stats.ArmourClass, stats.Armour) { IsTower = true };
            float ground = terrain?.FootprintHeight(area.Min, area.Max, out _) ?? 0f;
            towers[i] = CreateTower(structures[i], stats, height, ground);
        }
    }

    private static Tower CreateTower(StructureHealth structure, BuildingStats stats, float height, float ground) =>
        new() { Stats = stats, Faction = structure.Faction, Footprint = structure.Area,
            Pivot = DefenseRenderer.Pivot(structure.Area, height) + Vector3.Up * ground, Target = -1 };

    public bool RemoveDestroyed(out ObstacleField field)
    {
        field = null;
        bool changed = false;
        int alive = 0;
        for (int i = 0; i < structures.Length; i++)
        {
            if (!structures[i].Destroyed) { alive++; continue; }
            if (removed[i]) continue;
            removed[i] = true;
            renderer.HideTower(i);
            changed = true;
        }
        if (!changed) return false;
        field = staticWalls;
        if (alive == 0) return true;

        int wallCount = staticWalls?.RectangleCount ?? 0;
        var rectangles = new ObstacleRectangle[wallCount + alive];
        if (staticWalls != null) staticWalls.Rectangles.CopyTo(rectangles);
        foreach (var structure in structures)
            if (!structure.Destroyed) rectangles[wallCount++] = structure.Area;
        field = new ObstacleField(rectangles);
        return true;
    }

    // =========================================================
    // Run before soldiers move so range queries use the positions indexed this tick.
    public void Step(UnitState units, SpatialGrid grid, ObstacleField obstacles, ProjectileSystem projectiles, float dt, int acquisitionStep, float acquisitionSeconds)
    {
        int refreshInterval = Mathf.Max(1, Mathf.RoundToInt(0.2f / acquisitionSeconds));
        for (int i = 0; i < towers.Length; i++)
        {
            ref Tower tower = ref towers[i];
            TowerWeapon weapon = tower.Stats.Weapon;
            if (structures[i] is not { Destroyed: false } structure) continue;
            renderer.SetDamageCracks(i, structure.Health / structure.MaxHealth, tower.Stats.ShieldEmitter.Enabled);
            if (weapon == TowerWeapon.None) continue;
            bool minigun = tower.Stats.Id == TwinMinigunTowerMeshes.Id && weapon == TowerWeapon.Projectile;
            bool rocket = weapon == TowerWeapon.Projectile && !minigun;
            float oldYaw = tower.Yaw, oldSlope = tower.BeamSlope, left = tower.LeftRecoil, right = tower.RightRecoil;
            float returnSpeed = rocket ? 1.25f : 1.8f;
            tower.LeftRecoil = Mathf.MoveToward(left, 0f, returnSpeed * dt);
            tower.RightRecoil = Mathf.MoveToward(right, 0f, returnSpeed * dt);
            if (tower.BeamRemaining <= 0f && tower.Cooldown > 0f) tower.Cooldown -= dt;

            if (grid == null || tower.Target < 0 || !Valid(in tower, units, tower.Target, obstacles))
            {
                tower.Target = -1;
                tower.Cooldown = Mathf.Max(0f, tower.Cooldown);
                if (grid != null && acquisitionStep >= 0 && i % refreshInterval == acquisitionStep % refreshInterval)
                    Acquire(ref tower, units, grid, obstacles);
            }

            bool aligned = false;
            if (tower.Target >= 0)
            {
                Vector3 offset = units.Position[tower.Target] - tower.Pivot;
                float desired = Mathf.Atan2(-offset.X, -offset.Z);
                float error = Mathf.Wrap(desired - tower.Yaw, -Mathf.Pi, Mathf.Pi);
                float step = tower.Stats.TurnRate * dt;
                tower.Yaw = Mathf.Wrap(tower.Yaw + Mathf.Clamp(error, -step, step), -Mathf.Pi, Mathf.Pi);
                aligned = Mathf.Abs(error) <= step;
                if (!rocket)
                {
                    Vector3 target = units.Position[tower.Target] + Vector3.Up * (units.Catalog[units.Type[tower.Target]].Height * 0.5f);
                    float slope = (target.Y - tower.Pivot.Y) / Mathf.Max(1f, ObstacleField.Xz(offset).Length());
                    tower.BeamSlope = Mathf.MoveToward(tower.BeamSlope, Mathf.Clamp(slope, -1f, 1f), step);
                    if (tower.BeamRemaining <= 0f) tower.BeamSlope = Mathf.Clamp(slope, -1f, 1f);
                }
            }

            if (aligned && tower.Cooldown <= 0f && tower.BeamRemaining <= 0f)
            {
                if (minigun)
                {
                    Vector3 muzzle = DefenseRenderer.MinigunMuzzle(tower.Pivot, tower.Yaw, tower.BeamSlope, tower.NextRocketMuzzle);
                    Vector3 aim = units.Position[tower.Target] + Vector3.Up *
                        (units.Catalog[units.Type[tower.Target]].Height * 0.5f);
                    // The short barrels sit inside the tower footprint. Ignore only
                    // that rectangle; direct rounds still stop at cover and terrain.
                    if (projectiles?.LaunchFrom(muzzle, aim, tower.Faction, tower.Stats.ProjectileType,
                        tower.Stats.Damage, tower.Stats.DamageType, tower.Footprint) == true)
                    {
                        renderer.EmitFlash(muzzle, tower.Yaw, 0.45f);
                        tower.NextRocketMuzzle ^= 1;
                        tower.Cooldown = Mathf.Max(0f, tower.Cooldown + tower.Stats.Interval);
                    }
                }
                else if (rocket)
                {
                    bool useLeft = tower.NextRocketMuzzle == 0;
                    if (projectiles?.LaunchFrom(DefenseRenderer.RocketMuzzle(tower.Pivot, tower.Yaw,
                        tower.NextRocketMuzzle, useLeft ? tower.LeftRecoil : tower.RightRecoil),
                        units.Position[tower.Target], tower.Faction, tower.Stats.ProjectileType,
                        tower.Stats.Damage, tower.Stats.DamageType) == true)
                    {
                        if (useLeft) tower.LeftRecoil = 0.30f;
                        else tower.RightRecoil = 0.30f;
                        tower.NextRocketMuzzle ^= 1;
                        tower.Cooldown = Mathf.Max(0f, tower.Cooldown + tower.Stats.Interval);
                    }
                }
                else
                {
                    tower.LeftRecoil = 0.64f;
                    tower.BeamRemaining = tower.Stats.BeamSeconds;
                    tower.Cooldown = tower.Stats.Interval;
                    renderer.EmitFlash(DefenseRenderer.Muzzle(tower.Pivot, tower.Yaw, tower.LeftRecoil, tower.BeamSlope), tower.Yaw);
                }
            }

            if (oldYaw != tower.Yaw || oldSlope != tower.BeamSlope || left != tower.LeftRecoil || right != tower.RightRecoil)
                renderer.Aim(i, tower.Pivot, tower.Yaw, tower.LeftRecoil, tower.RightRecoil, tower.BeamSlope);
            if (tower.BeamRemaining <= 0f) continue;
            float firingSeconds = Mathf.Min(dt, tower.BeamRemaining);
            TraceBeam(i, in tower, units, grid, obstacles, tower.Stats.Damage * firingSeconds);
            tower.BeamRemaining -= firingSeconds;
            if (tower.BeamRemaining <= 0f) renderer.HideBeam(i);
        }
    }

    private static void Acquire(ref Tower tower, UnitState units, SpatialGrid grid, ObstacleField obstacles)
    {
        float nearest = float.PositiveInfinity;
        var candidates = grid.GetCandidates(tower.Pivot, tower.Stats.MaximumRange, tower.Faction);
        while (candidates.MoveNext())
        {
            int id = candidates.Current;
            if (!InRange(in tower, units, id, out float distance) || distance >= nearest ||
                !Visible(in tower, units.Position[id], obstacles)) continue;
            tower.Target = id;
            nearest = distance;
        }
    }

    // =========================================================
    // Check every hostile footprint intersected by the current beam segment.
    // Soldiers do not occlude one another, but cover and terrain stop the segment.
    private void TraceBeam(int index, in Tower tower, UnitState units, SpatialGrid grid, ObstacleField obstacles, float damage)
    {
        Vector3 from = DefenseRenderer.Muzzle(tower.Pivot, tower.Yaw, tower.LeftRecoil, tower.BeamSlope);
        Vector3 forward = Vector3.Forward.Rotated(Vector3.Up, tower.Yaw);
        float length = Mathf.Max(0f, tower.Stats.MaximumRange - (from - tower.Pivot).Dot(forward));
        Vector3 segment = (forward + Vector3.Up * tower.BeamSlope) * length;
        float end = obstacles?.FirstSightHitExcept(from, segment, tower.Footprint) ?? 1f;
        end = PiercingBeam.ClipTerrain(units.Grounding?.Field, from, segment, end);
        segment *= end;
        Vector3 to = from + segment;
        Vector3? impact = end < 1f ? to : null;

        Vector2 flat = ObstacleField.Xz(segment);
        float reach = flat.Length();
        if (grid == null || reach <= 0.001f)
        {
            renderer.ShowBeam(index, from, to, tower.Faction, impact);
            return;
        }
        const float beamRadius = 0.14f;
        Vector2 along = flat / reach;
        float slope = segment.Y / reach;
        float nearest = float.PositiveInfinity;
        var candidates = grid.GetSegmentCandidates(from, segment, units.MaxFootprint + beamRadius, tower.Faction);
        while (candidates.MoveNext())
        {
            int id = candidates.Current;
            if (units.Health[id] <= 0f) continue;
            ref readonly var type = ref units.Catalog[units.Type[id]];
            if (!PiercingBeam.Intersect(units.Position[id] - from, along, reach, slope,
                type.Footprint, type.Height, beamRadius, out float entry)) continue;
            nearest = Mathf.Min(nearest, entry);
            units.Damage(id, damage, tower.Stats.DamageType);
        }
        if (!impact.HasValue && float.IsFinite(nearest)) impact = from + segment * (nearest / reach);
        renderer.ShowBeam(index, from, to, tower.Faction, impact);
    }

    private static bool Valid(in Tower tower, UnitState units, int id, ObstacleField obstacles) =>
        InRange(in tower, units, id, out _) && Visible(in tower, units.Position[id], obstacles);

    private static bool InRange(in Tower tower, UnitState units, int id, out float distance)
    {
        distance = ObstacleField.Xz(units.Position[id] - tower.Pivot).LengthSquared();
        return units.Health[id] > 0f && units.Faction[id] != tower.Faction &&
            distance >= tower.Stats.MinimumRange * tower.Stats.MinimumRange &&
            distance <= tower.Stats.MaximumRange * tower.Stats.MaximumRange;
    }

    private static bool Visible(in Tower tower, Vector3 target, ObstacleField obstacles) =>
        !tower.Stats.RequiresLineOfSight || obstacles == null ||
        !obstacles.BlocksSightExcept(tower.Pivot, target, tower.Footprint);
}
