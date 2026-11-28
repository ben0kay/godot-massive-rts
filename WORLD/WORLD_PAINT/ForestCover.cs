// Compiles passable forest cells into batched trees and an organic forest floor.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed class ForestCover
{
    private const float CrownRadius = 0.56f;
    private readonly record struct Tree(int Index, Vector2 Center, float RadiusSquared);
    private readonly Dictionary<Vector2I, Tree> trees = new();
    private Rect2I visibilityBounds;
    private HashSet<int> faded = new(), nearby = new();
    private readonly MultiMesh trunks, crowns, shadows;
    private readonly MeshInstance3D groundCover;
    private float timeUntilRefresh;
    private static readonly Color LeafGreen = new("#9cb85e"), LeafGold = new("#d2bd69");
    private const int LeafCapacity = 96, SwayCapacity = 64;
    private readonly Node3D parent;
    private MultiMesh leaves;
    private uint leafSeed;
    private readonly List<(Vector3 Start, Vector3 Drift, float Age)> fallingLeaves = new(LeafCapacity);
    private readonly List<(int Tree, Vector2 Direction, float Strength)> swaying = new(SwayCapacity);
    private readonly HashSet<int> movingTrees = new();
    private Vector3[] previousHeavyPositions;
    private UnitState sampledUnits;
    private static Color Tint(float opacity) => new(1f, 1f, 1f, opacity);

    internal ForestCover(Node3D parent)
    {
        this.parent = parent;
        var shader = GD.Load<Shader>("res://WORLD/WORLD_PAINT/ForestTree.gdshader");
        ShaderMaterial Material(Color tint, bool foliage = false)
        {
            var material = new ShaderMaterial { Shader = shader };
            material.SetShaderParameter("tree_color", tint);
            material.SetShaderParameter("foliage", foliage);
            return material;
        }

        trunks = Batch(parent, new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.18f, Height = 1f,
            RadialSegments = 6, Rings = 0, Material = Material(new Color("#523322")) });
        crowns = Batch(parent, PineCrown(Material(new Color("#3b683d"), foliage: true)), customData: true);
        shadows = Batch(parent, GroundShadowMeshes.Pine());

        groundCover = new MeshInstance3D
        {
            Name = "ForestFloor",
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = GD.Load<ShaderMaterial>("res://WORLD/WORLD_PAINT/ForestFloor.tres")
        };
        parent.AddChild(groundCover);
    }

    // Keep the native render objects attached for the map's lifetime.
    internal void Rebuild(HashSet<Vector2I> cells, TerrainHeightField surface)
    {
        ResetMotion();
        trees.Clear();
        faded.Clear();
        nearby.Clear();
        timeUntilRefresh = 0f;
        groundCover.Mesh = ForestFloor.Build(cells, surface);
        if (cells.Count > trunks.InstanceCount)
        {
            int capacity = System.Math.Max(cells.Count, trunks.InstanceCount * 2);
            trunks.InstanceCount = crowns.InstanceCount = shadows.InstanceCount = capacity;
        }
        trunks.VisibleInstanceCount = crowns.VisibleInstanceCount = shadows.VisibleInstanceCount = cells.Count;
        if (cells.Count == 0)
        {
            visibilityBounds = default;
            return;
        }
        float cellSize = WorldPaint.CellSize;
        float Ground(Vector2 point) => surface?.At(point.X, point.Y) ?? 0f;
        Vector3 OnGround(Vector2 point) => new(point.X, Ground(point) + 0.06f, point.Y);
        Vector2I min = new(int.MaxValue, int.MaxValue), max = new(int.MinValue, int.MinValue);
        foreach (var cell in cells)
        {
            min = new Vector2I(System.Math.Min(min.X, cell.X), System.Math.Min(min.Y, cell.Y));
            max = new Vector2I(System.Math.Max(max.X, cell.X), System.Math.Max(max.Y, cell.Y));
            int index = trees.Count;
            uint seed = unchecked(((uint)cell.X * 0x9E3779B9u) ^ ((uint)cell.Y * 0x85EBCA6Bu));
            seed ^= seed >> 16;
            float height = 0.65f + (seed & 255) / 255f * 1.10f;
            float width = 0.72f + ((seed >> 8) & 255) / 255f * 0.51f;
            Vector2 low = (Vector2)cell * cellSize;
            // Jitter only the tree visuals, not the forest cell or its gameplay effects.
            // Stable per-cell hashing keeps trees in the same place after save/reload.
            uint jitter = unchecked(seed * 0x7FEB352Du + 0xA511E9B3u);
            jitter ^= jitter >> 16;
            jitter = unchecked(jitter * 0x846CA68Bu);
            jitter ^= jitter >> 15;
            Vector2 offset = new((jitter & 0xFFFFu) / 65535f * 2f - 1f,
                (jitter >> 16) / 65535f * 2f - 1f);
            Vector2 center = low + Vector2.One * (cellSize * 0.5f) + offset * (cellSize * 0.15f);
            // Vary the silhouette independently of the position hash. Keep the maximum
            // crown radius unchanged so occupancy can still use the painted cell.
            uint shape = unchecked(jitter * 0x9E3779B9u + 0x85EBCA6Bu);
            shape ^= shape >> 16;
            float yaw = (shape & 255u) / 255f * Mathf.Tau;
            float narrow = 0.84f + ((shape >> 8) & 255u) / 255f * 0.16f;
            float crownHeight = (0.6f + height * 0.65f) *
                (0.90f + ((shape >> 16) & 255u) / 255f * 0.24f);
            var crownBasis = new Basis(Vector3.Up, yaw) *
                Basis.Identity.Scaled(new Vector3(width, crownHeight, width * narrow));
            float radius = CrownRadius * width;
            trees.Add(cell, new Tree(index, center, radius * radius));
            float ground = Ground(center);
            trunks.SetInstanceTransform(index, new Transform3D(Basis.Identity.Scaled(new Vector3(width, height, width)),
                new Vector3(center.X, ground + height * 0.5f, center.Y)));
            crowns.SetInstanceTransform(index, new Transform3D(crownBasis,
                new Vector3(center.X, ground + height + 0.25f, center.Y)));
            Vector2 cast = GroundShadowMeshes.SceneryOffset(height + 0.35f);
            Vector2 along = cast.Normalized();
            var shadowBasis = new Basis(new Vector3(along.X, 0f, along.Y) * (cast.Length() + radius * 0.55f),
                Vector3.Up, new Vector3(-along.Y, 0f, along.X) * radius);
            shadows.SetInstanceTransform(index, new Transform3D(shadowBasis,
                OnGround(center) - Vector3.Up * 0.025f));
            shadows.SetInstanceColor(index, Colors.White);
            trunks.SetInstanceColor(index, Colors.White);
            crowns.SetInstanceColor(index, Colors.White);
            crowns.SetInstanceCustomData(index, new Color(0, 0, 0, 0));

        }
        visibilityBounds = new Rect2I(min, max - min + Vector2I.One);
    }

    private static Mesh PineCrown(Material material)
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(material);
        const int sides = 7;
        for (int tier = 0; tier < 3; tier++)
        {
            float radius = CrownRadius * (1f - 0.23f * tier);
            float bottom = -0.4f + 0.29f * tier;
            // Stagger branch whorls and vary the rim instead of using three perfect cones.
            float rotation = tier * 0.41f;
            float lean = 0.035f + 0.012f * tier;
            var tip = new Vector3(Mathf.Sin(rotation + 0.7f) * lean,
                bottom + 0.63f + (tier == 1 ? 0.045f : 0f), Mathf.Cos(rotation + 0.7f) * lean);
            Vector3 Rim(int side)
            {
                side %= sides;
                float angle = Mathf.Tau * side / sides + rotation;
                float irregular = 0.92f + 0.08f * Mathf.Sin(side * 2.39f + tier * 1.83f);
                float y = bottom + 0.036f * Mathf.Sin(side * 1.97f + tier * 2.1f);
                return new Vector3(Mathf.Cos(angle) * radius * irregular, y, Mathf.Sin(angle) * radius * irregular);
            }
            for (int side = 0; side < sides; side++)
            {
                var a = Rim(side);
                var b = Rim(side + 1);
                surface.SetNormal((tip - a).Cross(b - a).Normalized());
                surface.SetColor(Colors.White.Darkened(0.14f - 0.05f * tier));
                surface.AddVertex(a);
                surface.AddVertex(b);
                surface.AddVertex(tip);
            }
        }
        return surface.Commit();
    }

    private static MultiMesh Batch(Node3D parent, Mesh mesh, bool customData = false)
    {
        var batch = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true, UseCustomData = customData, Mesh = mesh };
        parent.AddChild(new MultiMeshInstance3D { Multimesh = batch,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        return batch;
    }

    internal bool Contains(Vector3 position) => trees.ContainsKey(Cell(position));

    private static Vector2I Cell(Vector3 position) => new(Mathf.FloorToInt(position.X / WorldPaint.CellSize),
        Mathf.FloorToInt(position.Z / WorldPaint.CellSize));

    internal void DropLeaves(Vector3 point)
    {
        if (fallingLeaves.Count > LeafCapacity - 4) return;
        if (leaves == null)
        {
            var material = new StandardMaterial3D
            {
                VertexColorUseAsAlbedo = true,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            };
            leaves = Batch(parent, new QuadMesh { Size = new Vector2(0.13f, 0.07f), Material = material });
            leaves.InstanceCount = LeafCapacity;
            leaves.VisibleInstanceCount = 0;
        }
        for (int i = 0; i < 4; i++)
        {
            uint seed = unchecked(++leafSeed * 2654435761u);
            float angle = (seed >> 16) * (Mathf.Tau / 65536f);
            float speed = 0.35f + (seed & 255) / 510f;
            fallingLeaves.Add((point, new Vector3(Mathf.Cos(angle) * speed, 0.65f, Mathf.Sin(angle) * speed), 0f));
        }
    }

    private void AdvanceLeaves(float delta)
    {
        if (fallingLeaves.Count == 0) return;
        for (int i = fallingLeaves.Count - 1; i >= 0; i--)
        {
            var leaf = fallingLeaves[i];
            leaf.Age += delta;
            if (leaf.Age >= 0.65f)
            {
                fallingLeaves.RemoveAt(i);
                continue;
            }
            fallingLeaves[i] = leaf;
        }
        for (int i = 0; i < fallingLeaves.Count; i++)
        {
            var leaf = fallingLeaves[i];
            float t = leaf.Age;
            Vector3 position = leaf.Start + leaf.Drift * t - Vector3.Up * (1.65f * t * t);
            float scale = Mathf.Min(1f, (0.65f - t) * 10f);
            var basis = Basis.FromEuler(new Vector3(t * 7f, leaf.Drift.X * 6f, t * 5f)).Scaled(Vector3.One * scale);
            leaves.SetInstanceTransform(i, new Transform3D(basis, position));
            leaves.SetInstanceColor(i, LeafGreen.Lerp(LeafGold, i % 3 * 0.35f));
        }
        leaves.VisibleInstanceCount = fallingLeaves.Count;
    }

    private void ResetMotion()
    {
        foreach (var entry in swaying) crowns.SetInstanceCustomData(entry.Tree, new Color(0, 0, 0, 0));
        swaying.Clear();
        movingTrees.Clear();
        fallingLeaves.Clear();
        if (leaves != null) leaves.VisibleInstanceCount = 0;
        sampledUnits = null;
    }

    internal void ResetVisibility()
    {
        ResetMotion();
        foreach (int index in faded)
        {
            trunks.SetInstanceColor(index, Colors.White);
            crowns.SetInstanceColor(index, Colors.White);
        }
        faded.Clear();
        timeUntilRefresh = 0f;
    }

    // =========================================================
    // Sample occupancy only at display cadence and upload changed tree colors.
    internal void UpdateVisibility(UnitState units, float delta)
    {
        AdvanceLeaves(delta);
        if (trees.Count == 0) return;
        timeUntilRefresh -= delta;
        if (timeUntilRefresh > 0f) return;
        timeUntilRefresh = 0.1f;
        nearby.Clear();
        movingTrees.Clear();
        for (int i = swaying.Count - 1; i >= 0; i--)
        {
            var entry = swaying[i];
            entry.Strength = Mathf.Max(0f, entry.Strength - 0.16f);
            crowns.SetInstanceCustomData(entry.Tree, new Color(entry.Direction.X * 0.5f + 0.5f,
                entry.Direction.Y * 0.5f + 0.5f, entry.Strength, 0));
            if (entry.Strength == 0f) swaying.RemoveAt(i);
            else swaying[i] = entry;
        }
        bool trackMovement = ReferenceEquals(sampledUnits, units);
        if (!trackMovement)
        {
            sampledUnits = units;
            if (previousHeavyPositions?.Length != units.Position.Length) previousHeavyPositions = new Vector3[units.Position.Length];
            System.Array.Copy(units.Position, previousHeavyPositions, units.Count);
        }
        for (int id = 0; id < units.Count; id++)
        {
            if (units.Health[id] <= 0f) continue;
            Vector3 position = units.Position[id];
            Vector3 travel = Vector3.Zero;
            if (units.Catalog[units.Type[id]].SizeTier >= 3f)
            {
                if (trackMovement) travel = position - previousHeavyPositions[id];
                previousHeavyPositions[id] = position;
            }
            Vector2I cell = Cell(position);
            // The crown fits inside its cell even at maximum scale and jitter.
            if (!visibilityBounds.HasPoint(cell) || !trees.TryGetValue(cell, out var tree)) continue;
            var direction = new Vector2(travel.X, travel.Z);
            if (direction.LengthSquared() > 0.0025f && movingTrees.Add(tree.Index))
            {
                int slot = 0;
                while (slot < swaying.Count && swaying[slot].Tree != tree.Index) slot++;
                bool existing = slot < swaying.Count;
                if (existing || swaying.Count < SwayCapacity)
                {
                    var normal = direction.Normalized();
                    var entry = (tree.Index, normal, 1f);
                    if (!existing) swaying.Add(entry);
                    else swaying[slot] = entry;
                    crowns.SetInstanceCustomData(tree.Index, new Color(normal.X * 0.5f + 0.5f, normal.Y * 0.5f + 0.5f, 1f, 0));
                }
            }
            if (nearby.Contains(tree.Index)) continue;
            if (tree.Center.DistanceSquaredTo(new Vector2(position.X, position.Z)) <= tree.RadiusSquared)
                nearby.Add(tree.Index);
        }
        foreach (int index in faded)
            if (!nearby.Contains(index))
            {
                trunks.SetInstanceColor(index, Colors.White);
                crowns.SetInstanceColor(index, Colors.White);
            }
        foreach (int index in nearby)
            if (!faded.Contains(index))
            {
                trunks.SetInstanceColor(index, Tint(0.48f));
                crowns.SetInstanceColor(index, Tint(0.35f));
            }
        (faded, nearby) = (nearby, faded);
    }
}
