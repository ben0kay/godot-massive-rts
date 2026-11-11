# Development Log — Godot Massive RTS

Concise, chronological record of completed implementation tasks. **Append new entries at the bottom**; oldest first. This is a history of source changes, not proof of in-game acceptance. See `NOTES/CodePreferences.md` for the logging rule.

## Entries

- **2026-11-10 | BENCH0 | Source implemented; awaiting owner validation.** Added standalone 3D isometric benchmark arena, batched geometric armies (up to 100k selectable), movement, benchmark melee/ranged workloads, projectiles, deployment presets, camera and performance overlay. No Godot/.NET runtime validation in Codex environment. Commit: `834a383a`.

- **2026-11-10 | BENCH0 optimization | Source implemented.** Added bulk soldier buffer uploads for dense updates, simplified straight lane movement, and exposed simulation cost per tick and ticks per frame. Owner performance testing pending.

- **2026-11-10 | BENCH0 targeting and projectiles | Source implemented.** Skip target searches beyond opposing faction bounds, report grid rebuild and targeting separately, and remove projectiles on their first enemy hit. Owner testing pending.


- **2026-11-10 | VIS1 (faceted units) | Source implemented; awaiting owner validation.** Added reusable low-poly prism/box/hex meshes with lighter top faces and darker fixed-direction sides via vertex colours, preserving MultiMesh batching, faction tints and white damage flashes; no dynamic lights or shadows. Owner visual/FPS testing pending.
