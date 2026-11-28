// Cosmetic faction-colored ground illumination, no real light or shield radius.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;

void fragment() {
    float distance_from_center = length(UV * 2.0 - 1.0);
    float falloff = 1.0 - smoothstep(0.28, 1.0, distance_from_center);
    // Match the shared reactor's gentle brightness pulse.
    float pulse = 0.90 + 0.10 * sin(TIME * 3.1);
    ALBEDO = mix(COLOR.rgb, vec3(1.0), 0.18);
    ALPHA = COLOR.a * falloff * pulse;
}
