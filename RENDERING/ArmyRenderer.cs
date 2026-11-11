// Six packed MultiMesh batches with dirty-instance updates and constant-time death removal.
using Godot;
namespace MassiveRts;

public partial class ArmyRenderer : Node3D
{
    private const int InstanceStride = 16; // Three affine rows, then RGBA.
    private const int SparseUpdateLimit = 64;
    private readonly float[][] buffers = new float[6][];
    private readonly int[][] changedSlots = new int[6][];
    private readonly int[] changeCounts = new int[6];
    private readonly MultiMesh[] batches = new MultiMesh[6];
    private int[][] members;
    private int[] slots;
    private readonly int[] counts = new int[6];
    private UnitState units;
    public int InstanceCount { get; private set; }
    // =========================================================
    public void Initialize(UnitState state)
    {
        units = state; slots = new int[state.Position.Length]; System.Array.Fill(slots, -1);
        members = new int[6][];
        // Three low-poly meshes are built once; all blue/red soldiers share them.
        // Per-face vertex tints fake a fixed light direction without dynamic lighting.
        var material = new StandardMaterial3D
        {
            AlbedoColor = Colors.White,
            VertexColorUseAsAlbedo = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        Mesh[] shapes = FacetedSoldierMeshes.Create(material);
        for (int b = 0; b < 6; b++)
        {
            // Capacity is counted from the completed deployment before allocating GPU buffers.
            int capacity = 0;
            for (int i = 0; i < state.Count; i++) if (Batch(i) == b) capacity++;
            members[b] = new int[capacity];
            buffers[b] = new float[capacity * InstanceStride];
            changedSlots[b] = new int[SparseUpdateLimit];
            Color faction = b < 3 ? Blue : Red;
            for (int offset = 0; offset < buffers[b].Length; offset += InstanceStride)
            {
                buffers[b][offset] = buffers[b][offset + 5] = buffers[b][offset + 10] = 1f;
                StoreColor(buffers[b], offset, faction);
            }
            Mesh mesh = shapes[b % 3];
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = mesh };
            mm.InstanceCount = capacity; mm.VisibleInstanceCount = 0; batches[b] = mm;
            AddChild(new MultiMeshInstance3D { Multimesh = mm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }
        Sync();
    }
    private int Batch(int id) => units.Faction[id] * 3 + units.Kind[id];
    private static readonly Color Blue = new(0.12f, 0.45f, 1f);
    private static readonly Color Red = new(1f, 0.15f, 0.12f);

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
        buffer[offset + 3] = position.X;
        buffer[offset + 7] = position.Y + (units.Kind[id] == 2 ? 0.65f : 0.55f);
        buffer[offset + 11] = position.Z;
        Color color = units.Flash[id] > 0 ? Colors.White : batch < 3 ? Blue : Red;
        if (buffer[offset + 12] != color.R || buffer[offset + 13] != color.G ||
            buffer[offset + 14] != color.B || buffer[offset + 15] != color.A)
            StoreColor(buffer, offset, color);

        int changeCount = changeCounts[batch]++;
        if (changeCount < SparseUpdateLimit) changedSlots[batch][changeCount] = slot;
    }

    private void Upload(int batch)
    {
        MultiMesh mesh = batches[batch];
        int changed = changeCounts[batch];
        int visible = counts[batch];
        if (changed > SparseUpdateLimit && visible > 0)
        {
            mesh.Buffer = buffers[batch];
        }
        else if (changed > 0 && visible > 0)
        {
            float[] buffer = buffers[batch];
            for (int i = 0; i < changed; i++)
            {
                int slot = changedSlots[batch][i];
                if (slot >= visible) continue;
                int offset = slot * InstanceStride;
                var position = new Vector3(buffer[offset + 3], buffer[offset + 7], buffer[offset + 11]);
                mesh.SetInstanceTransform(slot, new Transform3D(Basis.Identity, position));
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
        for (int b = 0; b < 6; b++) Upload(b);
    }
}
