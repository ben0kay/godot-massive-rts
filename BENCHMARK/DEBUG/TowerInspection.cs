// Draws one selected tower's footprint, effective radius, health, and kills.
using Godot;

namespace MassiveRts;

internal sealed partial class TowerInspection : Control
{
    private const int Segments = 96;
    private readonly Vector3[] rangeWorld = new Vector3[Segments];
    private readonly Vector2[] rangeScreen = new Vector2[Segments];
    private readonly Vector3[] footprint = new Vector3[4];
    private StructureHealth selected;
    private Camera3D camera;
    private Vector3 pivot;
    private float radius;

    public override void _Ready() => Clear();

    internal void Clear()
    {
        selected = null;
        Hide();
        SetProcess(false);
    }

    internal void Select(StructureHealth tower, BuildingStats stats, Vector3 center, Camera3D view, TerrainHeightField terrain)
    {
        selected = tower;
        camera = view;
        pivot = center;
        radius = (stats.AttacksSoldiers || stats.AirDefense.Enabled) ? stats.MaximumRange : stats.ShieldEmitter.Enabled ? stats.ShieldEmitter.Radius : 0f;
        Vector2 min = tower.Area.Min - Vector2.One * 0.12f;
        Vector2 max = tower.Area.Max + Vector2.One * 0.12f;
        footprint[0] = Ground(min.X, min.Y);
        footprint[1] = Ground(max.X, min.Y);
        footprint[2] = Ground(max.X, max.Y);
        footprint[3] = Ground(min.X, max.Y);
        if (radius > 0f)
        {
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.Tau / Segments;
                rangeWorld[i] = Ground(center.X + Mathf.Cos(angle) * radius, center.Z + Mathf.Sin(angle) * radius);
            }
        }
        Show();
        SetProcess(true);
        QueueRedraw();

        Vector3 Ground(float x, float z) => new(x, (terrain?.At(x, z) ?? 0f) + 0.06f, z);
    }

    public override void _Process(double delta)
    {
        if (selected == null || selected.Destroyed) Clear();
        else QueueRedraw();
    }

    public override void _Draw()
    {
        if (selected == null || camera.IsPositionBehind(pivot)) return;
        Color ink = Factions.Color(selected.Faction).Lightened(0.46f);
        if (radius > 0f)
        {
            bool entireRingVisible = true;
            for (int i = 0; i < Segments; i++)
            {
                entireRingVisible &= !camera.IsPositionBehind(rangeWorld[i]);
                rangeScreen[i] = camera.UnprojectPosition(rangeWorld[i]);
            }
            if (entireRingVisible) DrawColoredPolygon(rangeScreen, new Color(ink.R, ink.G, ink.B, 0.10f));
            for (int i = 0; i < Segments; i++)
            {
                int next = (i + 1) % Segments;
                if (!entireRingVisible && (camera.IsPositionBehind(rangeWorld[i]) || camera.IsPositionBehind(rangeWorld[next]))) continue;
                DrawLine(rangeScreen[i], rangeScreen[next], new Color(ink.R, ink.G, ink.B, 0.85f), 2f, true);
            }
        }

        for (int i = 0; i < footprint.Length; i++)
            DrawLine(camera.UnprojectPosition(footprint[i]), camera.UnprojectPosition(footprint[(i + 1) % footprint.Length]), ink, 2f, true);

        Vector2 bar = camera.UnprojectPosition(pivot + Vector3.Up * 1.25f) - new Vector2(24f, 9f);
        DrawRect(new Rect2(bar - Vector2.One, new Vector2(50f, 5f)), new Color("#101723"));
        DrawRect(new Rect2(bar, new Vector2(48f * selected.Health / selected.MaxHealth, 3f)), new Color("#7ae490"));

        Vector2 label = camera.UnprojectPosition(pivot) + new Vector2(32f, 4f);
        string kills = $"Kills {selected.Kills}";
        DrawString(ThemeDB.FallbackFont, label + Vector2.One, kills, fontSize: 13, modulate: Colors.Black);
        DrawString(ThemeDB.FallbackFont, label, kills, fontSize: 13, modulate: Colors.White);
    }
}
