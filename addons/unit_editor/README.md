# Unit Editor

Build the C# project in Godot 4.7 .NET, then enable **Unit Editor** in **Project → Project Settings → Plugins**. Open the **Unit Editor** main tab and select a soldier. Search accepts display names, stable IDs, and shapes. The form uses the registered unit definitions from the default catalogue.

The preview shows the same outline and size as the game, with optional footprint, cleave, and shockwave overlays. The cleave overlay illustrates the angle. Actual melee contact also depends on the defender’s footprint. The summary includes the shared movement multiplier.

**Save unit** writes the selected .tres. Godot’s external-data save also validates and saves the draft. **Reload** asks before discarding edits. Save or reload before switching units or refreshing the catalogue. If the file changed on disk, saving is blocked until you reload. Disabling and re-enabling the plugin in the same editor session preserves the draft. Save before closing Godot or rebuilding the C# assembly with the plugin disabled.

The ranged section links to the separate projectile resource. Unknown future fields are listed for editing in the Inspector. Restart the running arena after saving to load the new unit settings.

Optional ability panels (**Melee cleave**, **Shockwave**, **Trample**) are collapsed by default when disabled: only the section heading and **Enabled** checkbox remain visible. Ticking Enabled expands the relevant values immediately. Disabling Cleave or Trample keeps their authored subordinate settings intact. Shockwave uses its existing cadence of **0 = disabled**; the editor remembers the previous positive cadence while you are editing, or chooses 5 when activating a unit with no existing cadence. Other shockwave settings (radius/damage/cover) stay intact. The projectile panel is shown for ranged units only. These are **editor UI visibility changes**, with no new per-soldier gameplay work.
