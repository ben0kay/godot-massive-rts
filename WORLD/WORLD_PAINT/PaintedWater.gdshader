// Procedural ripple normals with restrained sky and sun reflections.
shader_type spatial;
render_mode ambient_light_disabled, cull_disabled, shadows_disabled;

uniform vec4 water_color : source_color;
uniform vec4 deep_water_color : source_color;
uniform vec4 shoreline_color : source_color;
uniform float shoreline_width = 0.9;
uniform float depth_fade_distance = 3.5;
uniform float ripple_strength = 0.045;
uniform float ripple_speed = 0.45;

varying vec2 map_position;

void vertex() {
    map_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
}

void fragment() {
    float shore_distance = UV.x;
    float depth = smoothstep(0.12, depth_fade_distance, shore_distance);
    vec3 color = mix(water_color.rgb * 0.69, deep_water_color.rgb * 0.70, depth);
    float contact = 1.0 - smoothstep(0.04, shoreline_width, shore_distance);
    color = mix(color, shoreline_color.rgb * 0.32, contact * 0.47);

    float time = TIME * ripple_speed;
    float broad = dot(map_position, vec2(1.09, 1.53)) + time * 1.8;
    float cross_wave = dot(map_position, vec2(-2.57, 1.31)) - time * 2.3;
    float fine = dot(map_position, vec2(4.70, -3.80)) + time * 2.8;
    float detail = 1.0 - smoothstep(0.12, 0.46, length(fwidth(map_position)));
    vec2 slope = vec2(0.64, 0.90) * cos(broad) +
        vec2(-0.89, 0.45) * cos(cross_wave + 0.32 * sin(broad)) * 0.57 +
        vec2(0.78, -0.63) * cos(fine) * detail * 0.27;
    vec3 world_normal = normalize(vec3(-slope.x * ripple_strength * 2.1, 1.0,
        -slope.y * ripple_strength * 2.1));
    NORMAL = normalize(mat3(VIEW_MATRIX) * world_normal);

    float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
    float fresnel = 0.035 + 0.70 * pow(1.0 - facing, 4.0);
    vec3 reflected = reflect(-normalize(mat3(INV_VIEW_MATRIX) * VIEW), world_normal);
    vec3 sky = mix(vec3(0.25, 0.34, 0.43), vec3(0.43, 0.59, 0.71),
        smoothstep(0.10, 0.95, reflected.y));
    float glimmer = sin(broad + cos(cross_wave) * 0.62) * sin(cross_wave);
    color *= 1.0 + glimmer * ripple_strength * 1.7;
    float rim_light = exp(-abs(shore_distance - 0.19) * 7.0) *
        (0.62 + 0.38 * sin(broad) * sin(broad));
    ALBEDO = color;
    EMISSION = mix(color, sky, fresnel) + vec3(0.018, 0.18, 0.23) * rim_light;
}

void light() {
    float facing = max(dot(NORMAL, LIGHT), 0.0);
    vec3 halfway = normalize(LIGHT + VIEW);
    float glint = pow(max(dot(NORMAL, halfway), 0.0), 48.0) * facing;
    SPECULAR_LIGHT += LIGHT_COLOR * (0.16 / PI) * glint * ATTENUATION;
}
