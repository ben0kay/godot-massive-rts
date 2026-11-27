// Camera-facing beam and flares. Additive falloff supplies a halo with bloom off.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, skip_vertex_transform;

varying flat float effect;
varying flat float contact;
varying flat vec4 team;
varying flat float beam_length;
varying flat float seed;

void vertex() {
    vec3 center = MODELVIEW_MATRIX[3].xyz;
    effect = INSTANCE_CUSTOM.x;
    contact = INSTANCE_CUSTOM.y;
    team = COLOR;
    beam_length = length(MODELVIEW_MATRIX[2].xyz);
    seed = dot(MODEL_MATRIX[3].xz, vec2(0.11, 0.07));
    if (effect < 0.5) {
        vec3 axis = -MODELVIEW_MATRIX[2].xyz;
        vec2 across = vec2(-axis.y, axis.x) / max(length(axis.xy), 0.0001);
        if (length(axis.xy) < 0.0001) across = vec2(1.0, 0.0);
        VERTEX = center + axis * (UV.y - 0.5) +
            vec3(across * length(MODELVIEW_MATRIX[0].xyz) * (UV.x - 0.5), 0.0);
    } else {
        float size = effect < 1.5 ? 1.35 : 1.70;
        VERTEX = center + vec3((UV - 0.5) * size, 0.10);
    }
}

void fragment() {
    vec3 tint = mix(team.rgb, vec3(1.0), 0.08);
    vec3 hot_core = mix(team.rgb, vec3(1.0), 0.58) * 1.3;
    if (effect < 0.5) {
        float edge = abs(UV.x * 2.0 - 1.0);
        float aa = clamp(fwidth(edge), 0.02, 0.40);
        float core = 1.0 - smoothstep(0.10, 0.16 + aa, edge);
        float sheath = 1.0 - smoothstep(0.12, 0.54, edge);
        float halo = 1.0 - smoothstep(0.0, 0.98, edge);
        float shimmer = 0.86 + 0.14 * sin(UV.y * beam_length * 4.0 - TIME * 24.0);
        ALBEDO = mix(tint, hot_core, core);
        float tip = min(UV.y, 1.0 - UV.y);
        float taper = smoothstep(0.0, clamp(1.2 / max(beam_length, 0.001), 0.035, 0.18), tip);
        ALPHA = team.a * taper * min(core * 0.93 + sheath * shimmer * 0.52 + halo * halo * 0.30, 1.0);
    } else {
        vec2 p = UV * 2.0 - 1.0;
        float radius = length(p);
        float core = 1.0 - smoothstep(0.05, 0.31, radius);
        float halo = 1.0 - smoothstep(0.06, 0.95, radius);
        float phase = fract(TIME * 13.0 + seed);
        float spin = floor(TIME * 13.0) * 2.4;
        vec2 ray = vec2(cos(spin), sin(spin));
        float streak = 1.0 - smoothstep(0.01, 0.09, abs(dot(p, ray)));
        float sparks = streak * (1.0 - smoothstep(0.28, 0.90, radius)) * (1.0 - phase);
        ALBEDO = mix(tint, hot_core, max(core, sparks * 0.55));
        ALPHA = (core * 0.78 + halo * halo * 0.35 + sparks * 0.35) *
            (effect < 1.5 ? 0.72 : contact * (0.78 + 0.22 * (1.0 - phase)));
    }
}
