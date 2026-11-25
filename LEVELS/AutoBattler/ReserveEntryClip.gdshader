// Reveal only the parts of each marching reinforcement inside the battlefield.
shader_type spatial;
render_mode unshaded;

#include "res://VISUALS/RENDERING/UnitArmor.gdshaderinc"

uniform float map_half_width;
varying vec2 battlefield_position;

void vertex() {
    battlefield_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
}

void fragment() {
    if (any(greaterThan(abs(battlefield_position), vec2(map_half_width)))) {
        discard;
    }
    ALBEDO = unit_armor_albedo(COLOR.rgb, UV);
    EMISSION = unit_armor_emission(COLOR.rgb, UV);
}
