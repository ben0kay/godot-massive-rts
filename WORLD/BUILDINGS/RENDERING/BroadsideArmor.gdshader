// Broadside's baked RGB mask selects neutral armor, faction paint or bright trim.
shader_type spatial;
#include "res://WORLD/BUILDINGS/RENDERING/TowerLighting.gdshaderinc"

#include "res://VISUALS/FEEDBACK/UNIT_HITS/SurfaceCracks.gdshaderinc"

varying vec3 armor_mask;
varying vec2 plate_point;
varying float plate_top;
varying flat float fracture_stage;
varying flat vec3 faction_tint;

void vertex() {
    armor_mask = COLOR.rgb;
    faction_tint = INSTANCE_CUSTOM.rgb;
    plate_point = VERTEX.xz * 2.0;
    plate_top = smoothstep(0.1, 0.65, NORMAL.y);
    fracture_stage = round(INSTANCE_CUSTOM.a * 3.0);
}

global uniform float battlefield_ambient_fill;

void fragment() {
    vec3 steel = vec3(0.12, 0.15, 0.19);
    vec3 paint = faction_tint;
    vec3 indicator = mix(paint, vec3(1.0), 0.30);
    vec3 steel_armor = steel * armor_mask.r;
    vec3 painted_armor = paint * armor_mask.g;
    if (fracture_stage >= 1.0 && plate_top > 0.0) {
        float crack = surface_crack_mask(plate_point, fracture_stage) * plate_top;
        float wear = 1.0 - crack * (0.56 + fracture_stage * 0.12);
        steel_armor *= wear;
        painted_armor *= wear;
    }
    ALBEDO = steel_armor + painted_armor;
    // Lift neutral steel in shade while keeping faction paint and trim distinct.
    float steel_fill = 0.78 * sqrt(battlefield_ambient_fill);
    EMISSION = steel_armor * steel_fill + painted_armor * 0.55 * battlefield_ambient_fill
        + (indicator + paint * 1.15) * armor_mask.b;
}
