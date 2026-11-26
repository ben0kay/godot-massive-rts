// Edits level metadata and mode-specific deployment or wave rules as one undoable change.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Waves = Godot.Collections.Array<MassiveRts.WaveDefinition>;

namespace MassiveRts;

public partial class LevelEditor
{
    // =========================================================
    private void ShowMapSettings()
    {
        if (map == null || runningTest != null) return;
        EndPaint();
        settingsDialog?.QueueFree();
        settingsDialog = new AcceptDialog { Title = $"{campaign.DisplayName} · Map Size and Settings" };
        interfaceRoot.AddChild(settingsDialog);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(510, 480),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var dialogContent = new VBoxContainer();
        settingsDialog.AddChild(dialogContent);
        dialogContent.AddChild(scroll);
        var form = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(form);
        mapSize = Number(form, "Map width and depth", metadata.MapSize, 64, LevelDefinition.DefaultMapSize, 4);
        Heading(form, "Base Terrain", 15);
        var baseTerrain = new OptionButton();
        baseTerrain.AddItem("Industrial");
        baseTerrain.AddItem("Steel Slate");
        baseTerrain.Select((int)map.BaseTerrain);
        form.AddChild(baseTerrain);
        Heading(form, "Custom name (optional)", 15);
        levelTitle = new LineEdit { Text = metadata.CustomName };
        form.AddChild(levelTitle);
        Heading(form, "Description", 15);
        levelDescription = new TextEdit { Text = metadata.Description, CustomMinimumSize = new Vector2(0, 70), WrapMode = TextEdit.LineWrappingMode.Boundary };
        form.AddChild(levelDescription);
        var zoom = Number(form, "Opening camera zoom", map.OpeningCameraSize, 30, 1800);
        var cameraMarker = map.GetNode<Marker3D>("CameraStart");
        var x = Number(form, "Camera focus X", cameraMarker.Position.X, -2500, 2500, 0.25);
        var z = Number(form, "Camera focus Z", cameraMarker.Position.Z, -2500, 2500, 0.25);
        var yawDegrees = Number(form, "Camera yaw", cameraMarker.RotationDegrees.Y, -180, 360, 0.25);
        Func<(Action Undo, Action Redo)> readRules = mode.IsAutoBattler ? AutoBattlerForm(form) : FortressForm(form);
        var errorLabel = Heading(dialogContent, "", 15);
        errorLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Button(dialogContent, "Apply settings", () =>
        {
            try
            {
                string title = levelTitle.Text.Trim(), description = levelDescription.Text;
                var before = (metadata.DisplayName, metadata.Description, metadata.MapSize, map.OpeningCameraSize, cameraMarker.Transform, map.BaseTerrain);
                var after = (title, description, (float)mapSize.Value, (float)zoom.Value,
                    new Transform3D(new Basis(Vector3.Up, Mathf.DegToRad((float)yawDegrees.Value)),
                        new Vector3((float)x.Value, cameraMarker.Position.Y, (float)z.Value)),
                    (CommandSandbox.BaseTerrainStyle)baseTerrain.Selected);
                var rules = readRules();
                void Apply((string Title, string Description, float Size, float Zoom, Transform3D Camera, CommandSandbox.BaseTerrainStyle Terrain) values, Action rulesAction)
                {
                    metadata.DisplayName = values.Title;
                    metadata.Description = values.Description;
                    metadata.MapSize = values.Size;
                    map.OpeningCameraSize = values.Zoom;
                    cameraMarker.Transform = values.Camera;
                    map.BaseTerrain = values.Terrain;
                    rulesAction();
                    RefreshMap(terrainChanged: before.Item3 != after.Item3);
                    FrameEditor();
                }
                Apply(after, rules.Redo);
                Remember(() => Apply(before, rules.Undo), () => Apply(after, rules.Redo));
                settingsDialog.Hide();
                if (after.Item3 < before.Item3) SetStatus("Map size reduced. Content outside the new boundary is retained.");
            }
            catch (Exception error) { errorLabel.Text = error.Message; }
        });
        settingsDialog.GetOkButton().Text = "Close";
        settingsDialog.PopupCentered();
    }

