# Unit Implementation Standard (UNIT-STD1)

> **Repository:** `ben0kay/godot-massive-rts` · **Branch:** `main` · **Reviewed:** 2026-11-28 at `1b6292c`
>
> **Status:** Authoring standard for future approved unit work. This is not authorization to add units, rebalance the roster, or refactor the catalogue. Live code and resources take precedence over historical plans and example values. Source implementation is not owner acceptance.

## 0. Mandatory reading and scope

Before adding or materially changing a unit, read [root README](../../README.md), [CodePreferences](../../CodePreferences.md), [endgoal](../../endgoal.md), [unit catalogue guide](README.md), [visual authoring guide](../../VISUALS/authoring.md), and any applicable AGENTS.md. Read the relevant combat, projectile, navigation, shields, production and deployment guides linked below, then inspect the actual live source and nearest existing unit definition. For a tower use the separate [Tower Implementation Standard](../../WORLD/BUILDINGS/TOWER_IMPLEMENTATION_STANDARD.md).

Confirm the requested role, size tier, attack target categories, intended game modes, reusable abilities and optional visual features. Use the owner's requested numbers when given; otherwise choose and clearly report provisional defaults based on the closest existing units. Do not infer that a new unit should have every available effect or be available for purchase in every mode.

## 1. Source ownership and the shortest path to a new unit

| Responsibility | Current owner |
| --- | --- |
| Editable identity, stats, abilities | `DEFINITIONS/UnitDefinition.cs`, one `DEFINITIONS/<Name>.tres` per unit |
| Weapon mounts | `DEFINITIONS/RocketMount.cs` and typed subresources |
| Ground roster and compiled stats | `CATALOG/DefaultUnitCatalog.tres`, `CATALOG/UnitCatalog.cs` (UnitOutline, UnitStats, RuntimeUnitCatalog) |
| Ground simulation | `../SIMULATION/UnitState.cs`, MovementSystem, SpatialGrid, shared separation |
| Attacks | `../COMBAT/CombatSystem.cs`, StructureCombat, AreaDamage, DamageMatrix and existing feature owners |
| Projectile assets | `../PROJECTILES/Types/<Name>/<Name>.tres`; shared ProjectileDefinition, ProjectileCatalog, ProjectileSystem, ProjectileRenderer |
| Ground meshes | `../../VISUALS/RENDERING/FacetedSoldierMeshes.cs`, ArmyRenderer, shared armor/animation shaders |
| Visual configuration | `UnitVisualConfig.cs` and `UnitVisualConfig.tres` |
| Editor | `../../addons/unit_editor/UnitEditorPanel.cs`, UnitEditorPanel.Fields.cs, UnitEditorPreview.cs |
| Deployment | `../DEPLOYMENT/`, `../../UI/Editor/LevelEditor.Deployment.cs`, `../../BENCHMARK/` |
| Aircraft | Catalogue Aircraft array; `../COMBAT/AIR/AirSquadron.cs`, AirSquadronVisuals, existing aircraft effects |
| Recruitment/production | `../../LEVELS/AutoBattler/PROGRESSION/DefaultAutoBattlerRecruitment.tres`, `../PRODUCTION/FabricatorRecipes.tres` |

Prefer the smallest change: copy a nearby definition, reuse an existing outline and weapon behavior, register the new ID, and add a distinct cached mesh if needed. Extend an existing typed resource and shared runtime only when the requested behavior cannot be expressed by existing data. No parallel unit class hierarchy or node/scene per soldier.

## 2. Existing roster as reference

The following is the reviewed catalogue, not a balance prescription. Ground types have 20 dense runtime indices; aircraft have a separate array. Use the current resources for exact values. The tier is physical interaction class, not purchase price or automatic power ranking.

