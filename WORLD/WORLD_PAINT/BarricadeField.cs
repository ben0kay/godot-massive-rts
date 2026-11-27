// Stores faction barricades on the paint grid and resolves traversal and projectile contacts.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed class BarricadeField
{
    private sealed class Segment
    {
        internal Vector2I Cell;
        internal Vector3 Min, Max;
        internal byte Faction;
        internal float Health;
        internal int Slot;
    }

    private const float CellSize = WorldPaint.CellSize;
    private static Mesh mesh;
    private readonly Dictionary<Vector2I, Segment> cells = new();
    private readonly List<Segment> slots = new();
    private readonly MultiMesh batch;
    private readonly float traversalDamage, lightSpeed, crushSpeed, crushTier;
    private Vector2I minCell, maxCell;

    internal BarricadeField(WorldPaint paint, WorldPaintData data)
    {
        traversalDamage = Setting(data.BarricadeTraversalDamage, 7f, 0f, 1000f);
        lightSpeed = Setting(data.BarricadeLightSpeed, 0.25f, 0.05f, 1f);
        crushSpeed = Setting(data.BarricadeCrushSpeed, 0.75f, 0.05f, 1f);
        crushTier = Setting(data.BarricadeCrushTier, 2f, 1f, 10f);
        float height = Setting(data.BarricadeHeight, 0.95f, 0.2f, 4f);
        float health = Setting(data.BarricadeHealth, 70f, 1f, 10000f);
        batch = new MultiMesh { Mesh = mesh ??= BuildMesh(), TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true, InstanceCount = data.BlueBarricadeCells.Count + data.RedBarricadeCells.Count, VisibleInstanceCount = 0 };
        Add(data.BlueBarricadeCells, Factions.Blue);
        Add(data.RedBarricadeCells, Factions.Red);
        batch.VisibleInstanceCount = slots.Count;

        void Add(Godot.Collections.Array<Vector2I> authored, byte faction)
        {
            var source = new HashSet<Vector2I>(authored);
            foreach (var cell in authored)
            {
                if (!paint.Contains(cell) || cells.ContainsKey(cell)) continue;
                bool horizontal = source.Contains(cell + Vector2I.Left) || source.Contains(cell + Vector2I.Right);
                bool vertical = source.Contains(cell + Vector2I.Up) || source.Contains(cell + Vector2I.Down);
                bool rotate = vertical && !horizontal;
                Vector2 center = ((Vector2)cell + Vector2.One * 0.5f) * CellSize;
                float ground = paint.Surface?.At(center.X, center.Y) ?? 0f;
                float width = horizontal || !vertical ? CellSize : CellSize * 0.28f;
                float depth = vertical ? CellSize : CellSize * 0.28f;
                Vector3 position = new(center.X, ground, center.Y);
                var half = new Vector3(width * 0.5f, 0f, depth * 0.5f);
                var segment = new Segment { Cell = cell, Min = position - half, Max = position + half + Vector3.Up * height,
                    Faction = faction, Health = health, Slot = slots.Count };
                if (slots.Count == 0) minCell = maxCell = cell;
                else
                {
                    minCell = new Vector2I(Mathf.Min(minCell.X, cell.X), Mathf.Min(minCell.Y, cell.Y));
                    maxCell = new Vector2I(Mathf.Max(maxCell.X, cell.X), Mathf.Max(maxCell.Y, cell.Y));
                }
                cells.Add(cell, segment);
                slots.Add(segment);
                var basis = new Basis(Vector3.Up, rotate ? Mathf.Pi * 0.5f : 0f) *
                    Basis.FromScale(new Vector3(CellSize, height, horizontal && vertical ? CellSize : CellSize * 0.28f));
                batch.SetInstanceTransform(segment.Slot, new Transform3D(basis, position));
                batch.SetInstanceColor(segment.Slot, Factions.Color(faction));
            }
        }
    }

    internal MultiMesh Batch => batch;

    internal Vector2 Traverse(byte faction, float tier, float radius, Vector2 start, Vector2 end, float seconds)
    {
        if (start == end || !Bounds(new Vector3(start.X, 0f, start.Y), new Vector3(end.X, 0f, end.Y), radius,
            out int left, out int right, out int near, out int far)) return end;

        bool crush = tier >= crushTier;
        float earliest = 1f;
        for (int z = near; z <= far; z++)
        for (int x = left; x <= right; x++)
        {
            if (!cells.TryGetValue(new Vector2I(x, z), out var segment) || segment.Faction == faction) continue;
            if (GroundSpan(segment, start, end - start, radius, out float entry, out _))
                earliest = Mathf.Min(earliest, entry);
        }
        if (earliest >= 1f) return end;

        // Apply the slowdown once, from first contact, regardless of how many cells overlap.
        float speed = crush ? crushSpeed : lightSpeed;
        Vector2 next = start.Lerp(end, earliest + (1f - earliest) * speed);
        Vector2 delta = next - start;
        for (int z = near; z <= far; z++)
        for (int x = left; x <= right; x++)
        {
            if (!cells.TryGetValue(new Vector2I(x, z), out var segment) || segment.Faction == faction ||
                !GroundSpan(segment, start, delta, radius, out float entry, out float exit)) continue;
            float contactSeconds = seconds * Mathf.Max(0f, exit - entry) *
                (earliest + (1f - earliest) * speed) / speed;
            Damage(segment, crush ? segment.Health : traversalDamage * Mathf.Min(seconds, contactSeconds));
        }
        return next;
    }

    internal Vector2I? FirstProjectileHit(byte faction, Vector3 start, Vector3 delta, out float fraction)
    {
        fraction = float.PositiveInfinity;
        if (!Bounds(start, start + delta, 0f, out int left, out int right, out int near, out int far)) return null;
        Vector2I? hit = null;
        for (int z = near; z <= far; z++)
        for (int x = left; x <= right; x++)
        {
            var cell = new Vector2I(x, z);
            if (!cells.TryGetValue(cell, out var segment) || segment.Faction == faction) continue;
            float enter = 0f, exit = 1f;
            if (!Axis(start.X, delta.X, segment.Min.X, segment.Max.X, ref enter, ref exit) ||
                !Axis(start.Y, delta.Y, segment.Min.Y, segment.Max.Y, ref enter, ref exit) ||
                !Axis(start.Z, delta.Z, segment.Min.Z, segment.Max.Z, ref enter, ref exit) || enter >= fraction) continue;
            fraction = enter;
            hit = cell;
        }
        return hit;
    }

    internal void Damage(Vector2I cell, float amount)
    {
        if (cells.TryGetValue(cell, out var segment)) Damage(segment, amount);
    }

    private void Damage(Segment segment, float amount)
    {
        if (amount <= 0f || !float.IsFinite(amount)) return;
        segment.Health -= amount;
        if (segment.Health > 0f) return;
        cells.Remove(segment.Cell);
        int last = slots.Count - 1;
        if (segment.Slot != last)
        {
            var moved = slots[last];
            slots[segment.Slot] = moved;
            moved.Slot = segment.Slot;
            batch.SetInstanceTransform(moved.Slot, batch.GetInstanceTransform(last));
            batch.SetInstanceColor(moved.Slot, batch.GetInstanceColor(last));
        }
        slots.RemoveAt(last);
        batch.VisibleInstanceCount = slots.Count;
    }

    private bool Bounds(Vector3 start, Vector3 end, float padding, out int left, out int right, out int near, out int far)
    {
        left = Mathf.Max(minCell.X, Mathf.FloorToInt((Mathf.Min(start.X, end.X) - padding) / CellSize));
        right = Mathf.Min(maxCell.X, Mathf.FloorToInt((Mathf.Max(start.X, end.X) + padding) / CellSize));
        near = Mathf.Max(minCell.Y, Mathf.FloorToInt((Mathf.Min(start.Z, end.Z) - padding) / CellSize));
        far = Mathf.Min(maxCell.Y, Mathf.FloorToInt((Mathf.Max(start.Z, end.Z) + padding) / CellSize));
        return cells.Count > 0 && left <= right && near <= far;
    }

    private static bool GroundSpan(Segment segment, Vector2 from, Vector2 delta, float padding, out float entry, out float exit)
    {
        entry = 0f;
        exit = 1f;
        return Axis(from.X, delta.X, segment.Min.X - padding, segment.Max.X + padding, ref entry, ref exit) &&
            Axis(from.Y, delta.Y, segment.Min.Z - padding, segment.Max.Z + padding, ref entry, ref exit);
    }

    private static bool Axis(float start, float delta, float min, float max, ref float entry, ref float exit)
    {
        if (Mathf.Abs(delta) < 0.000001f) return start >= min && start <= max;
        float a = (min - start) / delta, b = (max - start) / delta;
        entry = Mathf.Max(entry, Mathf.Min(a, b));
        exit = Mathf.Min(exit, Mathf.Max(a, b));
        return entry <= exit;
    }

    private static float Setting(float value, float fallback, float min, float max) =>
        float.IsFinite(value) ? Mathf.Clamp(value, min, max) : fallback;

    private static Mesh BuildMesh()
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(new StandardMaterial3D { VertexColorUseAsAlbedo = true, Metallic = 0.35f, Roughness = 0.72f });
        void Panel(Vector3 at, Vector3 size, Color tint, float pitch = 0f) =>
            FacetedSoldierMeshes.AppendBeveledRectangle(surface, at, size, tint, sideShade: 0.75f, pitch: pitch);
        Panel(new Vector3(0f, 0.38f, 0f), new Vector3(1f, 0.69f, 0.72f), new Color("#697078"), -0.13f);
        Panel(new Vector3(0f, 0.78f, 0f), new Vector3(1f, 0.14f, 0.58f), new Color("#bac2ce"));
        for (int index = -1; index <= 1; index += 2)
            Panel(new Vector3(index * 0.34f, 0.4f, 0f), new Vector3(0.12f, 0.8f, 1f), new Color("#40444a"));
        return surface.Commit();
    }
}
