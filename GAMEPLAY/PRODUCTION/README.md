# Fabricator production

Edit `FabricatorRecipes.tres` to tune each registered unit's batch. Keys use stable unit IDs and each vector stores batch size (X), Carbon cost (Y), and seconds (Z). Combat stats still come from the unit catalog.

Each Fabricator uses the player's shared Carbon balance and snapshots one paid recipe at a time. Changing the dropdown or turning off automatic production takes effect for the next batch. Finished units release individually, with a minimum 0.075-second interval (2× the previous dispensing rate). Units move 1.5× their authored speed only while clearing the Fabricator exit, then return to their normal speed. The hatch prevents overlapping spawns, and blocked exits retain the remaining batch for retry. A blocked attempt retains the remaining batch for retry.

The H Breacher recipe produces six units for 165 Carbon in 12 seconds, followed by the normal release sequence.
