// One compact MultiMesh for stationary shield domes and their contact ripples.
using System;
using Godot;

namespace MassiveRts;

internal sealed class ShieldFieldDomeRenderer
{
    private const int Capacity = DeploymentLayout.MaxBuildings;
    private static Mesh sharedMesh;
    private static ShaderMaterial sharedMaterial;
    private readonly MultiMesh batch;
    private readonly MultiMeshInstance3D view;
    private readonly int[] slots = new int[Capacity];
    private readonly int[] owners = new int[Capacity];
    private int count;

    internal ShieldFieldDomeRenderer(Node3D owner, bool visible)
    {
        Array.Fill(slots, -1);
        sharedMesh ??= CreateHemisphere();
        sharedMaterial ??= new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://WORLD/BUILDINGS/RENDERING/ShieldFieldDome.gdshader")
        };
        batch = new MultiMesh
        {
            Mesh = sharedMesh, TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true, UseCustomData = true, InstanceCount = Capacity, VisibleInstanceCount = 0
        };
        view = new MultiMeshInstance3D
        {
            Name = "ShieldInfluenceDomes", Multimesh = batch, MaterialOverride = sharedMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = visible
        };
        owner.AddChild(view);
    }

    // =========================================================
    // Use the same ellipsoid proportions as the projectile interception test.
    internal void Show(int tower, Vector3 center, float radius, Color faction, Color ripple = default)
    {
        int slot = slots[tower];
        if (slot < 0)
        {
            slot = count++;
            slots[tower] = slot;
            owners[slot] = tower;
        }
        batch.SetInstanceTransform(slot, new Transform3D(
            Basis.Identity.Scaled(new Vector3(radius, radius * StructureShieldDomes.HeightRatio, radius)), center));
        batch.SetInstanceColor(slot, faction);
        batch.SetInstanceCustomData(slot, ripple);
        batch.VisibleInstanceCount = count;
    }

    internal void Hide(int tower)
    {
        int slot = slots[tower];
        if (slot < 0) return;
        int last = --count;
        slots[tower] = -1;
        if (slot != last)
        {
            int moved = owners[last];
            owners[slot] = moved;
            slots[moved] = slot;
            batch.SetInstanceTransform(slot, batch.GetInstanceTransform(last));
            batch.SetInstanceColor(slot, batch.GetInstanceColor(last));
            batch.SetInstanceCustomData(slot, batch.GetInstanceCustomData(last));
        }
        batch.VisibleInstanceCount = count;
    }

    internal void Clear()
    {
        Array.Fill(slots, -1);
        count = 0;
        batch.VisibleInstanceCount = 0;
    }

    internal void SetVisible(bool visible) => view.Visible = visible;

    // =========================================================
    // Open-bottom unit hemisphere, smooth outward normals and clockwise winding.
    // A single front-facing transparent shell avoids double-layer tinting.
    private static Mesh CreateHemisphere()
    {
        const int segments = 48, bands = 12;
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 Point(int ring, int segment)
        {
            float elevation = Mathf.Pi * 0.5f * ring / bands;
            float azimuth = Mathf.Tau * segment / segments;
            float horizontal = Mathf.Cos(elevation);
            return new Vector3(horizontal * Mathf.Cos(azimuth), Mathf.Sin(elevation),
                horizontal * Mathf.Sin(azimuth));
        }
        void Vertex(Vector3 point)
        {
            surface.SetNormal(point.Normalized());
            surface.SetColor(Colors.White);
            surface.AddVertex(point);
        }
        for (int ring = 0; ring < bands; ring++)
        {
            for (int segment = 0; segment < segments; segment++)
            {
                Vector3 a = Point(ring, segment), b = Point(ring, segment + 1);
                if (ring == bands - 1)
                {
                    Vertex(a); Vertex(b); Vertex(Vector3.Up);
                    continue;
                }
                Vector3 c = Point(ring + 1, segment), d = Point(ring + 1, segment + 1);
                Vertex(a); Vertex(b); Vertex(c);
                Vertex(b); Vertex(d); Vertex(c);
            }
        }
        return surface.Commit();
    }
}
