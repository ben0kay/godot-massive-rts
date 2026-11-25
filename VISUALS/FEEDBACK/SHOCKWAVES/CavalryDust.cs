// Samples actual travel at a low cadence. No scan of infantry after registration.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed class CavalryDust
{
    private const float SampleInterval = 0.10f;
    private readonly record struct Track(int Id, Vector3 Position);
    private readonly List<Track> cavalry = new();
    private UnitState lastUnits;
    private int scanned;
    private float elapsed;

    internal void Sample(UnitState units, float delta, ShockwaveRenderer effects)
    {
        if (lastUnits != units || scanned > units.Count)
        {
            Clear();
            lastUnits = units;
        }
        elapsed += delta;
        if (elapsed < SampleInterval) return;
        ref readonly var stats = ref units.Catalog[RuntimeUnitCatalog.Diamond];
        for (int i = cavalry.Count - 1; i >= 0; i--)
        {
            var track = cavalry[i];
            if (units.Health[track.Id] <= 0f)
            {
                cavalry[i] = cavalry[^1];
                cavalry.RemoveAt(cavalry.Count - 1);
                continue;
            }
            Vector3 now = units.Position[track.Id];
            cavalry[i] = track with { Position = now };
            Vector2 movement = ObstacleField.Xz(now - track.Position);
            float speed = movement.Length() / elapsed;
            if (speed < 0.7f || speed > stats.Speed * 1.65f) continue;

            Vector2 rear = ObstacleField.Xz(now) - movement.Normalized() * stats.Footprint;
            Vector3 point = units.OnTerrain(new Vector3(rear.X, 0f, rear.Y)) + Vector3.Up * 0.18f;
            float size = Mathf.Lerp(0.60f, 1.65f, Mathf.Clamp(speed / stats.Speed, 0f, 1f));
            effects.EmitCavalryDust(point, size);
        }
        for (; scanned < units.Count; scanned++)
            if (units.Type[scanned] == RuntimeUnitCatalog.Diamond && units.Health[scanned] > 0f)
                cavalry.Add(new Track(scanned, units.Position[scanned]));
        elapsed = 0f;
    }

    internal void Clear()
    {
        cavalry.Clear();
        lastUnits = null;
        scanned = 0;
        elapsed = 0f;
    }
}
