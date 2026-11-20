shader_type spatial;
render_mode unshaded, shadows_disabled;

uniform float map_half_width = 2500.0;
uniform float editor_grid_size = 0.0;
instance uniform bool terrain_replaces_ground = false;
varying float ground_height;
varying vec2 world_position;

void vertex() {
    if (terrain_replaces_ground) VERTEX = vec3(0.0);
    ground_height = VERTEX.y;
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
}

float grid_lines(vec2 position, vec2 pixel_width, float spacing) {
    vec2 distance_to_line = abs(fract(position / spacing + 0.5) - 0.5) * spacing;
    vec2 coverage = 1.0 - smoothstep(pixel_width * 0.3, pixel_width * 1.2, distance_to_line);
    vec2 fade = 1.0 - smoothstep(spacing * 0.15, spacing * 0.4, pixel_width);
    return max(coverage.x * fade.x, coverage.y * fade.y);
}

void fragment() {
    float distance_to_edge = map_half_width - max(abs(world_position.x), abs(world_position.y));
    vec3 exterior = vec3(0.065, 0.075, 0.095);
    vec3 ground = vec3(0.25, 0.26, 0.29);
    ground *= 1.0 + 0.24 * clamp(ground_height / 16.0, -1.0, 1.0);
    vec3 edge = vec3(0.64, 0.72, 0.81);
    float transition = max(fwidth(distance_to_edge), 0.05);
    float inside = smoothstep(-transition, transition, distance_to_edge);
    if (editor_grid_size > 0.0) {
        vec2 pixel_width = max(fwidth(world_position), vec2(0.0001));
        float minor = grid_lines(world_position, pixel_width, editor_grid_size);
        float major = grid_lines(world_position, pixel_width, editor_grid_size * 8.0);
        ground = mix(ground, vec3(0.085, 0.10, 0.13), max(minor * 0.46, major * 0.78));
    }
    ALBEDO = mix(exterior, mix(edge, ground, smoothstep(2.0, 2.0 + transition, distance_to_edge)), inside);
}
