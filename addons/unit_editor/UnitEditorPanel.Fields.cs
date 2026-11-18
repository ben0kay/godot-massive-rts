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
        if (draft == null) return;
        if (draft.Definition is BuildingDefinition)
        {
            BuildDefenseForm();
            return;
        }
        if (draft.Definition is ProjectileDefinition)
        {
            BuildProjectileForm();
            return;
        }
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

        Number(nameof(Unit.TurnDegreesPerSecond), "Turn rate", () => Unit.TurnDegreesPerSecond,
            v => Unit.TurnDegreesPerSecond = v, 0, 720, 1, "°/s", () => Unit.Shape != UnitShape.Circle,
            "Degrees per second while moving or aiming. Zero uses 300°/s. Circles skip turning.");

        Section("Defense");
        TextField(nameof(Unit.ArmourClass), "Armour class", Unit.ArmourClass, value => Unit.ArmourClass = value);
        Number(nameof(Unit.Health), "Health", () => Unit.Health, v => Unit.Health = v, 0.001, 1000, 0.001, " HP", unbounded: true);
        Number(nameof(Unit.Armour), "Armour", () => Unit.Armour * 100f, v => Unit.Armour = v / 100f, 0, 100, 0.1, "%");
        Number(nameof(Unit.ChargeResistance), "Charge resistance", () => Unit.ChargeResistance, v => Unit.ChargeResistance = v,
            0.01, 4, 0.01, "", tooltip: "Momentum consumed when this unit is struck by a charge.");

        Section("Primary attack");
        TextField(nameof(Unit.DamageType), "Damage type", Unit.DamageType, value => Unit.DamageType = value);
        Number(nameof(Unit.UnitDamagePercent), "Damage vs units", () => Unit.UnitDamagePercent,
            v => Unit.UnitDamagePercent = v, -1, 1000, 1, "%", tooltip: "−1 uses the damage matrix. Otherwise this is the percentage of base damage.");
        Number(nameof(Unit.StructureDamagePercent), "Damage vs structures", () => Unit.StructureDamagePercent,
            v => Unit.StructureDamagePercent = v, -1, 1000, 1, "%", tooltip: "−1 uses the damage matrix. Otherwise this is the percentage of base damage.");
        EnumField(nameof(Unit.AllowedTargets), "Allowed targets", Unit.AllowedTargets, value => Unit.AllowedTargets = value);
        EnumField(nameof(Unit.PreferredTarget), "Preferred target", Unit.PreferredTarget, value => Unit.PreferredTarget = value);
        Number(nameof(Unit.StructureDetectionRange), "Structure detection", () => Unit.StructureDetectionRange,
            v => Unit.StructureDetectionRange = v, 0, 100, 0.1, " u", () => (Unit.AllowedTargets & TargetCategory.Structures) != 0);
        Number(nameof(Unit.MeleeLungeDistance), "Melee lunge", () => Unit.MeleeLungeDistance,
            v => Unit.MeleeLungeDistance = v, 0, 1, 0.01, " u", () => IsMelee);
        EnumField(nameof(Unit.Style), "Attack style", Unit.Style, value => Unit.Style = value);
        Number(nameof(Unit.Damage), "Damage", () => Unit.Damage, v => Unit.Damage = v, 0, 1000, 0.001, "", unbounded: true);
        checks.Add(() => Unit.AllowedTargets == TargetCategory.None || Unit.Damage > 0f
            ? null : "Combat units require positive base damage.");
        Number(nameof(Unit.Interval), "Attack interval", () => Unit.Interval, v => Unit.Interval = v, 0.001, 10, 0.001, " s", unbounded: true);
        Number(nameof(Unit.Reach), "Authored reach", () => Unit.Reach, v => Unit.Reach = v, 0.001, 100, 0.001, " u",
            tooltip: "Ranged firing distance. Melee uses this for acquisition and charge approach. Melee strike distance uses the unit footprints and any extra cleave reach.", unbounded: true);
        Number(nameof(Unit.MeleeFacingAngleDegrees), "Melee facing angle", () => Unit.MeleeFacingAngleDegrees, v => Unit.MeleeFacingAngleDegrees = v,
            1, 180, 1, "°", () => IsMelee && !HasCleave && Unit.Shape != UnitShape.Circle);

        represented.Add(nameof(Unit.CleaveEnabled));
        var hitPattern = new OptionButton();
        hitPattern.AddItem("Single target");
        hitPattern.AddItem("Area (cleave)");
        hitPattern.Select(Unit.CleaveEnabled ? 1 : 0);
        hitPattern.ItemSelected += index => { Unit.CleaveEnabled = index == 1; MarkEdited(); };
        var meleePatternRow = Row("Hit pattern", hitPattern);
        refreshFields.Add(() => meleePatternRow.Visible = IsMelee);

        var rangedPattern = new Label();
        var rangedPatternRow = Row("Hit pattern", rangedPattern);
        refreshFields.Add(() =>
        {
            rangedPatternRow.Visible = !IsMelee;
            rangedPattern.Text = Unit.Projectile?.ExplodesOnImpact == true ? "Area (blast)" : "Single target";
        });

        var primaryAttack = card;
        var cleaveFields = new VBoxContainer();
        primaryAttack.AddChild(cleaveFields);
        card = cleaveFields;
        refreshFields.Add(() => cleaveFields.Visible = HasCleave);
        Number(nameof(Unit.CleaveExtraReach), "Extra cleave reach", () => Unit.CleaveExtraReach,
            v => Unit.CleaveExtraReach = v, 0, UnitDefinition.MaxCleaveExtraReach, 0.05, " u", () => HasCleave,
            "World units added beyond normal melee contact for the main target and extra cleave hits.");
        Number(nameof(Unit.CleaveAngleDegrees), "Cleave angle", () => Unit.CleaveAngleDegrees, v => Unit.CleaveAngleDegrees = v, 1, 180, 1, "°", () => HasCleave);
        Number(nameof(Unit.CleaveMaxAdditionalTargets), "Extra targets", () => Unit.CleaveMaxAdditionalTargets,
            v => Unit.CleaveMaxAdditionalTargets = (int)v, 0, UnitDefinition.MaxCleaveAdditionalTargets, 1, "", () => HasCleave);
        Number(nameof(Unit.CleaveSecondaryFraction), "Extra damage", () => Unit.CleaveSecondaryFraction * 100f,
            v => Unit.CleaveSecondaryFraction = v / 100f, 0, 100, 0.1, "%", () => HasCleave);
        card = primaryAttack;

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
        Row("Projectile", picker, "Choose a projectile resource. Clear to use the default projectile.");
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
        var editProjectile = new Button { Text = "Edit projectile settings" };
        card.AddChild(editProjectile);
        editProjectile.Pressed += () =>
        {
            string path = Unit.Projectile?.ResourcePath ?? ProjectileCatalog.BasicArrowPath;
            Callable.From(() => RequestSwitch(() => LoadEntity(path))).CallDeferred();
        };
        refreshFields.Add(() =>
        {
            picker.Editable = !IsMelee;
            editProjectile.Disabled = IsMelee || Unit.Projectile is not null &&
                !Unit.Projectile.ResourcePath.EndsWith(".tres", StringComparison.OrdinalIgnoreCase);
        });

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

    // =========================================================
    // Use the same form controls and validation as the soldier editor.
    private void BuildDefenseForm()
    {
        var defense = Defense;
        Section("Identity");
        Row("Stable ID", new LineEdit { Text = defense.Id, Editable = false }, "Used by deployment saves.");
        TextField(nameof(defense.DisplayName), "Display name", defense.DisplayName, value => defense.DisplayName = value);

        Number(nameof(defense.Health), "Health", () => defense.Health, value => defense.Health = value,
            1, 50000, 1, " HP", unbounded: true);

        Section("Footprint");
        Number(nameof(defense.Width), "Width", () => defense.Width, value => defense.Width = (int)value, 1, 16, 1, " cells");
        Number(nameof(defense.Depth), "Depth", () => defense.Depth, value => defense.Depth = (int)value, 1, 16, 1, " cells");

        AbilitySection("Static defense", nameof(defense.AttacksSoldiers), defense.AttacksSoldiers,
            value => defense.AttacksSoldiers = value, () => true, () => defense.AttacksSoldiers);
        Number(nameof(defense.MinimumRange), "Minimum range", () => defense.MinimumRange, value => defense.MinimumRange = value, 0, 100, 0.1, " u");
        Number(nameof(defense.MaximumRange), "Maximum range", () => defense.MaximumRange, value => defense.MaximumRange = value, 0.1, 100, 0.1, " u");
        checks.Add(() => defense.MinimumRange <= defense.MaximumRange ? null : "Minimum range cannot exceed maximum range.");
        TextField(nameof(defense.DamageType), "Damage type", defense.DamageType, value => defense.DamageType = value);
        Number(nameof(defense.Damage), "Damage", () => defense.Damage, value => defense.Damage = value, 0, 100000, 0.1, "");
        Number(nameof(defense.ShotsPerSecond), "Fire rate", () => defense.ShotsPerSecond, value => defense.ShotsPerSecond = value, 0.05, 60, 0.05, " shots/s");
        Number(nameof(defense.TurretDegreesPerSecond), "Turret turn rate", () => defense.TurretDegreesPerSecond,
            value => defense.TurretDegreesPerSecond = value, 1, 3600, 1, "°/s");
        Number(nameof(defense.BeamSeconds), "Beam duration", () => defense.BeamSeconds, value => defense.BeamSeconds = value, 0.01, 2, 0.01, " s");
        Flag(nameof(defense.RequiresLineOfSight), "Requires line of sight", defense.RequiresLineOfSight,
            value => defense.RequiresLineOfSight = value, () => defense.AttacksSoldiers);
    }

    private void BuildProjectileForm()
    {
        var projectile = Projectile;
        Section("Flight");
        EnumField(nameof(projectile.ObstacleFire), "Obstacle fire", projectile.ObstacleFire, v => projectile.ObstacleFire = v);
        Flag(nameof(projectile.BlocksOnObstacles), "Stops at obstacles", projectile.BlocksOnObstacles,
            v => projectile.BlocksOnObstacles = v, () => true);
        Number(nameof(projectile.Speed), "Speed", () => projectile.Speed, v => projectile.Speed = v, 1, 200, 0.1, " u/s", unbounded: true);
        Number(nameof(projectile.ArcHeight), "Maximum arc height", () => projectile.ArcHeight, v => projectile.ArcHeight = v,
            0, 80, 0.05, " u", unbounded: true);
        Number(nameof(projectile.ArcHeightPerDistance), "Arc per unit", () => projectile.ArcHeightPerDistance,
            v => projectile.ArcHeightPerDistance = v, 0, 1, 0.01, "");
        Flag(nameof(projectile.TintByFaction), "Tint by faction", projectile.TintByFaction,
            v => projectile.TintByFaction = v, () => true);

        AbilitySection("Explosion", nameof(projectile.ExplodesOnImpact), projectile.ExplodesOnImpact,
            v => projectile.ExplodesOnImpact = v, () => true, () => projectile.ExplodesOnImpact);
        Number(nameof(projectile.ExplosionRadius), "Blast radius", () => projectile.ExplosionRadius,
            v => projectile.ExplosionRadius = v, 0.1, 64, 0.1, " u");
        // Keep the authored fraction as-is, including values such as 31.25%.
        Number(nameof(projectile.ExplosionEdgeFraction), "Damage at edge", () => projectile.ExplosionEdgeFraction * 100f,
            v => projectile.ExplosionEdgeFraction = v / 100f, 0, 100, 0.01, "%", tooltip: "Percentage of the firing unit's center damage.");
        card.AddChild(new Label { Text = "Base damage is set on the unit." });
        Section("Shared resource");
        card.AddChild(new Label { Text = "Saving updates every unit using this projectile.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart });
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

    private HBoxContainer Row(string name, Control editor, string tooltip = "")
    {
        var row = new HBoxContainer();
        card.AddChild(row);
        var label = new Label { Text = name, CustomMinimumSize = new Vector2(115, 0), TooltipText = tooltip };
        row.AddChild(label);
        editor.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        editor.TooltipText = tooltip;
        row.AddChild(editor);
        return row;
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
