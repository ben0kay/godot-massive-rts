// Muted paired tread marks, soft edges and a slow pooled fade.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;
varying flat float opacity;
void vertex() { opacity = INSTANCE_CUSTOM.x; }
void fragment() {
    float lane = abs(abs(UV.x - 0.5) - 0.37);
    float track = 1.0 - smoothstep(0.085, 0.125, lane);
    float lug = 1.0 - smoothstep(0.27, 0.47, abs(fract(UV.y * 3.0) - 0.5));
    float ends = smoothstep(0.0, 0.08, UV.y) * (1.0 - smoothstep(0.92, 1.0, UV.y));
    ALBEDO = vec3(0.055, 0.060, 0.065);
    ALPHA = track * (0.11 + lug * 0.19) * opacity * ends;
}
