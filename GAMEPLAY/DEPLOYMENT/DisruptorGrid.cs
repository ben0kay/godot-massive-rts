// Shared placement and battle effects for walkable Clash traps.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed class DisruptorGrid
{
    internal const string Id = "disruptor_grid", Name = "Disruptor Grid";
    internal const int Price = 10, MaxTiles = 4096;
    internal const float Size = DeploymentLayout.CellSize;
    private readonly HashSet<Vector2I> blue, red;
    private float elapsed;

    internal DisruptorGrid(IEnumerable<Vector2I> blue, IEnumerable<Vector2I> red)
    {
        this.blue = new(blue);
        this.red = new(red);
    }

    internal static Vector2I Cell(Vector3 point) => new(Mathf.FloorToInt(point.X / Size), Mathf.FloorToInt(point.Z / Size));
    internal static Vector3 Center(Vector2I cell) => new((cell.X + 0.5f) * Size, 0, (cell.Y + 0.5f) * Size);
    internal static bool Inside(Vector2I cell, float mapSize)
    {
        var center = Center(cell);
        float bound = (mapSize - Size) * 0.5f;
        return Mathf.Abs(center.X) <= bound && Mathf.Abs(center.Z) <= bound;
    }

    private bool Affects(Vector3 point, byte faction) =>
        faction == Factions.Blue ? red.Contains(Cell(point)) : faction == Factions.Red && blue.Contains(Cell(point));

    internal float SpeedScale(Vector3 point, byte faction) => Affects(point, faction) ? 0.8f : 1f;

    internal void Step(UnitState units, float dt)
    {
        elapsed += dt;
        if (elapsed < 0.5f) return;
        elapsed -= 0.5f;
        for (int id = 0; id < units.Count; id++)
            if (units.Health[id] > 0f && Affects(units.Position[id], units.Faction[id]))
                units.Damage(id, 1f);
    }

    internal sealed class LineTool
    {
        internal delegate bool GroundPicker(Vector2 screen, out Vector3 point);
        private Vector2I? start;
        private MouseButton button;
        internal void Cancel() => start = null;

        internal bool Handle(InputEvent input, GroundPicker pick, IEnumerable<Vector2I> placed,
            int allowance, Func<Vector2I, bool> allowed, Action<HashSet<Vector2I>> commit,
            Action<string> report, Action<IReadOnlyCollection<Vector2I>> preview)
        {
            if (input is InputEventMouseMotion motion)
            {
                if (start.HasValue && !Input.IsMouseButtonPressed(button)) Clear();
                if (start.HasValue && pick(motion.Position, out var end)) Edit(Cell(end), false);
                return true;
            }
            if (input is not InputEventMouseButton mouse ||
                mouse.ButtonIndex is not (MouseButton.Left or MouseButton.Right)) return false;
            if (mouse.Pressed)
            {
                Clear();
                if (pick(mouse.Position, out var point) && allowed(Cell(point)))
                {
                    start = Cell(point);
                    button = mouse.ButtonIndex;
                    Edit(start.Value, false);
                }
            }
            else if (start.HasValue && button == mouse.ButtonIndex)
            {
                if (pick(mouse.Position, out var point)) Edit(Cell(point), true);
                Clear();
            }
            return true;

            void Clear()
            {
                Cancel();
                preview(Array.Empty<Vector2I>());
            }

            void Edit(Vector2I end, bool apply)
            {
                var first = start.Value;
                var delta = end - first;
                var axis = Mathf.Abs(delta.X) >= Mathf.Abs(delta.Y)
                    ? new Vector2I(Math.Sign(delta.X), 0) : new Vector2I(0, Math.Sign(delta.Y));
                int length = Mathf.Max(Mathf.Abs(delta.X), Mathf.Abs(delta.Y)) + 1;
                var draft = new HashSet<Vector2I>(placed);
                int before = draft.Count;
                var added = new List<Vector2I>();
                preview(Array.Empty<Vector2I>());
                for (int i = 0; i < length; i++)
                {
                    var cell = first + axis * i;
                    if (!allowed(cell)) { report("Keep the line inside the deployment area."); return; }
                    if (button == MouseButton.Right) draft.Remove(cell);
                    else if (draft.Add(cell)) added.Add(cell);
                }
                if (draft.Count > allowance) { report("Not enough tiles for this line."); return; }
                if (apply && draft.Count != before) commit(draft);
                if (!apply) preview(added);
                report($"{length:N0} cells in line · {allowance - draft.Count:N0} tiles available");
            }
        }
    }
}
