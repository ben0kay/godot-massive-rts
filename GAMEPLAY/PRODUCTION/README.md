# Fabricator production

Edit `FabricatorRecipes.tres` to tune each registered unit's batch. Keys use stable unit IDs and each vector stores batch size (X), Carbon cost (Y), and seconds (Z). Combat stats still come from the unit catalog.

Each Fabricator uses the player's shared Carbon balance and snapshots one paid recipe at a time. Changing the dropdown or turning off automatic production takes effect for the next batch. Finished units release individually, with a minimum 0.075-second interval (2× the previous dispensing rate). Units move 1.5× their authored speed only while clearing the Fabricator exit, then return to their normal speed. Units may overlap on release and separate during movement. A physically blocked exit retains the remaining batch for retry.

The H Breacher recipe produces six units for 165 Carbon in 12 seconds, followed by the normal release sequence.

The Long Triangle Marksman (`long_triangle_marksman`) can also be produced from the same catalogue-fed unit picker: 8 units for 210 Carbon in 14 seconds. Its stats are controlled by `GAMEPLAY/UNITS/DEFINITIONS/LongTriangleMarksman.tres`.

The Citadel queues one Worker per paid order using the shared Carbon balance. Tune cost, time and queue length in `DefaultCitadelWorkerSettings.tres`, or assign a level override via **Worker Production Settings**. A finished Worker waits for an open exit and available troop slot. Worker orders are manual.

The Tank (`tank`) has a provisional recipe of 1 unit for 220 Carbon in 22 seconds. Missing recipes leave a zero-sized unavailable slot (excluded from the picker); explicitly malformed recipes still fail validation. This allows registered combat units to remain unavailable for production until a recipe is authored.
