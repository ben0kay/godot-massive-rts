# Battlefield visual plan

For faction-specific unit, defense and structure appearance, see [Faction Visual Authoring Guide](authoring.md). This plan covers the wider battlefield, lighting and scenery.

Aim for a dark industrial battlefield with bright, readable faction colors, satin highlights on upper bevels, and clear silhouettes at battle zoom. Apply the art direction through reusable level settings. Any authored level can serve as a before-and-after comparison with the same army, camera and map layout.

## 1. Lighting and unit finish

The comparison controls are in **Benchmark → Display and controls → Battlefield lighting**. Real-shadow mode exposes **Shadow distance**, defaulting to 1,800 world units to cover the elevated orthographic camera. Use the performance overlay’s **Viewport GPU** reading for like-for-like comparisons. In-game visual and performance validation is pending.

Start with one shared directional light and a restrained top-face and bevel highlight. Adapt the unshaded unit materials and fracture shader to support the lit mode while preserving faction tint, hit flashes and fracture detail. Keep the current look and fake undershadows as the default while evaluating the experiment. Keep instanced unit batches and use shared shader materials without individual lights on soldiers.

| Benchmark mode | Unit lighting | Shadows |
|---|---|---|
| Current | Existing unshaded materials | Fake undershadows |
| Lit | Directional light with soft satin highlights | Fake undershadows |
| Lit + Real Shadows | Same directional lighting | Real-time shadows with fake undershadows disabled |

For the third mode, enable casting on the unit render batches and use a shadow-receiving ground material. Compare the same running battle and camera at 10,000 and 25,000 total soldiers on the GTX 1080. Record GPU frame time, total frame time, battle zoom and shadow distance. Try a shorter shadow distance to see whether it retains useful shadows at normal play zoom. Check that colors, hit flashes and cracks remain legible in all modes before promoting any mode to a default.

## 2. Base terrain and future painting

The base terrain chooser is implemented. Painting ground materials is a later pass.

Add a **Base Terrain** choice to new-level creation, defaulting to **Industrial**. Save the choice with the level and share it between the Level Editor, its playtest and the actual battle. Use the existing [Steel Slate material](../WORLD/TERRAIN/RENDERING/SteelSlate.tres) as the starting point for dark blue-grey plating with broad panel seams and quiet surface wear.

Later, allow painting ground materials over the base terrain, such as dirt or grass over industrial plating. Ground paint is a visual layer. Elevation, water and obstacle tools continue to define the terrain geometry and how units traverse it.

## 3. Industrial walls

The wall style and closed-loop fill are implemented. In-game validation is pending.

Rename the solid obstacle authoring tool **Industrial Walls**. Open runs look like segmented barriers with beveled caps and restrained amber accents. Closed loops stay hollow, and only painted wall cells block movement and line of sight. Rectangle Fill paints solid blocks for flat-topped structures. Use the same resolved geometry in the editor preview and battle.

## 4. Painted scenery

The water edge, boulder and pine treatments are implemented. In-game validation is pending.

- **Water:** Give painted shores an engineered edge with a narrow steel retaining lip and a restrained cyan highlight. Keep bridge openings clear and preserve existing water boundaries.
- **Rocks:** Replace the light pillars with darker, irregular faceted boulders. Use a few broad silhouettes with small rubble around the base and retain the painted obstacle footprints.
- **Trees:** Use low-poly pines with tapered, layered crowns, subtle green variation and varied heights. Keep trees batched and readable over a muted forest floor at battle zoom.

## Later

Revisit projectile trails after the ground, scenery and unit finish are settled. Keep material tuning in `.tres`, shader code in `.gdshader`, and introduce a `.gdshaderinc` when multiple shaders share the same calculation.
