# NAVIGATION — Static Obstacle Routes

ObstacleNavigation prepares a corner visibility graph per compiled unit footprint when a battle with walls starts. Clear corridors use ordinary steering. Blocked corridors use shared routes keyed by source and destination sectors and unit type. A cached route must pass the exact start-to-entry and exit-to-goal checks before use. Incompatible coarse entries fall back to smaller sectors. While waiting or when no route is available, a soldier holds at its legal position. Existing routes are followed until the next waypoint is reached, the route becomes invalid, or a direct corridor opens.

Route builds are limited per rendered frame. The Inspector exposes sector size, cache capacity, and builds per frame under **Obstacles** in BenchmarkConfig. The overlay reports queue depth and latency. The rectangle cap bounds graph preparation and storage. Dense congestion at a corner and very tight or complex openings still need owner testing. Pending-route timing can vary with rendering frame rate.

The wall field independently clips each ground displacement, including cavalry and crowd separation. Maps without walls allocate no navigation graph or route state.
