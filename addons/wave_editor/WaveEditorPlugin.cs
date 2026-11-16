// Edits the open command scene's wave resources through Godot's scene undo history.
#if TOOLS
using System;
using Godot;
using WaveList = Godot.Collections.Array<MassiveRts.WaveDefinition>;

namespace MassiveRts;

[Tool]
public partial class WaveEditorPlugin : EditorPlugin
{
    private const string WavesProperty = nameof(CommandSandbox.WaveDefinitions);
    private const string EdgeProperty = nameof(CommandSandbox.EnemySpawnEdge);
    private const string DelayProperty = nameof(CommandSandbox.BetweenWavesSeconds);
    private EditorDock dock;
    private VBoxContainer form;
    private UnitDefinition[] roster;
    private int selected;
    private bool rebuilding;

    public override void _EnterTree()
    {
        var catalog = GD.Load<UnitCatalog>("res://GAMEPLAY/UNITS/CATALOG/DefaultUnitCatalog.tres");
        var definitions = new System.Collections.Generic.List<UnitDefinition>();
        foreach (var definition in catalog.Definitions)
            if (definition != null) definitions.Add(definition);
        roster = definitions.ToArray();

        dock = new EditorDock
        {
            Title = "Wave Editor",
            LayoutKey = "MassiveRtsWaveEditor",
            DefaultSlot = EditorDock.DockSlot.Bottom,
            AvailableLayouts = EditorDock.DockLayout.Horizontal
        };
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 215) };
        form = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(form);
        dock.AddChild(scroll);
        AddDock(dock);
        SceneChanged += OnSceneChanged;
        dock.VisibilityChanged += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        SceneChanged -= OnSceneChanged;
        dock.VisibilityChanged -= Refresh;
        RemoveDock(dock);
        dock.QueueFree();
        form = null;
    }

    private void OnSceneChanged(Node scene)
    {
        selected = 0;
        Refresh();
    }

    private Node CommandScene()
    {
        Node root = EditorInterface.Singleton.GetEditedSceneRoot();
        if (root == null || root.GetScript().As<Script>()?.ResourcePath != "res://BENCHMARK/RTS/CommandSandbox.cs")
            return null;
        return root;
    }

    private void Refresh()
    {
        if (form == null) return;
        rebuilding = true;
        foreach (Node child in form.GetChildren())
        {
            form.RemoveChild(child);
            child.QueueFree();
        }

        Node scene = CommandScene();
        if (scene == null)
        {
            form.AddChild(new Label { Text = "Open CommandSandbox or an inherited command level." });
            rebuilding = false;
            return;
        }

        var waves = scene.Get(WavesProperty).As<WaveList>() ?? new WaveList();
        selected = Math.Clamp(selected, 0, Math.Max(0, waves.Count - 1));
        var settings = new HBoxContainer();
        form.AddChild(settings);
        var edge = new OptionButton();
        foreach (var option in Enum.GetValues<WaveSpawnEdge>()) edge.AddItem(option.ToString());
        edge.Select(scene.Get(EdgeProperty).AsInt32());
        edge.ItemSelected += index => Set(scene, EdgeProperty, (int)index, "Change wave spawn edge");
        settings.AddChild(new Label { Text = "Spawn edge" });
        settings.AddChild(edge);
        settings.AddChild(new Label { Text = "Between waves" });
        var delay = new SpinBox { MinValue = 1, MaxValue = 120, Step = 1, Suffix = "s",
            Value = scene.Get(DelayProperty).AsDouble() };
        delay.ValueChanged += seconds => Set(scene, DelayProperty, (float)seconds, "Change wave delay");
        settings.AddChild(delay);
        settings.AddChild(Button("Refresh", Refresh));

        var formation = new HBoxContainer();
        form.AddChild(formation);
        const string patternProperty = nameof(CommandSandbox.EnemySpawnPattern);
        var pattern = new OptionButton();
        foreach (var option in Enum.GetValues<WaveSpawnPattern>()) pattern.AddItem(option.ToString());
        pattern.Select(scene.Get(patternProperty).AsInt32());
        pattern.ItemSelected += index =>
        {
            Set(scene, patternProperty, (int)index, "Change wave spawn pattern");
            Callable.From(Refresh).CallDeferred();
        };
        formation.AddChild(new Label { Text = "Spawn pattern" });
        formation.AddChild(pattern);
        if (pattern.Selected != (int)WaveSpawnPattern.Stream)
            AddSpawnNumber(formation, scene, "Lane spacing", nameof(CommandSandbox.EnemySpawnSpacing), 1, 24, 0.5, "u");
        if (pattern.Selected == (int)WaveSpawnPattern.Rows)
        {
            AddSpawnNumber(formation, scene, "Units per row", nameof(CommandSandbox.EnemiesPerRow), 2, 24, 1, "");
            AddSpawnNumber(formation, scene, "Row delay", nameof(CommandSandbox.EnemyRowDelay), 0.25, 10, 0.25, "s");
        }

        var controls = new HBoxContainer();
        form.AddChild(controls);
        var picker = new OptionButton();
        for (int i = 0; i < waves.Count; i++) picker.AddItem($"Wave {i + 1}");
        if (waves.Count > 0) picker.Select(selected);
        picker.ItemSelected += index => { selected = (int)index; Refresh(); };
        controls.AddChild(picker);
        controls.AddChild(Button("Add wave", () => AddWave(scene), roster.Length == 0));
        controls.AddChild(Button("Remove wave", () => RemoveWave(scene), waves.Count <= 1));
        controls.AddChild(new Label { Text = "Save changes with the level scene." });

        if (waves.Count > 0)
        {
            var counts = new GridContainer { Columns = 4 };
            form.AddChild(counts);
            var enemies = waves[selected]?.Enemies;
            foreach (var unit in roster)
            {
                int count = enemies != null && enemies.TryGetValue(unit.Id, out int amount) ? amount : 0;
                string id = unit.Id;
                var value = new SpinBox { MinValue = 0, MaxValue = CommandSandbox.WaveUnitCapacity,
                    Step = 1, Value = count, CustomMinimumSize = new Vector2(85, 0) };
                value.ValueChanged += amount => ChangeCount(scene, id, (int)amount, value);
                counts.AddChild(new Label { Text = unit.DisplayName });
                counts.AddChild(value);
            }
        }
        rebuilding = false;
    }

    private void AddSpawnNumber(HBoxContainer row, Node scene, string label, string property,
        double min, double max, double step, string suffix)
    {
        row.AddChild(new Label { Text = label });
        var number = new SpinBox { MinValue = min, MaxValue = max, Step = step, Suffix = suffix,
            Value = scene.Get(property).AsDouble() };
        number.ValueChanged += value =>
        {
            Variant updated = property == nameof(CommandSandbox.EnemiesPerRow) ? (Variant)(int)value : (Variant)(float)value;
            Set(scene, property, updated, $"Change {label.ToLowerInvariant()}");
        };
        row.AddChild(number);
    }

    private void ChangeCount(Node scene, string id, int count, SpinBox input)
    {
        if (rebuilding || scene != CommandScene()) return;
        var waves = scene.Get(WavesProperty).As<WaveList>() ?? new WaveList();
        if (selected >= waves.Count) return;
        var original = waves[selected]?.Enemies;
        var replacement = new WaveDefinition();
        if (original != null)
            foreach (var enemy in original) replacement.Enemies[enemy.Key] = enemy.Value;
        if (count == 0) replacement.Enemies.Remove(id);
        else replacement.Enemies[id] = count;

        WaveList changed = Copy(waves);
        changed[selected] = replacement;
        if (replacement.Enemies.Count == 0 || CountEnemies(changed) > CommandSandbox.WaveUnitCapacity)
        {
            input.SetValueNoSignal(original != null && original.TryGetValue(id, out int old) ? old : 0);
            return;
        }
        Set(scene, WavesProperty, changed, "Change wave enemy count");
    }

    private void AddWave(Node scene)
    {
        var waves = scene.Get(WavesProperty).As<WaveList>() ?? new WaveList();
        if (CountEnemies(waves) >= CommandSandbox.WaveUnitCapacity) return;
        var wave = new WaveDefinition();
        wave.Enemies[roster[0].Id] = 1;
        WaveList changed = Copy(waves);
        changed.Add(wave);
        Set(scene, WavesProperty, changed, "Add enemy wave");
        selected = changed.Count - 1;
        Refresh();
    }

    private void RemoveWave(Node scene)
    {
        var waves = scene.Get(WavesProperty).As<WaveList>();
        if (waves == null || waves.Count <= 1 || selected >= waves.Count) return;
        WaveList changed = Copy(waves);
        changed.RemoveAt(selected);
        Set(scene, WavesProperty, changed, "Remove enemy wave");
        Refresh();
    }

    private void Set(Node scene, string property, Variant value, string label)
    {
        if (rebuilding || scene != CommandScene()) return;
        var history = GetUndoRedo();
        history.CreateAction(label);
        history.AddDoProperty(scene, property, value);
        history.AddUndoProperty(scene, property, scene.Get(property));
        history.CommitAction();
    }

    private static WaveList Copy(WaveList source)
    {
        var copy = new WaveList();
        foreach (var wave in source) copy.Add(wave);
        return copy;
    }

    private static long CountEnemies(WaveList waves)
    {
        long count = 0;
        foreach (var wave in waves)
            if (wave != null)
                foreach (var enemy in wave.Enemies) count += enemy.Value;
        return count;
    }

    private static Button Button(string text, Action pressed, bool disabled = false)
    {
        var button = new Button { Text = text, Disabled = disabled };
        button.Pressed += pressed;
        return button;
    }
}
#endif