    private Func<(Action Undo, Action Redo)> AutoBattlerForm(Control parent)
    {
        var rules = map.AutoBattlerRules;
        Heading(parent, CampaignCatalog.BattleType(campaign.Id), 17);
        var presetCounts = new List<(string Id, SpinBox Count)>();
        if (CampaignCatalog.IsSkirmish(campaign.Id))
        {
            Heading(parent, "Preset Blue army", 17);
            foreach (var unit in units)
            {
                rules.SkirmishArmy.TryGetValue(unit.Id, out int count);
                presetCounts.Add((unit.Id, Number(parent, unit.DisplayName, count, 0, 100000)));
            }
        }
        var allowance = Number(parent, "Blue unit limit", rules.BlueUnitLimit, 0, 50000);
        Heading(parent, "Allowed Blue unit types", 15);
        var allowed = new HashSet<string>(rules.AllowedBlueUnitIds);
        var checks = new List<(string Id, CheckBox Box)>();
        var restrict = new CheckBox { Text = "Restrict unit types", ButtonPressed = allowed.Count > 0 };
        parent.AddChild(restrict);
        var types = new VBoxContainer { Visible = restrict.ButtonPressed };
        parent.AddChild(types);
        restrict.Toggled += on => types.Visible = on;
        foreach (var unit in units)
        {
            var item = new CheckBox { Text = unit.DisplayName, ButtonPressed = allowed.Contains(unit.Id) };
            types.AddChild(item);
            checks.Add((unit.Id, item));
        }
        return () =>
        {
            string[] chosen = restrict.ButtonPressed ? checks.Where(item => item.Box.ButtonPressed).Select(item => item.Id).ToArray() : Array.Empty<string>();
            if (restrict.ButtonPressed && chosen.Length == 0) throw new InvalidOperationException("Select at least one permitted unit type.");
            int oldAllowance = rules.BlueUnitLimit, nextAllowance = (int)allowance.Value;
            string[] oldAllowed = rules.AllowedBlueUnitIds;
            var oldArmy = rules.SkirmishArmy;
            var newArmy = new Godot.Collections.Dictionary<string, int>();
            foreach (var (id, count) in presetCounts)
                if (count.Value > 0) newArmy[id] = (int)count.Value;
            return (() => { rules.BlueUnitLimit = oldAllowance; rules.AllowedBlueUnitIds = oldAllowed; rules.SkirmishArmy = oldArmy; },
                () => { rules.BlueUnitLimit = nextAllowance; rules.AllowedBlueUnitIds = chosen;
                    if (presetCounts.Count > 0) rules.SkirmishArmy = newArmy; });
        };
    }

    private void ShowReinforcementSettings()
    {
        EndPaint();
        settingsDialog?.QueueFree();
        settingsDialog = new AcceptDialog { Title = "Red reinforcements" };
        interfaceRoot.AddChild(settingsDialog);
        var parent = new VBoxContainer { CustomMinimumSize = new Vector2(390, 0) };
        settingsDialog.AddChild(parent);
        var rules = map.AutoBattlerRules;
        Heading(parent, "A painted Red staging formation takes precedence over the fallback wave.", 15)
            .AutowrapMode = TextServer.AutowrapMode.WordSmart;
        var reserveType = new OptionButton();
        foreach (var unit in units) reserveType.AddItem(unit.DisplayName);
        reserveType.Select(Math.Max(0, Array.FindIndex(units, unit => unit.Id == rules.RedReserveUnitId)));
        parent.AddChild(reserveType);
        var reserveCount = Number(parent, "Fallback count (0 disables fallback)", rules.RedReserveCount, 0, 100000);
        var reserveSeconds = Number(parent, "Arrival seconds after Fight", rules.RedReserveSeconds, 0, 299, 1);
        var reserveOffset = Number(parent, "Entry offset along Red edge (%)", rules.RedReserveEdgeOffset, -100, 100, 1);
        settingsDialog.GetOkButton().Text = "Apply settings";
        settingsDialog.Confirmed += () =>
        {
            var before = (rules.RedReserveUnitId, rules.RedReserveCount, rules.RedReserveSeconds, rules.RedReserveEdgeOffset);
            var after = (units[reserveType.Selected].Id, (int)reserveCount.Value, (float)reserveSeconds.Value, (float)reserveOffset.Value);
            SetValue(before, after, value =>
                (rules.RedReserveUnitId, rules.RedReserveCount, rules.RedReserveSeconds, rules.RedReserveEdgeOffset) = value);
        };
        settingsDialog.PopupCentered();
    }

