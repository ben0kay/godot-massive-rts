// Samples melee steering work for the benchmark without timing every soldier.
namespace MassiveRts;

public struct MovementProfile
{
    public const int SampleStride = 32;
    public int Ticks, SidestepCalls, BlockedSidesteps;
    public long SidestepTicks, HoldTicks;
    public double TotalMilliseconds, RunUpMilliseconds;
}
