// Cosmetic shell casings: one shared capped mesh batch, no rigid bodies or gameplay collision.
using Godot;

namespace MassiveRts;

internal sealed class ShellCasingEffects
{
    private const int Capacity = 256;
    private const float Lifetime = 4.5f, FadeSeconds = 1.0f, Gravity = 12f;
    private const float Radius = 0.14f;
    private struct Casing
    {
        public Vector3 Position, Velocity, SpinAxis;
        public float Scale, Angle, Spin, Age, Ground;
        public int Bounces;
        public bool Settled;
    }

    private readonly Casing[] active = new Casing[Capacity];
    private readonly TerrainHeightField terrain;
    private readonly MultiMesh batch;
    private static Mesh sharedMesh;
    private int count;
    private uint sequence;

    internal ShellCasingEffects(Node3D owner, TerrainHeightField terrain)
    {
        this.terrain = terrain;
        batch = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = sharedMesh ??= BuildMesh(),
            InstanceCount = Capacity, VisibleInstanceCount = 0
        };
        owner.AddChild(new MultiMeshInstance3D
        {
            Name = "ShellCasings", Multimesh = batch,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 5f
        });
    }

    // =========================================================
    // Brass tube with a dark recessed mouth, thick rim and base. Baked facet
    // colors remain readable with the unshaded material at normal battle zoom.
    private static Mesh BuildMesh()
    {
        using var s = new SurfaceTool();
        s.Begin(Mesh.PrimitiveType.Triangles);
        s.SetMaterial(new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        });
        const int sides = 10;
        Color brass = new(0.83f, 0.56f, 0.24f), rim = new(0.96f, 0.76f, 0.40f), dark = new(0.21f, 0.12f, 0.06f);
        void Ring(float ya, float ra, float yb, float rb, Color color)
        {
            Vector3 P(float y, float r, int i)
            {
                float angle = i * Mathf.Tau / sides;
                return new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
            }
            for (int i = 0; i < sides; i++)
            {
                float shade = 0.76f + 0.24f * Mathf.Cos((i + 0.5f) * Mathf.Tau / sides);
                Color lit = new(color.R * shade, color.G * shade, color.B * shade, 1f);
                Vector3 a = P(ya, ra, i), b = P(ya, ra, i + 1), c = P(yb, rb, i + 1), d = P(yb, rb, i);
                void V(Vector3 p) { s.SetColor(lit); s.AddVertex(p); }
                V(a); V(b); V(c); V(a); V(c); V(d);
            }
        }
        Ring(-0.32f, 0f, -0.32f, Radius, rim);
        Ring(-0.32f, Radius, -0.25f, Radius, rim);
        Ring(-0.25f, Radius, -0.22f, 0.115f, dark);
        Ring(-0.22f, 0.115f, -0.18f, 0.132f, brass);
        Ring(-0.18f, 0.132f, 0.27f, 0.115f, brass);
        Ring(0.27f, 0.115f, 0.32f, 0.12f, rim);
        Ring(0.32f, 0.12f, 0.32f, 0.083f, rim);
        Ring(0.32f, 0.083f, 0.20f, 0.080f, dark);
        Ring(0.20f, 0.080f, 0.20f, 0f, dark);
        return s.Commit();
    }

    // Independent deterministic cosmetic variation, never consumes combat RNG.
    internal void Emit(Vector3 port, Vector3 outward, float scale)
    {
        if (count >= Capacity || !float.IsFinite(scale) || scale <= 0f) return;
        scale = Mathf.Clamp(scale, 0.1f, 3f);
        outward.Y = 0f;
        outward = outward.Normalized();
        if (outward.LengthSquared() < 0.001f) return;
        float seed = (unchecked(++sequence * 2654435761u) >> 16) / 65535f;
        Vector3 direction = outward.Rotated(Vector3.Up, (seed - 0.5f) * 0.34f);
        active[count++] = new Casing
        {
            Position = port, Velocity = direction * Mathf.Lerp(3.8f, 5.6f, seed) * Mathf.Lerp(0.55f, 1f, Mathf.Min(1f, scale))
                + Vector3.Up * Mathf.Lerp(2.5f, 3.6f, seed),
            SpinAxis = new Vector3(0.35f + seed, 0.22f, 1f).Normalized(),
            Angle = seed * Mathf.Tau, Spin = Mathf.Lerp(11f, 20f, seed), Scale = scale
        };
    }

    internal void Advance(float delta)
    {
        if (count == 0) return;
        for (int i = count - 1; i >= 0; i--)
        {
            ref Casing c = ref active[i];
            c.Age += delta;
            if (c.Age >= Lifetime)
            {
                active[i] = active[--count];
                continue;
            }
            if (!c.Settled)
            {
                c.Velocity.Y -= Gravity * delta;
                c.Position += c.Velocity * delta;
                c.Angle += c.Spin * delta;
                c.Ground = (terrain?.At(c.Position.X, c.Position.Z) ?? 0f) + Radius * c.Scale;
                if (c.Position.Y <= c.Ground && c.Velocity.Y <= 0f)
                {
                    c.Position.Y = c.Ground;
                    if (c.Bounces++ == 0)
                    {
                        c.Velocity = new Vector3(c.Velocity.X * 0.40f, Mathf.Clamp(-c.Velocity.Y * 0.32f, 0.6f, 2.0f), c.Velocity.Z * 0.40f);
                        c.Spin *= 0.55f;
                    }
                    else c.Settled = true;
                }
            }
        }
        // Pack live slots once after swap-removal.
        for (int i = 0; i < count; i++)
        {
            ref readonly Casing c = ref active[i];
            Basis pose = c.Settled
                ? new Basis(Vector3.Up, c.Angle) * new Basis(Vector3.Right, Mathf.Pi * 0.5f)
                : new Basis(c.SpinAxis, c.Angle);
            batch.SetInstanceTransform(i, new Transform3D(pose * Basis.FromScale(Vector3.One * c.Scale), c.Position));
            Color tint = Colors.White;
            tint.A = Mathf.Clamp((Lifetime - c.Age) / FadeSeconds, 0f, 1f);
            batch.SetInstanceColor(i, tint);
        }
        batch.VisibleInstanceCount = count;
    }
}
