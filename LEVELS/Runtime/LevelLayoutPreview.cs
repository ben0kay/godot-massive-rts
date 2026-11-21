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
                float footprint = UnitOutline.FromDefinition(placement.Unit, UnitOutline.EffectiveSize(placement.Unit)).Footprint;
                var basis = new Basis(Vector3.Up, placement.GlobalRotation.Y);
                foreach (var position in placement.Positions(footprint))
                {
                    var point = position;
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

        if (level.GetNodeOrNull<Node3D>("StartingDefenses") is Node3D defenses)
        {
            var renderer = new DefenseRenderer();
            preview.AddChild(renderer);
            renderer.Initialize(System.Array.Empty<BuildingPlacement>(), null, CommandLaserPlacement.BaseHeight, battle: false);
            int index = 0;
            foreach (var child in defenses.GetChildren())
                if (child is CommandLaserPlacement tower && index < CommandSandbox.MaxStartingTowers)
                {
                    var area = tower.Footprint(tower.GlobalPosition);
                    float ground = terrain?.SampleFootprint(area.Min, area.Max) ?? 0f;
                    renderer.ShowTower(index++, area, tower.Faction, CommandLaserPlacement.BaseHeight, ground,
                        tower.EffectiveDefinition.Projectile != null, tower.EffectiveDefinition.ShieldEmitterEnabled);
                }
        }

        if (level.PresetRules is { LineAxis: not PresetBattleRules.DeploymentAxis.Disabled } rules)
        {
            bool alongZ = rules.LineAxis == PresetBattleRules.DeploymentAxis.X;
            float half = level.MapSize * 0.5f;
            float offset = Mathf.Clamp(rules.LineOffset, -half, half);
            float blue = rules.FlipBlueSide ? 1 : -1;
            AddDeploymentLine(preview, terrain, alongZ, offset, half, blue);
        }
        if (level.PresetRules?.BlueDeploymentCells is not { Count: > 0 } cells) return preview;
        using var region = new SurfaceTool();
        bool hasRegion = false;
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
                Mathf.Abs(z + cellSize * 0.5f) > level.MapSize * 0.5f ||
                !level.PresetRules.AllowsSide(new Vector3(x + cellSize * 0.5f, 0, z + cellSize * 0.5f),
                    cellSize * 0.5f, blue: true)) continue;
            hasRegion = true;
            Vector3 At(float vx, float vz) => new(vx, Height(vx, vz) + 0.08f, vz);
            var a = At(x, z);
            var b = At(x + cellSize, z);
            var c = At(x + cellSize, z + cellSize);
            var d = At(x, z + cellSize);
            region.AddVertex(a); region.AddVertex(b); region.AddVertex(c);
            region.AddVertex(a); region.AddVertex(c); region.AddVertex(d);
        }
        if (hasRegion) preview.AddChild(new MeshInstance3D { Mesh = region.Commit() });
        return preview;
    }
    private static void AddDeploymentLine(Node3D parent, CommandTerrain terrain, bool alongZ, float offset, float half, float blue)
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled, VertexColorUseAsAlbedo = true });
        void Vertex(float across, float along)
        {
            float x = alongZ ? across : along, z = alongZ ? along : across;
            surface.AddVertex(new Vector3(x, (terrain?.SampleHeight(x, z) ?? 0) + 0.2f, z));
        }
        foreach (float side in new[] { blue, -blue })
        {
            surface.SetColor(side == blue ? new Color("#63baff") : new Color("#ff746f"));
            for (float from = -half; from < half; from += 2)
            {
                float end = Mathf.Min(half, from + 2);
                float edge = offset + side * 0.6f;
                Vertex(offset, from); Vertex(edge, from); Vertex(edge, end);
                Vertex(offset, from); Vertex(edge, end); Vertex(offset, end);
            }
        }
        parent.AddChild(new MeshInstance3D { Mesh = surface.Commit() });
    }
}
