# Command map resources

Place `CARBON/CarbonDeposit.tscn` under the **Resources** node of a command level. Set Initial Carbon, Visual Size and Footprint Cells in the Inspector. Each instance starts with its own authored reserve and uses its level-relative node path as its identity. The map rounds the footprint to its build grid and places the deposit upright at the sampled terrain height.

Deposits participate in the shared obstacle field and block building placement. Click a deposit during play to inspect its reserve. Deposits keep their quantity until a future harvesting system changes it.
