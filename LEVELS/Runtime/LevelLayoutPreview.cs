// Draws the authored formation layout and deployment region without running combat.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal static class LevelLayoutPreview
{
    // =========================================================
    public static Node3D Build(CommandSandbox level, bool redOnly = false, string formationContainer = "StartingUnits", bool decorationsOnly = false,
        bool holographicDeployment = false)
    {
        var preview = new Node3D { Name = "LayoutPreview" };
        level.AddChild(preview, false, Node.InternalMode.Back);
        bool staging = formationContainer == RedReserveFormation.ContainerName;
        var terrain = staging ? null : level.GetNodeOrNull<CommandTerrain>("Terrain");
        float Height(float x, float z) => terrain?.SampleHeight(x, z) ?? 0f;
        if (!decorationsOnly)
        {
            var batches = new PlacementBatches(preview);
            batches.Refresh(level, redOnly, formationContainer);
        }
        if (staging) return preview;

        if (level.AutoBattlerRules is { LineAxis: not AutoBattlerRules.DeploymentAxis.Disabled } rules)
        {
            bool alongZ = rules.LineAxis == AutoBattlerRules.DeploymentAxis.X;
            float half = level.MapSize * 0.5f;
            float offset = Mathf.Clamp(rules.LineOffset, -half, half);
            float blue = rules.FlipBlueSide ? 1 : -1;
            AddDeploymentLine(preview, terrain, alongZ, offset, half, blue);
        }
        if (level.AutoBattlerRules?.BlueDeploymentCells is not { Count: > 0 } cells) return preview;
        float cellSize = DeploymentLayout.CellSize;
        float mapHalfWidth = level.MapSize * 0.5f;
        var visibleCells = new HashSet<Vector2I>();
        foreach (var cell in cells)
        {
            Vector2 center = ((Vector2)cell + Vector2.One * 0.5f) * cellSize;
            if (Mathf.Abs(center.X) > mapHalfWidth || Mathf.Abs(center.Y) > mapHalfWidth ||
                !level.AutoBattlerRules.AllowsSide(new Vector3(center.X, 0f, center.Y), cellSize * 0.5f, blue: true))
                continue;
            visibleCells.Add(cell);
        }
        if (visibleCells.Count == 0) return preview;

        using var region = new SurfaceTool();
        region.Begin(Mesh.PrimitiveType.Triangles);
        if (holographicDeployment)
        {
            var hologram = new ShaderMaterial
            {
                Shader = GD.Load<Shader>("res://GAMEPLAY/DEPLOYMENT/DeploymentZone.gdshader")
            };
            hologram.SetShaderParameter("faction_color", Factions.Color(Factions.Blue));
            region.SetMaterial(hologram);
        }
        else region.SetMaterial(new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(0.23f, 0.62f, 1f, 0.4f) });

        foreach (var cell in visibleCells)
        {
            Vector2 origin = (Vector2)cell * cellSize;
            if (holographicDeployment)
                region.SetColor(new Color(
                    visibleCells.Contains(cell + Vector2I.Left) ? 0f : 1f,
                    visibleCells.Contains(cell + Vector2I.Right) ? 0f : 1f,
                    visibleCells.Contains(cell + Vector2I.Up) ? 0f : 1f,
                    visibleCells.Contains(cell + Vector2I.Down) ? 0f : 1f));

            Add(Vector2.Zero); Add(Vector2.Right); Add(Vector2.One);
            Add(Vector2.Zero); Add(Vector2.One); Add(Vector2.Down);

            void Add(Vector2 uv)
            {
                Vector2 point = origin + uv * cellSize;
                region.SetUV(uv);
                region.AddVertex(new Vector3(point.X, Height(point.X, point.Y) + 0.08f, point.Y));
            }
        }
        preview.AddChild(new MeshInstance3D
        {
            Mesh = region.Commit(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
        return preview;
    }
    // The editor retains these batches for the lifetime of the loaded map.
    internal sealed class PlacementBatches
    {
        private readonly Node3D root;
        private readonly Dictionary<(UnitDefinition Unit, byte Team), MultiMesh> meshes = new();
        private DefenseRenderer defenses;

        internal PlacementBatches(Node3D root) => this.root = root;

        internal void Refresh(CommandSandbox level, bool redOnly, string formationContainer)
        {
            bool staging = formationContainer == RedReserveFormation.ContainerName;
            var terrain = staging ? null : level.GetNodeOrNull<CommandTerrain>("Terrain");
            float Height(float x, float z) => terrain?.SampleHeight(x, z) ?? 0f;
            var groups = new Dictionary<(UnitDefinition Unit, byte Team), List<Transform3D>>();
            if (level.GetNodeOrNull<Node3D>(formationContainer) is Node3D units)
                foreach (var child in units.GetChildren())
                {
                    if (child is not CommandUnitPlacement placement || placement.Unit == null ||
                        (redOnly && placement.Faction != CommandUnitPlacement.Team.Red)) continue;
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

            foreach (var entry in meshes)
                if (!groups.ContainsKey(entry.Key)) entry.Value.VisibleInstanceCount = 0;
            foreach (var entry in groups)
            {
                if (!meshes.TryGetValue(entry.Key, out var batch))
                {
                    batch = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                        Mesh = FacetedSoldierMeshes.CreatePlacementPreview(entry.Key.Unit, ArmyRenderer.FactionColor(entry.Key.Team)) };
                    meshes.Add(entry.Key, batch);
                    root.AddChild(new MultiMeshInstance3D { Multimesh = batch });
                }
                if (batch.InstanceCount < entry.Value.Count)
                    batch.InstanceCount = System.Math.Max(entry.Value.Count, batch.InstanceCount * 2);
                for (int i = 0; i < entry.Value.Count; i++) batch.SetInstanceTransform(i, entry.Value[i]);
                batch.VisibleInstanceCount = entry.Value.Count;
            }

            if (defenses != null) defenses.Visible = !staging;
            if (staging) return;
            if (level.GetNodeOrNull<Node3D>("StartingDefenses") is not Node3D towers) return;
            if (defenses == null)
            {
                defenses = new DefenseRenderer();
                root.AddChild(defenses);
            }
            defenses.Initialize(System.Array.Empty<BuildingPlacement>(), null, CommandLaserPlacement.BaseHeight, battle: false);
            int index = 0;
            foreach (var child in towers.GetChildren())
                if (child is CommandLaserPlacement tower && tower.EffectiveDefinition is { } definition &&
                    (!redOnly || tower.Red) && index < (redOnly ? DeploymentLayout.MaxBuildings : CommandSandbox.MaxStartingTowers))
                {
                    var area = tower.Footprint(tower.GlobalPosition);
                    float ground = terrain?.SampleFootprint(area.Min, area.Max) ?? 0f;
                    var emitter = ShieldEmitterStats.Compile(definition.ShieldEmitterEnabled, definition.ShieldRadius,
                        definition.ShieldCapacity, definition.ShieldRegenerationPerSecond, definition.ShieldDecayPerSecond);
                    defenses.ShowTower(index++, area, tower.Faction, CommandLaserPlacement.BaseHeight, ground,
                        definition.Projectile != null, emitter.Enabled, emitter.Radius, definition.Id == PrefabShelterMeshes.Id, definition.Id == TwinMinigunTowerMeshes.Id, AntiAirTowerMeshes.Variant(definition.Id));
                }
        }
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
