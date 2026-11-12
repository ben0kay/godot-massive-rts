// Main-screen layout and explicit resource draft, save, and reload workflow.
#if TOOLS
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

[Tool]
public partial class UnitEditorPanel : VBoxContainer
{
    internal sealed record Draft(string Path, string Hash, UnitDefinition Unit);
    private sealed record Entry(string Path, string Id, string Name, UnitShape Shape);
    private const string CatalogPath = "res://UNITS/CATALOG/DefaultUnitCatalog.tres";
    private readonly List<Entry> entries = new();
    private readonly List<Entry> visibleEntries = new();
    private readonly ItemList list = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
    private readonly LineEdit search = new() { PlaceholderText = "Search units" };
    private readonly Label pathLabel = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true };
    private readonly Label status = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Button save = new() { Text = "Save unit" };
    private readonly Button reload = new() { Text = "Reload" };
    private readonly Button refresh = new() { Text = "Refresh catalogue" };
    private readonly HFlowContainer fields = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly UnitEditorPreview preview = new() { TooltipText = "The cleave arc shows direction. Attack contact depends on both soldiers’ footprints." };
    private readonly Label summary = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly List<Func<string>> checks = new();
    private readonly List<Action> refreshFields = new();
    private readonly ConfirmationDialog confirmReload = new() { Title = "Discard unit changes?", DialogText = "Reload this unit from disk and discard the current draft?" };
    private readonly ConfirmationDialog confirmSwitch = new()
    {
        Title = "Switch units?",
        Exclusive = true,
        DialogHideOnOk = false
    };
    private Draft draft;
    private string switchTarget;
    private bool dirty;
    private float speedScale = 0.8f;
    internal Draft PendingDraft => dirty ? draft : null;
    private UnitDefinition Unit => draft.Unit;

    internal void Initialize(Draft suspended)
    {
        var toolbar = new HBoxContainer();
        AddChild(toolbar);
        toolbar.AddChild(pathLabel);
        toolbar.AddChild(save);
        toolbar.AddChild(reload);
        toolbar.AddChild(refresh);

        var split = new HSplitContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SplitOffset = 30 };
        AddChild(split);
        var roster = new VBoxContainer { CustomMinimumSize = new Vector2(215, 0) };
        split.AddChild(roster);
        roster.AddChild(search);
        roster.AddChild(list);

        var workspace = new HSplitContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        split.AddChild(workspace);
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
        var overlays = new HFlowContainer();
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
        confirmSwitch.Confirmed += () => FinishUnitSwitch(saveDraft: true);
        confirmSwitch.CustomAction += action =>
        {
            if (action == "discard") FinishUnitSwitch(saveDraft: false);
        };
        confirmSwitch.Canceled += () => switchTarget = null;

        list.ItemSelected += index =>
        {
            if (index < 0 || index >= visibleEntries.Count) return;
            string path = visibleEntries[(int)index].Path;
            // Allow any focused numeric field to commit before checking whether the draft is dirty.
            Callable.From(() => RequestUnitSwitch(path)).CallDeferred();
        };
        search.TextChanged += _ => PopulateList();
        refresh.Pressed += RefreshCatalogue;
        save.Pressed += () => Save();
        reload.Pressed += () =>
        {
            if (draft == null) return;
            if (dirty) confirmReload.PopupCentered();
            else LoadUnit(draft.Path);
        };
        confirmReload.Confirmed += () => { if (draft != null) LoadUnit(draft.Path); };

        if (suspended != null)
        {
            draft = suspended;
            dirty = true;
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
        var catalog = ResourceLoader.Load<UnitCatalog>(CatalogPath, cacheMode: ResourceLoader.CacheMode.Ignore);
        speedScale = ResourceLoader.Load<SimulationSettings>(SimulationSettings.DefaultPath,
            cacheMode: ResourceLoader.CacheMode.Ignore)?.EffectiveMovementSpeedMultiplier ?? 0.8f;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var paths = new HashSet<string>(StringComparer.Ordinal);
        int invalid = 0;
        if (catalog?.Definitions != null)
        {
            foreach (UnitDefinition unit in catalog.Definitions)
            {
                if (unit == null || string.IsNullOrWhiteSpace(unit.Id) || !Enum.IsDefined(unit.Shape) ||
                    !IsUnitPath(unit.ResourcePath) || !FileAccess.FileExists(unit.ResourcePath) ||
                    ids.Contains(unit.Id) || paths.Contains(unit.ResourcePath))
                {
                    invalid++;
                    continue;
                }
                ids.Add(unit.Id);
                paths.Add(unit.ResourcePath);
                entries.Add(new Entry(unit.ResourcePath, unit.Id,
                    string.IsNullOrWhiteSpace(unit.DisplayName) ? unit.Id : unit.DisplayName, unit.Shape));
            }
        }
        PopulateList();
        if (!dirty && entries.Count > 0)
            LoadUnit(entries.Exists(entry => entry.Path == draft?.Path) ? draft.Path : entries[0].Path);
        else if (dirty)
            status.Text = "Restored your unsaved draft.";
        if (entries.Count == 0) status.Text = "No editable units found in the default catalogue.";
        else if (invalid > 0) status.Text = $"Skipped {invalid} invalid or duplicate catalogue entries.";
    }

    private static bool IsUnitPath(string path) =>
        path.StartsWith("res://", StringComparison.Ordinal) && path.EndsWith(".tres", StringComparison.Ordinal) &&
        !path.Contains("::", StringComparison.Ordinal);

    private void PopulateList()
    {
        list.Clear();
        visibleEntries.Clear();
        string filter = search.Text.Trim();
        foreach (Entry entry in entries)
        {
            if (!entry.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                !entry.Id.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                !entry.Shape.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
            list.AddItem($"{entry.Shape}  ·  {entry.Name}");
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
    // Keep the roster and form on the same unit until a pending edit is resolved.
    private void RequestUnitSwitch(string path)
    {
        if (!IsInsideTree() || path == draft?.Path || confirmSwitch.Visible) return;
        if (!dirty)
        {
            LoadUnit(path);
            return;
        }

        SelectActive();
        switchTarget = path;
        confirmSwitch.DialogText = $"Save your changes to {Unit.DisplayName} before switching units?";
        confirmSwitch.PopupCentered();
    }

    private void FinishUnitSwitch(bool saveDraft)
    {
        if (switchTarget == null) return;
        if (saveDraft)
        {
            Save();
            if (dirty)
            {
                confirmSwitch.DialogText = status.Text;
                return;
            }
        }

        string path = switchTarget;
        switchTarget = null;
        confirmSwitch.Hide();
        LoadUnit(path);
    }

    private void LoadUnit(string path)
    {
        string hash = FileAccess.GetSha256(path);
        var unit = ResourceLoader.Load<UnitDefinition>(path, cacheMode: ResourceLoader.CacheMode.Ignore);
        if (unit == null || string.IsNullOrEmpty(hash) || hash != FileAccess.GetSha256(path))
        {
            status.Text = "Could not read a stable copy of the unit. Try reloading.";
            SelectActive();
            return;
        }
        draft = new Draft(path, hash, unit);
        dirty = false;
        status.Text = "";
        int index = entries.FindIndex(entry => entry.Path == path);
        string displayName = string.IsNullOrWhiteSpace(unit.DisplayName) ? unit.Id : unit.DisplayName;
        var updated = new Entry(path, unit.Id, displayName, unit.Shape);
        if (index >= 0 && entries[index] != updated)
        {
            entries[index] = updated;
            PopulateList();
        }
        else SelectActive();
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
        Error result = ResourceSaver.Save(Unit, draft.Path);
        if (result != Error.Ok)
        {
            status.Text = $"Save failed ({result}). Your draft is still open.";
            return;
        }
        string path = draft.Path;
        draft = draft with { Hash = FileAccess.GetSha256(path) };
        dirty = false;
        ResourceLoader.Load<UnitDefinition>(path, cacheMode: ResourceLoader.CacheMode.Replace);
        EditorInterface.Singleton.GetResourceFilesystem().UpdateFile(path);
        LoadUnit(path);
        status.Text = "Unit saved. Restart the running battle to apply the values.";
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
        pathLabel.Text = draft == null ? "Unit Editor" : $"{draft.Path.GetFile()}   ·   {(dirty ? "Unsaved changes" : "Saved")}";
        pathLabel.TooltipText = draft?.Path ?? "";
        if (draft == null) return;
        foreach (var update in refreshFields) update();
        preview.SetUnit(Unit);
        float size = UnitOutline.EffectiveSize(Unit);
        float footprint = Enum.IsDefined(Unit.Shape) ? UnitOutline.FromDefinition(Unit, size).Footprint : 0f;
        bool melee = Unit.Style == AttackStyle.Melee;
        summary.Text = $"Size {size:0.##}×   ·   Footprint {footprint:0.###} u\n" +
            $"Effective speed {Unit.Speed * speedScale:0.###} u/s\n" +
            $"{Unit.Damage:0.##} damage every {Unit.Interval:0.##} s";
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
