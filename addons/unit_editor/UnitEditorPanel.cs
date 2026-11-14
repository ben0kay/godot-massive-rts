// Main-screen layout and explicit resource draft, save, and reload workflow.
#if TOOLS
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

[Tool]
public partial class UnitEditorPanel : VBoxContainer
{
    internal sealed record Draft(string Path, string Hash, Resource Definition);
    private sealed record Entry(string Path, string Id, string Name, string Kind, bool Defense);
    private const string CatalogPath = "res://GAMEPLAY/UNITS/CATALOG/DefaultUnitCatalog.tres";
    private const string DefenseCatalogPath = "res://WORLD/BUILDINGS/CATALOG/DefaultBuildingCatalog.tres";
    private readonly OptionButton category = new();
    private bool showDefenses;
    private HFlowContainer overlays;
    private readonly List<Entry> entries = new();
    private readonly List<Entry> visibleEntries = new();
    private readonly ItemList list = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
    private readonly LineEdit search = new() { PlaceholderText = "Search entities" };
    private readonly Label pathLabel = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true };
    private readonly Label status = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Button save = new() { Text = "Save entity" };
    private readonly Button reload = new() { Text = "Reload" };
    private readonly Button refresh = new() { Text = "Refresh catalogue" };
    private readonly HFlowContainer fields = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly UnitEditorPreview preview = new() { TooltipText = "The cleave arc shows direction. Attack contact depends on both soldiers’ footprints." };
    private readonly Label summary = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly List<Func<string>> checks = new();
    private readonly List<Action> refreshFields = new();
    private readonly ConfirmationDialog confirmReload = new() { Title = "Discard entity changes?", DialogText = "Reload this entity from disk and discard the current draft?" };
    private readonly ConfirmationDialog confirmSwitch = new()
    {
        Title = "Switch entities?",
        Exclusive = true,
        DialogHideOnOk = false
    };
    private Draft draft;
    private Action pendingSwitch;
    private bool dirty;
    private float speedScale = 0.8f;
    internal Draft PendingDraft => dirty ? draft : null;
    private UnitDefinition Unit => (UnitDefinition)draft.Definition;
    private BuildingDefinition Defense => (BuildingDefinition)draft.Definition;

    internal void Initialize(Draft suspended)
    {
        var toolbar = new HBoxContainer();
        AddChild(toolbar);
        toolbar.AddChild(pathLabel);
        toolbar.AddChild(save);
        toolbar.AddChild(reload);
        toolbar.AddChild(refresh);

        var split = new HSplitContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        AddChild(split);
        var roster = new VBoxContainer { CustomMinimumSize = new Vector2(215, 0) };
        split.AddChild(roster);
        category.AddItem("Units");
        category.AddItem("Defenses");
        roster.AddChild(category);
        roster.AddChild(search);
        category.ItemSelected += index =>
        {
            bool defense = index == 1;
            category.Select(showDefenses ? 1 : 0);
            if (defense != showDefenses)
                Callable.From(() => RequestSwitch(() => ShowCategory(defense))).CallDeferred();
        };
        roster.AddChild(list);

        var workspace = new HSplitContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        split.AddChild(workspace);
        split.SplitOffsets = new[] { 30 };
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(295, 0),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        workspace.AddChild(scroll);
        scroll.AddChild(fields);
        var previewColumn = new VBoxContainer { CustomMinimumSize = new Vector2(300, 0) };
        workspace.AddChild(previewColumn);
        previewColumn.AddChild(Heading("Combat preview"));
        previewColumn.AddChild(preview);
        overlays = new HFlowContainer();
        previewColumn.AddChild(overlays);
        ToggleOverlay(overlays, "Footprint", value => preview.ShowFootprint = value);
        ToggleOverlay(overlays, "Cleave", value => preview.ShowCleave = value);
        ToggleOverlay(overlays, "Shockwave", value => preview.ShowShockwave = value);
        previewColumn.AddChild(summary);
        AddChild(status);
        AddChild(confirmReload);
        AddChild(confirmSwitch);
        confirmSwitch.GetOkButton().Text = "Save and Switch";
        confirmSwitch.AddButton("Discard and Switch", true, "discard");
        confirmSwitch.Confirmed += () => FinishSwitch(saveDraft: true);
        confirmSwitch.CustomAction += action =>
        {
            if (action == "discard") FinishSwitch(saveDraft: false);
        };
        confirmSwitch.Canceled += () => pendingSwitch = null;

        list.ItemSelected += index =>
        {
            if (index < 0 || index >= visibleEntries.Count) return;
            string path = visibleEntries[(int)index].Path;
            // Allow any focused numeric field to commit before checking whether the draft is dirty.
            Callable.From(() => { if (path != draft?.Path) RequestSwitch(() => LoadEntity(path)); }).CallDeferred();
        };
        search.TextChanged += _ => PopulateList();
        refresh.Pressed += RefreshCatalogue;
        save.Pressed += () => Save();
        reload.Pressed += () =>
        {
            if (draft == null) return;
            if (dirty) confirmReload.PopupCentered();
            else LoadEntity(draft.Path);
        };
        confirmReload.Confirmed += () => { if (draft != null) LoadEntity(draft.Path); };

        if (suspended != null)
        {
            draft = suspended;
            dirty = true;
            showDefenses = draft.Definition is BuildingDefinition;
            category.Select(showDefenses ? 1 : 0);
            BuildForm();
        }
        RefreshCatalogue();
        UpdateView();
    }

    private void RefreshCatalogue()
    {
        // Populate the list once when restoring a suspended draft.
        if (dirty && entries.Count > 0) return;
        entries.Clear();
        var catalog = ResourceLoader.Load<UnitCatalog>(CatalogPath, cacheMode: ResourceLoader.CacheMode.IgnoreDeep);
        var defenses = ResourceLoader.Load<BuildingCatalog>(DefenseCatalogPath, cacheMode: ResourceLoader.CacheMode.IgnoreDeep);
        speedScale = ResourceLoader.Load<SimulationSettings>(SimulationSettings.DefaultPath,
            cacheMode: ResourceLoader.CacheMode.Ignore)?.EffectiveMovementSpeedMultiplier ?? 0.8f;
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var ids = new HashSet<(bool Defense, string Id)>();
        int invalid = 0;

        void Register(Entry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Id) ||
                string.IsNullOrEmpty(entry.Path) || !entry.Path.StartsWith("res://", StringComparison.Ordinal) ||
                !entry.Path.EndsWith(".tres", StringComparison.Ordinal) ||
                entry.Path.Contains("::", StringComparison.Ordinal) || !FileAccess.FileExists(entry.Path) ||
                paths.Contains(entry.Path) || !ids.Add((entry.Defense, entry.Id)))
            {
                invalid++;
                return;
            }
            paths.Add(entry.Path);
            entries.Add(entry);
        }

        if (catalog?.Definitions != null)
            foreach (var unit in catalog.Definitions) Register(Describe(unit));
        if (defenses?.Definitions != null)
            foreach (var defense in defenses.Definitions) Register(Describe(defense));

        if (!dirty) ShowCategory(showDefenses);
        else
        {
            PopulateList();
            status.Text = "Restored your unsaved draft.";
        }
        if (invalid > 0) status.Text = $"Skipped {invalid} invalid or duplicate catalogue entries.";
    }

    private static Entry Describe(Resource definition) => definition switch
    {
        UnitDefinition unit when Enum.IsDefined(unit.Shape) =>
            new Entry(unit.ResourcePath, unit.Id, DisplayName(unit.DisplayName, unit.Id), unit.Shape.ToString(), false),
        BuildingDefinition defense =>
            new Entry(defense.ResourcePath, defense.Id, DisplayName(defense.DisplayName, defense.Id), "Static defense", true),
        _ => null
    };

    private static string DisplayName(string name, string id) => string.IsNullOrWhiteSpace(name) ? id : name;

    private void ShowCategory(bool defense)
    {
        var entry = entries.Find(item => item.Defense == defense && item.Path == draft?.Path)
            ?? entries.Find(item => item.Defense == defense);
        if (entry != null)
        {
            LoadEntity(entry.Path);
            return;
        }

        draft = null;
        dirty = false;
        showDefenses = defense;
        category.Select(defense ? 1 : 0);
        PopulateList();
        BuildForm();
        UpdateView();
        status.Text = "No editable entries found in this catalogue.";
    }

    private void PopulateList()
    {
        list.Clear();
        visibleEntries.Clear();
        string filter = search.Text.Trim();
        foreach (Entry entry in entries)
        {
            if (entry.Defense != showDefenses) continue;
            if (!entry.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                !entry.Id.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                !entry.Kind.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
            list.AddItem(entry.Defense ? entry.Name : $"{entry.Kind}  ·  {entry.Name}");
            visibleEntries.Add(entry);
        }
        SelectActive();
    }

    private void SelectActive()
    {
        list.DeselectAll();
        int index = visibleEntries.FindIndex(entry => entry.Path == draft?.Path);
        if (index >= 0) list.Select(index);
    }

    // =========================================================
    // Keep the roster and form on the same entity until a pending edit is resolved.
    private void RequestSwitch(Action action)
    {
        if (!IsInsideTree() || confirmSwitch.Visible) return;
        if (!dirty)
        {
            action();
            return;
        }

        SelectActive();
        pendingSwitch = action;
        confirmSwitch.DialogText = $"Save your changes to {Describe(draft.Definition)?.Name ?? draft.Path.GetFile()} before switching?";
        confirmSwitch.PopupCentered();
    }

    private void FinishSwitch(bool saveDraft)
    {
        if (pendingSwitch == null) return;
        if (saveDraft)
        {
            Save();
            if (dirty)
            {
                confirmSwitch.DialogText = status.Text;
                return;
            }
        }

        Action action = pendingSwitch;
        pendingSwitch = null;
        confirmSwitch.Hide();
        action();
    }

    private void LoadEntity(string path)
    {
        string hash = FileAccess.GetSha256(path);
        Resource definition = ResourceLoader.Load(path, cacheMode: ResourceLoader.CacheMode.Ignore);
        Entry updated = Describe(definition);
        if (updated == null || string.IsNullOrEmpty(hash) || hash != FileAccess.GetSha256(path))
        {
            status.Text = "Could not read a stable copy of the entity. Try reloading.";
            SelectActive();
            return;
        }

        draft = new Draft(path, hash, definition);
        dirty = false;
        status.Text = "";
        int index = entries.FindIndex(entry => entry.Path == path);
        if (index >= 0) entries[index] = updated with { Path = path };
        if (updated.Defense != showDefenses) search.Text = "";
        showDefenses = updated.Defense;
        category.Select(showDefenses ? 1 : 0);
        PopulateList();
        BuildForm();
        UpdateView();
    }

    internal void Save()
    {
        if (!dirty || draft == null) return;
        foreach (var check in checks)
        {
            string error = check();
            if (error == null) continue;
            status.Text = error;
            return;
        }
        if (FileAccess.GetSha256(draft.Path) != draft.Hash)
        {
            status.Text = "The resource changed on disk. Reload to review the external changes before saving.";
            return;
        }
        Error result = ResourceSaver.Save(draft.Definition, draft.Path);
        if (result != Error.Ok)
        {
            status.Text = $"Save failed ({result}). Your draft is still open.";
            return;
        }
        string path = draft.Path;
        draft = draft with { Hash = FileAccess.GetSha256(path) };
        dirty = false;
        ResourceLoader.Load(path, cacheMode: ResourceLoader.CacheMode.Replace);
        EditorInterface.Singleton.GetResourceFilesystem().UpdateFile(path);
        LoadEntity(path);
        status.Text = "Entity saved. Restart the benchmark scene to apply the values.";
        UpdateView();
    }

    private void MarkEdited()
    {
        dirty = true;
        status.Text = "";
        UpdateView();
    }

    private void UpdateView()
    {
        save.Disabled = !dirty;
        reload.Disabled = draft == null;
        refresh.Disabled = dirty;
        pathLabel.Text = draft == null ? "Entity Editor" : $"{draft.Path.GetFile()}   ·   {(dirty ? "Unsaved changes" : "Saved")}";
        pathLabel.TooltipText = draft?.Path ?? "";
        overlays.Visible = draft?.Definition is UnitDefinition;
        preview.SetEntity(draft?.Definition);
        if (draft == null)
        {
            summary.Text = "";
            return;
        }
        foreach (var update in refreshFields) update();
        if (showDefenses)
        {
            summary.Text = $"{Defense.Width} × {Defense.Depth} cells";
            if (Defense.AttacksSoldiers)
                summary.Text += $"\n{Defense.MinimumRange:0.##}–{Defense.MaximumRange:0.##} u range" +
                    $"\n{Defense.Damage:0.##} damage · {Defense.ShotsPerSecond:0.##} shots/s";
            return;
        }
        float size = UnitOutline.EffectiveSize(Unit);
        float footprint = Enum.IsDefined(Unit.Shape) ? UnitOutline.FromDefinition(Unit, size).Footprint : 0f;
        bool melee = Unit.Style == AttackStyle.Melee;
        summary.Text = $"Size {size:0.##}×   ·   Footprint {footprint:0.###} u\n" +
            $"Effective speed {Unit.Speed * speedScale:0.###} u/s\n" +
            $"{Unit.Damage:0.##} damage every {Unit.Interval:0.##} s";
        if (!melee && Unit.Projectile?.ExplodesOnImpact == true)
        {
            float edge = Unit.Damage * Unit.Projectile.ExplosionEdgeFraction;
            summary.Text += $"\n{Unit.Projectile.ExplosionRadius:0.##} u blast radius   ·   {Unit.Damage:0.##} centre / {edge:0.##} outer";
        }
        if (melee && Unit.CleaveEnabled)
            summary.Text += $"\n{Unit.CleaveAngleDegrees:0.#}° cleave   ·   {Unit.CleaveMaxAdditionalTargets} extras at {Unit.CleaveSecondaryFraction:P0}";
        if (melee && Unit.ShockwaveEveryAttacks > 0)
            summary.Text += $"\nShockwave every {Unit.ShockwaveEveryAttacks} attacks\n" +
                $"{Unit.ShockwaveBaseRadius * size:0.##} u radius   ·   {Unit.ShockwaveDamage:0.##} centre / {Unit.EffectiveShockwaveOuterDamage(Unit.ShockwaveDamage):0.##} outer";
    }

    private static Label Heading(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 17);
        return label;
    }

    private void ToggleOverlay(Container parent, string text, Action<bool> set)
    {
        var toggle = new CheckBox { Text = text, ButtonPressed = true };
        toggle.Toggled += value => { set(value); preview.QueueRedraw(); };
        parent.AddChild(toggle);
    }
}
#endif
