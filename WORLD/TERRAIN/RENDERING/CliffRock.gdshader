// Layered weathered rock with vertical fissures and a lighter plateau cap.
shader_type spatial;
render_mode cull_disabled, ambient_light_disabled, specular_disabled;
varying vec3 world_position;
varying float cap;

float stone_noise(vec3 p) {
    vec3 cell = floor(p);
    vec3 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    vec4 h = vec4(0.0, 1.0, 57.0, 58.0);
    float n = dot(cell, vec3(1.0, 57.0, 113.0));
    vec4 a = fract(sin(n + h) * 43758.5453);
    vec4 b = fract(sin(n + 113.0 + h) * 43758.5453);
    vec4 v = mix(a, b, f.z);
    return mix(mix(v.x, v.y, f.x), mix(v.z, v.w, f.x), f.y);
}

void vertex() {
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    cap = step(0.6, NORMAL.y);
}

void fragment() {
    vec3 p = world_position;
    float large = stone_noise(p * vec3(0.65, 1.5, 0.65));
    float grit = stone_noise(p * 13.0);
    float broad = stone_noise(p * vec3(0.19, 0.45, 0.19));
    float strata = smoothstep(0.58, 0.83, fract(p.y * 0.82 + broad * 0.65 + large * 0.22));
    float fine_strata = smoothstep(0.78, 0.94, fract(p.y * 2.1 + broad * 0.7));
    float cap_cracks = smoothstep(0.04, 0.12, abs(stone_noise(p * vec3(0.85, 0.4, 0.85)) - 0.5));
    float fissure = smoothstep(0.62, 0.86, stone_noise(p * vec3(2.1, 0.34, 2.1)));
    vec3 rock = mix(vec3(0.22, 0.22, 0.21), vec3(0.29, 0.29, 0.27), cap);
    ALBEDO = rock * (0.70 + broad * 0.32 + large * 0.24 + grit * 0.12 - (1.0 - cap) * (strata * 0.24 + fine_strata * 0.09 + fissure * 0.25) - cap * (1.0 - cap_cracks) * 0.22);
    EMISSION = ALBEDO * 0.43;
    ROUGHNESS = 1.0;
}

void light() {
    float facing = max(dot(NORMAL, LIGHT), 0.0);
    vec3 sun = clamp(LIGHT_COLOR / PI, vec3(0.0), vec3(1.0));
    DIFFUSE_LIGHT += ALBEDO * sun * facing * ATTENUATION * 0.34;
}
