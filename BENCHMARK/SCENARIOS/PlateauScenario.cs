// Repeatable plateau with an exact flat core and smooth shoulders.
using Godot;

namespace MassiveRts;

public static class PlateauScenario
{
    private const float PlainEdge = -4.2f;

    public static TerrainHeightField Create(float height, float shoulder)
    {
        var topMin = new Vector2(PlainEdge + shoulder, -96f);
        var topMax = new Vector2(155f, 96f);
        var apron = new Vector2(shoulder, shoulder);
        return new TerrainHeightField(topMin - apron, topMax + apron, point =>
        {
            float distance = point.DistanceTo(point.Max(topMin).Min(topMax));
            float t = Mathf.Clamp(1f - distance / shoulder, 0f, 1f);
            return height * t * t * (3f - 2f * t);
        });
    }
}
