// Packed MultiMesh batches with dirty-instance updates and constant-time death removal.
using System.Diagnostics;
using Godot;
namespace MassiveRts;

public partial class ArmyRenderer : Node3D
{
    private const int InstanceStride = 16; // Three affine rows, then RGBA.
    private const int SparseUpdateLimit = 64;
    private float[][] buffers;
    private int[][] changedSlots;
    private int[] changeCounts;
    private MultiMesh[] batches;
    private int[][] members;
    private int[] slots;
    private int[] counts;
    private int typeCount;
    private float[] halfHeights;
    private UnitState units;
    public int DenseUploads { get; private set; }
    public int SparseUploads { get; private set; }
    public int ChangedWrites { get; private set; }
    public double UploadMilliseconds { get; private set; }
    public int InstanceCount { get; private set; }
    // =========================================================
    public void Initialize(UnitState state, Mesh[] shapes)
    {
        units = state; slots = new int[state.Position.Length]; System.Array.Fill(slots, -1);
        typeCount = state.Catalog.Count;
        int batchCount = typeCount * Factions.Count;
        members = new int[batchCount][];
        buffers = new float[batchCount][];
        changedSlots = new int[batchCount][];
        changeCounts = new int[batchCount];
        batches = new MultiMesh[batchCount];
        counts = new int[batchCount];
        halfHeights = new float[typeCount];
        for (int i = 0; i < typeCount; i++) halfHeights[i] = state.Catalog[i].Height * 0.5f;
        var capacities = new int[batchCount];
        for (int i = 0; i < state.Count; i++) capacities[Batch(i)]++;
        for (int b = 0; b < batchCount; b++)
        {
            int capacity = capacities[b];
            if (capacity == 0) continue;
            members[b] = new int[capacity];
            buffers[b] = new float[capacity * InstanceStride];
            changedSlots[b] = new int[SparseUpdateLimit];
            Color faction = FactionColor(b / typeCount);
            for (int offset = 0; offset < buffers[b].Length; offset += InstanceStride)
            {
                buffers[b][offset] = buffers[b][offset + 5] = buffers[b][offset + 10] = 1f;
                StoreColor(buffers[b], offset, faction);
            }
            Mesh mesh = shapes[b % typeCount];
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = mesh };
            mm.InstanceCount = capacity; mm.VisibleInstanceCount = 0; batches[b] = mm;
            AddChild(new MultiMeshInstance3D { Multimesh = mm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }
        Sync();
    }
    private int Batch(int id) => units.Faction[id] * typeCount + units.Type[id];
    private static readonly Color[] FactionColors =
    {
        new(0.12f, 0.45f, 1f),
        new(1f, 0.15f, 0.12f),
        new(0.22f, 0.91f, 0.29f)
    };
    internal static Color FactionColor(int faction) => FactionColors[faction];

    private static void StoreColor(float[] buffer, int offset, Color color)
    {
        buffer[offset + 12] = color.R;
        buffer[offset + 13] = color.G;
        buffer[offset + 14] = color.B;
        buffer[offset + 15] = color.A;
    }

    // Stage instance changes in reusable arrays. Only sparse batches need a slot list.
    private void Write(int id, int batch, int slot)
    {
        Vector3 position = units.Position[id];
        int offset = slot * InstanceStride;
        float[] buffer = buffers[batch];
        var (white, scale) = units.HitFeedback?.Evaluate(id) ?? (false, 1f);
        buffer[offset] = buffer[offset + 5] = buffer[offset + 10] = scale;
        buffer[offset + 3] = position.X;
        buffer[offset + 7] = position.Y + halfHeights[units.Type[id]] * scale;
        buffer[offset + 11] = position.Z;
        Color color = white ? Colors.White : FactionColor(units.Faction[id]);
        if (buffer[offset + 12] != color.R || buffer[offset + 13] != color.G ||
            buffer[offset + 14] != color.B || buffer[offset + 15] != color.A)
            StoreColor(buffer, offset, color);

        int changeCount = changeCounts[batch]++;
        if (changeCount < SparseUpdateLimit) changedSlots[batch][changeCount] = slot;
    }

    private void Upload(int batch)
    {
        MultiMesh mesh = batches[batch];
        if (mesh == null) return;
        int changed = changeCounts[batch];
        int visible = counts[batch];
        ChangedWrites += changed;
        if (changed > SparseUpdateLimit && visible > 0)
        {
            DenseUploads++;
            mesh.Buffer = buffers[batch];
        }
        else if (changed > 0 && visible > 0)
        {
            SparseUploads++;
            float[] buffer = buffers[batch];
            for (int i = 0; i < changed; i++)
            {
                int slot = changedSlots[batch][i];
                if (slot >= visible) continue;
                int offset = slot * InstanceStride;
                var position = new Vector3(buffer[offset + 3], buffer[offset + 7], buffer[offset + 11]);
                float scale = buffer[offset];
                Basis basis = scale == 1f ? Basis.Identity : Basis.Identity.Scaled(Vector3.One * scale);
                mesh.SetInstanceTransform(slot, new Transform3D(basis, position));
                mesh.SetInstanceColor(slot, new Color(buffer[offset + 12], buffer[offset + 13], buffer[offset + 14], buffer[offset + 15]));
            }
        }
        if (mesh.VisibleInstanceCount != visible) mesh.VisibleInstanceCount = visible;
        changeCounts[batch] = 0;
    }

    // =========================================================
    public void Sync()
    {
        for (int id = 0; id < units.Count; id++)
        {
            if (!units.Dirty[id]) continue;
            units.Dirty[id] = false;
            int batch = Batch(id), slot = slots[id];
            if (units.Health[id] <= 0)
            {
                if (slot < 0) continue;
                int last = --counts[batch], moved = members[batch][last];
                if (slot != last) { members[batch][slot] = moved; slots[moved] = slot; Write(moved, batch, slot); }
                slots[id] = -1; InstanceCount--;
            }
            else
            {
                if (slot < 0) { slot = counts[batch]++; members[batch][slot] = id; slots[id] = slot; InstanceCount++; }
                Write(id, batch, slot);
            }
        }
        DenseUploads = SparseUploads = ChangedWrites = 0;
        long uploadStart = Stopwatch.GetTimestamp();
        for (int b = 0; b < batches.Length; b++) Upload(b);
        UploadMilliseconds = Stopwatch.GetElapsedTime(uploadStart).TotalMilliseconds;
    }
}
