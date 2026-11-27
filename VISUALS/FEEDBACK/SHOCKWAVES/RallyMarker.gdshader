// Small camera-facing faction chevron, driven by the real rally recipient list.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled, skip_vertex_transform;
varying flat vec3 tint;
varying flat float pulse;
varying flat float life;
void vertex() {
    tint = COLOR.rgb;
    life = INSTANCE_CUSTOM.x;
    pulse = 0.5 + 0.5 * sin(TIME * 5.0 + INSTANCE_CUSTOM.y * 6.283185);
    float size = length(MODELVIEW_MATRIX[0].xyz) * mix(0.90, 1.08, pulse);
    VERTEX = MODELVIEW_MATRIX[3].xyz + vec3((UV - 0.5) * size, 0.0);
}
void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    float shape = abs(p.y - (0.62 - abs(p.x) * 0.85));
    float edge = 1.0 - smoothstep(0.55, 0.83, abs(p.x));
    float core = (1.0 - smoothstep(0.07, 0.16, shape)) * edge;
    float halo = (1.0 - smoothstep(0.12, 0.36, shape)) * edge;
    ALBEDO = mix(tint, vec3(1.0), 0.40 + core * 0.22);
    ALPHA = (core * 0.76 + halo * 0.16) * mix(0.70, 1.0, pulse) * smoothstep(0.0, 0.12, life);
}