| Definition file (under DEFINITIONS/) | Stable ID | Tier | Design / reuse reference |
| --- | --- | ---: | --- |
| CircleInfantry.tres | circle_infantry | 1 | Cheap general-purpose melee, round robot, sword and gait |
| SquareInfantry.tres | square_infantry | 1 | Strong melee, hammer and cosmetic shield |
| TriangleArcher.tres | triangle_archer | 1 | Fragile ranged infantry, LaserBolt, spread/forest deflection |
| DiamondCavalry.tres | diamond_cavalry | 1.5 | Fast flanker, charge and trample |
| PentagonElite.tres | pentagon_elite | 2 | Slow elite melee, cleave and axe |
| HexagonSiege.tres | hexagon_siege | 3 | Heavy cleave, periodic stomp, opt-in blocking wreck |
| ChevronArtillery.tres | chevron_artillery | 3 | Indirect explosive fire, units and buildings |
| HBreacher.tres | h_breacher | 1.5 | Twin ram melee, structure preference |
| LongTriangleMarksman.tres | long_triangle_marksman | 1.5 | Longer-range precision infantry, LongLaserBolt |
| Worker.tres | worker | 3 | Noncombat utility, zero battle rating, Citadel worker production |
| Pikeman.tres | pikeman | 1 | Single-target melee bonus versus Cavalry |
| Dreadnaught.tres | dreadnaught | 5 | Super-heavy melee hull, independent miniguns and rocket cannon |
| TwinRocketLauncher.tres | twin_rocket_launcher | 2 | Fixed alternating mounts, indirect splash |
| PrismLance.tres | prism_lance | 3 | Charged, piercing beam |
| MobileGenerator.tres | mobile_generator | 2 | Melee support and shield aura |
| Obliterator.tres | obliterator | 2 | Single-target first-hit bonus |
| SiegeRam.tres | siege_ram | 3 | Structures-only melee, tracked vehicle |
| Interceptor.tres | interceptor_infantry | 2 | Melee plus guided anti-air launcher |
| RallyCommander.tres | rally_commander | 2 | Timed allied rally pulse |
| Tank.tres | tank | 4 | Independent primary cannon, nested roof minigun, tracks/casings |
| VWing.tres | v_wing | 1 (air) | Air interceptor |
| HeavyBomber.tres | heavy_bomber | 2 (air) | Ground-attack aircraft, payload and exhaust |

Suggested design workflow: select a role first, compare health, damage, interval, range, speed, tier, footprint and BattleRating against 2–3 neighbors, then document trade-offs. A support unit's contribution need not be reflected by direct DPS alone. Do not silently tune the neighbors.

## 3. Definition and catalogue registration

### A. Author a typed resource

Create a PascalCase file in DEFINITIONS/ with a unique stable snake_case Id, human DisplayName, short Role, BattleRating, Shape, Width, Height, SizeMultiplier, SizeTier, movement, defense and attack settings as appropriate. Use [Export] fields, not hardcoded scattered values. Godot enum integers in .tres are serialized values: verify the current enum before assigning a number. Existing shapes are reusable; Tank and Siege Ram both use SiegeRam shape (15), and the Commander reuses Pentagon. A new outline is only necessary when existing geometry cannot represent the role.

- BattleRating is the estimate used for painted strength and roster presentation, not recruitment price. Worker has rating 0.
- Interval is seconds between attacks (not attacks per second). Verify compiled effective speed, since the catalogue applies the global movement multiplier.
- Health, normal attack Damage, Speed and other positive fields may fall back to defaults when invalid or zero. A truly noncombat unit uses AllowedTargets = None, as Worker does.
- Keep all factions on the same definition and allow the shared palette to supply visual identity.

### B. Register ground units correctly

The current catalogue is **not fully dynamic**: adding a .tres alone is insufficient.

1. Add the resource to DefaultUnitCatalog.tres's Definitions array (unique ext_resource ID; maintain resource counts).
2. Append the new stable ID constant, byte index and OrderedIds entry in RuntimeUnitCatalog. Never renumber or reorder existing runtime indices or serialized UnitShape values. Existing ground indices are 0–19 at this review. Keep arrays based on Count/OrderedIds.Length; audit any fixed counts and byte limits.
3. Follow the existing missing-definition fallback for older/custom catalogues. Do not overwrite custom overrides. The constructor requires unique IDs, a complete known ground set and at least one melee unit; Dreadnaught shape is reserved for its own ID.
4. Compile settings once at battle start. Check MaxFootprint, MaxSpeed, mesh array order, feature-state allocation and reinforcement append paths. Save formats reference textual IDs, not new numeric indices.
5. If a new UnitShape is approved, append its enum entry; update UnitOutline construction/vertices/footprint and rendering dispatch together. Inspect all shape-special cases, not only the preview.

