uniform sampler2D stone_noise : repeat_enable, filter_linear_mipmap;
uniform sampler2D stone_normal : repeat_enable, filter_linear_mipmap;
uniform vec4 slate_color : source_color = vec4(0.31, 0.34, 0.39, 1.0);
uniform float patch_scale : hint_range(0.005, 0.50) = 0.035;
uniform float grain_scale : hint_range(0.02, 2.0) = 0.21;
uniform float relief : hint_range(0.0, 1.0) = 0.23;
uniform float sheen : hint_range(0.0, 1.0) = 0.25;
uniform vec2 panel_size = vec2(3.8, 2.9);
uniform bool industrial_panels = false;
uniform float seam_strength : hint_range(0.0, 0.5) = 0.13;

uniform bool show_map_boundary = true;
uniform float map_half_width = 2500.0;
uniform float editor_grid_size = 0.0;
instance uniform bool terrain_replaces_ground = false;

varying vec2 ground_xz;
varying float ground_height;

void vertex() {
    if (terrain_replaces_ground) VERTEX = vec3(0.0);
    vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    ground_height = world.y;
    ground_xz = world.xz;
}

float grid_lines(vec2 position, vec2 pixel_width, float spacing) {
    vec2 distance_to_line = abs(fract(position / spacing + 0.5) - 0.5) * spacing;
    vec2 coverage = 1.0 - smoothstep(pixel_width * 0.3, pixel_width * 1.2, distance_to_line);
    vec2 fade = 1.0 - smoothstep(spacing * 0.15, spacing * 0.4, pixel_width);
    return max(coverage.x * fade.x, coverage.y * fade.y);
}

void fragment() {
    float stone = texture(stone_noise, ground_xz * patch_scale).r;
    vec3 grain = texture(stone_normal, ground_xz * grain_scale).rgb * 2.0 - 1.0;
    vec3 normal_world = normalize(vec3(grain.x * relief, max(grain.z, 0.2), grain.y * relief));

    // Offset alternating rows and interrupt the joints with the stone pattern.
    vec2 slabs = ground_xz / panel_size + vec2(stone - 0.5) * (industrial_panels ? 0.025 : 0.16);
    slabs.x += mod(floor(slabs.y), 2.0) * 0.5;
    vec2 edge = 0.5 - abs(fract(slabs) - 0.5);
    float seam = 1.0 - smoothstep(0.008, 0.026 + fwidth(ground_xz.x) * 0.18, min(edge.x, edge.y));
    float seam_fade = 1.0 - smoothstep(0.08, 0.45, length(fwidth(ground_xz)));
    seam *= industrial_panels ? 1.0 - smoothstep(0.4, 1.4, length(fwidth(ground_xz))) :
        seam_fade * smoothstep(0.31, 0.65, stone);

    vec3 key = normalize(vec3(-0.48, 0.70, 0.53));
    vec3 halfway = normalize(key + INV_VIEW_MATRIX[2].xyz);
    float diffuse = dot(normal_world, key) - key.y;
    float polished = smoothstep(0.43, 0.72, stone);
    float specular = pow(max(dot(normal_world, halfway), 0.0), mix(11.0, 25.0, polished));
    vec3 base = slate_color.rgb * (0.83 + stone * 0.30 + diffuse * 0.55 - seam * (industrial_panels ? 0.24 : seam_strength));
    vec3 ground = base + vec3(0.63, 0.77, 1.0) * specular * sheen * mix(0.10, 0.42, polished);
    if (industrial_panels) {
        float ambient = texture(stone_noise, ground_xz * 0.011).r;
        ground *= mix(vec3(0.46, 0.54, 0.75), vec3(0.78, 0.83, 0.96),
            smoothstep(0.18, 0.82, ambient));
    }
    ground *= 1.0 + 0.24 * clamp(ground_height / 16.0, -1.0, 1.0);
    if (editor_grid_size > 0.0) {
        vec2 pixel_width = max(fwidth(ground_xz), vec2(0.0001));
        float minor = grid_lines(ground_xz, pixel_width, editor_grid_size);
        float major = grid_lines(ground_xz, pixel_width, editor_grid_size * 8.0);
        ground = mix(ground, vec3(0.085, 0.10, 0.13), max(minor * 0.46, major * 0.78));
    }
    ALBEDO = ground;
    if (show_map_boundary) {
        float distance_to_edge = map_half_width - max(abs(ground_xz.x), abs(ground_xz.y));
        float transition = max(fwidth(distance_to_edge), 0.05);
        float inside = smoothstep(-transition, transition, distance_to_edge);
        vec3 edge = vec3(0.64, 0.72, 0.81);
        vec3 exterior = vec3(0.065, 0.075, 0.095);
        ALBEDO = mix(exterior, mix(edge, ground, smoothstep(2.0, 2.0 + transition, distance_to_edge)), inside);
    }
#ifdef RECEIVE_BATTLEFIELD_SHADOWS
    EMISSION = ALBEDO * 0.48;
#endif
}
