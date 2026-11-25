// Broadside's baked RGB mask selects neutral armor, faction paint or bright trim.
shader_type spatial;
#include "res://WORLD/BUILDINGS/RENDERING/TowerLighting.gdshaderinc"

varying vec3 armor_mask;
varying flat vec3 faction_tint;

void vertex() {
    armor_mask = COLOR.rgb;
    faction_tint = INSTANCE_CUSTOM.rgb;
}

global uniform float battlefield_ambient_fill;

void fragment() {
    vec3 steel = vec3(0.12, 0.15, 0.19);
    vec3 paint = faction_tint;
    vec3 indicator = mix(paint, vec3(1.0), 0.30);
    vec3 steel_armor = steel * armor_mask.r;
    vec3 painted_armor = paint * armor_mask.g;
    ALBEDO = steel_armor + painted_armor;
    // Lift neutral steel in shade while keeping faction paint and trim distinct.
    float steel_fill = 0.78 * sqrt(battlefield_ambient_fill);
    EMISSION = steel_armor * steel_fill + painted_armor * 0.55 * battlefield_ambient_fill
        + (indicator + paint * 1.15) * armor_mask.b;
}
