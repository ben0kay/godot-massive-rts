// Compact authoritative soldier arrays; stable IDs survive deaths until the next reset.
using Godot;
namespace MassiveRts;

public sealed class UnitState
{
    public readonly Vector3[] Position;
    public readonly Vector3[] Goal;
    public readonly float[] Health, Cooldown, Flash;
    public readonly int[] Target;
    public readonly byte[] Faction, Kind;
    public readonly bool[] Dirty;
    public int Count, Blue, Red;
    public int Alive => Blue + Red;
    public UnitState(int capacity)
    {
        Position = new Vector3[capacity]; Goal = new Vector3[capacity];
        Health = new float[capacity]; Cooldown = new float[capacity]; Flash = new float[capacity];
        Target = new int[capacity]; Faction = new byte[capacity]; Kind = new byte[capacity];
        Dirty = new bool[capacity];
    }
    // =========================================================
    public void Damage(int id, float damage, float flashSeconds)
    {
        if (id < 0 || id >= Count || Health[id] <= 0) return;
        Health[id] = Mathf.Max(0, Health[id] - damage); Flash[id] = flashSeconds; Dirty[id] = true;
        if (Health[id] == 0) { if (Faction[id] == 0) Blue--; else Red--; }
    }
}
// Single flat-ground seam for later elevation; simulation owns world-space positions.
public static class BattlefieldHeight
{
    public static float At(float x, float z) => 0f;
}
