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
    float strata = smoothstep(0.67, 0.88, fract(p.y * 1.55 + large * 0.48));
    float fissure = smoothstep(0.62, 0.86, stone_noise(p * vec3(2.1, 0.34, 2.1)));
    vec3 rock = mix(vec3(0.19, 0.22, 0.27), vec3(0.31, 0.33, 0.36), cap);
    ALBEDO = rock * (0.78 + large * 0.29 + grit * 0.10 - (1.0 - cap) * (strata * 0.18 + fissure * 0.23));
    EMISSION = ALBEDO * 0.43;
    ROUGHNESS = 1.0;
}

void light() {
    float facing = max(dot(NORMAL, LIGHT), 0.0);
    vec3 sun = clamp(LIGHT_COLOR / PI, vec3(0.0), vec3(1.0));
    DIFFUSE_LIGHT += ALBEDO * sun * facing * ATTENUATION * 0.34;
}
