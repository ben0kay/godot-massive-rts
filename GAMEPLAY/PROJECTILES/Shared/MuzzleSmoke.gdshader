// A soft, camera-facing cluster of expanding smoke lobes.
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, shadows_disabled, skip_vertex_transform;

varying flat float age;
varying flat float seed;

void vertex() {
    age = INSTANCE_CUSTOM.x;
    seed = INSTANCE_CUSTOM.y;
    float size = mix(0.85, 3.3, sqrt(age));
    VERTEX = MODELVIEW_MATRIX[3].xyz + vec3((UV - 0.5) * size, 0.0);
}

float puff(vec2 p, vec2 center, float radius) {
    float distance_to_center = length(p - center) / radius;
    return 1.0 - smoothstep(0.25, 1.0, distance_to_center);
}

void fragment() {
    vec2 p = (UV - 0.5) * 2.0;
    float phase = seed * 6.283185 + age * 2.3;
    p += 0.047 * vec2(sin(p.y * 12.0 + phase), cos(p.x * 13.0 - phase));
    vec2 roll = vec2(cos(phase), sin(phase)) * 0.13;
    float core = puff(p, vec2(0.02, 0.07), 0.60);
    float lobes = puff(p, vec2(-0.39, 0.16) + roll, 0.42);
    lobes = max(lobes, puff(p, vec2(0.36, -0.15) - roll, 0.44));
    lobes = max(lobes, puff(p, vec2(0.10, -0.40) + roll * 0.7, 0.37));
    lobes = max(lobes, puff(p, vec2(-0.18, 0.45) - roll * 0.7, 0.33));
    float density = max(core, lobes * 0.81);
    float light = clamp(0.45 - p.y * 0.32 + lobes * 0.23, 0.0, 1.0);
    ALBEDO = mix(vec3(0.26, 0.28, 0.32), vec3(0.67, 0.69, 0.72), light);
    ALPHA = density * smoothstep(0.0, 0.08, age) * (1.0 - smoothstep(0.24, 1.0, age)) * 0.57;
}
