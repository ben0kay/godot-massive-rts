shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled, skip_vertex_transform;

varying flat float age;
varying flat float kind;

void vertex() {
    age = INSTANCE_CUSTOM.x;
    kind = INSTANCE_CUSTOM.y;
    vec2 p = UV - 0.5;
    float size = length(MODELVIEW_MATRIX[0].xyz);
    if (kind > 1.5) {
        p *= mix(0.45, 1.0, age);
        p.y += age * 0.18;
    } else if (kind > 0.5) {
        vec2 aim = (VIEW_MATRIX * vec4(INSTANCE_CUSTOM.z, 0.0, INSTANCE_CUSTOM.w, 0.0)).xy;
        aim = length(aim) > 0.001 ? normalize(aim) : vec2(1.0, 0.0);
        p = aim * (p.x + age * 0.18) + vec2(-aim.y, aim.x) * p.y;
    }
    VERTEX = MODELVIEW_MATRIX[3].xyz + vec3(p * size, 0.0);
}

void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    float fade = 1.0 - age;
    if (kind > 1.5) {
        p.x += sin(p.y * 8.0 + age * 3.0) * 0.10;
        float lobe = min(length(p + vec2(0.23, -0.10)), length(p - vec2(0.24, 0.06)));
        ALBEDO = vec3(0.51, 0.44, 0.34);
        ALPHA = (1.0 - smoothstep(0.16, 0.72, lobe)) * fade * 0.32;
    } else if (kind > 0.5) {
        float curve = p.x - (0.33 - 0.74 * p.y * p.y);
        float line = 1.0 - smoothstep(0.02, 0.17, abs(curve));
        ALBEDO = vec3(1.1, 0.79, 0.47);
        ALPHA = line * (1.0 - smoothstep(0.13, 0.90, abs(p.y))) * fade * 0.72;
    } else {
        float core = 1.0 - smoothstep(0.03, 0.27, length(p));
        float ray = min(abs(p.x * 0.7 + p.y), abs(p.x - p.y * 0.7));
        float sparks = (1.0 - smoothstep(0.015, 0.095, ray)) * (1.0 - smoothstep(0.15, 0.90, length(p)));
        ALBEDO = mix(vec3(1.07, 0.55, 0.20), vec3(1.45, 1.22, 0.88), core);
        ALPHA = max(core, sparks * 0.7) * fade * fade;
    }
}
