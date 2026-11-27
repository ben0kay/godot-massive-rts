// World-space texture variation keeps adjoining painted cells visually continuous.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;

uniform sampler2D ground_detail : repeat_enable, filter_linear_mipmap_anisotropic;
uniform bool marsh = false;
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
    float patch = noise_at(world_xz * 0.52);
    float grit = texture(ground_detail, world_xz * 0.16).r;
    vec2 skew = vec2(world_xz.x + world_xz.y * 0.38, world_xz.y - world_xz.x * 0.20);
    float grass_fiber = texture(ground_detail, skew * vec2(0.32, 0.12) + vec2(0.27, 0.39)).r;
    float grass = smoothstep(0.36, 0.65, patch + (grit - 0.5) * 0.24);
    vec3 dirt = mix(vec3(0.091, 0.064, 0.043), vec3(0.225, 0.174, 0.109), smoothstep(0.18, 0.81, grit));
    vec3 foliage = mix(vec3(0.047, 0.068, 0.028), vec3(0.145, 0.175, 0.067),
        smoothstep(0.22, 0.81, grass_fiber));
    ALBEDO = mix(dirt, foliage, grass);
    if (marsh) {
        vec2 channels = skew * vec2(0.12, 0.63);
        channels.y += (noise_at(world_xz * 0.13) - 0.5) * 1.3;
        float flow = noise_at(channels);
        float pool = noise_at(world_xz * 0.22);
        float wet = smoothstep(0.41, 0.67, flow * 0.73 + pool * 0.27);
        float ripple = noise_at(channels * vec2(1.7, 3.6) + vec2(TIME * 0.065, -TIME * 0.04));
        vec3 mud = mix(vec3(0.072, 0.063, 0.039), vec3(0.17, 0.155, 0.091), grit);
        vec3 water = mix(vec3(0.044, 0.085, 0.076), vec3(0.14, 0.22, 0.18), ripple);
        float sheen = smoothstep(0.65, 0.88, ripple) * wet;
        ALBEDO = mix(mud, water, wet) + vec3(0.055, 0.075, 0.068) * sheen;
    }
    ALPHA = smoothstep(0.03, 0.33, UV.x + (grit - 0.5) * 0.13);
}
