// Draws faceted fragments and ground splats using bounded shared MultiMesh batches.
using Godot;

namespace MassiveRts;

public partial class DeathEffectRenderer : Node3D
{
    private const int InstanceStride = 16;
    private const int SparseLimit = 64;
    private RuntimeUnitCatalog catalog;
    private readonly MultiMesh[] pieces = new MultiMesh[2];
    private readonly float[][] pieceBuffers = new float[2][];
    private MultiMesh groundSplats;
    private uint syncedRevision;

    public void Initialize(RuntimeUnitCatalog units, DeathFeedbackSystem pool)
    {
        catalog = units;
        if (pool.Capacity > 0)
        {
            var material = new ShaderMaterial
            {
                Shader = GD.Load<Shader>("res://VISUALS/FEEDBACK/UNIT_DEATHS/CrumbleFragment.gdshader")
            };
            for (int variant = 0; variant < pieces.Length; variant++)
            {
                // Every event uses the same count of each mesh, so neither batch can overflow.
                int capacity = pool.Capacity * ((pool.FragmentsPerDeath + 1 - variant) / 2);
                pieces[variant] = CreateBatch(BuildFragmentMesh(material, variant), capacity);
                pieceBuffers[variant] = new float[capacity * InstanceStride];
            }
        }

        var splatMaterial = new ShaderMaterial
        {
            Shader = new Shader
            {
                Code = @"shader_type spatial;
render_mode unshaded, depth_draw_never, cull_disabled;
varying vec4 tint;
void vertex() { tint = COLOR; }
void fragment() { ALBEDO = tint.rgb; ALPHA = tint.a; }"
            }
        };
        groundSplats = CreateBatch(BuildSplatMesh(splatMaterial), DeathFeedbackSystem.SplatCapacity);
    }

