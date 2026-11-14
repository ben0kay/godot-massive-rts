// Draws squad selection and the projected formation preview in viewport pixels.
using System.Globalization;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

public partial class CommandOverlay : RectangleDimensions
{
    internal Camera3D View;
    internal UnitState Units;
    internal readonly List<int> Selection = new();
    internal CommandStructure Building;
    internal bool Selecting, Preview;
    internal float Spacing;
    internal float Radius(int id) => Units.Catalog[Units.Type[id]].Footprint;
    internal Vector2 Press, Pointer;
    internal Vector3 Front, Right, LineStart, LineEnd;
    internal int Columns;
    private static readonly Color SelectionInk = new(0.35f, 0.8f, 1f, 0.9f);
    private static readonly Color GhostInk = new(0.65f, 0.85f, 1f, 0.55f);

    public override void _Draw()
    {
        if (View == null) return;
        foreach (int id in Selection)
            if (Units.Health[id] > 0) DrawFootprint(Units.Position[id], Radius(id), SelectionInk);
        if (Building?.Rally is Vector3 rally && !Building.Destroyed)
        {
            Vector3 gate = Building.Center + Vector3.Back * 3f;
            DrawLine(View.UnprojectPosition(gate), View.UnprojectPosition(rally), GhostInk, 1.25f, true);
            DrawFootprint(rally, 0.65f, SelectionInk);
        }
        if (Selecting)
        {
            var box = new Rect2(Press, Pointer - Press).Abs();
            DrawRect(box, new Color(0.25f, 0.65f, 1f, 0.12f));
            DrawRect(box, SelectionInk, false, 1f);
        }
        if (!Preview) return;

        for (int i = 0; i < Selection.Count; i++)
            DrawFootprint(CommandMovement.Slot(Front, Right, Columns, i, Selection.Count, Spacing),
                Radius(Selection[i]), GhostInk);
        Vector2 start = View.UnprojectPosition(LineStart);
        Vector2 end = View.UnprojectPosition(LineEnd);
        DrawLine(start, end, GhostInk, 1.5f, true);
        Vector2 interior = View.UnprojectPosition(LineStart + Right.Cross(Vector3.Up)) - start;
        float width = LineStart.DistanceTo(LineEnd);
        DrawDimension(start, end, interior, width.ToString("0.##", CultureInfo.InvariantCulture) + " u");
    }

    private void DrawFootprint(Vector3 center, float radius, Color color)
    {
        if (View.IsPositionBehind(center)) return;
        const int segments = 20;
        Vector2 previous = View.UnprojectPosition(center + Vector3.Right * radius);
        for (int i = 1; i <= segments; i++)
        {
            float angle = Mathf.Tau * i / segments;
            Vector3 point = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            Vector2 next = View.UnprojectPosition(point);
            DrawLine(previous, next, color, 1f, true);
            previous = next;
        }
    }
}
