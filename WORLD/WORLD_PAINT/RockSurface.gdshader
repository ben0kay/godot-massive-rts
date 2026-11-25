shader_type spatial;
render_mode ambient_light_disabled, shadows_disabled;

#include "res://VISUALS/RENDERING/SceneryLighting.gdshaderinc"

void fragment() {
    ALBEDO = COLOR.rgb;
    EMISSION = ALBEDO * 0.58;
}
