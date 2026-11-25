// Modular steel panels and inset lights shared by walls and basin trim.
shader_type spatial;
render_mode unshaded, cull_disabled;

uniform bool basin_trim = false;
varying vec3 world_point;
varying vec3 world_normal;

void vertex() {
    world_point = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    world_normal = normalize(MODEL_NORMAL_MATRIX * NORMAL);
}

void fragment() {
    vec3 n = normalize(world_normal);
    bool top = n.y > 0.5;
    float run = abs(n.x) > 0.5 ? world_point.z : world_point.x;
    vec2 panel = top ? world_point.xz / 2.0 : vec2(run / 2.0, world_point.y / 2.0);
    if (basin_trim && !top) {
        panel.y = UV.y;
    }
    vec2 local = fract(panel);
    vec2 edge = min(local, 1.0 - local);
    float distance_to_edge = min(edge.x, edge.y);
    float aa = clamp(fwidth(distance_to_edge), 0.006, 0.05);
    float seam = 1.0 - smoothstep(0.025 - aa, 0.025 + aa, distance_to_edge);
    float bevel = smoothstep(0.025, 0.065, distance_to_edge) *
        (1.0 - smoothstep(0.068, 0.12, distance_to_edge));
    float lit_edge = step(edge.x, edge.y) * (1.0 - step(0.5, local.x)) +
        step(edge.y, edge.x) * (1.0 - step(0.5, local.y));
    vec3 steel = top ? vec3(0.20, 0.25, 0.33) : vec3(0.08, 0.12, 0.18);
    steel *= 0.75 + 0.25 * max(dot(n, normalize(vec3(-0.5, 0.7, 0.5))), 0.0);
    steel = steel * (1.0 - 0.59 * seam) + mix(vec3(-0.035), vec3(0.12, 0.15, 0.20), clamp(lit_edge, 0.0, 1.0)) * bevel;

    if (!top && (!basin_trim || UV.x > 0.5)) {
        vec2 center = basin_trim ? vec2(0.5, 0.79) : vec2(0.5, 0.65);
        vec2 size = basin_trim ? vec2(0.24, 0.05) : vec2(0.20, 0.035);
        vec2 offset = abs(local - center) - size;
        float distance_to_strip = max(offset.x, offset.y);
        float slot = 1.0 - smoothstep(0.022, 0.044, distance_to_strip);
        float lamp = 1.0 - smoothstep(-aa, aa, distance_to_strip);
        float halo = exp(-max(distance_to_strip, 0.0) * 15.0);
        vec3 tint = basin_trim ? vec3(0.08, 0.65, 1.0) : vec3(1.0, 0.43, 0.12);
        steel = mix(steel, vec3(0.018, 0.026, 0.04), slot * 0.9);
        steel += tint * halo * 0.38;
        steel = mix(steel, mix(tint, vec3(1.0), 0.70), lamp);
        EMISSION = tint * lamp * 1.5;
    }
    ALBEDO = steel;
}
