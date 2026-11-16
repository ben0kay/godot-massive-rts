// Inspector settings for paid Worker orders at the Citadel.
using Godot;

namespace MassiveRts;

[GlobalClass]
public partial class CitadelWorkerSettings : Resource
{
    [Export] public string UnitId { get; set; } = "worker";
    [Export(PropertyHint.Range, "0,10000,1,or_greater")] public int CarbonCost { get; set; } = 100;
    [Export(PropertyHint.Range, "0.1,120,0.1,or_greater")] public float ProductionSeconds { get; set; } = 3f;
    [Export(PropertyHint.Range, "1,64,1")] public int QueueLimit { get; set; } = 10;
}
