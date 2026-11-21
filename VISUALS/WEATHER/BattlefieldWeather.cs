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
    private ObstacleRectangle[] water;

    // The compiled water footprint already excludes rocks and bridge decks.
    internal void SetWater(ObstacleRectangle[] rectangles) => water = rectangles;


    internal BattlefieldWeather(Camera3D camera)
    {
        this.camera = camera;
    }

    internal void Configure(bool enabled, float mist, float drift, float haze)
    {
        bool visible = enabled && (mist > 0f || haze > 0f);
        drifting = visible && drift > 0f;
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
            BindWaterMask();
        }
        material.SetShaderParameter("mist_density", mist);
        material.SetShaderParameter("haze_density", haze);
        overlay.Show();
    }

    // Rasterize soft banks once on enable. The shader only needs one filtered lookup.
    private void BindWaterMask()
    {
        if (water == null || water.Length == 0) return;
        const float bankFade = 5f;
        const int maxSide = 512;
        Vector2 min = water[0].Min, max = water[0].Max;
        foreach (var area in water)
        {
            min = min.Min(area.Min);
            max = max.Max(area.Max);
        }
        Vector2 border = Vector2.One * (bankFade + 2f);
        min -= border;
        Vector2 size = max + border - min;
        float spacing = Mathf.Max(1f, Mathf.Max(size.X, size.Y) / maxSide);
        int width = Mathf.Min(maxSide, Mathf.CeilToInt(size.X / spacing));
        int height = Mathf.Min(maxSide, Mathf.CeilToInt(size.Y / spacing));
        size = new Vector2(width, height) * spacing;
        var data = new byte[width * height];

        foreach (var area in water)
        {
            Vector2 low = area.Min, high = area.Max;
            int left = Mathf.Max(0, Mathf.FloorToInt((low.X - bankFade - min.X) / spacing));
            int right = Mathf.Min(width - 1, Mathf.CeilToInt((high.X + bankFade - min.X) / spacing));
            int top = Mathf.Max(0, Mathf.FloorToInt((low.Y - bankFade - min.Y) / spacing));
            int bottom = Mathf.Min(height - 1, Mathf.CeilToInt((high.Y + bankFade - min.Y) / spacing));
            for (int y = top; y <= bottom; y++)
            for (int x = left; x <= right; x++)
            {
                Vector2 point = min + new Vector2(x + 0.5f, y + 0.5f) * spacing;
                float distance = point.DistanceTo(point.Max(low).Min(high));
                byte density = (byte)Mathf.RoundToInt((1f - Mathf.SmoothStep(0f, bankFade, distance)) * 255f);
                int index = y * width + x;
                if (density > data[index]) data[index] = density;
            }
        }
        using var image = Image.CreateFromData(width, height, false, Image.Format.R8, data);
        material.SetShaderParameter("water_mask", ImageTexture.CreateFromImage(image));
        material.SetShaderParameter("water_bounds", new Vector4(min.X, min.Y, size.X, size.Y));
    }

    internal void Advance(float delta)
    {
        if (!drifting) return;
        driftOffset += delta * driftSpeed;
        material.SetShaderParameter("drift_offset", driftOffset);
    }
}
