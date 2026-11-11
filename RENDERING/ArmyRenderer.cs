// Six packed MultiMesh batches with dirty-instance updates and constant-time death removal.
using Godot;
namespace MassiveRts;

public partial class ArmyRenderer : Node3D
{
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
        for (int b = 0; b < 6; b++)
        {
            // Capacity is counted from the completed deployment before allocating GPU buffers.
            int capacity = 0;
            for (int i = 0; i < state.Count; i++) if (Batch(i) == b) capacity++;
            members[b] = new int[capacity];
            var material = new StandardMaterial3D { VertexColorUseAsAlbedo = true, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
            Mesh mesh = (b % 3) switch
            {
                1 => new BoxMesh { Size = new Vector3(0.8f, 1.1f, 0.8f), Material = material },
                2 => new CylinderMesh { TopRadius = 0.65f, BottomRadius = 0.65f, Height = 1.3f, RadialSegments = 6, Material = material },
                _ => new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 0.9f, RadialSegments = 3, Material = material }
            };
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = mesh };
            mm.InstanceCount = capacity; mm.VisibleInstanceCount = 0; batches[b] = mm;
            AddChild(new MultiMeshInstance3D { Multimesh = mm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }
        Sync();
    }
    private int Batch(int id) => units.Faction[id] * 3 + units.Kind[id];
    private Color Colour(int id) => units.Flash[id] > 0 ? Colors.White : units.Faction[id] == 0 ? new Color(0.12f, 0.45f, 1f) : new Color(1f, 0.15f, 0.12f);
    // =========================================================
    private void Write(int id, int batch, int slot)
    {
        Vector3 position = units.Position[id] + Vector3.Up * (units.Kind[id] == 2 ? 0.65f : 0.55f);
        batches[batch].SetInstanceTransform(slot, new Transform3D(Basis.Identity, position));
        batches[batch].SetInstanceColor(slot, Colour(id));
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
        for (int b = 0; b < 6; b++) batches[b].VisibleInstanceCount = counts[b];
    }
}
