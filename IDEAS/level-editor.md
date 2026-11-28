# Level editor

Tools for faster, more flexible terrain and scenario authoring. Read [current editor documentation](../UI/Editor/LevelEditor.md) before implementation.

## Already source implemented
Manual undergrowth painter (Grassland/Rocky/Marsh with density and scale), visible Undo/Redo, procedural terrain generator baseline, road and marsh paint, rubble, tiered/organic elevation, cliffs, bridges, geothermal vents, trenches and Light Barricades. Retain these systems; investigate specific shortcomings only when requested. Owner visual and gameplay acceptance is separate from implementation.

## Terrain surface painter
- **Proposal** — Cosmetic-only terrain material brush with soft blended edges, adjustable size and opacity; initial palette includes volcanic rock, dry earth, moss, concrete, snow and scorched earth. Preserve gameplay masks, height and obstacle data.
- **Goal** — Make different regions and levels visually distinct without creating new terrain mechanics.
- **Notes** — Use compact masks/chunked textures and shared shader/materials; avoid per-cell nodes. Reuse brush, rectangle fill, erase, undo and persistence.

## Automatic undergrowth
- **Proposal** — Extend UNDERGROWTH1 with optional deterministic decorative scatter around existing terrain: ferns/bushes at forest fringes, pebbles/scrub at cliff bases, reeds at riverbanks, sparse roadside weeds, stones near rock outcrops and wetland plants in marsh.
- **Goal** — Quickly decorate authored and procedurally generated maps.
- **Notes** — Preserve manual stamps, preview before applying, support a single undoable operation, skip incompatible surfaces and cap density. No gameplay effects or per-unit checks.

## Productivity and placement tools

| Idea | Proposal |
| --- | --- |
| Mirror painter | Mirror terrain, scenery, structures and optionally faction-swapped deployments across an adjustable axis. Preview and commit one operation. |
| Region copy/paste | Select a rectangular region; copy selected layers, rotate/mirror where supported, validate bounds and placement before applying. |
| Prefab library | Save reusable fortress layouts, outposts, artillery batteries, defensive lines and formations as editable authored data with thumbnail and anchor. Stamp, rotate and validate; no new runtime object system. |
| Polygon fill | Fill arbitrary closed outlines for compatible terrain paints; validate self-intersection and reuse brush data. |
| Line/spline painter | Continuous straight or winding strokes with width for roads, water and barriers; extend existing line tools only where missing. |
| Elevation smoothing | Reduce local height discontinuities without intentionally raising/lowering the entire patch; preserve protected surfaces and support undo. |
| Height eyedropper | Sample actual current terrain elevation to set the existing height tool. |
| Scatter brush | Deterministic varied rotation/scale/spacing of supported scenery objects, bounded by existing budgets. |
| Layer visibility | Toggle display of units, defenses, terrain overlays or scenery independently in editor; don't change saved gameplay. |
| Resize anchor | Choose the direction a map expands/shrinks; show conflicts and preserve or explicitly handle out-of-bounds content. |
| Measurement tool | Measure world distance, bridge width and map dimensions. |
| Range preview | Show attack/detection/ability radii of hovered placement, when not already supported. |
| Navigation overlay | Display representative tier clearance, blocked cells and accessible regions; no thousands of path queries. |
| Randomize selection | Adjust selected scenery rotations/scales within safe ranges; one undoable action. |
| Undo history panel | Optional list of meaningful operations; reuse existing history rather than storing duplicate full maps. |
| Map validation | Report objective/roster validity, inaccessible zones, narrow crossings, invalid placements and budget limits; use bounded connectivity checks. |
| Test as player | Launch the exact saved Skirmish roster and rules rather than unrestricted editor testing; keep existing playtest. |
| Level thumbnails | Generate cached overhead thumbnails for level picker and previews using existing terrain data. |

