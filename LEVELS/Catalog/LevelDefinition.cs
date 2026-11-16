// Describes an authored level without loading its scene in the campaign menu.
using Godot;

namespace MassiveRts;

[Tool]
[GlobalClass]
public partial class LevelDefinition : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public int Order { get; set; }
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
    [Export(PropertyHint.File, "*.tscn,*.scn")] public string ScenePath { get; set; } = "";
    [Export] public bool Enabled { get; set; } = true;

    internal const float DefaultMapSize = 5000f;
    private float mapSize = DefaultMapSize;

    [Export(PropertyHint.Range, "64,5000,4")]
    public float MapSize
    {
        get => mapSize;
        set
        {
            if (!float.IsFinite(value)) return;
            float size = Mathf.Round(Mathf.Clamp(value, 64f, DefaultMapSize) / 4f) * 4f;
            if (size == mapSize) return;
            mapSize = size;
            EmitChanged();
        }
    }
}
