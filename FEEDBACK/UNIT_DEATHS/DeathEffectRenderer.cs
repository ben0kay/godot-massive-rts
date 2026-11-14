// Draws the active death pool with shared soldier meshes and one geometric puff mesh.
using System;
using Godot;

namespace MassiveRts;

public partial class DeathEffectRenderer : Node3D
{
    private RuntimeUnitCatalog catalog;
    private MultiMesh[] bodies;
    private int[] bodyCounts;
    private MultiMesh blobs;
    private uint syncedRevision;
    private static readonly Vector3[] PuffDirections =
    {
        new(1f, 0f, 0f), new(-0.5f, 0f, 0.866f), new(-0.5f, 0f, -0.866f)
    };

    public void Initialize(RuntimeUnitCatalog units, Mesh[] meshes)
    {
        catalog = units;
        bodies = new MultiMesh[meshes.Length];
        bodyCounts = new int[meshes.Length];
        for (int type = 0; type < meshes.Length; type++)
            bodies[type] = CreateBatch(meshes[type], DeathFeedbackSystem.Capacity);

        var puff = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 5, Rings = 2 };
        puff.Material = new ShaderMaterial
        {
            Shader = new Shader
            {
                Code = @"shader_type spatial;
render_mode unshaded, depth_draw_never;
varying vec4 tint;
void vertex() { tint = COLOR; }
void fragment() { ALBEDO = tint.rgb; ALPHA = tint.a; }"
            }
        };
        blobs = CreateBatch(puff, DeathFeedbackSystem.Capacity * DeathFeedbackSystem.BlobsPerDeath);
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

            float time = effect.Age / DeathFeedbackSystem.BlobSeconds;
            float outward = time * (2f - time);
            float angle = (effect.Seed & 255u) * (Mathf.Tau / 256f);
            var rotation = new Basis(Vector3.Up, angle);
            for (int j = 0; j < DeathFeedbackSystem.BlobsPerDeath; j++)
            {
                float variation = ((effect.Seed >> (8 + j * 4)) & 15u) / 15f;
                float distance = (0.22f + 0.28f * variation) * outward;
                float height = 0.035f + 0.16f * time * (1f - time);
                Vector3 point = effect.Ground + rotation * PuffDirections[j] * distance + Vector3.Up * height;
                float scale = (0.06f + 0.05f * variation) * (1f + time * 0.45f);
                blobs.SetInstanceTransform(blobCount, new Transform3D(Basis.Identity.Scaled(Vector3.One * scale), point));
                blobs.SetInstanceColor(blobCount++, new Color(0.23f + 0.1f * variation, 0.025f, 0.035f, 1f - time * time));
            }
        }
        for (int type = 0; type < bodies.Length; type++)
            if (bodies[type].VisibleInstanceCount != bodyCounts[type]) bodies[type].VisibleInstanceCount = bodyCounts[type];
        blobs.VisibleInstanceCount = blobCount;
    }
}
