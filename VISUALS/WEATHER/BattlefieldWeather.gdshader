// World-anchored wisps and distance haze in one camera pass.
shader_type spatial;
render_mode unshaded, fog_disabled, shadows_disabled, cull_disabled, depth_draw_never, depth_test_disabled;

uniform sampler2D scene_depth : hint_depth_texture, repeat_disable, filter_nearest;
uniform float mist_density : hint_range(0.0, 1.0) = 0.45;
uniform float haze_density : hint_range(0.0, 1.0) = 0.25;
uniform sampler2D water_mask : repeat_disable, filter_linear;
uniform vec4 water_bounds = vec4(0.0);
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

    // Offset and rotate the layers independently so their shapes never share a grid.
    mat2 turn = mat2(vec2(0.80, 0.60), vec2(-0.60, 0.80));
    vec2 drifting = world.xz - vec2(0.80, 0.60) * drift_offset;
    float broad = noise_at(drifting * 0.029);
    float fold = noise_at(turn * drifting * 0.071 + vec2(17.4, 9.3));
    vec2 bent = turn * drifting + vec2(broad - 0.5, fold - 0.5) * 12.0;
    float detail = noise_at(bent * vec2(0.060, 0.115) - vec2(3.3, 21.8));
    float cloud = broad * 0.45 + fold * 0.32 + detail * 0.23;

    float mist = 0.0;
    if (mist_density > 0.0) {
        float wisps = smoothstep(0.43, 0.73, cloud);
        float ground_fade = 1.0 - smoothstep(0.35, 4.5, world.y);
        mist = mist_density * wisps * ground_fade * 0.29;
        if (water_bounds.z > 0.0) {
            vec2 uv = (world.xz - water_bounds.xy) / water_bounds.zw;
            if (all(greaterThan(uv, vec2(0.0))) && all(lessThan(uv, vec2(1.0)))) {
                float river = texture(water_mask, uv).r;
                float river_wisps = smoothstep(0.29, 0.69, mix(cloud, detail, 0.34));
                mist += mist_density * river * river_wisps * (0.35 + 0.65 * ground_fade) * 0.38;
            }
        }
    }

    // Orthographic depth relative to the ground at the centre of the view.
    float centre_depth = INV_VIEW_MATRIX[3].y / max(INV_VIEW_MATRIX[2].y, 0.01);
    float relative_depth = (-view_position.z - centre_depth) / max(abs(INV_PROJECTION_MATRIX[1][1]), 0.01);
    float depth_fade = smoothstep(-0.65, 0.95, relative_depth);
    float haze = haze_density * (0.32 + 0.68 * depth_fade) * smoothstep(0.25, 0.77, cloud) * 0.20;
    ALBEDO = vec3(0.69, 0.77, 0.82);
    ALPHA = min(0.50, 1.0 - (1.0 - mist) * (1.0 - haze));
}
