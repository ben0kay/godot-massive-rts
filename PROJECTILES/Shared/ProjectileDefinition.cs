// Editable projectile flight and visual settings shared by ranged units.
using Godot;

namespace MassiveRts;

public enum ObstacleFireMode { RequiresDirectLineOfSight, IndirectFire }

[Tool]
[GlobalClass]
public partial class ProjectileDefinition : Resource
{
    [Export] public ObstacleFireMode ObstacleFire { get; set; }
    [Export] public bool BlocksOnObstacles { get; set; } = true;
    [Export] public Mesh VisualMesh { get; set; }
    [Export(PropertyHint.Range, "1,200,0.1,or_greater")] public float Speed { get; set; } = 45f;
    [Export(PropertyHint.Range, "0,5,0.05,or_greater")] public float ArcHeight { get; set; } = 0.65f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float ArcHeightPerDistance { get; set; } = 0.08f;

    [ExportGroup("Explosion")]
    private bool explodesOnImpact;
    [Export] public bool ExplodesOnImpact
    {
        get => explodesOnImpact;
        set
        {
            if (explodesOnImpact == value) return;
            explodesOnImpact = value;
            NotifyPropertyListChanged();
        }
    }
    [Export(PropertyHint.Range, "0.1,64,0.1,suffix:u")] public float ExplosionRadius { get; set; } = 7f;
    // Fraction of the firing unit's authored centre damage.
    [Export(PropertyHint.Range, "0,1,0.0125")] public float ExplosionEdgeFraction { get; set; } = 0.3125f;

    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        if (ExplodesOnImpact) return;
        string name = property["name"].AsString();
        if (name == nameof(ExplosionRadius) || name == nameof(ExplosionEdgeFraction))
            property["usage"] = (long)(PropertyUsageFlags.Storage | PropertyUsageFlags.ScriptVariable);
    }
}

public readonly record struct ProjectileFlight(float Speed, float ArcHeight, float ArcHeightPerDistance,
    ObstacleFireMode ObstacleFire, bool BlocksOnObstacles, float ExplosionRadius, float ExplosionEdgeFraction);
