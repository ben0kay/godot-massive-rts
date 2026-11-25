// Batches faction-tinted plates and animated electrical emitters.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal static class DisruptorGridVisual
{
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
                Mesh = new BoxMesh { Size = new Vector3(1.84f, 0.12f, 1.84f), Material = material }
            };
        }
        var mesh = view.Multimesh;
        mesh.InstanceCount = cells.Count;
        int index = 0;
        foreach (var cell in cells)
        {
            var center = DisruptorGrid.Center(cell);
            center.Y = height(center) + 0.075f;
            mesh.SetInstanceTransform(index++, new Transform3D(Basis.Identity, center));
        }
    }
}