## Environment controls
Add a level-local Environment section with lighting, ambient color, fog and weather presets. Preview before saving, retain sensible defaults for older maps. See [visual updates](visual-updates.md#terrain-materials-and-atmosphere).

## Scenario authoring
Add optional event triggers, guard zones, patrols, starting conditions and objective metadata only after agreement on scenario behavior. See [maps and modes](maps-and-modes.md#scenario-events-and-objectives). Reuse current level rules and reserve authoring.

## Generator follow-up priorities
- Add supported biomes and layouts (Fortress Basin, Twin Plateaus, later Archipelago); use existing terrain data and safe layout constraints.
- Automatic bridge placement with valid bank connection, sufficient width for relevant tiers and bounded connectivity validation.
- Optional road routing and automatic undergrowth around supported terrain features.
- Better candidate warnings, protected deployment/build zones and reproducible preview; do not change saved maps by regenerating them automatically.

## Procedural map generator

### Original MAPGEN1 design brief (retained for reference)

**Repository:** ben0kay/godot-massive-rts · **Branch:** main  
**Status:** Initial generator source implemented; remaining items are future ideas, not implementation authorization. Current Level Editor supports seeded Temperate maps (Open Plains, River Divide, Mountain Pass and Cliffs), 192–1024 map sizes, elevation/scenery settings, preview, forest layouts, river width/meander, manual bridge placement, and editable generated data with undo. Read [current editor documentation](../UI/Editor/LevelEditor.md) and live code before extending. The original proposed phases below are historical; do not repeat completed work.

#### 1. Objective

Create entire editable battlefields from a seed and a small set of controls in the existing in-game Level Editor. The owner should be able to generate a map, immediately inspect it, manually raise/lower/flatten terrain, add or remove cliffs, forests, rocks, rivers and bridges, paint armies/defenses, and save it using the ordinary level workflow.

Large open spaces for thousands of troops are the priority; geography should provide meaningful routes and chokepoints rather than blanket noise or obstacle clutter. Preserve the game's original isometric geometric sci-fi visual style and current ground material direction. The generator is an authoring tool first, not a new game mode or a replacement terrain engine.

#### 2. Read first and inspect live source

- Root README.md, CodePreferences.md, endgoal.md and any applicable AGENTS.md.
- WORLD/TERRAIN/README.md and WORLD/TERRAIN/TERR1_IMPLEMENTATION_PLAN.md (older design notes: verify actual implementation rather than importing stale assumptions, including simulation frequency and mode names).
- UI/Editor/LevelEditor.Elevation.cs, UI/Editor/SAVING/LevelEditorSave.cs, WORLD/TERRAIN/AUTHORING/CommandTerrain.cs, WORLD/TERRAIN/SHARED/TerrainHeightField.cs, WORLD/TERRAIN/RENDERING/TerrainRenderer.cs.
- WORLD/WORLD_PAINT/ (WorldPaint, WorldPaintData, WorldPaintMeshBuilder, BridgeDeck and current placement/merge code), WORLD/OBSTACLES/README.md, GAMEPLAY/NAVIGATION/README.md and live routing/clearance code.
- Existing map size, deployment rules, editor history/undo, save paths and current mode-specific requirements. See VISUALS/authoring.md where applicable.

Current source already supports elevation brush offsets and undoable strokes, chunked terrain sampling (currently 32-cell chunks, 2.1 world-unit sample spacing), terrain surface grounding and painted rock/water/forest cells with bounded compilation. LevelEditorSave validates terrain and world-paint limits. Reuse these owners; inspect current limits and data contracts before writing code.

#### 3. Editor UX

Add a Generate Terrain / Generate Map panel within the Level Editor, ideally using the existing UI factory/theme.

| Control | Proposed behavior |
| --- | --- |
| Seed | Integer seed, editable; Randomize generates a new seed |
| Map size | Use existing per-level size limits and validation; examples 250, 500, 1000, 2000, 4000 only if supported |
| Layout preset | Open Plains, River Divide, Mountain Pass, Fortress Basin, Twin Plateaus, Archipelago (later) |
| Biome | Temperate first; Desert, Arctic, Volcanic and Marshland later |
| Elevation intensity | Controls broad relief and hills, not noisy microterrain |
| Forest density | Controls clustered tree coverage |
| Rock/cliff density | Controls broad formations and scattered obstacles, subject to actual caps |
| Rivers | Count/coverage, width and meander; preset may constrain these |
| Auto bridges | Place valid crossings with configurable width/quantity as supported |
| Protected flat areas | Preserve clear deployment and/or build zones and base approaches |

Provide a useful overhead preview and explicit Generate/Apply action. Changing sliders must not rebuild a huge map each frame; debounce a lightweight preview or regenerate only on request. Preview should be derived from the candidate data, not a misleading independent random layout. Show actual map size, seed, warnings, generation failures and optional counts.

Generate into temporary data, validate, then commit as one logical undoable terrain edit if the existing history system supports it. Confirm replacing already-authored terrain/scenery. Default to preserving placed troops, defenses, structures, objectives, deployments and reinforcements; reject conflicts or require explicit owner confirmation. Never silently erase a level, overwrite a saved map, or resize it unexpectedly. Reuse ordinary save/reload and level identifiers.

#### 4. Generation pipeline

1. Normalize settings and map bounds using existing level units/grid coordinates. Use a local deterministic seeded PRNG/noise source; separate feature streams for elevation, water, scenery and bridges where practical.
2. Choose layout anchors: deployment zones, objective/build areas, major routes, river corridors and optional plateau locations. Keep enough genuinely flat open space for mass formations and large footprints. Respect existing authored protected regions if retaining content.
3. Generate broad low-frequency height variation with smooth slopes and optionally flat plateaus. Blend protected areas to their intended level without steep rings. Produce the same editable elevation/terrain representation used by the current brushes; preserve renderer/grounding agreement.
4. Generate rivers/lakes in coherent continuous shapes, using existing water cells and their movement-only blocking semantics. Prefer downhill flow or plausible valley corridors; ensure water/terrain visual elevations make sense. Do not simulate hydrology during battle.
5. Place rock/cliff formations and clustered forest patches with spacing from water, bridges, deployment zones and structure footprints. Use existing world-paint cells and supported cliff outline data; avoid thousands of tiny isolated blockers, excessive rectangle counts or invalid polygons.
6. For river presets, choose a small number of meaningful crossings, validate bank connections and legal bridge width using existing bridge data/clearance rules. Include an adequate route for relevant heavy units, not just infantry.
7. Compile a candidate through existing terrain/obstacle pipelines and perform bounded connectivity checks. Fix/reject unusable candidates; report why rather than retrying indefinitely. Keep deterministic retry limits and seed behavior.
8. Apply candidate authored data through the current editor, refresh affected previews/geometry once, and save normally only when requested.

#### 5. Layout presets and playability

| Preset | Intended layout |
| --- | --- |
| Open Plains | Broad battle space, gentle relief, optional sparse scenery; good for enormous armies |
| River Divide | River separates two sides; multiple appropriately sized crossings and clear approaches |
| Mountain Pass | Wide valley or pass between cliffs, room to deploy and route large units |
| Fortress Basin | Protected flat fortress/build area with defensible slopes and several valid approaches |
| Twin Plateaus | Two raised positions with gradual ramps and a central valley |
| Archipelago (later) | Islands linked by bridges; requires robust connectivity and width validation |

For Clash, connect opposing legal deployment areas for relevant ground footprints. For Offense/Defense, preserve objective access and legal Citadel/fortress placements. For Fortress Defense, validate buildable space, resources if applicable, and routes from authored wave starts. Use the current mode rules, including Missions and Skirmishes, rather than adding another menu taxonomy. Aesthetic symmetry is optional; usable routes and sufficient space are mandatory. Reject spawn areas on water/cliffs or inside obstacles and flag narrow or isolated routes.

Do not run thousands of individual A* requests. Validate a bounded coarse connectivity/clearance representation for representative footprint classes, then use existing precise checks where needed. This does not prove every combat route succeeds; owner tests remain necessary.

#### 6. Biomes and later terrain mechanics

- Temperate: subdued ground colors, gently rolling land, forests, water and rock clusters.
- Desert: dry rocky ground, broad dunes/valleys, sparse vegetation and dry channels.
- Arctic: snowy ground, frozen surfaces and icy lakes.
- Volcanic: dark basalt/rock, lava channels and glowing hot-ground patches.
- Marshland: wet muddy ground, shallow pools and clustered vegetation.

Ice traction and periodic lava damage remain proposals; mass-dependent marsh slowdown is source implemented. See [gameplay-mechanics.md](gameplay-mechanics.md). First generator version should use existing supported surfaces, obstacles and materials. Do not represent unsupported mechanics as working; introduce new terrain surface masks, damage/movement policies and weighted routing only under separate approval. If a biome cannot be represented safely by existing systems, defer it or label its initial version cosmetic.

#### 7. Architecture and performance

- Keep the generator and typed settings with terrain/world authoring (e.g. WORLD/TERRAIN/PROCEDURAL/ if justified); UI belongs with the Level Editor. Do not put generation in BenchmarkArena or create per-mode generators.
- One shared authoritative heightfield, world-paint/obstacle compiler, terrain renderer, surface queries and navigation. Output existing editable elevation offsets/cells, WorldPaintData and supported bridge/cliff/level resources. No parallel map format or duplicate gameplay path.
- Generation occurs on explicit editor action, never per simulation tick, soldier, or render frame. Use bounded chunks/arrays; validate sample/cell/rectangle and memory budgets before allocation. For large maps, keep sparse flat data where possible; avoid dense world-sized meshes and excessive scattered blockers. Reuse shared/batched meshes and materials.
- If responsiveness requires background compute, use pure managed data with cancellation/revision checks; Godot scene/resource/render mutations remain on the allowed thread. Do not introduce asynchronous complexity without need.
- Save the actual authored/generated map data as authoritative so later edits are preserved and older saves are stable. Optionally persist seed, settings and generator version as metadata; do not regenerate saved maps from a seed on each battle load or change them when algorithms change.
- Determinism: same generator version + normalized settings + seed + preserved inputs should produce the same height/paint/bridge data, independent of frame timing. Stable ordering and bounded retries. Do not use global random state.
- No extra battle overhead simply because a map was generated. Keep existing fixed-step timing, targeting, projectile, movement/separation and air/ground contracts intact. Existing limits must not be raised blindly.

#### 8. Proposed delivery phases

| Phase | Scope |
| --- | --- |
| MAPGEN1-A | Editor panel, seed/settings, temporary candidate, Open Plains/Temperate with low-frequency elevation and flat deployment/build masks; normal edit/save/reload; undo/replacement safeguards |
| MAPGEN1-B | Clustered forests, rocks and coherent rivers; River Divide and Mountain Pass; automatic legal bridges, bounded connectivity and footprint checks |
| MAPGEN1-C | Additional layouts and biome materials, richer cliff/plateau generation, editor preview polish; extend only supported terrain features |
| MAPGEN1-D | Optional separately approved ice/lava/marsh gameplay and future strategic territory integration |

The phases above describe the original design sequence, not the current implementation status. Inspect existing capabilities and scope only the requested remaining work; do not expand into campaign systems or new movement rules automatically. A future strategic Campaign may store a territory's seed, generated/authored map, biome and persistent fortress/resource state, but that is not needed for editor generation.

#### 9. Owner acceptance and validation

- Same seed/settings reproduce the candidate; a different seed gives meaningful variation.
- Existing authored levels load unchanged. Generation does not silently delete armies, structures or objectives; undo/replacement and failed candidate behavior are safe.
- Generated maps can be raised/lowered, flattened, painted, adjusted and saved/reloaded through current editor tools; preview and runtime match.
- Test open terrain, slopes, forests/rocks, rivers/bridges and terrain grounding with different footprint tiers. Confirm armies can reach relevant opponents/objectives and projectile/LOS behavior remains consistent.
- Check each applicable battle mode and map size supported by the current editor. Compare generated vs equivalently hand-painted maps with the same roster and count before making performance claims.
- Follow CodePreferences: brief source/diff review, one practical dotnet build for C# changes if available, owner handles extended Godot/FPS testing. No unmeasured performance promises.

#### 10. Codex handoff

When authorized, read the referenced documents and inspect current editor/terrain/paint/save/navigation code. Report any conflicts with the proposal before broad changes. Implement the smallest agreed phase using existing data contracts and one focused commit, update relevant feature documentation and append the required devlog entry for actual source implementation. Report exact files, commit, checks and outstanding owner tests.
