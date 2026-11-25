// One instanced preview batch per unit type. Edits update packed slots in place.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed class DeploymentPreview
{
    private readonly MultiMesh mesh;
    private readonly float halfHeight;
    internal TerrainHeightField Terrain { get; set; }
    private const float PopSpeed = 0.75f;
    private const float PeakTime = 0.11f;
    private const float DipTime = 0.20f;
    private const float ReboundTime = 0.27f;
    private const float SettleTime = 0.36f;
    private float animationTime;
    private readonly Dictionary<Vector2I, float> popping = new();
    private readonly List<Vector2I> settled = new();
    private readonly List<SoldierPlacement> placements = new();
    private readonly Dictionary<Vector2I, int> slots = new();

    public DeploymentPreview(Node3D parent, Mesh shape, float height)
    {
        halfHeight = height * 0.5f;
        mesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = shape };
        parent.AddChild(new MultiMeshInstance3D { Multimesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    public void Add(SoldierPlacement placement, bool animate = false, float delay = 0f)
    {
        int slot = placements.Count;
        placements.Add(placement);
        var cell = new Vector2I(placement.X, placement.Z);
        slots.Add(cell, slot);
        if (animate) popping.Add(cell, animationTime + delay);
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
        popping.Remove(cell);
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
        popping.Clear();
        animationTime = 0f;
        placements.Clear();
        mesh.VisibleInstanceCount = 0;
    }

    public void Advance(float delta)
    {
        if (popping.Count == 0) { animationTime = 0f; return; }
        animationTime += delta;
        settled.Clear();
        foreach (var (cell, startedAt) in popping)
        {
            float age = (animationTime - startedAt) * PopSpeed;
            if (age < 0f) continue;
            WriteTransform(slots[cell]);
            if (age >= SettleTime) settled.Add(cell);
        }
        foreach (var cell in settled) popping.Remove(cell);
    }

    private void Write(int slot)
    {
        WriteTransform(slot);
        mesh.SetInstanceColor(slot, ArmyRenderer.FactionColor(placements[slot].Faction));
    }

    private void WriteTransform(int slot)
    {
        SoldierPlacement placement = placements[slot];
        float scale = popping.TryGetValue(new Vector2I(placement.X, placement.Z), out float startedAt)
            ? PopScale((animationTime - startedAt) * PopSpeed) : 1f;
        Vector3 position = placement.Position();
        position.Y += halfHeight * scale + (Terrain?.At(position.X, position.Z) ?? 0f);
        mesh.SetInstanceTransform(slot, new Transform3D(Basis.Identity.Scaled(Vector3.One * scale), position));
    }

    private static float PopScale(float age)
    {
        if (age < 0f) return 0f;
        if (age < PeakTime)
        {
            float remaining = 1f - age / PeakTime;
            return Mathf.Lerp(0.10f, 1.22f, 1f - remaining * remaining * remaining);
        }
        if (age < DipTime) return Ease(1.22f, 0.93f, PeakTime, DipTime);
        if (age < ReboundTime) return Ease(0.93f, 1.04f, DipTime, ReboundTime);
        return Ease(1.04f, 1f, ReboundTime, SettleTime);

        float Ease(float from, float to, float start, float end) =>
            Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, (age - start) / (end - start)));
    }
}
