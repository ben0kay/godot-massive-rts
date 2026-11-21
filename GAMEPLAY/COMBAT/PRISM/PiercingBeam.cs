// Shared terrain clipping and footprint intersection for instantaneous and sustained beams.
using Godot;

namespace MassiveRts;

internal static class PiercingBeam
{
    internal static float ClipTerrain(TerrainHeightField terrain, Vector3 from, Vector3 segment, float end)
    {
        if (terrain != null) return Mathf.Min(end, terrain.SurfaceHit(from, segment, end));
        return segment.Y < 0f && from.Y + segment.Y < 0f
            ? Mathf.Min(end, Mathf.Clamp(-from.Y / segment.Y, 0f, 1f)) : end;
    }

    internal static bool Intersect(Vector3 offset, Vector2 along, float reach, float slope,
        float footprint, float height, float beamRadius, out float entry)
    {
        entry = 0f;
        Vector2 cross = ObstacleField.Xz(offset);
        float projection = cross.Dot(along);
        float perpendicular = cross.X * along.Y - cross.Y * along.X;
        float radius = footprint + beamRadius;
        float chord = radius * radius - perpendicular * perpendicular;
        if (chord < 0f) return false;
        float half = Mathf.Sqrt(chord);
        float enter = Mathf.Max(0f, projection - half), exit = Mathf.Min(reach, projection + half);
        if (enter > exit) return false;
        float a = enter * slope, b = exit * slope;
        if (Mathf.Max(a, b) + beamRadius < offset.Y || Mathf.Min(a, b) - beamRadius > offset.Y + height)
            return false;
        entry = enter;
        if (slope != 0f)
        {
            float firstY = slope > 0f ? offset.Y - beamRadius : offset.Y + height + beamRadius;
            entry = Mathf.Clamp(firstY / slope, enter, exit);
        }
        return true;
    }
}