### C. Aircraft are a separate registration path

Use the Aircraft array in DefaultUnitCatalog.tres, not the ground OrderedIds list. Reuse UnitDefinition for authoring but inspect AirSquadron's flight, dogfight, bomb registration, altitude, target eligibility and crash cleanup. AircraftMesh/AircraftIcon, CanTargetAircraft, CanBombGround and BombProjectile are existing fields. AirSquadronVisuals selects an authored mesh or existing fighter/bomber fallback; enhanced V-wing and bomber art/exhaust have ID-specific hooks. Do not assume a new aircraft automatically gets those effects, ground movement or ground recruitment. Verify aircraft placement, faction lists, AA targeting and applicable battle modes separately. Follow AIR1 and live runtime; planned features are not proof of implementation.

## 4. Physical size, movement and collision

- SizeMultiplier controls linear physical size, normally clamped to 0.5–3.0. UnitOutline.EffectiveSize and FromDefinition compile the footprint; Height and melee Reach scale with it. Ranged Reach is authored weapon range, not multiplied simply because a model grows. Mount positions/muzzle/casing/recoil distances use the compiled size. Shockwave base radius scales with size. Avoid applying the multiplier again in rendering.
- SizeTier is independent of the size multiplier. Runtime rounds to half-tier increments with a minimum of 1. Use existing tiers 1, 1.5, 2, 3, 4, 5 as references. A two-tier gap anchors a heavier unit against the lighter unit's shove (CanShove). Do not bypass tier-aware separation, yielding or trample.
- Collision follows the compiled outline/circular footprint, not every decorative bevel, gun or antenna. Keep the rendered hull grounded, preserve painter clearance, bridge width restrictions and navigation around obstacles. A visually dramatic extension may need explicit clearance/muzzle review.
- Reuse Speed, AccelerationSecondsOverride, TurnDegreesPerSecond and existing ground movement/terrain modifiers. Do not add a separate Update loop for each unit. Circular infantry may be visually oriented without changing its rotationally symmetric simulation.
- When resizing an existing unit, check old authored placements, dense formations, separation and turret alignment. Never change tier, damage or weapon range unless requested.

Read [navigation](../NAVIGATION/README.md), [simulation](../SIMULATION/README.md), [deployment](../DEPLOYMENT/README.md), and [UNIT1](UNIT1_SIZE_SCALING_PLAN.md) for context; use live implementation for current contracts.

## 5. Combat and weapon selection

| Desired behavior | Reuse |
| --- | --- |
| Ordinary melee | Style = Melee, Damage, Interval, Reach, MeleeFacingAngleDegrees; shared contact and facing checks |
| Anti-specific-unit melee | MeleeBonusTargetId + MeleeBonusMultiplier (Pikeman) |
| First strike against each enemy | FirstHitMultiplier, single-target melee only (Obliterator) |
| Cleave | CleaveEnabled, CleaveExtraReach, CleaveAngleDegrees, CleaveSecondaryFraction, CleaveMaxAdditionalTargets (Pentagon, Hexagon) |
| Periodic radial secondary attack | ShockwaveEveryAttacks, ShockwaveBaseRadius, ShockwaveDamage, ShockwaveOuterDamage, ShockwaveRespectsCover (Hexagon) |
| Charge/trample | TrampleEnabled and existing run-up, distance, efficiency, hit-cap and resistance fields (Cavalry) |
| Ordinary direct ranged | Style = Ranged, Projectile, Reach, MinimumFireRange, AimSpreadDegrees, ForestDeflectionChance (Archer/Marksman) |
| Indirect splash | ProjectileDefinition obstacle/arc/explosion settings and existing AreaDamage path (Light Artillery, rockets) |
| Beam | PrismBeamEnabled and charge/radius/max-hit/flash fields (Prism Lance); not a normal projectile |
| Buildings-only attack | AllowedTargets = Structures, PreferredTarget = Structures (Siege Ram) |
| Support shield | ShieldEmitterEnabled and ShieldRadius/Capacity/RegenerationPerSecond/DecayPerSecond (Mobile Generator) |
| Commander buff | RallyPulse, Commander registration and actual recipient list (Rally Commander; not currently a generic exported rally flag) |
| Ground anti-air | Existing Interceptor and AirSquadron integration; inspect ID-specific registration before extending |

