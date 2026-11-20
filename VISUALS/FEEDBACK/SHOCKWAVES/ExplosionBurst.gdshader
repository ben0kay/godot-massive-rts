// Draws an expanding, short-lived flash and uneven sparks on a camera-facing quad.
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, shadows_disabled, skip_vertex_transform;

varying flat float progress;
varying flat float seed;
varying flat vec4 faction_color;

void vertex() {
    progress = INSTANCE_CUSTOM.x;
    seed = INSTANCE_CUSTOM.y;
    faction_color = COLOR;
    float radius = length(MODELVIEW_MATRIX[0].xyz);
    float expansion = mix(0.30, 1.0, 1.0 - pow(1.0 - progress, 3.0));
    VERTEX = MODELVIEW_MATRIX[3].xyz + vec3((UV - 0.5) * 2.0 * radius * expansion, 0.0);
}

void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    float distance_from_center = length(p);
    float angle = atan(p.y, p.x + 0.00001) + seed * 6.283185;
    float spoke = pow(0.5 + 0.5 * cos(angle * 9.0), 13.0);
    float reach = 0.57 + 0.28 * (0.5 + 0.5 * sin(angle * 5.0 + 2.0));
    float sparks = spoke * (1.0 - smoothstep(reach * 0.63, reach, distance_from_center))
        * smoothstep(0.07, 0.29, distance_from_center);
    float core = 1.0 - smoothstep(0.02, mix(0.38, 0.17, progress), distance_from_center);
    float halo = (1.0 - smoothstep(0.04, 0.56, distance_from_center)) * 0.38;
    vec3 warm = mix(vec3(1.0, 0.28, 0.045), vec3(1.0, 0.94, 0.65), core);
    vec3 tinted_edge = mix(vec3(1.0, 0.89, 0.76), faction_color.rgb, 0.55);
    vec3 tinted_core = mix(vec3(1.0, 0.98, 0.93), faction_color.rgb, 0.19);
    ALBEDO = mix(warm, mix(tinted_edge, tinted_core, core), faction_color.a);
    ALPHA = max(core, sparks * 0.85 + halo) * (1.0 - progress) * (1.0 - progress);
}
