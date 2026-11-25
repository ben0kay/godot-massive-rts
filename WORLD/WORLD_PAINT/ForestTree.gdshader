// Blend per-instance opacity so troops under a canopy stay visible.
shader_type spatial;
render_mode depth_prepass_alpha, ambient_light_disabled, shadows_disabled;

uniform vec4 tree_color : source_color;
uniform bool foliage = false;
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

void light() {
    float facing = max(dot(NORMAL, LIGHT), 0.0);
    vec3 sun_color = foliage ? ALBEDO * vec3(1.15, 1.85, 1.02) : ALBEDO;
    DIFFUSE_LIGHT += sun_color * LIGHT_COLOR * (0.72 / PI) * facing * ATTENUATION;
}
