// Compiles an authored command-level terrain preview and runtime height surface.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

[Tool]
public partial class CommandTerrain : Node3D
{
    private readonly record struct Hill(Vector2 Center, float Radius, float Rise);
    private Node3D preview;
    private int fingerprint;
    private double previewDelay;
    public event Action SurfaceChanged;
    public TerrainHeightField Field { get; private set; }

    public override void _Ready()
    {
        Rebuild(force: true);
        SetProcess(Engine.IsEditorHint());
    }

    public override void _Process(double delta)
    {
        previewDelay += delta;
        if (previewDelay < 0.25) return;
        previewDelay = 0;
        Rebuild(force: false);
    }

    public float SampleHeight(float x, float z) => Field?.At(x, z) ?? 0f;

    public float SampleFootprint(Vector2 min, Vector2 max) => Field?.FootprintHeight(min, max, out _) ?? 0f;

    internal static CommandTerrain Find(Node source)
    {
        for (Node parent = source.GetParent(); parent != null; parent = parent.GetParent())
            if (parent.GetNodeOrNull<CommandTerrain>("Terrain") is CommandTerrain terrain) return terrain;
        return null;
    }

    private void Rebuild(bool force)
    {
        var hills = new List<Hill>();
        var hash = new HashCode();
        foreach (Node child in GetChildren())
        {
            if (child is not TerrainHill marker || marker.Rise <= 0f) continue;
            var hill = new Hill(ObstacleField.Xz(marker.GlobalPosition),
                Mathf.Clamp(marker.Radius, 8f, 256f), Mathf.Clamp(marker.Rise, 0f, 80f));
            hills.Add(hill);
            hash.Add(hill);
        }

        int signature = hash.ToHashCode();
        if (!force && signature == fingerprint) return;
        fingerprint = signature;
        if (preview != null)
        {
            RemoveChild(preview);
            preview.QueueFree();
            preview = null;
        }
        Field = null;
        if (hills.Count == 0)
        {
            SurfaceChanged?.Invoke();
            return;
        }

        // Compile only chunks that a hill can touch, even if hills sit far apart.
        var regions = new List<Rect2>(hills.Count);
        float highest = 1f;
        foreach (var hill in hills)
        {
            var size = Vector2.One * hill.Radius;
            regions.Add(new Rect2(hill.Center - size, size * 2f));
            highest = Mathf.Max(highest, hill.Rise);
        }
        Field = new TerrainHeightField(regions, point =>
        {
            float height = 0f;
            foreach (var hill in hills)
            {
                float distanceSquared = hill.Center.DistanceSquaredTo(point);
                if (distanceSquared >= hill.Radius * hill.Radius) continue;
                float factor = 1f - Mathf.Sqrt(distanceSquared) / hill.Radius;
                height = Mathf.Max(height, hill.Rise * factor * factor * (3f - 2f * factor));
            }
            return height;
        });
        preview = TerrainRenderer.Build(Field, highest, DeploymentLayout.WorldHalfWidth, includeSurround: false);
        AddChild(preview, false, InternalMode.Back);
        // Chunks are stored in world coordinates, independent of the authoring node.
        preview.TopLevel = true;
        preview.GlobalTransform = Transform3D.Identity;
        SurfaceChanged?.Invoke();
    }
}
