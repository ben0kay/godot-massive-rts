// Reprojects only displaced soldiers, once after movement and crowd corrections.
namespace MassiveRts;

public sealed class TerrainGrounding
{
    private readonly int[] moved;
    private readonly bool[] pending;
    private int count;
    public TerrainHeightField Field { get; }

    public TerrainGrounding(TerrainHeightField field, int capacity)
    {
        Field = field;
        moved = new int[capacity];
        pending = new bool[capacity];
    }

    public void MarkMoved(int id)
    {
        if (pending[id]) return;
        pending[id] = true;
        moved[count++] = id;
    }

    public int Apply(UnitState units)
    {
        for (int index = 0; index < count; index++)
        {
            int id = moved[index];
            ref var position = ref units.Position[id];
            position.Y = Field.At(position.X, position.Z);
            pending[id] = false;
        }
        int samples = count;
        count = 0;
        return samples;
    }
}
