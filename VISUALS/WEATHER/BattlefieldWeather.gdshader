// World-anchored wisps and distance haze in one camera pass.
shader_type spatial;
render_mode unshaded, fog_disabled, shadows_disabled, cull_disabled, depth_draw_never, depth_test_disabled;

uniform sampler2D scene_depth : hint_depth_texture, repeat_disable, filter_nearest;
uniform float mist_density : hint_range(0.0, 1.0) = 0.45;
uniform float haze_density : hint_range(0.0, 1.0) = 0.25;
uniform float drift_offset = 0.0;

void vertex() {
    POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}

float hash_cell(vec2 cell) {
    vec3 p = fract(vec3(cell.xyx) * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return fract((p.x + p.y) * p.z);
}

float noise_at(vec2 position) {
    vec2 cell = floor(position);
    vec2 t = fract(position);
    t = t * t * (3.0 - 2.0 * t);
    return mix(mix(hash_cell(cell), hash_cell(cell + vec2(1.0, 0.0)), t.x),
        mix(hash_cell(cell + vec2(0.0, 1.0)), hash_cell(cell + vec2(1.0)), t.x), t.y);
}

void fragment() {
    float depth = texture(scene_depth, SCREEN_UV).r;
    if (depth <= 0.000001) discard;

#if CURRENT_RENDERER == RENDERER_COMPATIBILITY
    vec3 ndc = vec3(SCREEN_UV, depth) * 2.0 - 1.0;
#else
    vec3 ndc = vec3(SCREEN_UV * 2.0 - 1.0, depth);
#endif
    vec4 view_point = INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
    vec3 view_position = view_point.xyz / view_point.w;
    vec3 world = (INV_VIEW_MATRIX * vec4(view_position, 1.0)).xyz;

    float mist = 0.0;
    if (mist_density > 0.0) {
        vec2 wind_space = vec2(dot(world.xz, vec2(0.8, 0.6)) - drift_offset,
            dot(world.xz, vec2(-0.6, 0.8)));
        vec2 stretched = wind_space * vec2(0.038, 0.145);
        float ribbon = noise_at(stretched);
        float gaps = noise_at(stretched * 2.2 + vec2(17.0, 9.0));
        float wisps = smoothstep(0.42, 0.78, ribbon * 0.75 + gaps * 0.25);
        mist = mist_density * wisps * (1.0 - smoothstep(0.35, 5.0, world.y)) * 0.38;
    }

    // Orthographic depth relative to the ground at the centre of the view.
    float centre_depth = INV_VIEW_MATRIX[3].y / max(INV_VIEW_MATRIX[2].y, 0.01);
    float relative_depth = (-view_position.z - centre_depth) / max(abs(INV_PROJECTION_MATRIX[1][1]), 0.01);
    float haze = haze_density * smoothstep(-0.12, 0.78, relative_depth) * 0.21;
    ALBEDO = vec3(0.69, 0.77, 0.82);
    ALPHA = 1.0 - (1.0 - mist) * (1.0 - haze);
}