TargetCategory flags are None=0, Units=1, Structures=2, Both=3; TargetPreference is a separate setting, not a restriction. Ground AllowedTargets is not an aircraft-target flag. Set structure detection range and actual firing range deliberately; check both unit and structure paths. Prism's compiled target mask is limited to units. PreferredTarget can be interpreted differently by the mode, so test the relevant shared combat path rather than assuming identical target priorities.

DamageType and ArmourClass resolve through the shared DamageMatrix; Armour is a fraction in 0–1. UnitDamagePercent and StructureDamagePercent use -1 to retain the damage-type matchup or a nonnegative explicit percentage for that target category. Keep existing shields, hit feedback, kill accounting and structure health paths authoritative. Do not manually subtract HP or duplicate projectile hits for new effects. Use existing movement padding and capped grid queries for area damage, and obey cover and minimum range.

### Weapon mounts: fixed, primary independent, secondary, nested

Read [combat guide](../COMBAT/README.md), RocketMount.cs, UnitCatalog.cs, [projectile guide](../PROJECTILES/README.md) and the live `../COMBAT/TURRETS/` sources before adding a mounted weapon. RocketMount is a historical name and supports non-rocket weapons.

- **Fixed alternating mounts:** TwinRocketLauncher.tres; existing launch path chooses the active mount and advances only on an accepted launch. Position, YawDegrees, ModelScale and projectile overrides are authored once.
- **Independent primary:** Tank's PrimaryCannon has IndependentAim and ReplacePrimaryWeapon. Requires ranged style and no Prism beam. A unit can have one primary replacement; it uses the normal target and attack cooldown, rather than adding a second primary shot. The hull keeps its movement facing. RotationSpeed, RotationArc, AlignmentTolerance and FireWhileMoving control aiming. Intersect mount and unit AllowedTargets. Negative override fields inherit the applicable compiled defaults; inspect CompileMount because secondary defaults differ.
- **Secondary independent weapons:** Dreadnaught uses independent mounts and its own bounded weapon state. Use existing support where applicable; do not assume every arbitrary ground unit automatically gets the Dreadnaught's secondary weapon scheduler. Inspect registration and state for the requested unit.
- **Nested roof weapon:** Tank has AttachToPrimaryTurret and IndependentAim with an explicit projectile; catalogue permits one roof weapon. Inspect RoofWeaponState and its registration/rendering. It has separate targeting/aim/cooldown and a 12-unit minigun range in the reviewed resource. Do not add arbitrary nested hierarchies or duplicate primary shots.
- **Model selection:** Integrated uses the unit's authored turret geometry; CompactCannon is the reusable procedural option for other vehicles. Set MuzzleOffset and CasingPortOffset in the correct local space, verify actual muzzle and receiver against the mesh, and preserve rotation/recoil alignment. The fixed mount convention runs from pivot toward local -Z. Respect shape-specific facing conventions and independent turret shader tags.

## 6. Visual authoring and optional feature checklist

The [visual guide](../../VISUALS/authoring.md) remains the authority for geometry and shaders. Prefer original, recognizable low-poly hard-surface silhouettes, layered armor, bevels, strong top/side contrast, gunmetal mechanisms and recognizable faction panels. Infantry and small units should retain strong faction identity; heavy vehicles can use more neutral metal (the updated Tank is the reference: gunmetal hull, faction glacis, roof and central skirt panels). Avoid exact percentage claims unless measured. Use the shared faction palette, not red/blue literals.

