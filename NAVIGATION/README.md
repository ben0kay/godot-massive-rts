# NAVIGATION — Static Obstacle Routes

ObstacleNavigation prepares a corner visibility graph per compiled unit footprint when a battle with walls starts. Clear corridors use ordinary steering. Blocked corridors use shared routes keyed by source and destination sectors and unit type. A cached route must pass the exact start-to-entry and exit-to-goal checks before use. Incompatible coarse entries fall back to smaller sectors. While a route request is pending, a soldier can approach the wall and wait just outside its footprint clearance. An unreachable goal can still leave a unit holding. Soldiers carry speed through route waypoints and brake at their actual stopping position. Route exits can lead to a clear position within attack range of a target whose centre is too close to a wall for the attacker’s footprint.

Route builds are limited per rendered frame. The Inspector exposes sector size, cache capacity, and builds per frame under **Obstacles** in BenchmarkConfig. The overlay reports queue depth and latency. The rectangle cap bounds graph preparation and storage. Dense congestion at a corner and very tight or complex openings still need owner testing. Pending-route timing can vary with rendering frame rate.

The wall field independently clips each ground displacement, including cavalry and crowd separation. Maps without walls allocate no navigation graph or route state.

Route followers check up to three waypoints ahead and take the furthest visible one in that window. If the next leg becomes blocked, a follower can return to the previous corner. Waypoints are placed 0.35 units beyond the footprint clearance. Tight gaps can lose a waypoint that fit with the previous smaller margin.
