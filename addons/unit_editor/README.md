# Entity Editor

Build the C# project in Godot 4.7 .NET, then enable **Entity Editor** in **Project → Project Settings → Plugins**. Open the **Entity Editor** main tab and choose Units or Defenses. Search accepts display names, stable IDs, and shapes. The form uses the default soldier and building catalogues.

The preview shows the same outline and size as the game, with optional footprint, cleave, and shockwave overlays. The cleave overlay illustrates the angle. Actual melee contact also depends on the defender’s footprint. The summary includes the shared movement multiplier.

**Save entity** writes the selected .tres. Godot’s external-data save also validates and saves the draft. **Reload** asks before discarding edits. Selecting another entity or category opens it immediately when the current draft is clean. With unsaved edits, choose **Save and Switch**, **Discard and Switch**, or **Cancel**. Save or reload before refreshing the catalogue. If the file changed on disk, saving is blocked until you reload. Disabling and re-enabling the plugin in the same editor session preserves the draft. Save before closing Godot or rebuilding the C# assembly with the plugin disabled.

The ranged section links to the separate projectile resource. Unknown future fields are listed for editing in the Inspector. Restart the running arena after saving to load the new unit settings.

Optional ability panels (**Melee cleave**, **Shockwave**, **Trample**) are collapsed by default when disabled: only the section heading and **Enabled** checkbox remain visible. Ticking Enabled expands the relevant values immediately. Disabling Cleave or Trample keeps their authored subordinate settings intact. Shockwave uses its existing cadence of **0 = disabled**; the editor remembers the previous positive cadence while you are editing, or chooses 5 when activating a unit with no existing cadence. Other shockwave settings (radius/damage/cover) stay intact. The projectile panel is shown for ranged units only. These are **editor UI visibility changes**, with no new per-soldier gameplay work.

**Ranged recoil** is a collapsible optional section for ranged units only, with enable checkbox, backward kick distance and return time. Triangle Archer defaults to 0.12 units, 0.18 seconds; other units default off. The kick affects only the rendered mesh; projectiles and movement remain authoritative. Restart the battle after editing.

For Heavy Artillery, set minimum range in **Ranged**, maximum range and centre damage in **Primary attack**, and recoil in **Ranged recoil**. Open the assigned projectile in the Inspector to edit its arc, explosion radius, and outer damage fraction. The explosion settings appear when **Explodes on Impact** is enabled.

**Defenses** exposes Laser Tower 001 and other registered building definitions. Edit footprint, minimum and maximum range, damage, fire rate, turret speed, beam duration and line of sight. Disabling the attack hides its settings and retains their values. The preview shows a top-down footprint and turret. Restart the benchmark scene after saving so it recompiles the building catalogue.
