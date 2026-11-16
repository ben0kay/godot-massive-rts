# Command map resources

Place `CARBON/CarbonDeposit.tscn` under the **Resources** node of a command level. Set Initial Carbon, Visual Size and Footprint Cells in the Inspector. Each instance starts with its own authored reserve and uses its level-relative node path as its identity. The map rounds the footprint to its build grid and places the deposit upright at the sampled terrain height.

Deposits participate in the shared obstacle field and block building placement. Click a deposit during play to inspect its reserve. Deposits keep their quantity until a future harvesting system changes it.

## CARBON2 — Worker-built Carbon Extractors

Select a blue **Worker** and **right-click a Carbon Deposit** to order construction. The Worker approaches a valid point at the mineral footprint's edge, then spends 4 seconds building a compact cyan-and-metal extractor rig. It becomes an ordinary **destructible structure** using the shared building-target list (including H Breachers), and automatically transfers Carbon into the **existing Citadel/Fabricator wallet**. There is no worker cargo or hauling. The worker may move away when construction is complete; production continues automatically.

Initial settings are Inspector-editable in `CARBON/DefaultCarbonExtractorSettings.tres`: **350 HP**, Medium armour class, **4 seconds** construction and **5 Carbon/second** extraction. Each deposit is finite (default 5,000 Carbon); its remaining quantity decreases by exactly the amount credited to the wallet. Deposits remain on the map when exhausted or when their extractor is destroyed, and can be rebuilt by assigning a fresh Worker after destruction. Only one active extractor/constructor per deposit is supported. Worker death or a new movement order cancels their construction task; an unfinished rig remains for another worker to finish. No resources are generated before construction finishes or after extractor destruction/depletion. Extractor meshes are shared, and active deposits do not process individually.
