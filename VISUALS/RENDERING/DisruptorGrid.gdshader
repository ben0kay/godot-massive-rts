shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, fog_disabled;

uniform vec4 faction_tint : source_color = vec4(0.4, 0.5, 1.0, 1.0);
varying float phase;

void vertex() {
    phase = dot(MODEL_MATRIX[3].xz, vec2(0.32, 0.47));
}

void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    float t = TIME * 2.2 + phase;
    float sway = sin(p.x * 5.0 + t) * 0.24;
    float filament = p.y - sway - sin(p.x * 16.0 - t * 1.7) * 0.065;
    float second = p.y + sway * 0.65 + sin(p.x * 8.0 - t) * 0.10;
    float ends = 1.0 - smoothstep(0.45, 1.0, abs(p.x));
    float shimmer = 0.52 + 0.48 * sin(p.x * 8.0 - t * 2.0);
    float core = (1.0 - smoothstep(0.018, 0.066, abs(filament))) * shimmer;
    float halo = exp(-abs(filament) * 8.5);
    float wisp = exp(-abs(second) * 13.0) * (0.55 + 0.45 * sin(p.x * 5.0 + t));
    vec3 hue = mix(faction_tint.rgb, vec3(0.66, 0.72, 1.0), 0.28);
    ALBEDO = mix(hue, vec3(0.92, 0.96, 1.0), core * 0.32);
    ALPHA = ends * (core * 0.52 + halo * 0.13 + wisp * 0.14);
}
