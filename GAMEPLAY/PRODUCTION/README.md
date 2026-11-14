# Fabricator production

Edit `FabricatorRecipes.tres` to tune each registered unit's batch. Keys use stable unit IDs and each vector stores batch size (X), Carbon cost (Y), and seconds (Z). Combat stats still come from the unit catalog.

Each Fabricator uses the player's shared Carbon balance and snapshots one paid recipe at a time. Changing the dropdown or turning off automatic production takes effect for the next batch. Finished units release individually, with a minimum 0.15-second interval. A blocked attempt retains the remaining batch for retry.
