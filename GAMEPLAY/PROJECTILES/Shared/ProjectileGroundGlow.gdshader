shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;

// Default retains the existing travelling projectile glow strength.
uniform float intensity : hint_range(0.0, 1.0) = 0.20;
varying vec4 glow_tint;

void vertex() {
    glow_tint = COLOR;
}

void fragment() {
    vec2 centered = UV * 2.0 - 1.0;
    float fade = 1.0 - smoothstep(0.04, 1.0, length(centered));
    ALBEDO = glow_tint.rgb;
    ALPHA = fade * fade * glow_tint.a * intensity;
}
