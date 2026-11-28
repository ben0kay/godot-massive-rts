// Lets editor playtests inspect a live tower without interfering with deployment.
using Godot;

namespace MassiveRts;

public partial class BenchmarkArena
{
    private TowerInspection towerInspection;

    public override void _UnhandledInput(InputEvent input)
    {
        if (HandleSupportInput(input)) return;
        if (towerInspection == null || painter == null || painter.Visible || spawning || defenseSystem == null ||
            input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } click) return;

        Vector3 from = camera.ProjectRayOrigin(click.Position);
        Vector3 direction = camera.ProjectRayNormal(click.Position);
        float nearest = float.PositiveInfinity;
        StructureHealth picked = null;
        BuildingStats stats = default;
        Vector3 pivot = default;
        for (int i = 0; i < defenseSystem.Structures.Count; i++)
        {
            if (!defenseSystem.TryGetTower(i, out var tower, out var candidate, out var center)) continue;
            Vector2 min = tower.Area.Min, max = tower.Area.Max;
            float floor = center.Y - Config.ObstacleHeight - 0.25f;
            float top = center.Y + 1f;
            float enter = 0f, exit = nearest;
            if (!ClipAxis(from.X, direction.X, min.X, max.X, ref enter, ref exit) ||
                !ClipAxis(from.Y, direction.Y, floor, top, ref enter, ref exit) ||
                !ClipAxis(from.Z, direction.Z, min.Y, max.Y, ref enter, ref exit)) continue;
            nearest = enter;
            picked = tower;
            stats = candidate;
            pivot = center;
        }
        if (picked == null) towerInspection.Clear();
        else
        {
            towerInspection.Select(picked, stats, pivot, camera, terrain);
            GetViewport().SetInputAsHandled();
        }
    }

    private static bool ClipAxis(float origin, float direction, float min, float max, ref float enter, ref float exit)
    {
        if (Mathf.Abs(direction) < 0.00001f) return origin >= min && origin <= max;
        float first = (min - origin) / direction, last = (max - origin) / direction;
        enter = Mathf.Max(enter, Mathf.Min(first, last));
        exit = Mathf.Min(exit, Mathf.Max(first, last));
        return enter <= exit;
    }
}
