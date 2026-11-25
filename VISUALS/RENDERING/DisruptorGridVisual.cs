// Batches faction-tinted holographic fields.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal static class DisruptorGridVisual
{
    internal static Texture2D PickerIcon => GD.Load<Texture2D>("res://VISUALS/RENDERING/DisruptorGridIcon.png");

    internal static void Refresh(MultiMeshInstance3D view, IReadOnlyCollection<Vector2I> cells, byte faction, Func<Vector3, float> height)
    {
        view.Visible = cells.Count > 0;
        if (!view.Visible) return;
        if (view.Multimesh == null)
        {
            var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://VISUALS/RENDERING/DisruptorGrid.gdshader") };
            material.SetShaderParameter("faction_tint", Factions.Color(faction));
            view.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            view.Multimesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = new PlaneMesh { Size = Vector2.One * DisruptorGrid.Size, Material = material }
            };
        }
        var mesh = view.Multimesh;
        mesh.InstanceCount = cells.Count;
        int index = 0;
        foreach (var cell in cells)
        {
            var center = DisruptorGrid.Center(cell);
            center.Y = height(center) + 0.14f;
            mesh.SetInstanceTransform(index++, new Transform3D(Basis.Identity, center));
        }
    }
}
