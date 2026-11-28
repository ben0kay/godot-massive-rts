// Runs editor support flyovers and temporary repair drones on the battle clock.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed partial class BattleSupport : Node3D
{
    internal enum Ability { Airstrike, Repair }
    internal const float BlastRadius = 14f, RepairRadius = 18f;
    private const float FlightSpeed = 105f, FlightHeight = 49f, DropSeconds = .75f;
    private const float RepairSeconds = 12f, RepairTick = .3f;
    private const int MaxActive = 12;
    private readonly List<Mission> missions = new();
    private readonly Mesh planeMesh, wingMesh, tailMesh, bombMesh, podMesh, droneMesh;
    internal bool Active => missions.Count > 0;

    private sealed class Mission
    {
        internal Ability Ability;
        internal Vector3 Target, Approach;
        internal MeshInstance3D Plane, Payload;
        internal MeshInstance3D[] Drones;
        internal int[] Patients;
        internal float Age, DropAt, ExitAt, RepairIn;
        internal bool Landed;
    }

    internal BattleSupport()
    {
        planeMesh = new CapsuleMesh { Radius = 1.1f, Height = 11f,
            Material = Paint(new Color("#627488")) };
        wingMesh = Box(new Vector3(3.3f, .35f, 12f), "#43556a");
        tailMesh = Box(new Vector3(1.5f, .27f, 5f), "#43556a");
        bombMesh = Box(new Vector3(.8f, 1.5f, .8f), "#d8894a");
        podMesh = Box(new Vector3(2.1f, 1.45f, 2.1f), "#537985");
        droneMesh = Box(new Vector3(.62f, .25f, .46f), "#9de9df");
    }

    private static StandardMaterial3D Paint(Color color) => new()
    {
        AlbedoColor = color, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
    };

    private static Mesh Box(Vector3 size, string color) =>
        new BoxMesh { Size = size, Material = Paint(new Color(color)) };

    private MeshInstance3D Model(Mesh mesh, Vector3 position)
    {
        var node = new MeshInstance3D { Mesh = mesh, Position = position,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(node);
        return node;
    }

    internal bool Call(Ability ability, Vector3 target, float boundary)
    {
        if (missions.Count >= MaxActive) return false;
        Vector3 approach = new(-boundary - 16f, target.Y + FlightHeight, target.Z);
        var plane = Model(wingMesh, approach);
        plane.AddChild(new MeshInstance3D { Mesh = planeMesh, RotationDegrees = new Vector3(0f, 0f, -90f) });
        plane.AddChild(new MeshInstance3D { Mesh = tailMesh, Position = new Vector3(-3.8f, .35f, 0f) });
        missions.Add(new Mission
        {
            Ability = ability, Target = target, Approach = approach, Plane = plane,
            DropAt = (target.X - approach.X) / FlightSpeed,
            ExitAt = 2f * (boundary + 16f) / FlightSpeed
        });
        return true;
    }

    internal void Step(UnitState units, SpatialGrid grid, ShockwaveRenderer effects, float dt, float padding, int damageType)
    {
        for (int i = missions.Count - 1; i >= 0; i--)
        {
            Mission mission = missions[i];
            mission.Age += dt;
            if (mission.Plane != null)
            {
                mission.Plane.Position = mission.Approach + Vector3.Right * (mission.Age * FlightSpeed);
                if (mission.Age >= mission.ExitAt) Remove(ref mission.Plane);
            }
            float sinceDrop = mission.Age - mission.DropAt;
            if (sinceDrop < 0f) continue;
            if (!mission.Landed)
            {
                mission.Payload ??= Model(mission.Ability == Ability.Airstrike ? bombMesh : podMesh, mission.Target);
                float progress = Mathf.Clamp(sinceDrop / DropSeconds, 0f, 1f);
                mission.Payload.Position = mission.Target + Vector3.Up * (.72f + FlightHeight * (1f - progress * progress));
                if (progress < 1f) continue;
                mission.Landed = true;
                if (mission.Ability == Ability.Airstrike)
                {
                    Remove(ref mission.Payload);
                    AreaDamage.Radial(units, grid, Factions.Blue, mission.Target, BlastRadius, 180f, .3f, padding, damageType: damageType);
                    effects.Emit(mission.Target, BlastRadius, mission.Target + Vector3.Up, Factions.Blue);
                    effects.EmitScorch(mission.Target, BlastRadius, units.Grounding?.Field);
                }
                else
                {
                    mission.Drones = new MeshInstance3D[3];
                    mission.Patients = new[] { -1, -1, -1 };
                    for (int drone = 0; drone < mission.Drones.Length; drone++)
                        mission.Drones[drone] = Model(droneMesh, mission.Target + Vector3.Up * 1.2f);
                }
            }
            if (mission.Drones != null)
            {
                if (sinceDrop < DropSeconds + RepairSeconds) Repair(mission, units, grid, dt, padding);
                else if (sinceDrop < DropSeconds + RepairSeconds + .55f)
                {
                    foreach (var drone in mission.Drones)
                        drone.Position = drone.Position.MoveToward(units.OnTerrain(new Vector3(drone.Position.X, mission.Target.Y, drone.Position.Z)), 4f * dt);
                }
                else
                {
                    foreach (var drone in mission.Drones)
                    {
                        drone.QueueFree();
                    }
                    mission.Drones = null;
                    Remove(ref mission.Payload);
                }
            }
            if (mission.Plane == null && mission.Payload == null) missions.RemoveAt(i);
        }
    }

    private static void Repair(Mission mission, UnitState units, SpatialGrid grid, float dt, float padding)
    {
        mission.RepairIn -= dt;
        if (mission.RepairIn <= 0f)
        {
            mission.RepairIn += RepairTick;
            System.Array.Fill(mission.Patients, -1);
            var candidates = grid.GetNearbySoldiers(mission.Target, RepairRadius + padding);
            while (candidates.MoveNext())
            {
                int id = candidates.Current;
                if (units.Faction[id] != Factions.Blue || units.Health[id] <= 0f || units.IsAirborne(id) ||
                    units.Health[id] >= units.Catalog[units.Type[id]].Health ||
                    ObstacleField.Xz(units.Position[id] - mission.Target).LengthSquared() > RepairRadius * RepairRadius) continue;
                int best = -1;
                float distance = float.PositiveInfinity;
                for (int drone = 0; drone < mission.Drones.Length; drone++)
                {
                    if (mission.Patients[drone] >= 0) continue;
                    float candidate = mission.Drones[drone].Position.DistanceSquaredTo(units.Position[id]);
                    if (candidate >= distance) continue;
                    best = drone;
                    distance = candidate;
                }
                if (best >= 0) mission.Patients[best] = id;
                if (mission.Patients[0] >= 0 && mission.Patients[1] >= 0 && mission.Patients[2] >= 0) break;
            }
        }

        for (int drone = 0; drone < mission.Drones.Length; drone++)
        {
            int id = mission.Patients[drone];
            bool valid = id >= 0 && units.Health[id] > 0f && !units.IsAirborne(id) &&
                ObstacleField.Xz(units.Position[id] - mission.Target).LengthSquared() <= RepairRadius * RepairRadius;
            float angle = mission.Age * 2f + drone * Mathf.Tau / 3f;
            Vector3 destination = valid ? units.Position[id] :
                units.OnTerrain(mission.Target + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 2.8f);
            Vector3 hover = destination + Vector3.Up * 1.2f;
            var model = mission.Drones[drone];
            model.Position = model.Position.MoveToward(hover, 15f * dt);
            if (valid && model.Position.DistanceSquaredTo(hover) < 2.25f)
                units.RestoreHealth(id, 28f * dt);
        }
    }

    private static void Remove(ref MeshInstance3D model)
    {
        model.QueueFree();
        model = null;
    }
}
