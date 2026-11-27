// Batches terrain-following illumination for clipped Railgun and Prism beams.
using System;
using Godot;

namespace MassiveRts;

internal sealed class BeamGroundGlow
{
    private const int Segments = 12;
    private static readonly Transform3D Hidden = new(Basis.FromScale(Vector3.Zero), Vector3.Zero);
    private static Mesh stripMesh;
    private readonly MultiMesh batch;
    private readonly int[] counts;
    private readonly float width;

    internal BeamGroundGlow(Node3D parent, int capacity, float width)
    {
        this.width = width;
        counts = new int[capacity];
        batch = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true, UseCustomData = true, Mesh = stripMesh ??= BuildBeamGroundMesh(),
            InstanceCount = capacity * Segments, VisibleInstanceCount = 0
        };
        parent.AddChild(new MultiMeshInstance3D
        {
            Multimesh = batch, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new ShaderMaterial
            {
                Shader = GD.Load<Shader>("res://WORLD/BUILDINGS/RENDERING/RailgunGroundGlow.gdshader")
            }
        });
        for (int i = 0; i < batch.InstanceCount; i++) batch.SetInstanceTransform(i, Hidden);
    }

    internal void SetVisibleCount(int count) => batch.VisibleInstanceCount = count * Segments;

    internal void SetTint(int slot, Color tint)
    {
        int first = slot * Segments;
        for (int i = 0; i < counts[slot]; i++) batch.SetInstanceColor(first + i, tint);
    }

    internal void Clear()
    {
        for (int i = 0; i < batch.VisibleInstanceCount; i++) batch.SetInstanceTransform(i, Hidden);
        SetVisibleCount(0);
        Array.Clear(counts);
    }

    private static Mesh BuildBeamGroundMesh()
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        void Vertex(float x, float z)
        {
            surface.SetUV(new Vector2(x + 0.5f, z + 0.5f));
            surface.AddVertex(new Vector3(x, 0f, z));
        }
        Vertex(-0.5f, -0.5f); Vertex(0.5f, -0.5f); Vertex(0.5f, 0.5f);
        Vertex(-0.5f, -0.5f); Vertex(0.5f, 0.5f); Vertex(-0.5f, 0.5f);
        return surface.Commit();
    }

    internal void Write(int slot, Vector3 from, Vector3 to, Color tint, TerrainHeightField terrain)
    {
        Vector3 horizontal = new(to.X - from.X, 0f, to.Z - from.Z);
        float length = horizontal.Length();
        int count = length < 0.001f ? 0 : terrain == null ? 1 :
            Mathf.Clamp(Mathf.CeilToInt(length / 3f), 1, Segments);
        int first = slot * Segments;
        if (count > 0)
        {
            Vector3 right = new Vector3(horizontal.Z, 0f, -horizontal.X) / length * width;
            Vector3 previous = from;
            float previousHeight = terrain?.At(previous.X, previous.Z) ?? 0f;
            for (int i = 0; i < count; i++)
            {
                float t0 = i / (float)count, t1 = (i + 1f) / count;
                Vector3 next = from.Lerp(to, t1);
                float nextHeight = terrain?.At(next.X, next.Z) ?? 0f;
                Vector3 start = new(previous.X, previousHeight + 0.09f, previous.Z);
                Vector3 end = new(next.X, nextHeight + 0.09f, next.Z);
                // Match neighboring endpoints exactly; don't stretch one plane
                // between distant hills. Height fade is interpolated in shader.
                float alpha0 = 1f - Mathf.Clamp((previous.Y - previousHeight) / 8f, 0f, 1f);
                float alpha1 = 1f - Mathf.Clamp((next.Y - nextHeight) / 8f, 0f, 1f);
                int instance = first + i;
                batch.SetInstanceTransform(instance, new Transform3D(
                    new Basis(right, Vector3.Up, end - start), (start + end) * 0.5f));
                batch.SetInstanceColor(instance, tint);
                batch.SetInstanceCustomData(instance, new Color(t0, t1, alpha0, alpha1));
                previous = next;
                previousHeight = nextHeight;
            }
        }
        for (int i = count; i < counts[slot]; i++)
            batch.SetInstanceTransform(first + i, Hidden);
        counts[slot] = count;
    }

    internal void Remove(int slot, int last)
    {
        int count = counts[last];
        if (slot != last)
        {
            int destination = slot * Segments, source = last * Segments;
            for (int i = 0; i < count; i++)
            {
                batch.SetInstanceTransform(destination + i, batch.GetInstanceTransform(source + i));
                batch.SetInstanceColor(destination + i, batch.GetInstanceColor(source + i));
                batch.SetInstanceCustomData(destination + i, batch.GetInstanceCustomData(source + i));
            }
            for (int i = count; i < counts[slot]; i++)
                batch.SetInstanceTransform(destination + i, Hidden);
            counts[slot] = count;
        }
        for (int i = 0; i < count; i++)
            batch.SetInstanceTransform(last * Segments + i, Hidden);
        counts[last] = 0;
    }

}
