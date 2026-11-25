#include "res://VISUALS/RENDERING/UnitArmor.gdshaderinc"
uniform bool show_fracture = true;
uniform bool use_two_tone_armor = false;
uniform bool infantry_attack = false;
uniform bool infantry_walk = false;
uniform bool square_heavy = false;
uniform bool hexagon_siege = false;
uniform bool triangle_archer = false;
uniform bool pentagon_elite = false;
uniform float infantry_height = 0.4125;
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
    bool stomping = false;
    if (infantry_attack) {
        float swing = floor(fracture / 16.0);
        fracture = mod(fracture, 16.0);
        stomping = hexagon_siege && swing > 0.0 && INSTANCE_CUSTOM.z > 0.5;
        if (stomping) {
            // The radial damage already occurred. Brace sharply, then recover.
            // Both boots stay on the ground during the stomp.
            float t = (swing - 1.0) / 14.0;
            float brace = t < 0.18 ? smoothstep(0.0, 0.18, t)
                : 1.0 - smoothstep(0.18, 1.0, t);
            if (UV.x < 4.5) VERTEX.y -= infantry_height * 0.085 * brace;
        } else if (triangle_archer && swing > 0.0 && UV.x < -0.5) {
            // The projectile has already launched. Slide the barrel rearward,
            // then return it to its receiver; retain the existing body recoil.
            float t = (swing - 1.0) / 14.0;
            float kick = t < 0.15 ? smoothstep(0.0, 0.15, t)
                : 1.0 - smoothstep(0.15, 1.0, t);
            VERTEX.z += half_extent.y * 0.18 * kick;
        } else if (swing > 0.0 && UV.x < -0.5) {
            // A quick outward wind-up, inward slash and smooth recovery.
            float t = (swing - 1.0) / 14.0;
            // Square has a longer wind-up and compact, forceful hammer strike.
            float windup = square_heavy ? 0.32 : 0.20;
            float impact = square_heavy ? 0.50 : 0.52;
            float back = square_heavy ? -0.46 : -0.30;
            float forward = square_heavy ? 0.95 : 1.15;
            float angle = t < windup ? mix(0.0, back, smoothstep(0.0, windup, t))
                : t < impact ? mix(back, forward, smoothstep(windup, impact, t))
                : mix(forward, 0.0, smoothstep(impact, 1.0, t));
            // The existing contact crescent lasts 0.17 seconds. Sweep across
            // the front immediately after the hit, then slowly return to rest.
            if (hexagon_siege) {
                angle = t < 0.07 ? mix(0.0, -0.75, smoothstep(0.0, 0.07, t))
                    : t < 0.29 ? mix(-0.75, 1.85, smoothstep(0.07, 0.29, t))
                    : mix(1.85, 0.0, smoothstep(0.29, 1.0, t));
            }
            // Elite axe: approximately 85 degrees of travel during the existing
            // contact crescent, followed by a deliberately slower recovery.
            if (pentagon_elite) {
                angle = t < 0.07 ? mix(0.0, -0.50, smoothstep(0.0, 0.07, t))
                    : t < 0.36 ? mix(-0.50, 0.98, smoothstep(0.07, 0.36, t))
                    : mix(0.98, 0.0, smoothstep(0.36, 1.0, t));
            }
            float c = cos(angle), s = sin(angle);
            vec2 pivot = hexagon_siege ? vec2(0.0) :
                half_extent * (pentagon_elite ? vec2(0.48, 0.04) :
                    square_heavy ? vec2(0.49, -0.035) : vec2(0.65, -0.05));
            // Rotate only the tagged right arm and sword around the shoulder.
            mat2 rotation = mat2(vec2(c, -s), vec2(s, c));
            VERTEX.xz = pivot + rotation * (VERTEX.xz - pivot);
            NORMAL.xz = rotation * NORMAL.xz;
        }
    }
    if (infantry_walk && !stomping && INSTANCE_CUSTOM.y > 0.0) {
        float phase = INSTANCE_CUSTOM.x * TAU;
        float weight = INSTANCE_CUSTOM.y;
        if (UV.x > 4.5) {
            // Each boot alternates forward travel and lift. The planted boot stays grounded.
            float side = sign(VERTEX.x);
            float travel = hexagon_siege ? 0.12 : pentagon_elite ? 0.15 : square_heavy ? 0.17 : triangle_archer ? 0.20 : 0.24;
            float lift = hexagon_siege ? 0.055 : pentagon_elite ? 0.065 : square_heavy ? 0.075 : triangle_archer ? 0.095 : 0.10;
            VERTEX.z -= sin(phase) * side * half_extent.y * travel * weight;
            VERTEX.y += max(0.0, cos(phase) * side) * infantry_height * lift * weight;
        } else {
            // Two small body rises per stride; do not lift the planted feet or the shadow.
            float bounce = hexagon_siege ? 0.009 : pentagon_elite ? 0.011 : square_heavy ? 0.0125 : triangle_archer ? 0.015 : 0.0225;
            VERTEX.y += (1.0 - cos(phase * 2.0)) * infantry_height * bounce * weight;
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
