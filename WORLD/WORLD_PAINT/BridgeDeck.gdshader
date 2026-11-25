// Steel deck panels, painted threshold marks and inset amber rail lights.
shader_type spatial;
render_mode ambient_light_disabled, shadows_disabled;

#include "res://VISUALS/RENDERING/SceneryLighting.gdshaderinc"
global uniform float battlefield_ambient_fill;

void fragment() {
    float part = UV2.y;
    float end_distance = min(UV.x, UV2.x - UV.x);
    vec3 steel = vec3(0.105, 0.145, 0.20);
    vec3 glow = vec3(0.0);

    if (part < 0.5) {
        vec2 panel = UV / vec2(2.0, 1.0);
        vec2 edge = min(fract(panel), 1.0 - fract(panel));
        float joint = 1.0 - smoothstep(0.015, 0.065, min(edge.x, edge.y));
        steel *= 1.0 - joint * 0.46;
        float threshold = smoothstep(0.12, 0.20, end_distance) *
            (1.0 - smoothstep(0.40, 0.48, end_distance));
        float stripes = step(0.46, fract((UV.y + end_distance) * 1.6));
        steel = mix(steel, vec3(0.39, 0.30, 0.17), threshold * stripes * 0.75);
    } else if (part > 1.5) {
        float inset = abs(fract(UV.x / 2.0) - 0.5);
        float strip = 1.0 - smoothstep(0.20, 0.25, inset);
        glow = vec3(1.0, 0.49, 0.16) * strip * 0.72;
    } else {
        steel *= 0.66;
    }
    ALBEDO = steel;
    EMISSION = steel * (0.35 + 0.58 * battlefield_ambient_fill) + glow;
}
