// Saved neutral terrain object with shared rock, heat and steam meshes.
using Godot;

namespace MassiveRts;

[Tool]
public partial class GeothermalVent : Node3D
{
    [Export] public float Radius { get; set; } = 7f;
    [Export] public float DormantSeconds { get; set; } = 8f;
    [Export] public float WarningSeconds { get; set; } = 2f;
    [Export] public float EruptionSeconds { get; set; } = 1f;
    [Export] public float InitialOffset { get; set; }
    [Export] public float Strength { get; set; } = 1f;
    [Export] public float EruptionDamage { get; set; }

    internal const int MaxPerMap = 32;
    internal float EffectiveRadius => Setting(Radius, 7f, 1f, 20f);
    internal float Rest => Setting(DormantSeconds, 8f, 0.25f, 120f);
    internal float Warning => Setting(WarningSeconds, 2f, 0f, 30f);
    internal float Burst => Setting(EruptionSeconds, 1f, 0.1f, 15f);
    internal float Power => Setting(Strength, 1f, 0.1f, 3f);
    internal float Damage => Setting(EruptionDamage, 0f, 0f, 1000f);

    private static Mesh baseMesh, steamMesh;
    private static Material radiusMaterial;
    private MeshInstance3D steam, radiusView;
    private ShaderMaterial steamMaterial;

    public override void _Ready()
    {
        baseMesh ??= BuildBase();
        AddChild(new MeshInstance3D { Mesh = baseMesh }, false, InternalMode.Back);
        steamMesh ??= new QuadMesh { Size = new Vector2(2f, 3f) };
        steamMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://WORLD/TERRAIN/RENDERING/GeothermalSteam.gdshader") };
        steam = new MeshInstance3D { Mesh = steamMesh, MaterialOverride = steamMaterial, ExtraCullMargin = 8f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(steam, false, InternalMode.Back);
        SetActivity(0f);
    }

    internal void Seat(TerrainHeightField terrain)
    {
        Vector3 at = GlobalPosition;
        at.Y = terrain?.At(at.X, at.Z) ?? 0f;
        if (at != GlobalPosition) GlobalPosition = at;
    }

    internal void SetActivity(float activity)
    {
        steamMaterial?.SetShaderParameter("activity", activity);
    }

    internal void ShowRadius(bool show)
    {
        if (show && radiusView == null)
        {
            radiusMaterial ??= new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 0.48f, 0.15f, 0.20f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                NoDepthTest = true
            };
            radiusView = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.98f, OuterRadius = 1f },
                MaterialOverride = radiusMaterial, Position = Vector3.Up * 0.18f,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(radiusView, false, InternalMode.Back);
        }
        if (radiusView == null) return;
        radiusView.Visible = show;
        if (show) radiusView.Scale = new Vector3(EffectiveRadius, 1f, EffectiveRadius);
    }

    internal static float Setting(float value, float fallback, float min, float max) =>
        float.IsFinite(value) ? Mathf.Clamp(value, min, max) : fallback;

    private static Mesh BuildBase()
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(new StandardMaterial3D { VertexColorUseAsAlbedo = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded });
        FacetedSoldierMeshes.AppendBeveledRectangle(surface, new Vector3(0f, 0.025f, 0f),
            new Vector3(2.6f, 0.05f, 2.6f), new Color("#242020"), sideShade: 0.75f);
        for (int i = 0; i < 12; i++)
        {
            float angle = i * Mathf.Tau / 12f;
            Vector3 direction = Vector3.Right.Rotated(Vector3.Up, angle);
            float height = 0.25f + (i * 7 % 5) * 0.07f;
            Vector3 at = direction * (0.84f + (i % 3) * 0.08f);
            at.Y = height * 0.5f;
            FacetedSoldierMeshes.AppendBeveledRectangle(surface, at,
                new Vector3(0.53f, height, 0.38f), i % 2 == 0 ? new Color("#605352") : new Color("#3d3637"),
                sideShade: 0.72f, pitch: (i % 3 - 1) * 0.23f);
            if (i % 3 == 0)
                FacetedSoldierMeshes.AppendBeveledRectangle(surface, direction * 0.49f + Vector3.Up * 0.08f,
                    new Vector3(0.18f, 0.06f, 0.09f), new Color("#d66827"), sideShade: 0.8f);
        }
        return surface.Commit();
    }
}
