// Paints grid-aligned industrial walls as undoable strokes.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

public partial class LevelEditor
{
    private enum WallPaintMode { Brush, Line, Fill }
    private WallPaintMode wallPaintMode;
    private bool factionWallRed = true;
    private Vector2I? wallStrokeStart;
    private Vector2I wallStrokeEnd;
    private MouseButton wallStrokeButton;
    private readonly HashSet<Vector2I> wallStrokeCells = new();

    private void CancelWallStroke()
    {
        if (!wallStrokeStart.HasValue) return;
        wallStrokeStart = null;
        wallStrokeCells.Clear();
        cursor?.Hide();
    }

    private bool HandleWallPaint(InputEvent input)
    {
        if (input is InputEventMouseMotion motion && wallStrokeStart.HasValue)
        {
            if (WorldPoint(motion.Position, out var hover)) ExtendWallStroke(Cell(hover));
            return true;
        }
        if (input is not InputEventMouseButton mouse ||
            mouse.ButtonIndex is not (MouseButton.Left or MouseButton.Right)) return false;
        if (mouse.Pressed)
        {
            if (!wallStrokeStart.HasValue && WorldPoint(mouse.Position, out var point))
            {
                wallStrokeStart = wallStrokeEnd = Cell(point);
                wallStrokeButton = mouse.ButtonIndex;
                ExtendWallStroke(wallStrokeEnd);
            }
        }
        else if (wallStrokeStart.HasValue && mouse.ButtonIndex == wallStrokeButton)
        {
            if (WorldPoint(mouse.Position, out var end)) ExtendWallStroke(Cell(end));
            CommitWallStroke(mouse.ButtonIndex == MouseButton.Right);
        }
        GetViewport().SetInputAsHandled();
        return true;
    }

    private void ExtendWallStroke(Vector2I end)
    {
        Vector2I start = wallStrokeStart.Value;
        if (wallPaintMode == WallPaintMode.Brush)
            AddWallLine(wallStrokeEnd, end);
        else if (wallPaintMode == WallPaintMode.Line)
        {
            wallStrokeCells.Clear();
            Vector2I difference = end - start;
            if (Math.Abs(difference.X) >= Math.Abs(difference.Y)) end.Y = start.Y;
            else end.X = start.X;
            AddWallLine(start, end);
        }
        wallStrokeEnd = end;
        var area = RectangleArea(wallPaintMode == WallPaintMode.Brush ? end : start, end);
        Vector2 center = (area.Min + area.Max) * 0.5f;
        cursor.GlobalPosition = new Vector3(center.X, 0.15f, center.Y);
        cursor.GlobalPosition += Vector3.Up * Surface(cursor.GlobalPosition);
        cursor.Scale = new Vector3(area.Width * Grid, 1, area.Depth * Grid);
        cursor.Show();
    }

    private void AddWallLine(Vector2I from, Vector2I to)
    {
        int steps = Math.Max(Math.Abs(to.X - from.X), Math.Abs(to.Y - from.Y));
        var last = from;
        wallStrokeCells.Add(from);
        for (int i = 1; i <= steps; i++)
        {
            var next = from + (Vector2I)((Vector2)(to - from) * (i / (float)steps)).Round();
            if (next.X != last.X && next.Y != last.Y)
                wallStrokeCells.Add(new Vector2I(next.X, last.Y));
            wallStrokeCells.Add(next);
            last = next;
        }
    }

    private void CommitWallStroke(bool erase)
    {
        var data = paint.Data;
        bool barricade = tool == Tool.Barricade;
        bool faction = tool == Tool.FactionWall;
        bool redWall = factionWallRed;
        var before = barricade ? (redWall ? data.RedBarricadeCells : data.BlueBarricadeCells) : !faction ? data.IndustrialWallCells : redWall ? data.RedWallCells : data.BlueWallCells;
        var occupied = new HashSet<Vector2I>();
        if (faction) occupied.UnionWith(data.IndustrialWallCells);
        if (!faction || !redWall) occupied.UnionWith(data.RedWallCells);
        if (!faction || redWall) occupied.UnionWith(data.BlueWallCells);
        if (barricade)
        {
            occupied.UnionWith(data.IndustrialWallCells);
            occupied.UnionWith(data.RedWallCells);
            occupied.UnionWith(data.BlueWallCells);
            occupied.UnionWith(redWall ? data.BlueBarricadeCells : data.RedBarricadeCells);
        }
        else
        {
            occupied.UnionWith(data.RedBarricadeCells);
            occupied.UnionWith(data.BlueBarricadeCells);
        }
        bool CanPaint(Vector2I cell) => paint.Contains(cell) && !occupied.Contains(cell);
        var draft = new HashSet<Vector2I>(before);
        bool changed = false;
        if (wallPaintMode == WallPaintMode.Fill)
        {
            var area = RectangleArea(wallStrokeStart.Value, wallStrokeEnd);
            if (!erase && (long)area.Width * area.Depth > WorldPaint.MaxPaintedCells)
            {
                RejectWallStroke();
                return;
            }
            if (erase)
                changed = draft.RemoveWhere(cell => cell.X >= area.X && cell.X < area.X + area.Width &&
                    cell.Y >= area.Z && cell.Y < area.Z + area.Depth) > 0;
            else
                for (int z = area.Z; z < area.Z + area.Depth; z++)
                for (int x = area.X; x < area.X + area.Width; x++)
                {
                    var cell = new Vector2I(x, z);
                    if (CanPaint(cell)) changed |= draft.Add(cell);
                }
        }
        else
            foreach (var cell in wallStrokeCells)
                if (erase || CanPaint(cell)) changed |= erase ? draft.Remove(cell) : draft.Add(cell);

        CancelWallStroke();
        if (!changed) return;
        if (barricade)
        {
            int otherCount = (redWall ? data.BlueBarricadeCells : data.RedBarricadeCells).Count;
            if (draft.Count + otherCount > WorldPaint.MaxBarricades)
            {
                SetStatus($"The map supports up to {WorldPaint.MaxBarricades} barricade segments.");
                return;
            }
            SetWallValue(before, Sorted(draft), cells =>
            {
                if (redWall) data.RedBarricadeCells = cells;
                else data.BlueBarricadeCells = cells;
                paint.BuildBarricades();
            });
            return;
        }
        if (draft.Count > WorldPaint.MaxPaintedCells ||
            ObstacleCellCompiler.Merge(draft, Grid, WorldPaint.MaxPaintedRectangles) == null)
        {
            RejectWallStroke();
            return;
        }
        SetWallValue(before, Sorted(draft), cells =>
        {
            if (!faction) data.IndustrialWallCells = cells;
            else if (redWall) data.RedWallCells = cells;
            else data.BlueWallCells = cells;
        });
    }

    private void SetWallValue<T>(T before, T after, Action<T> setter)
    {
        void Apply(T value)
        {
            setter(value);
            map.ReadAuthoredWalls(updateVisuals: true);
        }
        Apply(after);
        Remember(() => Apply(before), () => Apply(after));
    }

    private void RejectWallStroke()
    {
        CancelWallStroke();
        SetStatus("Wall stroke exceeds the paint budget. Try a smaller or less fragmented area.");
    }
}
