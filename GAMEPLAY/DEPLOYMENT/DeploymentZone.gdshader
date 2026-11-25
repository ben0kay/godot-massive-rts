// The vertex color marks the four exposed sides of each painted deployment cell.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;

uniform vec4 faction_color : source_color;
varying vec2 world_xz;
varying flat vec4 exposed;

void vertex() {
    world_xz = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
    exposed = COLOR;
}

void fragment() {
    vec4 distance_to_side = vec4(UV.x, 1.0 - UV.x, UV.y, 1.0 - UV.y);
    vec4 edge_distance = mix(vec4(1.0), distance_to_side, exposed);
    float boundary = min(min(edge_distance.x, edge_distance.y), min(edge_distance.z, edge_distance.w));
    vec2 pixel = max(fwidth(UV), vec2(0.0001));
    float aa = clamp(max(pixel.x, pixel.y), 0.01, 0.13);
    float outline = 1.0 - smoothstep(0.035, 0.035 + aa, boundary);
    float halo = 1.0 - smoothstep(0.03, 0.24, boundary);
    float pulse = 0.5 + 0.5 * sin(dot(world_xz, vec2(0.52, 0.39)) - TIME * 2.5);

    vec2 line_distance = min(UV, 1.0 - UV) / pixel;
    float grid = 1.0 - smoothstep(0.0, 1.1, min(line_distance.x, line_distance.y));
    grid *= 1.0 - smoothstep(0.08, 0.25, aa);
    float scan = pow(0.5 + 0.5 * sin(dot(world_xz, vec2(0.44, 0.74)) - TIME * 1.4), 8.0);

    ALBEDO = mix(faction_color.rgb, vec3(0.78, 0.90, 1.0), outline * (0.24 + 0.26 * pulse));
    ALPHA = 0.05 + grid * 0.065 + scan * 0.028 + halo * 0.17 + outline * (0.40 + 0.31 * pulse);
}