    // =========================================================
    // Both chunks have a flat base at -0.5 so their resting height matches the terrain.
    private static Mesh BuildFragmentMesh(Material material, int variant)
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(material);
        Vector3 a = new(-0.50f, -0.5f, -0.34f), b = new(0.48f, -0.5f, -0.42f);
        Vector3 c = new(0.37f, -0.5f, 0.44f), d = new(-0.41f, -0.5f, 0.35f);
        Vector3 peak = new(variant == 0 ? -0.17f : 0.22f, 0.5f, -0.19f);
        Vector3 edge = new(0.13f, variant == 0 ? -0.03f : 0.35f, 0.35f);
        AddFacet(surface, a, peak, b, 0.77f);
        AddFacet(surface, b, peak, edge, 0.98f);
        AddFacet(surface, b, edge, c, 0.72f);
        AddFacet(surface, c, edge, d, 0.61f);
        AddFacet(surface, d, edge, peak, 0.88f);
        AddFacet(surface, d, peak, a, 0.66f);
        AddFacet(surface, a, b, c, 0.54f);
        AddFacet(surface, a, c, d, 0.54f);
        return surface.Commit();
    }

    private static void AddFacet(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c, float shade)
    {
        surface.SetColor(new Color(shade, shade, shade));
        surface.AddVertex(a);
        surface.AddVertex(c);
        surface.AddVertex(b);
    }

    // =========================================================
    // A single irregular, flat low-poly stain. Individual deaths only transform
    // and color instances of this one mesh, rather than spawning decals or nodes.
    private static Mesh BuildSplatMesh(Material material)
    {
        float[] radii = { 0.82f, 1.12f, 0.74f, 1.02f, 0.91f, 1.18f,
            0.78f, 1.06f, 0.84f, 1.13f, 0.76f, 0.98f };
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(material);
        surface.SetNormal(Vector3.Up);
        for (int i = 0; i < radii.Length; i++)
        {
            float a = Mathf.Tau * i / radii.Length;
            float b = Mathf.Tau * ((i + 1) % radii.Length) / radii.Length;
            Vector3 first = new(Mathf.Cos(a) * radii[i], 0f, Mathf.Sin(a) * radii[i]);
            Vector3 next = new(Mathf.Cos(b) * radii[(i + 1) % radii.Length], 0f,
                Mathf.Sin(b) * radii[(i + 1) % radii.Length]);
            surface.AddVertex(Vector3.Zero);
            surface.AddVertex(first);
            surface.AddVertex(next);
        }
        return surface.Commit();
    }

    private MultiMesh CreateBatch(Mesh mesh, int capacity)
    {
        var batch = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = mesh,
            InstanceCount = capacity,
            VisibleInstanceCount = 0
        };
        AddChild(new MultiMeshInstance3D { Multimesh = batch, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        return batch;
    }

    // =========================================================
    private void SyncFragments(DeathFeedbackSystem pool)
    {
        if (pool.Capacity == 0) return;
        float halfGravity = pool.Gravity * 0.5f;
        for (int variant = 0; variant < pieces.Length; variant++)
        {
            MultiMesh batch = pieces[variant];
            int visible = pool.Count * ((pool.FragmentsPerDeath + 1 - variant) / 2);
            bool dense = visible > SparseLimit;
            int slot = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                float age = pool.GetAge(i);
                float fade = Mathf.Min(1f, (pool.Lifetime - age) / pool.FadeDuration);
                for (int j = variant; j < pool.FragmentsPerDeath; j += 2)
                {
                    ref readonly var fragment = ref pool.GetFragment(i, j);
                    Transform3D pose = FragmentPose(fragment, age, halfGravity);
                    Color color = fragment.Tint;
                    color.A = fade;
                    if (dense) WriteFragment(pieceBuffers[variant], slot, pose, color);
                    else
                    {
                        batch.SetInstanceTransform(slot, pose);
                        batch.SetInstanceColor(slot, color);
                    }
                    slot++;
                }
            }
            if (dense) batch.Buffer = pieceBuffers[variant];
            if (batch.VisibleInstanceCount != visible) batch.VisibleInstanceCount = visible;
        }
    }

    private static Transform3D FragmentPose(in DeathFeedbackSystem.Fragment fragment, float age, float halfGravity)
    {
        float progress = Mathf.Min(age / fragment.Flight, 1f);
        // Ease the travel to a stop and follow the cached terrain profile.
        float travel = progress * (2f - progress);
        float floorAtStart = fragment.Start.Y - fragment.Lift;
        float floorAtMiddle = fragment.MiddleGround + fragment.Size.Y * 0.5f;
        float floorAtEnd = fragment.EndGround + fragment.Size.Y * 0.5f;
        float y = travel < 0.5f
            ? Mathf.Lerp(floorAtStart, floorAtMiddle, travel * 2f)
            : Mathf.Lerp(floorAtMiddle, floorAtEnd, travel * 2f - 1f);
        float arc = fragment.Lift * (1f - progress) +
            halfGravity * age * (fragment.Flight - age);
        float afterLanding = Mathf.Max(0f, age - fragment.Flight);
        float rebound = Mathf.Max(0f, afterLanding * (fragment.Bounce - halfGravity * afterLanding));
        y += progress < 1f ? arc : rebound;
        Vector3 point = new(fragment.Start.X + fragment.Travel.X * travel, y,
            fragment.Start.Z + fragment.Travel.Y * travel);
        Basis basis = new Basis(Vector3.Up, fragment.Yaw + fragment.Spin * travel);
        return new Transform3D(new Basis(basis.X * fragment.Size.X, basis.Y * fragment.Size.Y,
            basis.Z * fragment.Size.Z), point);
    }

    // Packed MultiMesh transforms are affine rows, while Basis exposes columns.
    private static void WriteFragment(float[] buffer, int slot, Transform3D pose, Color color)
    {
        int i = slot * InstanceStride;
        buffer[i] = pose.Basis.X.X;      buffer[i + 1] = pose.Basis.Y.X;
        buffer[i + 2] = pose.Basis.Z.X;  buffer[i + 3] = pose.Origin.X;
        buffer[i + 4] = pose.Basis.X.Y;  buffer[i + 5] = pose.Basis.Y.Y;
        buffer[i + 6] = pose.Basis.Z.Y;  buffer[i + 7] = pose.Origin.Y;
        buffer[i + 8] = pose.Basis.X.Z;  buffer[i + 9] = pose.Basis.Y.Z;
        buffer[i + 10] = pose.Basis.Z.Z; buffer[i + 11] = pose.Origin.Z;
        buffer[i + 12] = color.R; buffer[i + 13] = color.G;
        buffer[i + 14] = color.B; buffer[i + 15] = color.A;
    }

    public void Sync(DeathFeedbackSystem pool)
    {
        if (syncedRevision == pool.Revision) return;
        syncedRevision = pool.Revision;
        SyncFragments(pool);
        for (int i = 0; i < pool.SplatCount; i++)
        {
            var splat = pool.GetSplat(i);
            float variation = ((splat.Seed >> 8) & 255u) / 255f;
            float radius = Mathf.Clamp(catalog[splat.Type].Footprint * (0.55f + 0.20f * variation),
                0.30f, 0.78f);
            float angle = (splat.Seed & 255u) * (Mathf.Tau / 256f);
            Basis basis = new Basis(Vector3.Up, angle).Scaled(new Vector3(radius, 1f,
                radius * (0.84f + 0.24f * variation)));
            groundSplats.SetInstanceTransform(i, new Transform3D(basis,
                splat.Ground + Vector3.Up * 0.028f));
            float fade = Mathf.Clamp((DeathFeedbackSystem.SplatSeconds - splat.Age) /
                DeathFeedbackSystem.SplatFadeSeconds, 0f, 1f);
            Color color = ArmyRenderer.FactionColor(splat.Faction).Darkened(0.20f);
            groundSplats.SetInstanceColor(i, new Color(color.R, color.G, color.B, 0.64f * fade));
        }
        groundSplats.VisibleInstanceCount = pool.SplatCount;
    }
}
