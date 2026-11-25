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
    ALPHA = max(base_edge, mix(center_opacity, rim_opacity, rim));
}
