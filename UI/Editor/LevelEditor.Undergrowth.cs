// Edits the decorative cell layer using the shared world-paint stroke history.
using Godot;
using PaintCells = Godot.Collections.Dictionary<Godot.Vector2I, int>;

namespace MassiveRts;

public partial class LevelEditor
{
    private PaintCells undergrowth;
    private int undergrowthPreset, undergrowthDensity = 50, undergrowthScale = 100;
    private bool undergrowthRectangle;

    private void UndergrowthControls()
    {
        var presets = new OptionButton();
        foreach (string name in UndergrowthMeshes.Presets) presets.AddItem(name);
        presets.Select(undergrowthPreset);
        presets.ItemSelected += index => { EndPaint(); undergrowthPreset = (int)index; };
        context.AddChild(presets);
        Number(context, "Density (%)", undergrowthDensity, 0, 100).ValueChanged += value =>
            { EndPaint(); undergrowthDensity = (int)value; };
        Number(context, "Scale (%)", undergrowthScale, 35, 160).ValueChanged += value =>
            { EndPaint(); undergrowthScale = (int)value; };
        var rectangle = new CheckBox { Text = "Rectangle fill", ButtonPressed = undergrowthRectangle };
        rectangle.Toggled += value => { EndPaint(); undergrowthRectangle = value; };
        context.AddChild(rectangle);
    }

    private bool PaintUndergrowth(Vector2I cell, bool erase)
    {
        if (erase) return undergrowth.Remove(cell);
        if (undergrowthDensity == 0) return false;
        int stamp = undergrowthPreset | (undergrowthDensity << 8) | (undergrowthScale << 16);
        if (undergrowth.TryGetValue(cell, out int previous) && stamp == previous) return false;
        undergrowth[cell] = stamp;
        return true;
    }
}