    private Func<(Action Undo, Action Redo)> FortressForm(Control parent)
    {
        Heading(parent, "Fortress setup", 17);
        var carbon = Number(parent, "Starting Carbon", map.StartingCarbon, 0, 1000000);
        var health = Number(parent, "Citadel HP (0 uses definition)", map.CitadelHealth, 0, 50000);
        var defaultSquad = new CheckBox { Text = "Spawn default Blue squad", ButtonPressed = map.SpawnDefaultBlueSquad };
        parent.AddChild(defaultSquad);
        int edge = (int)map.EnemySpawnEdge, pattern = (int)map.EnemySpawnPattern;
        Choice(parent, "Spawn edge", Enum.GetNames<WaveSpawnEdge>(), edge, value => edge = value);
        Choice(parent, "Spawn pattern", Enum.GetNames<WaveSpawnPattern>(), pattern, value => pattern = value);
        var width = Number(parent, "Spawn width", map.EnemySpawnWidth, 1, 5000);
        var spawnRate = Number(parent, "Units per second (stream / scatter)", map.EnemySpawnRate, 1, 60, 0.25);
        var rowSize = Number(parent, "Enemies per row", map.EnemiesPerRow, 2, 24);
        var rowDelay = Number(parent, "Row delay (seconds)", map.EnemyRowDelay, 0.25, 10, 0.25);
        var waveDelay = Number(parent, "Time between waves (seconds)", map.BetweenWavesSeconds, 1, 120);
        Heading(parent, "Waves · one per line, e.g. circle_infantry=25, triangle_archer=10", 15).AutowrapMode = TextServer.AutowrapMode.WordSmart;
        var wavesText = new TextEdit
        {
            Text = string.Join("\n", map.WaveDefinitions.Select(wave => string.Join(", ", wave.Enemies.Select(entry => $"{entry.Key}={entry.Value}")))),
            CustomMinimumSize = new Vector2(0, 145)
        };
        parent.AddChild(wavesText);
        var ids = new OptionButton();
        foreach (var unit in units) ids.AddItem(unit.Id);
        parent.AddChild(ids);
        Button(parent, "Insert selected unit ID", () =>
        {
            if (ids.Selected >= 0) wavesText.InsertTextAtCaret(ids.GetItemText(ids.Selected) + "=");
        });

        return () =>
        {
            Waves waves = ParseWaves(wavesText.Text);
            var before = (map.StartingCarbon, map.CitadelHealth, map.SpawnDefaultBlueSquad, map.EnemySpawnEdge, map.EnemySpawnPattern,
                map.EnemySpawnWidth, map.EnemySpawnRate, map.EnemiesPerRow, map.EnemyRowDelay, map.BetweenWavesSeconds, map.WaveDefinitions);
            var after = ((int)carbon.Value, (float)health.Value, defaultSquad.ButtonPressed, (WaveSpawnEdge)edge, (WaveSpawnPattern)pattern,
                (float)width.Value, (float)spawnRate.Value, (int)rowSize.Value, (float)rowDelay.Value, (float)waveDelay.Value, waves);
            void Apply((int Carbon, float Health, bool DefaultSquad, WaveSpawnEdge Edge, WaveSpawnPattern Pattern,
                float Width, float Rate, int RowSize, float RowDelay, float WaveDelay, Waves Waves) values)
            {
                map.StartingCarbon = values.Carbon;
                map.CitadelHealth = values.Health;
                map.SpawnDefaultBlueSquad = values.DefaultSquad;
                map.EnemySpawnEdge = values.Edge;
                map.EnemySpawnPattern = values.Pattern;
                map.EnemySpawnWidth = values.Width;
                map.EnemySpawnRate = values.Rate;
                map.EnemiesPerRow = values.RowSize;
                map.EnemyRowDelay = values.RowDelay;
                map.BetweenWavesSeconds = values.WaveDelay;
                map.WaveDefinitions = values.Waves;
            }
            return (() => Apply(before), () => Apply(after));
        };
    }

    private Waves ParseWaves(string text)
    {
        var waves = new Waves();
        var ids = units.Select(unit => unit.Id).ToHashSet();
        int total = 0;
        foreach (string line in text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var wave = new WaveDefinition();
            foreach (string entry in line.Split(',', StringSplitOptions.TrimEntries))
            {
                string[] pair = entry.Split('=', StringSplitOptions.TrimEntries);
                if (pair.Length != 2 || !ids.Contains(pair[0]) || !int.TryParse(pair[1], out int count) ||
                    count < 1 || wave.Enemies.ContainsKey(pair[0]))
                    throw new InvalidOperationException($"Invalid wave entry '{entry}'. Use a registered unit ID and a positive count once per wave.");
                if (count > CommandSandbox.WaveUnitCapacity - total)
                    throw new InvalidOperationException($"Fortress waves support {CommandSandbox.WaveUnitCapacity} enemies in total.");
                total += count;
                wave.Enemies.Add(pair[0], count);
            }
            waves.Add(wave);
        }
        return waves;
    }

    private void UnitPicker(Control parent, int index, Action<int> changed) =>
        Choice(parent, "Unit type", units.Select(unit => unit.DisplayName).ToArray(), index, changed);

    private static void Choice(Control parent, string title, string[] options, int index, Action<int> changed)
    {
        Heading(parent, title, 15);
        var picker = new OptionButton();
        foreach (string option in options) picker.AddItem(option);
        picker.Select(index);
        picker.ItemSelected += selected => changed((int)selected);
        parent.AddChild(picker);
    }
}
