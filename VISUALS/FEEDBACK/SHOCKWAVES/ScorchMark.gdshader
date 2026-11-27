// Soft irregular soot with no texture lookup or light.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

varying vec2 mark_data;

void vertex() {
    mark_data = INSTANCE_CUSTOM.xy;
}

void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    float phase = mark_data.y * 6.283185;
    float radius = length(p + 0.07 * vec2(sin(p.y * 13.0 + phase), cos(p.x * 11.0 - phase)));
    float edge = 1.0 - smoothstep(0.20, 0.92, radius);
    float grain = fract(sin(dot(floor(UV * 37.0), vec2(12.9898, 78.233)) + phase) * 43758.5453);
    float soot = edge * mix(0.72, 1.0, grain);
    ALBEDO = mix(vec3(0.023, 0.019, 0.017), vec3(0.105, 0.080, 0.056), radius);
    ALPHA = soot * clamp(mark_data.x, 0.0, 1.0) * 0.74;
}
