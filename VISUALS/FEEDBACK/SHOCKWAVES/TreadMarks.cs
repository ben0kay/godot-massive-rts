// Optional visual-only twin tread impressions. Track opted-in units, not all soldiers every tick.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed class TreadMarks
{
    private const int Capacity = 512;
    private const float Lifetime = 12f, Fade = 4f, SampleSeconds = 0.16f;
    private readonly record struct Track(int Id, Vector3 Last);
    private readonly record struct Mark(Transform3D Pose, float Expires);
    private readonly List<Track> tracked = new();
    private readonly Mark[] marks = new Mark[Capacity];
    private readonly Node3D parent;
    private UnitState source;
    private bool[] enabled;
    private MultiMesh mesh;
    private int scanned, count, recycle;
    private float elapsed, clock, refresh;

    internal TreadMarks(Node3D parent) => this.parent = parent;

    // =========================================================
    internal void Sample(UnitState units, float delta)
    {
        if (source != units || scanned > units.Count)
        {
            Clear();
            source = units;
            enabled = new bool[units.Catalog.Count];
            for (int i = 0; i < enabled.Length; i++)
                enabled[i] = units.Catalog.Definition(i)?.LeavesTreadMarks == true;
        }
        elapsed += delta;
        if (elapsed < SampleSeconds) return;
        elapsed = 0f;
        for (; scanned < units.Count; scanned++)
            if (enabled[units.Type[scanned]] && units.Health[scanned] > 0f)
                tracked.Add(new Track(scanned, units.Position[scanned]));
        for (int i = tracked.Count - 1; i >= 0; i--)
        {
            var track = tracked[i];
            if (units.Health[track.Id] <= 0f)
            {
                tracked[i] = tracked[^1];
                tracked.RemoveAt(tracked.Count - 1);
                continue;
            }
            Vector3 now = units.Position[track.Id];
            Vector3 motion = now - track.Last;
            motion.Y = 0f;
            float distance = motion.Length();
            if (distance < 0.65f) continue;
            tracked[i] = track with { Last = now };
            if (distance > 5f) continue; // no long marks from teleport/redeployment
            ref readonly var type = ref units.Catalog[units.Type[track.Id]];
            Vector3 along = motion / distance;
            float width = Mathf.Max(0.5f, type.Outline.RadiusX * 1.40f);
            Vector3 center = (track.Last + now) * 0.5f - along * (type.Outline.RadiusZ * 0.62f);
            var terrain = units.Grounding?.Field;
            Vector3 start = center - along * distance * 0.5f, end = center + along * distance * 0.5f;
            start.Y = (terrain?.At(start.X, start.Z) ?? 0f) + 0.055f;
            end.Y = (terrain?.At(end.X, end.Z) ?? 0f) + 0.055f;
            if (Mathf.Abs(end.Y - start.Y) > distance * 0.6f) continue;
            Vector3 right = new(along.Z, 0f, -along.X);
            Emit(new Transform3D(new Basis(right * width, Vector3.Up, end - start), (start + end) * 0.5f));
        }
    }

    private void Emit(Transform3D pose)
    {
        if (mesh == null)
        {
            var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://VISUALS/FEEDBACK/SHOCKWAVES/TreadMark.gdshader") };
            mesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true,
                Mesh = new PlaneMesh { Size = Vector2.One, Material = material },
                InstanceCount = Capacity, VisibleInstanceCount = 0
            };
            parent.AddChild(new MultiMeshInstance3D { Multimesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }
        int slot = count < Capacity ? count++ : recycle++ % Capacity;
        marks[slot] = new Mark(pose, clock + Lifetime);
        Write(slot);
        mesh.VisibleInstanceCount = count;
    }

    private void Write(int slot)
    {
        mesh.SetInstanceTransform(slot, marks[slot].Pose);
        Opacity(slot);
    }

    private void Opacity(int slot) => mesh.SetInstanceCustomData(slot,
        new Color(Mathf.Clamp((marks[slot].Expires - clock) / Fade, 0f, 1f), 0f, 0f, 0f));

    internal void Advance(float dt)
    {
        if (count == 0) return;
        clock += dt;
        refresh -= dt;
        if (refresh > 0f) return;
        refresh = 0.15f;
        for (int i = count - 1; i >= 0; i--)
        {
            if (marks[i].Expires <= clock)
            {
                int last = --count;
                if (i == last) continue;
                marks[i] = marks[last];
                Write(i);
            }
            else if (marks[i].Expires - clock < Fade) Opacity(i);
        }
        mesh.VisibleInstanceCount = count;
    }

    internal void Clear()
    {
        source = null;
        tracked.Clear();
        scanned = count = recycle = 0;
        elapsed = clock = refresh = 0f;
        if (mesh != null) mesh.VisibleInstanceCount = 0;
    }
}
