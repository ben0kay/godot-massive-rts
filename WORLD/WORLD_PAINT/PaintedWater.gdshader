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

    float contact = 1.0 - smoothstep(0.16, 0.9, shore_distance);
    color = mix(color, shoreline_color.rgb * 0.33, contact * 0.60);
    vec2 wave = map_position * 2.6 + vec2(time * 0.9, -time * 0.58);
    float crests = abs(sin(wave.x + sin(wave.y * 0.92)) + sin(wave.y + sin(wave.x * 0.73)));
    float caustics = 1.0 - smoothstep(0.016, 0.23, crests);
    color += vec3(0.085, 0.33, 0.44) * caustics * clamp(ripple_strength * 8.0, 0.0, 1.0) *
        smoothstep(0.24, 0.58, shore_distance);
    float reflected_light = exp(-abs(shore_distance - 0.32) * 4.0) *
        (0.45 + 0.55 * sin(map_position.x * 3.14) * sin(map_position.x * 3.14) *
            sin(map_position.y * 3.14) * sin(map_position.y * 3.14));
    ALBEDO = color + vec3(0.025, 0.24, 0.39) * reflected_light;
}
