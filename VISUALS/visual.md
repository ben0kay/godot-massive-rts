# Battlefield visual plan

Aim for a dark industrial battlefield with bright, readable faction colors, satin highlights on upper bevels, and clear silhouettes at battle zoom. Use Level 009 for before-and-after comparisons with the same army, camera and map layout.

## 1. Lighting and unit finish

Start with one shared directional light and a restrained top-face and bevel highlight. Adapt the unshaded unit materials and fracture shader to support the lit mode while preserving faction tint, hit flashes and fracture detail. Keep the current look and fake undershadows as the default while evaluating the experiment. Keep instanced unit batches and use shared shader materials without individual lights on soldiers.

| Benchmark mode | Unit lighting | Shadows |
|---|---|---|
| Current | Existing unshaded materials | Fake undershadows |
| Lit | Directional light with soft satin highlights | Fake undershadows |
| Lit + Real Shadows | Same directional lighting | Real-time shadows with fake undershadows disabled |

For the third mode, enable casting on the unit render batches and use a shadow-receiving ground material. Compare the same running battle and camera at 10,000 and 25,000 total soldiers on the GTX 1080. Record GPU frame time, total frame time, battle zoom and shadow distance. Try a shorter shadow distance to see whether it retains useful shadows at normal play zoom. Check that colors, hit flashes and cracks remain legible in all modes before promoting any mode to a default.

## 2. Ground and scenery

Tune the existing [Steel Slate material](../WORLD/TERRAIN/RENDERING/SteelSlate.tres) toward dark blue-grey industrial plating with broader panel seams and quiet surface wear. Test one section of Level 009 at battle zoom before applying the look widely. Give painted rocks darker, varied faceted silhouettes and forests more varied pine shapes over a muted floor. Keep scenery previews batched and preserve the painted navigation footprints.

## 3. Industrial obstacle walls

Give existing obstacle walls segmented panels, beveled caps and small amber accents. Match their movement and sight-blocking footprints. Reuse the wall style in editor previews and battle views. Painted natural rocks remain available for natural maps.

## Later

Revisit projectile trails after the ground, scenery and unit finish are settled. Keep material tuning in `.tres`, shader code in `.gdshader`, and introduce a `.gdshaderinc` only when multiple shaders share the same calculation.
