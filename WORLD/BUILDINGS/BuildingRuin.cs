// Stores an authored destroyed building and previews the shared battle husk mesh.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

public partial class BuildingRuin : Node3D
{
    internal const string Container = "Scenery";
    [Export] public Vector2 Footprint { get; set; } = new(10f, 10f);

    internal Rect2 BoundsAt(Vector3 point) => new(ObstacleField.Xz(point) - Footprint * .5f, Footprint);
    internal bool Fits(Vector3 point, float half)
    {
        var bounds = BoundsAt(point);
        return Footprint.IsFinite() && Footprint.X > 0f && Footprint.Y > 0f &&
            new Rect2(-half, -half, half * 2f, half * 2f).Encloses(bounds);
    }

    internal static IEnumerable<BuildingRuin> AuthoredOn(Node3D map)
    {
        if (map?.GetNodeOrNull<Node3D>(Container) is not Node3D scenery) yield break;
        foreach (Node node in scenery.GetChildren())
            if (node is BuildingRuin ruin) yield return ruin;
    }

    public override void _Ready()
    {
        AddChild(new MeshInstance3D
        {
            Mesh = WreckField.SharedHuskMesh(),
            Scale = WreckField.HuskScale(Footprint),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        }, false, InternalMode.Back);
    }
}
