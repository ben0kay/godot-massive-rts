// Very faint faction-tinted influence shell, not a physical shield or projectile barrier.
shader_type spatial;
render_mode unshaded, cull_back, depth_draw_never, shadows_disabled;

uniform float center_opacity : hint_range(0.0, 0.15) = 0.012;
uniform float rim_opacity : hint_range(0.0, 0.35) = 0.12;
varying float local_height;

void vertex() {
    local_height = VERTEX.y;
}

void fragment() {
    float grazing = 1.0 - clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0);
    float rim = smoothstep(0.60, 0.98, grazing);
    float base_edge = (1.0 - smoothstep(0.0, 0.018, local_height)) * 0.035;
    ALBEDO = mix(COLOR.rgb, vec3(1.0), 0.16 + rim * 0.14);
    // Slow upward-moving faction-tinted bands; the center stays nearly clear.
    float wave = pow(0.5 + 0.5 * cos(6.283185 * (local_height * 3.0 - TIME * 0.32)), 12.0);
    float band = wave * 0.027 * smoothstep(0.06, 0.25, local_height) * (1.0 - smoothstep(0.84, 1.0, local_height));
    ALPHA = max(base_edge, mix(center_opacity, rim_opacity, rim)) + band;
}
