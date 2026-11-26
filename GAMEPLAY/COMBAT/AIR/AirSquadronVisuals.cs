// Batches V-wing models, projected shadows, wingtip wisps and gun tracers.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed partial class AirSquadronVisuals : Node3D
{
    private const int TrailSamples = 14;
    private const float TrailInterval = 0.042f;
    private static readonly Vector3 Wingtip = new(1.72f, 0f, -0.97f);
    private sealed class Batch
    {
        internal MultiMesh Mesh;
        internal int Visible;
    }

    private readonly Dictionary<UnitDefinition, Batch> aircraft = new();
    private readonly MultiMesh shadows;
    private readonly ImmediateMesh streaks = new();
    private readonly Vector3[] trailLeft, trailRight;
    private readonly int[] trailHeads, trailCounts;
    private float trailTimer;

    internal AirSquadronVisuals(AirSquadron.Fighter[] fighters)
    {
        int capacity = fighters.Length;
        var counts = new Dictionary<UnitDefinition, int>();
        foreach (var fighter in fighters) counts[fighter.Unit] = counts.GetValueOrDefault(fighter.Unit) + 1;
        foreach (var (unit, count) in counts)
            aircraft.Add(unit, new Batch { Mesh = MakeBatch(unit.AircraftMesh ?? MakeWing(), count) });
        shadows = MakeBatch(GroundShadowMeshes.Soldier(), capacity, transparent: true);
        trailLeft = new Vector3[capacity * TrailSamples];
        trailRight = new Vector3[trailLeft.Length];
        trailHeads = new int[capacity];
        trailCounts = new int[capacity];
        AddChild(new MeshInstance3D
        {
            Mesh = streaks,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
    }

    private MultiMesh MakeBatch(Mesh mesh, int capacity, bool transparent = false)
    {
        var batch = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true,
            Mesh = mesh, InstanceCount = capacity
        };
        AddChild(new MultiMeshInstance3D
        {
            Multimesh = batch,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true,
                Transparency = transparent ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
        return batch;
    }

    internal static Mesh MakeWing()
    {
        using var mesh = new SurfaceTool();
        mesh.Begin(Mesh.PrimitiveType.Triangles);
        var nose = new Vector3(0f, 0.04f, 1.48f);
        var spine = new Vector3(0f, 0.32f, 0.29f);
        var tail = new Vector3(0f, 0f, -0.75f);
        for (int side = -1; side <= 1; side += 2)
        {
            var tip = new Vector3(side * Wingtip.X, Wingtip.Y, Wingtip.Z);
            var root = new Vector3(side * 0.38f, 0.06f, 0.43f);
            var notch = new Vector3(side * 0.50f, 0f, -0.31f);
            Triangle(nose, root, spine, 1f);
            Triangle(root, notch, spine, 0.82f);
            Triangle(notch, tail, spine, 0.64f);
            Triangle(root, tip, notch, 0.95f);
            Triangle(tip, tip.Lerp(root, 0.20f), tip.Lerp(notch, 0.26f), 1f, 0.025f);
        }
        return mesh.Commit();

        void Triangle(Vector3 a, Vector3 b, Vector3 c, float light, float lift = 0f)
        {
            mesh.SetColor(new Color(light, light, light));
            var offset = Vector3.Up * lift;
            mesh.AddVertex(a + offset);
            mesh.AddVertex(b + offset);
            mesh.AddVertex(c + offset);
        }
    }

    private static Basis Pose(in AirSquadron.Fighter fighter) =>
        Basis.LookingAt(fighter.Forward, Vector3.Up, true).Rotated(fighter.Forward, fighter.Bank);

    internal void RecordTrails(AirSquadron.Fighter[] fighters, float delta)
    {
        trailTimer += delta;
        if (trailTimer < TrailInterval) return;
        trailTimer %= TrailInterval;
        for (int id = 0; id < fighters.Length; id++)
        {
            ref readonly var fighter = ref fighters[id];
            if (fighter.Health <= 0f) continue;
            var basis = Pose(in fighter);
            int sample = id * TrailSamples + trailHeads[id];
            trailLeft[sample] = fighter.Position + basis * new Vector3(-Wingtip.X, Wingtip.Y, Wingtip.Z);
            trailRight[sample] = fighter.Position + basis * Wingtip;
            trailHeads[id] = (trailHeads[id] + 1) % TrailSamples;
            trailCounts[id] = Mathf.Min(trailCounts[id] + 1, TrailSamples);
        }
    }

    internal void Sync(AirSquadron.Fighter[] fighters, TerrainHeightField terrain)
    {
        int live = 0;
        foreach (var batch in aircraft.Values) batch.Visible = 0;
        streaks.ClearSurfaces();
        bool drawing = false;
        for (int id = 0; id < fighters.Length; id++)
        {
            ref readonly var fighter = ref fighters[id];
            if (fighter.Health <= 0f) continue;
            var basis = Pose(in fighter);
            var position = fighter.Position;
            var batch = aircraft[fighter.Unit];
            batch.Mesh.SetInstanceTransform(batch.Visible, new Transform3D(basis, position));
            batch.Mesh.SetInstanceColor(batch.Visible++, ArmyRenderer.FactionColor(fighter.Faction));
            var shadowPosition = new Vector3(position.X, (terrain?.At(position.X, position.Z) ?? 0f) + 0.06f, position.Z);
            shadows.SetInstanceTransform(live, new Transform3D(new Basis(Vector3.Up, fighter.Heading).Scaled(new Vector3(1.6f, 1f, 0.9f)), shadowPosition));
            shadows.SetInstanceColor(live++, new Color(0.03f, 0.05f, 0.08f, 0.22f));

            Vector3 left = position + basis * new Vector3(-Wingtip.X, Wingtip.Y, Wingtip.Z);
            Vector3 right = position + basis * Wingtip;
            for (int age = 0; age < trailCounts[id]; age++)
            {
                int sample = id * TrailSamples + (trailHeads[id] - 1 - age + TrailSamples) % TrailSamples;
                float opacity = 0.5f * (1f - age / (float)TrailSamples);
                float tailOpacity = opacity - 0.5f / TrailSamples;
                Line(left, trailLeft[sample], Colors.White, opacity, tailOpacity);
                Line(right, trailRight[sample], Colors.White, opacity, tailOpacity);
                left = trailLeft[sample];
                right = trailRight[sample];
            }

            if (fighter.TracerLife > 0f)
            {
                float alpha = Mathf.Min(1f, fighter.TracerLife * 16f);
                var amber = new Color(1f, 0.88f, 0.54f);
                Line(position + basis * new Vector3(-0.42f, 0f, 0.72f), fighter.TracerEnd, amber, alpha, alpha * 0.32f);
                Line(position + basis * new Vector3(0.42f, 0f, 0.72f), fighter.TracerEnd, amber, alpha, alpha * 0.32f);
            }
        }
        if (drawing) streaks.SurfaceEnd();
        foreach (var batch in aircraft.Values) batch.Mesh.VisibleInstanceCount = batch.Visible;
        shadows.VisibleInstanceCount = live;

        void Line(Vector3 start, Vector3 end, Color color, float startAlpha, float endAlpha)
        {
            if (!drawing)
            {
                streaks.SurfaceBegin(Mesh.PrimitiveType.Lines);
                drawing = true;
            }
            color.A = startAlpha;
            streaks.SurfaceSetColor(color);
            streaks.SurfaceAddVertex(start);
            color.A = Mathf.Max(endAlpha, 0f);
            streaks.SurfaceSetColor(color);
            streaks.SurfaceAddVertex(end);
        }
    }
}
