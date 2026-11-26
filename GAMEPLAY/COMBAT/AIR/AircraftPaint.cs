// Shared air-start brush and batched previews for authoring and test deployment.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal static class AircraftPaint
{
    private const float Spacing = 4f;

    internal static Vector2 Cell(Vector3 world) => ObstacleField.Xz(world).Snapped(Vector2.One * Spacing);

    internal static Vector2[] Edit(Vector2[] existing, Vector2 from, Vector2 to, float halfWidth, bool erase, Predicate<Vector2> allowsPlacement = null,
        int capacity = AirSquadron.MaxPerFaction)
    {
        var points = new List<Vector2>(existing);
        var offset = to - from;
        int steps = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(Mathf.Abs(offset.X), Mathf.Abs(offset.Y)) / Spacing));
        float limit = halfWidth - Spacing;
        bool changed = false;
        for (int step = 0; step <= steps; step++)
        {
            var point = from.Lerp(to, step / (float)steps).Snapped(Vector2.One * Spacing);
            if (erase) changed |= points.Remove(point);
            else if (points.Count < capacity && Mathf.Abs(point.X) <= limit &&
                     Mathf.Abs(point.Y) <= limit && !points.Contains(point) && allowsPlacement?.Invoke(point) != false)
            {
                points.Add(point);
                changed = true;
            }
        }
        return changed ? points.ToArray() : existing;
    }

    internal static void RefreshBombMarker(Node3D parent, ref Label3D marker, byte faction,
        Vector2? target, Func<Vector3, float> surface)
    {
        if (!GodotObject.IsInstanceValid(marker) || marker.GetParent() != parent)
        {
            if (!target.HasValue) return;
            marker = new Label3D
            {
                Text = "⊕ BOMB",
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Modulate = Factions.Color(faction),
                PixelSize = 0.06f
            };
            parent.AddChild(marker, false, Node.InternalMode.Back);
        }
        marker.Visible = target.HasValue;
        if (target is { } point)
        {
            var position = new Vector3(point.X, 0f, point.Y);
            position.Y = surface(position) + 1.5f;
            marker.Position = position;
        }
    }

    internal static MultiMeshInstance3D RefreshPreview(Node3D parent, MultiMeshInstance3D preview,
        UnitDefinition unit, Vector2[] blue, Vector2[] red, Func<Vector3, float> surface)
    {
        if (!GodotObject.IsInstanceValid(preview) || preview.GetParent() != parent)
        {
            if (blue.Length + red.Length == 0) return null;
            preview = new MultiMeshInstance3D
            {
                Multimesh = new MultiMesh
                {
                    TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true,
                    Mesh = AirSquadronVisuals.AircraftMesh(unit),
                    InstanceCount = AirSquadron.MaxPerFaction * 2
                },
                MaterialOverride = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true,
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled
                },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            parent.AddChild(preview, false, Node.InternalMode.Back);
        }
        var batch = preview.Multimesh;
        int index = 0;
        AddFlight(blue, Factions.Blue);
        AddFlight(red, Factions.Red);
        batch.VisibleInstanceCount = index;
        return preview;

        void AddFlight(Vector2[] points, byte faction)
        {
            for (int i = 0; i < Mathf.Min(points.Length, AirSquadron.MaxPerFaction); i++)
            {
                var point = points[i];
                var position = new Vector3(point.X, 0f, point.Y);
                position.Y = surface(position) + 1.2f;
                float heading = point.LengthSquared() > 1f ? Mathf.Atan2(-point.X, -point.Y) : faction == Factions.Blue ? 0f : Mathf.Pi;
                batch.SetInstanceTransform(index, new Transform3D(new Basis(Vector3.Up, heading), position));
                batch.SetInstanceColor(index++, Factions.Color(faction));
            }
        }
    }
}
