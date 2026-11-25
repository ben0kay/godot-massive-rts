shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled;

uniform vec4 water_color : source_color;
uniform vec4 deep_water_color : source_color;
uniform vec4 shoreline_color : source_color;
uniform float shoreline_width = 0.9;
uniform float depth_fade_distance = 3.5;
uniform float ripple_strength = 0.045;
uniform float ripple_speed = 0.45;

varying vec2 map_position;

void vertex() {
    map_position = VERTEX.xz;
}

void fragment() {
    float shore_distance = UV.x;
    float edge = shore_distance / shoreline_width;
    float depth = smoothstep(shoreline_width, depth_fade_distance, shore_distance);
    vec3 shallow = mix(water_color.rgb, vec3(0.13, 0.44, 0.42), 0.18);
    vec3 color = mix(shallow, deep_water_color.rgb, depth);
    float time = TIME * ripple_speed;
    float phase = dot(map_position, vec2(0.70, 0.57)) + time;
    float variation = sin(phase + sin(map_position.y * 0.73 - time * 0.56));
    variation *= sin(dot(map_position, vec2(0.43, -0.82)) + time * 0.34);
    color *= 1.0 + variation * ripple_strength * smoothstep(0.4, 1.4, edge);

    float bank = 1.0 - smoothstep(0.08, 0.66, edge);
    float contact = smoothstep(0.18, 0.42, edge) * (1.0 - smoothstep(0.45, 0.84, edge));
    float highlight = smoothstep(0.37, 0.79, edge) * (1.0 - smoothstep(1.0, 1.55, edge));
    color = mix(color, shoreline_color.rgb, bank * 0.92);
    color *= 1.0 - contact * 0.15;
    // Narrow retaining lip and cyan trim inside the painted shoreline.
    float aa = clamp(fwidth(shore_distance), 0.025, 0.14);
    float lip = 1.0 - smoothstep(0.22 - aa, 0.22 + aa, shore_distance);
    float trim = smoothstep(0.15 - aa, 0.15 + aa, shore_distance) *
        (1.0 - smoothstep(0.25 - aa, 0.25 + aa, shore_distance));
    vec2 panel = abs(fract(map_position / 2.0 + 0.5) - 0.5);
    float joint = 1.0 - smoothstep(0.018, 0.04, max(panel.x, panel.y));
    vec3 steel = vec3(0.16, 0.21, 0.29) * (1.0 - 0.28 * joint);
    ALBEDO = mix(color + shallow * highlight * 0.09, steel, lip);
    ALBEDO = mix(ALBEDO, vec3(0.23, 0.73, 0.87), trim * 0.72);
}
