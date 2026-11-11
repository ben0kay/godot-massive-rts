// One instanced preview batch per unit type. Edits update packed slots in place.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed class DeploymentPreview
{
    private readonly MultiMesh mesh;
    private readonly float halfHeight;
    private readonly List<SoldierPlacement> placements = new();
    private readonly Dictionary<Vector2I, int> slots = new();

    public DeploymentPreview(Node3D parent, Mesh shape, float height)
    {
        halfHeight = height * 0.5f;
        mesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = shape };
        parent.AddChild(new MultiMeshInstance3D { Multimesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    public void Add(SoldierPlacement placement)
    {
        int slot = placements.Count;
        placements.Add(placement);
        slots.Add(new Vector2I(placement.X, placement.Z), slot);
        if (placements.Count > mesh.InstanceCount)
        {
            mesh.InstanceCount = Math.Max(64, mesh.InstanceCount * 2);
            for (int i = 0; i < placements.Count; i++) Write(i);
        }
        else Write(slot);
        mesh.VisibleInstanceCount = placements.Count;
    }

    public void Remove(Vector2I cell)
    {
        if (!slots.Remove(cell, out int slot)) return;
        int last = placements.Count - 1;
        if (slot != last)
        {
            SoldierPlacement moved = placements[last];
            placements[slot] = moved;
            slots[new Vector2I(moved.X, moved.Z)] = slot;
            Write(slot);
        }
        placements.RemoveAt(last);
        mesh.VisibleInstanceCount = placements.Count;
    }

    public void Clear()
    {
        slots.Clear();
        placements.Clear();
        mesh.VisibleInstanceCount = 0;
    }

    private void Write(int slot)
    {
        SoldierPlacement placement = placements[slot];
        Vector3 position = placement.Position() + Vector3.Up * halfHeight;
        mesh.SetInstanceTransform(slot, new Transform3D(Basis.Identity, position));
        mesh.SetInstanceColor(slot, placement.Faction == 0 ? ArmyRenderer.Blue : ArmyRenderer.Red);
    }
}
