// Draws the authored formation layout and deployment region without running combat.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal static class LevelLayoutPreview
{
    // =========================================================
    public static Node3D Build(CommandSandbox level)
    {
        var preview = new Node3D { Name = "LayoutPreview" };
        level.AddChild(preview, false, Node.InternalMode.Back);
        var terrain = level.GetNodeOrNull<CommandTerrain>("Terrain");
        float Height(float x, float z) => terrain?.SampleHeight(x, z) ?? 0f;
        var groups = new Dictionary<(UnitDefinition Unit, byte Team), List<Transform3D>>();
        if (level.GetNodeOrNull<Node3D>("StartingUnits") is Node3D units)
            foreach (var child in units.GetChildren())
            {
                if (child is not CommandUnitPlacement placement || placement.Unit == null) continue;
                var key = (placement.Unit, (byte)placement.Faction);
                if (!groups.TryGetValue(key, out var transforms))
                    groups.Add(key, transforms = new());
                var right = placement.GlobalBasis.X.Normalized();
                float spacing = 2f * UnitOutline.FromDefinition(placement.Unit, UnitOutline.EffectiveSize(placement.Unit)).Footprint + 0.3f;
                int count = Mathf.Clamp(placement.Count, 1, 256);
                int columns = Mathf.Min(count, CommandMovement.DefaultColumns);
                var basis = new Basis(Vector3.Up, placement.GlobalRotation.Y);
                for (int slot = 0; slot < count; slot++)
                {
                    var point = CommandMovement.Slot(placement.GlobalPosition, right, columns, slot, count, spacing);
                    point.Y = Height(point.X, point.Z) + placement.Unit.Height * UnitOutline.EffectiveSize(placement.Unit) * 0.5f;
                    transforms.Add(new Transform3D(basis, point));
                }
            }

        foreach (var entry in groups)
        {
            var mesh = FacetedSoldierMeshes.CreatePlacementPreview(entry.Key.Unit, ArmyRenderer.FactionColor(entry.Key.Team));
            var batch = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh,
                InstanceCount = entry.Value.Count };
            for (int i = 0; i < entry.Value.Count; i++) batch.SetInstanceTransform(i, entry.Value[i]);
            preview.AddChild(new MultiMeshInstance3D { Multimesh = batch });
        }

        if (level.PresetRules?.BlueDeploymentCells is not { Count: > 0 } cells) return preview;
        var region = new SurfaceTool();
        region.Begin(Mesh.PrimitiveType.Triangles);
        var material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(0.23f, 0.62f, 1f, 0.4f) };
        region.SetMaterial(material);
        float cellSize = DeploymentLayout.CellSize;
        foreach (var cell in cells)
        {
            float x = cell.X * cellSize, z = cell.Y * cellSize;
            if (Mathf.Abs(x + cellSize * 0.5f) > level.MapSize * 0.5f ||
                Mathf.Abs(z + cellSize * 0.5f) > level.MapSize * 0.5f) continue;
            Vector3 At(float vx, float vz) => new(vx, Height(vx, vz) + 0.08f, vz);
            var a = At(x, z);
            var b = At(x + cellSize, z);
            var c = At(x + cellSize, z + cellSize);
            var d = At(x, z + cellSize);
            region.AddVertex(a); region.AddVertex(b); region.AddVertex(c);
            region.AddVertex(a); region.AddVertex(c); region.AddVertex(d);
        }
        preview.AddChild(new MeshInstance3D { Mesh = region.Commit() });
        return preview;
    }
}
