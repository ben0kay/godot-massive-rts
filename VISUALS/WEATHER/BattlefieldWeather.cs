// Owns the optional camera-space weather pass and advances its world-space drift.
using Godot;

namespace MassiveRts;

internal sealed class BattlefieldWeather
{
    private readonly Camera3D camera;
    private MeshInstance3D overlay;
    private ShaderMaterial material;
    private float driftOffset, driftSpeed;
    private bool drifting;

    internal BattlefieldWeather(Camera3D camera)
    {
        this.camera = camera;
    }

    internal void Configure(bool enabled, float mist, float drift, float haze)
    {
        bool visible = enabled && (mist > 0f || haze > 0f);
        drifting = visible && mist > 0f && drift > 0f;
        driftSpeed = drift;
        if (!visible)
        {
            if (overlay != null) overlay.Hide();
            return;
        }

        if (overlay == null)
        {
            material = new ShaderMaterial
            {
                Shader = GD.Load<Shader>("res://VISUALS/WEATHER/BattlefieldWeather.gdshader"),
                RenderPriority = 1
            };
            overlay = new MeshInstance3D
            {
                Name = "Battlefield Weather",
                Position = new Vector3(0f, 0f, -2f),
                Mesh = new QuadMesh { Size = new Vector2(2f, 2f), Material = material },
                ExtraCullMargin = 16f,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            camera.AddChild(overlay);
        }
        material.SetShaderParameter("mist_density", mist);
        material.SetShaderParameter("haze_density", haze);
        overlay.Show();
    }

    internal void Advance(float delta)
    {
        if (!drifting) return;
        driftOffset += delta * driftSpeed;
        material.SetShaderParameter("drift_offset", driftOffset);
    }
}
