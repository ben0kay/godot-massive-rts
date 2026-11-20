// Dither the instance opacity so nearby trees reveal troops without alpha sorting.
shader_type spatial;
render_mode unshaded, shadows_disabled;
uniform vec4 tree_color : source_color;
varying float opacity;

void vertex() {
    opacity = COLOR.a;
}

void fragment() {
    ALBEDO = tree_color.rgb;
    ALPHA = opacity;
    ALPHA_HASH_SCALE = 1.0;
}
