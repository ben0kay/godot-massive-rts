shader_type spatial;
render_mode unshaded, cull_disabled;

#include "res://VISUALS/RENDERING/UnitArmor.gdshaderinc"

varying vec3 faction_tint;
void vertex() {
    faction_tint = COLOR.rgb;
}
void fragment() {
    ALBEDO = unit_armor_albedo(faction_tint, UV);
    EMISSION = unit_armor_emission(faction_tint, UV);
}
