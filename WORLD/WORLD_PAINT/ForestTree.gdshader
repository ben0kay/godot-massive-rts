// Blend per-instance opacity so troops under a canopy stay visible.
shader_type spatial;
render_mode depth_prepass_alpha, ambient_light_disabled, shadows_disabled;

#include "res://VISUALS/RENDERING/SceneryLighting.gdshaderinc"
uniform vec4 tree_color : source_color;
varying float opacity;

void vertex() {
    opacity = COLOR.a;
}

global uniform float battlefield_ambient_fill;

void fragment() {
    ALBEDO = tree_color.rgb * COLOR.rgb;
    // Keep foliage legible in shade when the battlefield fill is lowered.
    float foliage_fill = 0.75 * sqrt(battlefield_ambient_fill);
    EMISSION = ALBEDO * foliage_fill;
    ALPHA = opacity;
}
