// Blend per-instance opacity so troops under a canopy stay visible.
shader_type spatial;
render_mode depth_prepass_alpha, ambient_light_disabled, shadows_disabled;

uniform vec4 tree_color : source_color;
uniform bool foliage = false;
varying float opacity;

void vertex() {
    opacity = COLOR.a;
    if (foliage && INSTANCE_CUSTOM.b > 0.0) {
        vec2 world_push = INSTANCE_CUSTOM.rg * 2.0 - 1.0;
        // Crowns have individual yaw; keep heavy-unit sway pointing along world travel.
        vec2 push = vec2(dot(world_push, normalize(MODEL_MATRIX[0].xz)),
                         dot(world_push, normalize(MODEL_MATRIX[2].xz)));
        float tip = smoothstep(-0.4, 0.75, VERTEX.y);
        float sway = 0.60 + 0.40 * sin(TIME * 13.0 + MODEL_MATRIX[3].x);
        VERTEX.xz += push * tip * sway * INSTANCE_CUSTOM.b * 0.20;
    }
}

global uniform float battlefield_ambient_fill;

void fragment() {
    ALBEDO = tree_color.rgb * COLOR.rgb;
    EMISSION = ALBEDO * 0.58 * battlefield_ambient_fill;
    ALPHA = opacity;
}

void light() {
    float facing = max(dot(NORMAL, LIGHT), 0.0);
    vec3 sun_color = foliage ? ALBEDO * vec3(1.15, 1.85, 1.02) : ALBEDO;
    DIFFUSE_LIGHT += sun_color * LIGHT_COLOR * (0.72 / PI) * facing * ATTENUATION;
}
