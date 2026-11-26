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
        internal MultiMesh Mesh, Shadow;
        internal int Visible;
    }

    private readonly Dictionary<UnitDefinition, Batch> aircraft = new();
    private readonly ImmediateMesh streaks = new();
    private readonly AircraftExhaust exhaust;
    private readonly Vector3[] trailLeft, trailRight;
    private readonly int[] trailHeads, trailCounts;
    private float trailTimer;

    internal AirSquadronVisuals(AirSquadron.Fighter[] fighters)
    {
        int capacity = fighters.Length;
        var counts = new Dictionary<UnitDefinition, int>();
        foreach (var fighter in fighters) counts[fighter.Unit] = counts.GetValueOrDefault(fighter.Unit) + 1;
        var visualConfig = UnitVisualConfig.Load();
        foreach (var (unit, count) in counts)
            aircraft.Add(unit, new Batch
            {
                Mesh = MakeBatch(AircraftMesh(unit, visualConfig), count, material: AircraftMaterial(unit, visualConfig)),
                Shadow = MakeBatch(MakeWing(unit.CanBombGround, shadow: true), count, transparent: true)
            });
        int bomberCount = 0, vWingCount = 0;
        foreach (var (unit, count) in counts)
        {
            if (AircraftExhaust.SupportsBomber(unit)) bomberCount += count;
            if (AircraftExhaust.SupportsVWing(unit)) vWingCount += count;
        }
        if ((bomberCount > 0 && (visualConfig.UseBomberEngineFlames || visualConfig.UseBomberSmokeTrails)) ||
            (vWingCount > 0 && visualConfig.UseVWingEngineFlame))
            exhaust = new AircraftExhaust(this, bomberCount, vWingCount, visualConfig);
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

    private MultiMesh MakeBatch(Mesh mesh, int capacity, bool transparent = false, Material material = null)
    {
        var batch = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true,
            Mesh = mesh, InstanceCount = capacity
        };
        AddChild(new MultiMeshInstance3D
        {
            Multimesh = batch,
            MaterialOverride = material ?? new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true,
                Transparency = transparent ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
        return batch;
    }

    private static Mesh legacyFighter, legacyBomber;
    private static Material legacyMaterial;
    private static bool Enhanced(UnitDefinition unit, UnitVisualConfig visuals) =>
        unit.AircraftMesh == null && (unit.Id == "v_wing" && visuals.UseEnhancedVWingMesh ||
            unit.Id == "heavy_bomber" && visuals.UseEnhancedBomberMesh);

    // Shared with deployment and editor previews. Explicit authored meshes always win.
    internal static Mesh AircraftMesh(UnitDefinition unit, UnitVisualConfig visuals)
    {
        if (unit.AircraftMesh != null) return unit.AircraftMesh;
        if (Enhanced(unit, visuals)) return AirframeMeshes.Get(unit.CanBombGround);
        return unit.CanBombGround ? (legacyBomber ??= MakeWing(true)) : (legacyFighter ??= MakeWing(false));
    }

    internal static Material AircraftMaterial(UnitDefinition unit, UnitVisualConfig visuals) =>
        Enhanced(unit, visuals) ? AirframeMeshes.Armor :
        (legacyMaterial ??= new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        });

    private static Vector3 WingtipFor(UnitDefinition unit) => unit.CanBombGround ? new Vector3(3.1f, 0f, -0.62f) : Wingtip;

    private static Mesh MakeWing(bool bomber, bool shadow = false)
    {
        using var mesh = new SurfaceTool();
        mesh.Begin(Mesh.PrimitiveType.Triangles);
        var wingtip = bomber ? new Vector3(3.1f, 0f, -0.62f) : Wingtip;
        var nose = new Vector3(0f, 0.04f, bomber ? 1.70f : 1.48f);
        var spine = new Vector3(0f, 0.32f, 0.29f);
        var tail = new Vector3(0f, 0f, -0.75f);
        for (int side = -1; side <= 1; side += 2)
        {
            var tip = new Vector3(side * wingtip.X, wingtip.Y, wingtip.Z);
            var root = new Vector3(side * 0.38f, 0.06f, 0.43f);
            var notch = new Vector3(side * (bomber ? 1.03f : 0.50f), 0f, bomber ? -1.16f : -0.31f);
            Triangle(nose, root, spine, 1f);
            Triangle(root, notch, spine, 0.82f);
            Triangle(notch, tail, spine, 0.64f);
            Triangle(root, tip, notch, 0.95f);
            if (!shadow) Triangle(tip, tip.Lerp(root, 0.20f), tip.Lerp(notch, 0.26f), 1f, 0.025f);
        }
        return mesh.Commit();

        void Triangle(Vector3 a, Vector3 b, Vector3 c, float light, float lift = 0f)
        {
            mesh.SetColor(new Color(light, light, light));
            if (shadow) a.Y = b.Y = c.Y = 0f;
            var offset = Vector3.Up * lift;
            mesh.AddVertex(a + offset);
            mesh.AddVertex(b + offset);
            mesh.AddVertex(c + offset);
        }
    }

    internal static Basis Pose(in AirSquadron.Fighter fighter) =>
        Basis.LookingAt(fighter.Forward, Vector3.Up, true).Rotated(fighter.Forward, fighter.Bank);

    internal void RecordTrails(AirSquadron.Fighter[] fighters, float delta)
    {
        exhaust?.Advance(fighters, delta);
        trailTimer += delta;
        if (trailTimer < TrailInterval) return;
        trailTimer %= TrailInterval;
        for (int id = 0; id < fighters.Length; id++)
        {
            ref readonly var fighter = ref fighters[id];
            if (fighter.Health <= 0f) continue;
            var basis = Pose(in fighter);
            int sample = id * TrailSamples + trailHeads[id];
            var tip = WingtipFor(fighter.Unit);
            trailLeft[sample] = fighter.Position + basis * new Vector3(-tip.X, tip.Y, tip.Z);
            trailRight[sample] = fighter.Position + basis * tip;
            trailHeads[id] = (trailHeads[id] + 1) % TrailSamples;
            trailCounts[id] = Mathf.Min(trailCounts[id] + 1, TrailSamples);
        }
    }

    internal void Sync(AirSquadron.Fighter[] fighters, TerrainHeightField terrain)
    {
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
            batch.Mesh.SetInstanceColor(batch.Visible, ArmyRenderer.FactionColor(fighter.Faction));
            float groundY = terrain?.At(position.X, position.Z) ?? 0f;
            var offset = GroundShadowMeshes.SceneryOffset(Mathf.Max(0f, position.Y - groundY));
            float shadowX = position.X + offset.X, shadowZ = position.Z + offset.Y;
            var shadowPosition = new Vector3(shadowX, (terrain?.At(shadowX, shadowZ) ?? 0f) + 0.08f, shadowZ);
            batch.Shadow.SetInstanceTransform(batch.Visible, new Transform3D(new Basis(Vector3.Up, fighter.Heading), shadowPosition));
            batch.Shadow.SetInstanceColor(batch.Visible++, new Color(0.025f, 0.035f, 0.055f, 0.42f));

            var tip = WingtipFor(fighter.Unit);
            Vector3 left = position + basis * new Vector3(-tip.X, tip.Y, tip.Z);
            Vector3 right = position + basis * tip;
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
        foreach (var batch in aircraft.Values)
            batch.Mesh.VisibleInstanceCount = batch.Shadow.VisibleInstanceCount = batch.Visible;
        exhaust?.Sync(fighters);

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
