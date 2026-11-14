# DEATH1 — Soldier death feedback

Lethal damage removes the soldier from gameplay immediately and captures its grounded position, type, faction and facing for presentation. The living army renderer removes dead instances on its normal sync. A separate renderer reuses the shared soldier meshes and faction colours, shrinking each body toward its ground point over 0.20 seconds.

Three small dark-red geometric puffs move outward, rise and fade over 0.28 seconds. One packed pool caps simultaneous deaths at 512 and puffs at 1,536. Extra deaths skip the effect when the pool is full. Each type uses a shared MultiMesh batch and all puffs share one batch. The effect allocates no nodes or collections per death.

Use **Display and controls → Death effects** to disable the effect and clear active visuals. Pause freezes the pool. Clearing or restarting a battle empties it. The initial ground elevation comes from the lethal-hit pose.
