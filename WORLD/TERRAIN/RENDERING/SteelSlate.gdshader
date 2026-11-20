// World-space slate with shallow stone relief and a broad, baked-light sheen.
shader_type spatial;
render_mode unshaded;

uniform sampler2D stone_noise : repeat_enable, filter_linear_mipmap;
uniform sampler2D stone_normal : repeat_enable, filter_linear_mipmap;
uniform vec4 slate_color : source_color = vec4(0.31, 0.34, 0.39, 1.0);
uniform float patch_scale : hint_range(0.005, 0.50) = 0.035;
uniform float grain_scale : hint_range(0.02, 2.0) = 0.21;
uniform float relief : hint_range(0.0, 1.0) = 0.23;
uniform float sheen : hint_range(0.0, 1.0) = 0.25;
uniform float seam_strength : hint_range(0.0, 0.5) = 0.13;

varying vec2 ground_xz;

void vertex() {
    ground_xz = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
}

void fragment() {
    float stone = texture(stone_noise, ground_xz * patch_scale).r;
    vec3 grain = texture(stone_normal, ground_xz * grain_scale).rgb * 2.0 - 1.0;
    vec3 normal_world = normalize(vec3(grain.x * relief, max(grain.z, 0.2), grain.y * relief));

    // Offset alternating rows and interrupt the joints with the stone pattern.
    vec2 slabs = ground_xz / vec2(3.8, 2.9) + vec2(stone - 0.5) * 0.16;
    slabs.x += mod(floor(slabs.y), 2.0) * 0.5;
    vec2 edge = 0.5 - abs(fract(slabs) - 0.5);
    float seam = 1.0 - smoothstep(0.008, 0.026 + fwidth(ground_xz.x) * 0.18, min(edge.x, edge.y));
    float seam_fade = 1.0 - smoothstep(0.08, 0.45, length(fwidth(ground_xz)));
    seam *= seam_fade * smoothstep(0.31, 0.65, stone);

    vec3 key = normalize(vec3(-0.48, 0.70, 0.53));
    vec3 halfway = normalize(key + INV_VIEW_MATRIX[2].xyz);
    float diffuse = dot(normal_world, key) - key.y;
    float polished = smoothstep(0.43, 0.72, stone);
    float specular = pow(max(dot(normal_world, halfway), 0.0), mix(11.0, 25.0, polished));
    vec3 base = slate_color.rgb * (0.83 + stone * 0.30 + diffuse * 0.55 - seam * seam_strength);
    ALBEDO = base + vec3(0.63, 0.77, 1.0) * specular * sheen * mix(0.10, 0.42, polished);
}
