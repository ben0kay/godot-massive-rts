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
    EMISSION = ALBEDO * 0.58 * battlefield_ambient_fill;
    ALPHA = opacity;
}
