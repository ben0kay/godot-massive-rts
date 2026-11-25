// Draws modular steel panels and amber marker strips on existing collision boxes.
shader_type spatial;
render_mode unshaded;

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
    vec2 panel = top ? world_point.xz / vec2(4.0, 3.0) : vec2(run / 4.0, world_point.y / 2.0);
    vec2 local = fract(panel);
    vec2 edge = min(local, 1.0 - local);
    float seam = 1.0 - smoothstep(0.014, 0.039, min(edge.x, edge.y));
    float bevel = smoothstep(0.035, 0.055, min(edge.x, edge.y)) *
        (1.0 - smoothstep(0.055, 0.08, min(edge.x, edge.y)));
    vec3 steel = top ? vec3(0.21, 0.26, 0.34) : vec3(0.12, 0.16, 0.22);
    steel *= 0.91 + 0.09 * max(dot(n, normalize(vec3(-0.5, 0.7, 0.5))), 0.0);
    steel = steel * (1.0 - 0.42 * seam) + vec3(0.085, 0.11, 0.15) * bevel;
    if (!top) {
        float marker = step(0.84, local.x) * step(local.x, 0.94) *
            step(abs(local.y - 0.54), 0.032);
        steel = mix(steel, vec3(0.95, 0.54, 0.21), marker);
    }
    ALBEDO = steel;
}
