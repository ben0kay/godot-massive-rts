// Draws short-lived faction-colored death puffs and fading ground splats in shared MultiMesh batches.
using System;
using Godot;

namespace MassiveRts;

public partial class DeathEffectRenderer : Node3D
{
    private RuntimeUnitCatalog catalog;
    private MultiMesh[] bodies;
    private int[] bodyCounts;
    private MultiMesh blobs, groundSplats;
    private uint syncedRevision;
    private static readonly Vector3[] PuffDirections =
    {
        new(1f, 0f, 0f), new(0f, 0f, 1f),
        new(-1f, 0f, 0f), new(0f, 0f, -1f)
    };

    public void Initialize(RuntimeUnitCatalog units, Mesh[] meshes)
    {
        catalog = units;
        bodies = new MultiMesh[meshes.Length];
        bodyCounts = new int[meshes.Length];
        for (int type = 0; type < meshes.Length; type++)
            bodies[type] = CreateBatch(meshes[type], DeathFeedbackSystem.Capacity);

        // One existing unshaded per-instance tint shader is shared by puffs and splats.
        var tintMaterial = new ShaderMaterial
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
        var puff = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 5, Rings = 2 };
        puff.Material = tintMaterial;
        blobs = CreateBatch(puff, DeathFeedbackSystem.Capacity * DeathFeedbackSystem.BlobsPerDeath);
        groundSplats = CreateBatch(BuildSplatMesh(tintMaterial), DeathFeedbackSystem.SplatCapacity);
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

    public void Sync(DeathFeedbackSystem pool)
    {
        if (syncedRevision == pool.Revision) return;
        syncedRevision = pool.Revision;
        Array.Clear(bodyCounts);
        int blobCount = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            var effect = pool[i];
            ref readonly UnitStats type = ref catalog[effect.Type];
            if (effect.Age < DeathFeedbackSystem.ShrinkSeconds)
            {
                float progress = effect.Age / DeathFeedbackSystem.ShrinkSeconds;
                float scale = 1f - progress * progress;
                Vector2 facing = effect.Facing;
                bool positiveX = type.Shape == UnitShape.Diamond;
                float yaw = facing == Vector2.Zero || type.Shape == UnitShape.Circle ? 0f :
                    Mathf.Atan2(positiveX ? -facing.Y : -facing.X, positiveX ? facing.X : -facing.Y);
                var pose = new Transform3D(new Basis(Vector3.Up, yaw).Scaled(Vector3.One * scale),
                    effect.Ground + Vector3.Up * (type.Height * 0.5f * scale));
                int slot = bodyCounts[effect.Type]++;
                bodies[effect.Type].SetInstanceTransform(slot, pose);
                bodies[effect.Type].SetInstanceColor(slot, ArmyRenderer.FactionColor(effect.Faction));
            }

            Color factionColor = ArmyRenderer.FactionColor(effect.Faction);
            float time = effect.Age / DeathFeedbackSystem.BlobSeconds;
            float outward = time * (2f - time);
            float angle = (effect.Seed & 255u) * (Mathf.Tau / 256f);
            var rotation = new Basis(Vector3.Up, angle);
            for (int j = 0; j < DeathFeedbackSystem.BlobsPerDeath; j++)
            {
                float variation = ((effect.Seed >> (8 + j * 4)) & 15u) / 15f;
                // A slightly wider spread; normalized lifetime also slows travel to the edge.
                float distance = (0.25f + 0.32f * variation) * outward;
                float height = 0.035f + 0.16f * time * (1f - time);
                Vector3 point = effect.Ground + rotation * PuffDirections[j] * distance + Vector3.Up * height;
                // Preserve the larger DEATH1-B puffs, now in the defeated unit's team color.
                float scale = (0.09f + 0.065f * variation) * (1f + time * 0.45f);
                blobs.SetInstanceTransform(blobCount, new Transform3D(Basis.Identity.Scaled(Vector3.One * scale), point));
                float shade = 0.85f + 0.15f * variation;
                blobs.SetInstanceColor(blobCount++, new Color(factionColor.R * shade,
                    factionColor.G * shade, factionColor.B * shade, 1f - time * time));
            }
        }
        for (int type = 0; type < bodies.Length; type++)
            if (bodies[type].VisibleInstanceCount != bodyCounts[type]) bodies[type].VisibleInstanceCount = bodyCounts[type];
        blobs.VisibleInstanceCount = blobCount;

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