Use the same cached mesh builder for combat, placement preview and Entity Editor. Preserve existing enhanced/legacy switches when changing existing units. Unit armor encodes material kind and baked brightness in UVs; tower RGB masks are a different contract. Preserve animation tags, fracture/attack alpha packing, optional instance custom channels, and lit/unlit/reinforcement/shield-shell paths. Do not add a draw call or custom data channel just to recolor one plate.

Select only the features appropriate to the role. The following are reusable where supported; **not all are universal opt-ins**. Check the listed owner and defaults before adding an effect.

| Feature | Existing authoring / owner | Reference / caveat |
| --- | --- | --- |
| Hit flash, pulse, damage cracks, death fragments | shared HitFeedback/FX2, UnitVisualConfig | Preserve standard path; PulseOnAttack controls firing pulse, not a damage multiplier |
| Recoil | RangedRecoilEnabled/Distance/ReturnSeconds; mount BarrelRecoilEnabled/RecoilDistance/RecoilRecoverySeconds | Use accepted-shot feedback; Tank barrel independent of hull |
| Casings | EjectShellCasings, ShellCasingScale; mount override/port; ShellCasingEffects | Tank/Broadside; capped cosmetic pool, not new physics or ammo |
| Tread marks | LeavesTreadMarks; TreadMarks | Siege Ram/Tank; distance sampled, temporary, nonblocking |
| Blocking death wreck | LeavesNavigationWreck and SizeTier >= 3; WreckField | Hexagon; movement-only, same/lower tiers block, higher crush; shared 32-slot cap with husks |
| Projectile impact sparks | existing direct-hit path and pooled MeleeEffects | Actual body contact; no extra collision query |
| Shield ricochets | ProjectileDefinition.ShieldRicochetEnabled, existing shield contact/ricochet paths | Light bolts/minigun rounds; projectile-level flag, not a blanket unit flag; inspect current dome and individual shield rules |
| Muzzle smoke / launch trails | ProjectileDefinition.UseLaunchEffects, UseFlightSmoke; UnitVisualConfig.UseMuzzleSmoke/UseProjectileLaunchEffects | Use actual successful launches and shared pools |
| Ground glow | ProjectileDefinition.UseGroundGlow and TintByFaction | Laser bolts/minigun examples |
| Infantry gaits and attacks | UnitVisualConfig per-type toggles, FacetedSoldierMeshes and shared animation shaders | Existing robot animation tags; new shape may require explicit wiring |
| Vehicle exhaust / chassis motion | existing Tank/Siege Ram rendering hooks | Type-specific today; do not assume an exported universal vehicle-smoke flag |
| Aircraft exhaust / crash | AirSquadronVisuals, AircraftExhaust and crash aftermath | Fighter/bomber hooks; verify mesh anchors, no per-aircraft particle nodes |
| Shield shell | ShieldSystem and ShieldShellRenderer | Real recipients only, capped, faction tinted; no new gameplay |
| Rally pulse/marker | RallyPulse and ShockwaveRenderer | Existing active-recipient visual; no extra buff logic or scans |
| Piercing beam flash | PrismBeamEffects and shared beam glow | Bounded shot effects; not continuous per-unit lights |

If adding a property, define a clear default (usually disabled), expose it in the appropriate Entity Editor form/Inspector, compile or cache it at initialization where useful, and wire actual firing/movement/death events. A checkbox with no runtime consumer is not an implemented feature. Keep visual settings independent of damage and simulation RNG.

## 7. Editors, placement, recruitment and production

A unit should be usable in all **requested** workflows, not merely appear in a catalogue.

