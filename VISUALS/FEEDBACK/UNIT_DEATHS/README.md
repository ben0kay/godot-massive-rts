# DEATH1 — Soldier death feedback

Lethal damage removes the soldier from gameplay immediately and captures its grounded position, type, faction and facing for presentation. The living army renderer removes dead instances on its normal sync. A separate renderer reuses the shared soldier meshes and faction colours, shrinking each body toward its ground point over 0.20 seconds.

Four slightly larger dark-red geometric blood-like puffs move outward, rise and fade over 0.28 seconds. Each puff's base scale is 0.09–0.155 (previously 0.06–0.11), with the same short expansion and fading. The four directions are distributed around the death point with a deterministic per-death rotation. One packed pool caps simultaneous deaths at 512 and puffs at 2,048. Extra deaths skip the effect when the pool is full. Each type uses a shared MultiMesh batch and all puffs share one batch. The effect allocates no nodes or collections per death.

Use **Display and controls → Death effects** to disable the effect and clear active visuals. Pause freezes the pool. Clearing or restarting a battle empties it. The initial ground elevation comes from the lethal-hit pose.
