// Editable health, construction and extraction balance for Carbon deposits.
using Godot;

namespace MassiveRts;

[Tool]
[GlobalClass]
public partial class CarbonExtractorSettings : Resource
{
    [Export(PropertyHint.Range, "1,5000,1")] public float Health { get; set; } = 350f;
    [Export] public string ArmourClass { get; set; } = "Medium";
    [Export(PropertyHint.Range, "0,1,0.01")] public float Armour { get; set; } = 0f;
    [Export(PropertyHint.Range, "0.5,30,0.5")] public float BuildSeconds { get; set; } = 4f;
    [Export(PropertyHint.Range, "1,100,1")] public int CarbonPerSecond { get; set; } = 5;
}
