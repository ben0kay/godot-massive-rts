// Draws squad selection and the projected formation preview in viewport pixels.
using System.Globalization;
using Godot;

namespace MassiveRts;

public partial class CommandOverlay : RectangleDimensions
{
    internal Camera3D View;
    internal UnitState Units;
    internal CommandMovement Movement;
    internal bool Selected, Selecting, Preview;
    internal Vector2 Press, Pointer;
    internal Vector3 Front, Right, LineStart, LineEnd;
    internal int Columns;
    private static readonly Color SelectionInk = new(0.35f, 0.8f, 1f, 0.9f);
    private static readonly Color GhostInk = new(0.65f, 0.85f, 1f, 0.55f);

    public override void _Draw()
    {
        if (View == null) return;
        if (Selected)
            for (int i = 0; i < Units.Count; i++) DrawFootprint(Units.Position[i], SelectionInk);
        if (Selecting)
        {
            var box = new Rect2(Press, Pointer - Press).Abs();
            DrawRect(box, new Color(0.25f, 0.65f, 1f, 0.12f));
            DrawRect(box, SelectionInk, false, 1f);
        }
        if (!Preview) return;

        for (int i = 0; i < Units.Count; i++) DrawFootprint(Movement.Slot(Front, Right, Columns, i), GhostInk);
        Vector2 start = View.UnprojectPosition(LineStart);
        Vector2 end = View.UnprojectPosition(LineEnd);
        DrawLine(start, end, GhostInk, 1.5f, true);
        Vector2 interior = View.UnprojectPosition(LineStart + Right.Cross(Vector3.Up)) - start;
        float width = LineStart.DistanceTo(LineEnd);
        DrawDimension(start, end, interior, width.ToString("0.##", CultureInfo.InvariantCulture) + " u");
    }

    private void DrawFootprint(Vector3 center, Color color)
    {
        if (View.IsPositionBehind(center)) return;
        const int segments = 20;
        Vector2 previous = View.UnprojectPosition(center + Vector3.Right * Movement.Radius);
        for (int i = 1; i <= segments; i++)
        {
            float angle = Mathf.Tau * i / segments;
            Vector3 point = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Movement.Radius;
            Vector2 next = View.UnprojectPosition(point);
            DrawLine(previous, next, color, 1f, true);
            previous = next;
        }
    }
}
