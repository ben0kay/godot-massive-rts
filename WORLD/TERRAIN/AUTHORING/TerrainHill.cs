// An editor-authored height contribution centered on this marker.
using Godot;

namespace MassiveRts;

[Tool]
public partial class TerrainHill : Marker3D
{
    [Export(PropertyHint.Range, "8,256,1")] public float Radius { get; set; } = 48f;
    [Export(PropertyHint.Range, "0,80,0.5")] public float Rise { get; set; } = 8f;
}
