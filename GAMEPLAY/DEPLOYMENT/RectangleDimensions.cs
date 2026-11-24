// Projects two dimension lines from the placement rectangle onto the viewport.
using System.Globalization;
using Godot;

namespace MassiveRts;

public partial class RectangleDimensions : Node2D
{
    private Camera3D camera, brushCamera;
    private readonly System.Collections.Generic.List<Vector3> brushPoints = new();
    private float brushRadius;
    private Color brushInk;

    internal void SetBrushGhost(Camera3D view, float radius, Color color)
    {
        brushCamera = view;
        brushRadius = radius;
        brushInk = color;
        brushPoints.Clear();
        Visible = camera != null;
        QueueRedraw();
    }

    internal void AddBrushGhost(Vector3 point)
    {
        brushPoints.Add(point);
        Show();
    }
    private Vector2 min, max;
    private string widthText, lengthText;
    private static readonly Color Ink = new(0.92f, 0.97f, 1f);
    private static readonly Color Backdrop = new(0.06f, 0.10f, 0.15f, 0.95f);

    public void Display(Camera3D view, ObstacleRectangle area)
    {
        camera = view;
        min = area.Min;
        max = area.Max;
        widthText = Format(area.Width, area.GridSize);
        lengthText = Format(area.Depth, area.GridSize);
        Show();
        SetProcess(true);
        QueueRedraw();
    }

    private static string Format(int cells, float gridSize) =>
        (cells * gridSize).ToString("0.##", CultureInfo.InvariantCulture) + " u";

    public void Clear()
    {
        camera = null;
        brushPoints.Clear();
        Hide();
        SetProcess(false);
        QueueRedraw();
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        foreach (var point in brushPoints) DrawFootprint(brushCamera, point, brushRadius, brushInk);
        if (camera == null) return;
        Vector3 origin = new(min.X, 0f, min.Y);
        Vector3 width = new(max.X, 0f, min.Y);
        Vector3 length = new(min.X, 0f, max.Y);
        if (camera.IsPositionBehind(origin) || camera.IsPositionBehind(width) || camera.IsPositionBehind(length)) return;

        Vector2 corner = camera.UnprojectPosition(origin);
        Vector2 widthEnd = camera.UnprojectPosition(width);
        Vector2 lengthEnd = camera.UnprojectPosition(length);
        DrawDimension(corner, widthEnd, lengthEnd - corner, widthText);
        DrawDimension(corner, lengthEnd, widthEnd - corner, lengthText);
    }

    protected void DrawFootprint(Camera3D view, Vector3 center, float radius, Color color)
    {
        if (view.IsPositionBehind(center)) return;
        const int segments = 20;
        Vector2 previous = view.UnprojectPosition(center + Vector3.Right * radius);
        for (int i = 1; i <= segments; i++)
        {
            float angle = Mathf.Tau * i / segments;
            Vector3 point = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            Vector2 next = view.UnprojectPosition(point);
            DrawLine(previous, next, color, 1f, true);
            previous = next;
        }
    }

    protected void DrawDimension(Vector2 start, Vector2 end, Vector2 interior, string label)
    {
        Vector2 edge = end - start;
        float distance = edge.Length();
        if (distance < 0.1f) return;
        Vector2 direction = edge / distance;
        Vector2 normal = new(-direction.Y, direction.X);
        if (normal.Dot(interior) > 0f) normal = -normal;

        Vector2 a = start + normal * 22f;
        Vector2 b = end + normal * 22f;
        DrawLine(start, a, Ink, 1f, true);
        DrawLine(end, b, Ink, 1f, true);
        DrawLine(a, b, Ink, 1.5f, true);
        float arrow = Mathf.Min(7f, distance * 0.25f);
        DrawArrow(a, direction, normal, arrow);
        DrawArrow(b, -direction, normal, arrow);

        const int fontSize = 14;
        Font font = ThemeDB.FallbackFont;
        Vector2 size = font.GetStringSize(label, fontSize: fontSize);
        Vector2 center = (a + b) * 0.5f;
        Vector2 box = size + new Vector2(10f, 4f);
        DrawRect(new Rect2(center - box * 0.5f, box), Backdrop);
        DrawString(font, center - size * 0.5f + Vector2.Down * font.GetAscent(fontSize), label,
            fontSize: fontSize, modulate: Ink);
    }

    private void DrawArrow(Vector2 tip, Vector2 inward, Vector2 normal, float size)
    {
        Vector2 basePoint = tip + inward * size;
        Vector2 wing = normal * (size * 0.45f);
        DrawLine(tip, basePoint + wing, Ink, 1.5f, true);
        DrawLine(tip, basePoint - wing, Ink, 1.5f, true);
    }
}
