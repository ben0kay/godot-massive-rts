// Blend per-instance opacity so troops under a canopy stay visible.
shader_type spatial;
render_mode unshaded, shadows_disabled, depth_prepass_alpha;
uniform vec4 tree_color : source_color;
varying float opacity;

void vertex() {
    opacity = COLOR.a;
}

void fragment() {
    ALBEDO = tree_color.rgb * COLOR.rgb;
    ALPHA = opacity;
}
