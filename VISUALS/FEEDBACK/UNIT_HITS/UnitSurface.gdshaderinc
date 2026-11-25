#ifdef BATTLEFIELD_LIT
uniform bool show_fracture = true;
#endif
uniform sampler2D crack_mask : filter_linear_mipmap, repeat_disable;
uniform vec2 half_extent = vec2(0.5);
uniform float darkness : hint_range(0.0, 1.0) = 0.84;
varying vec2 surface_point;
varying float upper_facet;
varying flat float fracture;

void vertex() {
    surface_point = VERTEX.xz / max(half_extent, vec2(0.01));
    upper_facet = smoothstep(0.08, 0.6, NORMAL.y);
    fracture = round(COLOR.a * 255.0);
}

#ifdef BATTLEFIELD_LIT
global uniform float battlefield_ambient_fill;
#endif

void fragment() {
    ALBEDO = COLOR.rgb;
#ifdef BATTLEFIELD_LIT
    if (show_fracture)
#endif
    if (fracture >= 4.0 && fracture < 16.0 && upper_facet > 0.0) {
        float stage = floor(fracture / 4.0);
        float variant = mod(fracture, 4.0);
        vec2 p = surface_point;
        if (variant >= 2.0) {
            p = p.yx;
        }
        if (mod(variant, 2.0) > 0.0) {
            p.x = -p.x;
        }
        vec3 lines = texture(crack_mask, p * 0.5 + 0.5).rgb;
        float mask = max(lines.r, max(lines.g * step(2.0, stage), lines.b * step(3.0, stage)));
        ALBEDO *= 1.0 - mask * darkness * upper_facet * (0.52 + 0.16 * stage);
    }
#ifdef BATTLEFIELD_LIT
    EMISSION = ALBEDO * 0.58 * battlefield_ambient_fill;
#endif
}
