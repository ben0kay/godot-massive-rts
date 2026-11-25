// World-space texture variation keeps adjoining painted cells visually continuous.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;

varying vec2 world_xz;

void vertex() {
    world_xz = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
}

float hash_point(vec2 point) {
    vec3 p = fract(vec3(point.xyx) * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return fract((p.x + p.y) * p.z);
}

float noise_at(vec2 point) {
    vec2 cell = floor(point);
    vec2 f = fract(point);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash_point(cell), hash_point(cell + vec2(1.0, 0.0)), f.x),
        mix(hash_point(cell + vec2(0.0, 1.0)), hash_point(cell + vec2(1.0)), f.x), f.y);
}

void fragment() {
    float patch = noise_at(world_xz * 0.95);
    float detail = noise_at(world_xz * 5.5);
    float grass = smoothstep(0.32, 0.68, patch + (detail - 0.5) * 0.30);
    vec3 dirt = mix(vec3(0.116, 0.082, 0.053), vec3(0.24, 0.18, 0.106), detail);
    vec3 foliage = mix(vec3(0.061, 0.093, 0.040), vec3(0.15, 0.20, 0.081), detail);
    ALBEDO = mix(dirt, foliage, grass);
    ALPHA = smoothstep(0.03, 0.33, UV.x + (detail - 0.5) * 0.11);
}
