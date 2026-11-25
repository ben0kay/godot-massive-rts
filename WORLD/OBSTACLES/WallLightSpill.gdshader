shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;

uniform vec4 spill_tint : source_color = vec4(1.0, 0.44, 0.12, 1.0);

void fragment() {
    vec2 p = vec2((UV.x - 0.5) * 2.0, UV.y);
    float edge = 1.0 - smoothstep(0.05, 1.0, length(p));
    ALBEDO = spill_tint.rgb;
    ALPHA = edge * edge * 0.24;
}
