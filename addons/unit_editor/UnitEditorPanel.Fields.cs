// Typed editor controls for authored stats, with live validation and ability-specific availability.
#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MassiveRts;

public partial class UnitEditorPanel
{
    private readonly HashSet<string> represented = new(StringComparer.Ordinal);
    private VBoxContainer card;
    // The shockwave's existing zero cadence is its disabled state. Retain the active value in the draft UI.
    private int lastShockwaveCadence = 5;
    private bool IsMelee => Unit.Style == AttackStyle.Melee;
    private bool HasCleave => IsMelee && Unit.CleaveEnabled;
    private bool HasShockwave => IsMelee && Unit.ShockwaveEveryAttacks > 0;
    private bool HasTrample => IsMelee && Unit.TrampleEnabled;

    private void BuildForm()
    {
        foreach (Node child in fields.GetChildren())
        {
            fields.RemoveChild(child);
            child.QueueFree();
        }
        checks.Clear();
        refreshFields.Clear();
        represented.Clear();
        lastShockwaveCadence = Unit.ShockwaveEveryAttacks > 0 ? Unit.ShockwaveEveryAttacks : 5;

        Section("Identity");
        represented.Add(nameof(Unit.Id));
        Row("Stable ID", new LineEdit { Text = Unit.Id, Editable = false }, "Used by deployment saves.");
        TextField(nameof(Unit.DisplayName), "Display name", Unit.DisplayName, value => Unit.DisplayName = value);
        TextField(nameof(Unit.Role), "Role", Unit.Role, value => Unit.Role = value);
        EnumField(nameof(Unit.Shape), "Shape", Unit.Shape, value => Unit.Shape = value);

        Section("Appearance and size");
        Number(nameof(Unit.Width), "Base width", () => Unit.Width, v => Unit.Width = v, 0.001, 100, 0.001, " u", unbounded: true);
        Number(nameof(Unit.Height), "Base height", () => Unit.Height, v => Unit.Height = v, 0.001, 100, 0.001, " u", unbounded: true);
        Number(nameof(Unit.SizeMultiplier), "Size multiplier", () => Unit.SizeMultiplier, v => Unit.SizeMultiplier = v, 0.5, 3, 0.05, "×");
        Number(nameof(Unit.FootprintRadius), "Diamond length input", () => Unit.FootprintRadius, v => Unit.FootprintRadius = v,
            0.001, 100, 0.001, " u", () => Unit.Shape == UnitShape.Diamond,
            "Legacy Diamond silhouette input. The collision radius is derived from the visible outline.", true);

        Section("Movement");
        Number(nameof(Unit.Speed), "Base speed", () => Unit.Speed, v => Unit.Speed = v, 0.001, 100, 0.001, " u/s", unbounded: true);
        Number(nameof(Unit.AccelerationSecondsOverride), "Acceleration override", () => Unit.AccelerationSecondsOverride,
            v => Unit.AccelerationSecondsOverride = v, 0, 5, 0.05, " s", tooltip: "Zero uses the shared acceleration setting.");

        Section("Defense");
        Number(nameof(Unit.Health), "Health", () => Unit.Health, v => Unit.Health = v, 0.001, 1000, 0.001, " HP", unbounded: true);
        Number(nameof(Unit.Armour), "Armour", () => Unit.Armour * 100f, v => Unit.Armour = v / 100f, 0, 100, 0.1, "%");
        Number(nameof(Unit.ChargeResistance), "Charge resistance", () => Unit.ChargeResistance, v => Unit.ChargeResistance = v,
            0.01, 4, 0.01, "", tooltip: "Momentum consumed when this unit is struck by a charge.");

        Section("Primary attack");
        EnumField(nameof(Unit.Style), "Attack style", Unit.Style, value => Unit.Style = value);
        Number(nameof(Unit.Damage), "Damage", () => Unit.Damage, v => Unit.Damage = v, 0.001, 1000, 0.001, "", unbounded: true);
        Number(nameof(Unit.Interval), "Attack interval", () => Unit.Interval, v => Unit.Interval = v, 0.001, 10, 0.001, " s", unbounded: true);
        Number(nameof(Unit.Reach), "Authored reach", () => Unit.Reach, v => Unit.Reach = v, 0.001, 100, 0.001, " u",
            tooltip: "Ranged firing distance. Melee uses this for acquisition and charge approach. Normal swings require body contact.", unbounded: true);
        Number(nameof(Unit.MeleeFacingAngleDegrees), "Melee facing angle", () => Unit.MeleeFacingAngleDegrees, v => Unit.MeleeFacingAngleDegrees = v,
            1, 180, 1, "°", () => IsMelee && !HasCleave && Unit.Shape != UnitShape.Circle);

        AbilitySection("Melee cleave", nameof(Unit.CleaveEnabled), Unit.CleaveEnabled,
            v => Unit.CleaveEnabled = v, () => IsMelee, () => HasCleave);
        Number(nameof(Unit.CleaveAngleDegrees), "Cleave angle", () => Unit.CleaveAngleDegrees, v => Unit.CleaveAngleDegrees = v, 1, 180, 1, "°", () => HasCleave);
        Number(nameof(Unit.CleaveMaxAdditionalTargets), "Extra targets", () => Unit.CleaveMaxAdditionalTargets,
            v => Unit.CleaveMaxAdditionalTargets = (int)v, 0, UnitDefinition.MaxCleaveAdditionalTargets, 1, "", () => HasCleave);
        Number(nameof(Unit.CleaveSecondaryFraction), "Extra damage", () => Unit.CleaveSecondaryFraction * 100f,
            v => Unit.CleaveSecondaryFraction = v / 100f, 0, 100, 0.1, "%", () => HasCleave);

        // Shockwave is toggled through the existing cadence: 0 means off.
        AbilitySection("Shockwave", nameof(Unit.ShockwaveEveryAttacks), HasShockwave,
            enabled => Unit.ShockwaveEveryAttacks = enabled ? lastShockwaveCadence : 0,
            () => IsMelee, () => HasShockwave);
        var cadence = Number(nameof(Unit.ShockwaveEveryAttacks), "Every N attacks", () => Unit.ShockwaveEveryAttacks,
            v => { Unit.ShockwaveEveryAttacks = (int)v; if (v > 0) lastShockwaveCadence = (int)v; },
            0, 255, 1, "", tooltip: "Use Enabled to toggle the shockwave; 0 disables it.");
        refreshFields.Add(() => cadence.SetValueNoSignal(Unit.ShockwaveEveryAttacks));
        Number(nameof(Unit.ShockwaveBaseRadius), "Base radius", () => Unit.ShockwaveBaseRadius,
            v => Unit.ShockwaveBaseRadius = v, 0.1, 64, 0.1, " u", () => HasShockwave);
        Number(nameof(Unit.ShockwaveDamage), "Centre damage", () => Unit.ShockwaveDamage,
            v => { Unit.ShockwaveDamage = v; if (Unit.ShockwaveOuterDamage > v) Unit.ShockwaveOuterDamage = v; },
            0.001, 1000, 0.001, "", () => HasShockwave, unbounded: true);
        represented.Add(nameof(Unit.ShockwaveEdgeDamageFraction));
        var outer = Number(nameof(Unit.ShockwaveOuterDamage), "Outer damage", () => Unit.EffectiveShockwaveOuterDamage(Unit.ShockwaveDamage),
            v => Unit.ShockwaveOuterDamage = v, 0, float.IsFinite(Unit.ShockwaveDamage) ? Math.Max(0, Unit.ShockwaveDamage) : 0, 0.001, "", () => HasShockwave,
            "Damage at the outer radius. Older units can inherit the value from their stored falloff fraction.");
        refreshFields.Add(() =>
        {
            outer.SetBlockSignals(true);
            float centre = float.IsFinite(Unit.ShockwaveDamage) ? Mathf.Max(0f, Unit.ShockwaveDamage) : 0f;
            outer.MaxValue = centre;
            outer.SetValueNoSignal(Unit.EffectiveShockwaveOuterDamage(centre));
            outer.SetBlockSignals(false);
        });
        Flag(nameof(Unit.ShockwaveRespectsCover), "Blocked by cover", Unit.ShockwaveRespectsCover, v => Unit.ShockwaveRespectsCover = v, () => HasShockwave);

        AbilitySection("Trample", nameof(Unit.TrampleEnabled), Unit.TrampleEnabled,
            v => Unit.TrampleEnabled = v, () => IsMelee, () => HasTrample);
        Number(nameof(Unit.TrampleRunUp), "Run-up", () => Unit.TrampleRunUp, v => Unit.TrampleRunUp = v, 0.5, 10, 0.1, " u", () => HasTrample);
        Number(nameof(Unit.TrampleDistance), "Charge distance", () => Unit.TrampleDistance, v => Unit.TrampleDistance = v, 0.5, 12, 0.1, " u", () => HasTrample);
        Number(nameof(Unit.TrampleEfficiency), "Starting momentum", () => Unit.TrampleEfficiency, v => Unit.TrampleEfficiency = v, 0.1, 4, 0.05, "", () => HasTrample);
        Number(nameof(Unit.TrampleDamageMultiplier), "Damage multiplier", () => Unit.TrampleDamageMultiplier, v => Unit.TrampleDamageMultiplier = v, 0, 4, 0.05, "×", () => HasTrample);
        Number(nameof(Unit.TrampleMaxHits), "Maximum hits", () => Unit.TrampleMaxHits, v => Unit.TrampleMaxHits = (int)v, 1, 6, 1, "", () => HasTrample);

        var rangedSection = Section("Ranged");
        refreshFields.Add(() => rangedSection.Visible = !IsMelee);
        Number(nameof(Unit.MinimumFireRange), "Minimum range", () => Unit.MinimumFireRange,
            v => Unit.MinimumFireRange = v, 0, 100, 0.1, " u", () => !IsMelee,
            "Ranged attacks cannot fire closer than this distance.", unbounded: true);
        checks.Add(() => IsMelee || Unit.MinimumFireRange <= Unit.Reach
            ? null : "Minimum range cannot exceed maximum firing range.");
        represented.Add(nameof(Unit.Projectile));
        var picker = new EditorResourcePicker { BaseType = nameof(ProjectileDefinition), EditedResource = Unit.Projectile };
        Row("Projectile", picker, "Open the resource to edit flight, impact and explosion settings. Clear to use the default arrow.");
        picker.ResourceChanged += value =>
        {
            if (value != null && (value is not ProjectileDefinition || string.IsNullOrEmpty(value.ResourcePath) ||
                !FileAccess.FileExists(value.ResourcePath)))
            {
                picker.EditedResource = Unit.Projectile;
                status.Text = "Pick an existing projectile resource from disk.";
                return;
            }
            if (Unit.Projectile == value) return;
            Unit.Projectile = (ProjectileDefinition)value;
            MarkEdited();
        };
        picker.ResourceSelected += (value, _) =>
        {
            if (value != null) EditorInterface.Singleton.EditResource(value);
        };
        refreshFields.Add(() => picker.Editable = !IsMelee);

        var recoilSection = AbilitySection("Ranged recoil", nameof(Unit.RangedRecoilEnabled), Unit.RangedRecoilEnabled,
            v => Unit.RangedRecoilEnabled = v, () => !IsMelee, () => !IsMelee && Unit.RangedRecoilEnabled);
        refreshFields.Add(() => recoilSection.Visible = !IsMelee);
        Number(nameof(Unit.RangedRecoilDistance), "Recoil distance", () => Unit.RangedRecoilDistance,
            v => Unit.RangedRecoilDistance = v, 0, 4, 0.01, " u", () => !IsMelee && Unit.RangedRecoilEnabled,
            "Visual-only backward kick. Does not affect movement or projectile origins.");
        Number(nameof(Unit.RangedRecoilReturnSeconds), "Recovery time", () => Unit.RangedRecoilReturnSeconds,
            v => Unit.RangedRecoilReturnSeconds = v, 0.05, 2, 0.01, " s", () => !IsMelee && Unit.RangedRecoilEnabled,
            "Time for the ranged unit to settle back after firing.");

        var unsupported = typeof(UnitDefinition).GetProperties()
            .Where(p => Attribute.IsDefined(p, typeof(ExportAttribute)) && !represented.Contains(p.Name))
            .Select(p => p.Name).ToArray();
        if (unsupported.Length == 0) return;
        Section("Additional parameters");
        card.AddChild(new Label
        {
            Text = "Edit in Inspector: " + string.Join(", ", unsupported),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
    }

    private PanelContainer Section(string title)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(280, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        fields.AddChild(panel);
        card = new VBoxContainer();
        panel.AddChild(card);
        card.AddChild(Heading(title));
        card.AddChild(new HSeparator());
        return panel;
    }

    // Optional abilities use one consistent compact header; the detail controls exist only in this editor.
    private PanelContainer AbilitySection(string title, string key, bool initial, Action<bool> set,
        Func<bool> available, Func<bool> active)
    {
        var panel = Section(title);
        VBoxContainer section = card;
        var toggle = new CheckBox { Text = "Enabled", ButtonPressed = initial };
        section.AddChild(toggle);
        var details = new VBoxContainer();
        section.AddChild(details);
        card = details;
        represented.Add(key);
        toggle.Toggled += enabled => { set(enabled); MarkEdited(); };
        refreshFields.Add(() =>
        {
            toggle.Disabled = !available();
            toggle.SetPressedNoSignal(active());
            details.Visible = active();
        });
        return panel;
    }

    private void Row(string name, Control editor, string tooltip = "")
    {
        var row = new HBoxContainer();
        card.AddChild(row);
        var label = new Label { Text = name, CustomMinimumSize = new Vector2(115, 0), TooltipText = tooltip };
        row.AddChild(label);
        editor.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        editor.TooltipText = tooltip;
        row.AddChild(editor);
    }

    private void TextField(string key, string label, string value, Action<string> set)
    {
        represented.Add(key);
        var input = new LineEdit { Text = value };
        input.TextChanged += text => { set(text); MarkEdited(); };
        Row(label, input);
    }

    private void EnumField<T>(string key, string label, T value, Action<T> set) where T : struct, Enum
    {
        represented.Add(key);
        var input = new OptionButton();
        T[] values = Enum.GetValues<T>();
        foreach (T item in values) input.AddItem(item.ToString());
        input.Select(Array.IndexOf(values, value));
        input.ItemSelected += index => { set(values[(int)index]); MarkEdited(); };
        checks.Add(() => input.Selected >= 0 ? null : $"Choose a valid {label.ToLowerInvariant()}.");
        Row(label, input);
    }

    private void Flag(string key, string label, bool value, Action<bool> set, Func<bool> enabled)
    {
        represented.Add(key);
        var input = new CheckBox { ButtonPressed = value };
        input.Toggled += pressed => { set(pressed); MarkEdited(); };
        refreshFields.Add(() => input.Disabled = !enabled());
        Row(label, input);
    }

    private SpinBox Number(string key, string label, Func<float> get, Action<float> set, double min, double max,
        double step, string suffix, Func<bool> enabled = null, string tooltip = "", bool unbounded = false)
    {
        represented.Add(key);
        var input = new SpinBox { MinValue = min, MaxValue = max, Step = step, Suffix = suffix, AllowGreater = unbounded };
        input.SetValueNoSignal(float.IsFinite(get()) ? get() : min);
        Row(label, input, tooltip);
        input.ValueChanged += value =>
        {
            if (!input.IsInsideTree() || (float)value == get()) return;
            set((float)value);
            MarkEdited();
        };
        if (enabled != null) refreshFields.Add(() => input.Editable = enabled());
        checks.Add(() =>
        {
            float value = get();
            return float.IsFinite(value) && value >= (float)min && (unbounded || value <= (float)input.MaxValue)
                ? null : $"Correct {label.ToLowerInvariant()} before saving.";
        });
        return input;
    }
}
#endif
