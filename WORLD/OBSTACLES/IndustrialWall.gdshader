// Modular steel panels and inset lights shared by walls and basin trim.
shader_type spatial;
render_mode unshaded, cull_disabled;

uniform bool basin_trim = false;
varying vec3 world_point;
varying vec3 world_normal;
varying float wall_height_from_center;

void vertex() {
    world_point = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    wall_height_from_center = world_point.y - MODEL_MATRIX[3].y;
    world_normal = normalize(MODEL_NORMAL_MATRIX * NORMAL);
}

// Integer hash keeps a panel's light stable while neighbouring cells are repainted.
float panel_random(ivec3 cell) {
    uint hash = uint(cell.x) * 1597334677u ^ uint(cell.y) * 3812015801u ^ uint(cell.z) * 2798796415u;
    hash = (hash ^ (hash >> 16u)) * 2246822519u;
    return float(hash & 65535u) / 65536.0;
}

void fragment() {
    vec3 n = normalize(world_normal);
    bool top = n.y > 0.5;
    float run = abs(n.x) > 0.5 ? world_point.z : world_point.x;
    vec2 panel = top ? world_point.xz / 2.0 : vec2(run / 2.0, (wall_height_from_center + 1.0) / 2.0);
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
    vec2 tile = floor((world_point.xz - n.xz * 0.05) / 2.0);
    float panel_seed = panel_random(ivec3(int(tile.x), int(tile.y), 7));
    vec3 steel = top ? vec3(0.15, 0.19, 0.26) : vec3(0.047, 0.067, 0.104);
    steel *= mix(0.84, 1.10, panel_seed);
    steel *= 0.75 + 0.25 * max(dot(n, normalize(vec3(-0.5, 0.7, 0.5))), 0.0);
    steel = steel * (1.0 - 0.59 * seam) + mix(vec3(-0.035), vec3(0.12, 0.15, 0.20), clamp(lit_edge, 0.0, 1.0)) * bevel;

    float light_row = 1.0 - step(0.5, abs(floor(panel.y)));
    if (!top && (basin_trim ? UV.x > 0.5 : panel_seed < 0.10)) {
        vec2 center = basin_trim ? vec2(0.5, 0.79) : vec2(0.5, 0.65);
        vec2 size = basin_trim ? vec2(0.24, 0.05) : vec2(0.20, 0.035);
        vec2 offset = abs(local - center) - size;
        float distance_to_strip = max(offset.x, offset.y);
        float lamp_visibility = basin_trim ? 1.0 : light_row;
        float slot = 1.0 - smoothstep(0.022, 0.044, distance_to_strip);
        float lamp = 1.0 - smoothstep(-aa, aa, distance_to_strip);
        float halo = exp(-max(distance_to_strip, 0.0) * 10.0);
        vec3 tint = basin_trim ? vec3(0.08, 0.65, 1.0) : vec3(1.0, 0.43, 0.12);
        steel = mix(steel, vec3(0.018, 0.026, 0.04), slot * lamp_visibility * 0.9);
        steel += tint * halo * lamp_visibility * 0.49;
        steel = mix(steel, mix(tint, vec3(1.0), 0.70), lamp * lamp_visibility);
        EMISSION = tint * lamp * lamp_visibility * 2.2;
    }
    ALBEDO = steel;
}