- Entity Editor: the ground definition list is catalogue-fed, with editable resource drafts and previews. Inspect RefreshCatalogue and the relevant field form (including weapon mounts). Ensure the new data is visible, saves, reloads, previews correctly, and validates. Aircraft may require a separate workflow; check rather than assume.
- Benchmark and Level Editor: inspect catalogue-fed selectors, painting/deployment, all factions, strength display, save/load, reinforcement staging and opening orders. Check any fixed roster choices, arrays and preview switches. Verify stable ID resolution and physical placement clearance.
- Campaign/Missions/Skirmishes: keep existing authored rosters and levels intact. For recruitment, the current AutoBattlerRecruitment reads ground Definitions and DefaultAutoBattlerRecruitment.tres Prices and InitiallyAvailable. Add a positive price only when intended; keep locked versus starting availability explicit. BattleRating is not price. Do not insert a unit into starting armies or unlock lists without authorization.
- Fortress Defense: FabricatorRecipes.tres maps stable IDs to Vector3i(batch count, Carbon cost, production seconds). Add a recipe only for units intended for Fabricator production; check release clearance, rally and queue behavior. Workers use separate Citadel production. Do not assume aircraft have a ground Fabricator path.
- Current integration gap to remember: catalogue registration does not imply an offer or recipe. Tank now has a provisional Fabricator recipe but no default recruitment price. Dreadnaught has a recruitment price but no Fabricator recipe. Missing Fabricator recipes are intentionally unavailable, not an error; explicitly malformed recipes still fail validation. This is a scope check, not permission to change those defaults.

Read [production guide](../PRODUCTION/README.md) and relevant mode docs. Search the live tree for the reference unit ID, index and shape to find hardcoded branches, not to blindly copy every occurrence.

## 8. Performance, lifecycle and validation

Follow CodePreferences and the shared simulation budgets. The goal is mass battles, not one highly detailed actor per unit.

- No per-soldier Godot nodes, rigid bodies, individual materials, lights, unbounded particles or per-frame mesh building. Use shared MultiMeshes, compact arrays, sparse state only for eligible units, lazy bounded effect pools and existing tick/acquisition budgets.
- Use shared spatial grid queries and local candidates, never all-pairs scans or new per-soldier path searches. Preserve movement/targeting cadence and route budgets. Keep cosmetic scatter independent of simulation RNG.
- State must initialize correctly on spawn, append on reinforcements, stop on death, and clear on reset/scene exit. Reuse existing allocation and compact-pool lifecycle; do not reset all existing units when reinforcements arrive.
- For visuals, validate preview/battle parity, all faction colors, grounded pose, real muzzle and recoil, casing port, shield shell size and damage feedback. Visual-only changes should not alter gameplay except where the owner explicitly requests physical size or collision.
- For gameplay, check target restrictions, damage/armour/shields, range/minimum range, movement/turning, blocked terrain, bridge clearance, crowd separation, structure attacks and relevant special abilities.
- For integration, check catalogue compile, placement/save/load, Entity Editor, reinforcements and only the authorized recruitment/production modes.
- AI validation: small source/diff review and at most one practical basic build for C# changes per CodePreferences. Owner does extensive Godot visual/gameplay/FPS testing. No claim of verified performance without measurement.

## 9. AI delivery checklist

- [ ] Read the mandatory guides and live nearest reference unit.
- [ ] Confirm role, scope, tier, proposed stats, target categories and modes.
- [ ] Author resource and append stable registration; preserve old IDs/indices.
- [ ] Reuse existing combat, targeting, movement and visual paths; add only missing approved behavior.
- [ ] Verify mesh/outline, muzzle, mounts, faction tint and appropriate opt-ins.
- [ ] Verify editor fields, placement, save/load and reinforcement lifecycle; recruitment/production if authorized.
- [ ] Update unit README, relevant feature docs and devlog; document any restrictions or unconnected features.
- [ ] Perform focused source/diff review and practical build if applicable.
- [ ] Make one focused commit, push as authorized without overwriting concurrent work, report SHA, affected files, choices, limitations and owner test checks.

### Example instruction to a future AI

> Read UNIT_IMPLEMENTATION_STANDARD.md, CodePreferences.md, root README, endgoal.md and VISUALS/authoring.md. Inspect the latest main source and closest existing unit. Implement the requested unit using existing authored resources, catalogue, combat/mount systems and cached meshes. Explain which existing opt-ins apply and enable only those appropriate to the requested design. Ensure Entity Editor and deployment support; add recruitment/production only within the authorized scope. Preserve all existing IDs, units and gameplay, keep simulation/visual budgets intact, update docs/devlog and push one focused commit. Report unverified behavior and owner validation steps.

