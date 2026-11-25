#include "res://VISUALS/RENDERING/UnitArmor.gdshaderinc"
uniform bool show_fracture = true;
uniform bool use_two_tone_armor = false;
uniform bool circle_sword_swing = false;
uniform vec4 armor_tint : source_color = vec4(1.0);
#include "res://VISUALS/FEEDBACK/UNIT_HITS/SurfaceCracks.gdshaderinc"
uniform vec2 half_extent = vec2(0.5);
uniform float darkness : hint_range(0.0, 1.0) = 0.84;
varying vec2 surface_point;
varying float upper_facet;
varying flat float fracture;

void vertex() {
    surface_point = VERTEX.xz / max(half_extent, vec2(0.01));
    upper_facet = smoothstep(0.08, 0.6, NORMAL.y);
    fracture = round(COLOR.a * 255.0);
    if (circle_sword_swing) {
        float swing = floor(fracture / 16.0);
        fracture = mod(fracture, 16.0);
        if (swing > 0.0 && UV.x < -0.5) {
            // A quick outward wind-up, inward slash and smooth recovery.
            float t = (swing - 1.0) / 14.0;
            float angle = t < 0.20 ? mix(0.0, -0.30, smoothstep(0.0, 0.20, t))
                : t < 0.52 ? mix(-0.30, 1.15, smoothstep(0.20, 0.52, t))
                : mix(1.15, 0.0, smoothstep(0.52, 1.0, t));
            float c = cos(angle), s = sin(angle);
            vec2 pivot = half_extent * vec2(0.65, -0.05);
            // Rotate only the tagged right arm and sword around the shoulder.
            mat2 rotation = mat2(vec2(c, -s), vec2(s, c));
            VERTEX.xz = pivot + rotation * (VERTEX.xz - pivot);
            NORMAL.xz = rotation * NORMAL.xz;
        }
    }
}

#ifdef BATTLEFIELD_LIT
global uniform float battlefield_ambient_fill;
#endif

void fragment() {
    vec3 tint = COLOR.rgb * armor_tint.rgb;
    ALBEDO = use_two_tone_armor ? unit_armor_albedo(tint, UV) : COLOR.rgb;
    if (show_fracture && fracture >= 4.0 && fracture < 16.0 && upper_facet > 0.0) {
        float stage = floor(fracture / 4.0);
        float variant = mod(fracture, 4.0);
        vec2 p = surface_point;
        if (variant >= 2.0) {
            p = p.yx;
        }
        if (mod(variant, 2.0) > 0.0) {
            p.x = -p.x;
        }
        float mask = surface_crack_mask(p, stage);
        ALBEDO *= 1.0 - mask * darkness * upper_facet * (0.52 + 0.16 * stage);
    }
#ifdef BATTLEFIELD_LIT
    EMISSION = ALBEDO * 0.58 * battlefield_ambient_fill;
#else
    EMISSION = vec3(0.0);
#endif
    if (use_two_tone_armor) {
        EMISSION += unit_armor_emission(tint, UV);
    }
}
