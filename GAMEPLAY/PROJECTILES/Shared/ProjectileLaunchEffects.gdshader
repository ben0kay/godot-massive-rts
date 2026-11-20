// A camera-facing ribbon follows the flight parabola. Young shots also draw a flash at their muzzle.
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, shadows_disabled, skip_vertex_transform;

varying flat float flash;
varying flat float flash_age;
varying flat vec3 faction_color;

void vertex() {
    vec3 anchor = MODELVIEW_MATRIX[3].xyz;
    flash = INSTANCE_CUSTOM.w < 0.0 ? 1.0 : 0.0;
    flash_age = INSTANCE_CUSTOM.x;
    faction_color = COLOR.rgb;
    if (flash > 0.5) {
        VERTEX = anchor + vec3((UV - 0.5) * mix(0.65, 1.15, flash_age), 0.0);
    } else {
        float t = UV.x;
        vec3 quadratic = vec3(0.0, -INSTANCE_CUSTOM.w, 0.0);
        vec3 offset = t * (INSTANCE_CUSTOM.xyz + quadratic * t);
        vec2 tangent = (mat3(MODELVIEW_MATRIX) * (INSTANCE_CUSTOM.xyz + 2.0 * quadratic * t)).xy;
        vec2 across = vec2(-tangent.y, tangent.x) / max(length(tangent), 0.0001);
        float width = mix(0.11, 0.035, t);
        VERTEX = anchor + mat3(MODELVIEW_MATRIX) * offset + vec3(across * width * (UV.y - 0.5), 0.0);
    }
}

void fragment() {
    if (flash > 0.5) {
        vec2 p = abs(UV * 2.0 - 1.0);
        float core = pow(max(1.0 - length(p), 0.0), 3.0);
        float rays = pow(max(1.0 - max(p.x, p.y), 0.0), 2.0) * exp(-18.0 * min(p.x, p.y));
        ALBEDO = vec3(1.0, 0.73, 0.29);
        ALPHA = min(core + rays, 1.0) * (1.0 - flash_age);
    } else {
        float center = 1.0 - abs(UV.y * 2.0 - 1.0);
        float fade = (1.0 - UV.x) * (1.0 - UV.x);
        float wisp = mix(0.58, 1.0, sin(UV.x * 53.0) * 0.5 + 0.5);
        vec3 tinted_edge = mix(vec3(1.0, 0.91, 0.78), faction_color, 0.48);
        ALBEDO = mix(tinted_edge, vec3(1.0, 0.98, 0.92), fade * 0.42);
        ALPHA = center * center * fade * wisp * 0.65;
    }
}
