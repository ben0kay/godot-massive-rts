// Camera-facing rocket and shell glare, arcing ribbons, and muzzle flashes.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, skip_vertex_transform;

varying flat float effect;
varying flat float flash_age;
varying flat vec3 faction_color;

void vertex() {
    vec3 anchor = MODELVIEW_MATRIX[3].xyz;
    effect = INSTANCE_CUSTOM.w;
    flash_age = INSTANCE_CUSTOM.x;
    faction_color = COLOR.rgb;
    if (effect < -1.5) {
        float size = INSTANCE_CUSTOM.x;
        // Sit just in front of the projectile mesh while depth testing against scenery.
        VERTEX = anchor + vec3((UV - 0.5) * 1.55 * size, 0.32 * size);
    } else if (effect < 0.0) {
        VERTEX = anchor + vec3((UV - 0.5) * mix(0.65, 1.15, flash_age), 0.0);
    } else {
        float t = UV.x;
        vec3 quadratic = vec3(0.0, -INSTANCE_CUSTOM.w, 0.0);
        vec3 offset = t * (INSTANCE_CUSTOM.xyz + quadratic * t);
        vec2 tangent = (mat3(MODELVIEW_MATRIX) * (INSTANCE_CUSTOM.xyz + 2.0 * quadratic * t)).xy;
        vec2 across = vec2(-tangent.y, tangent.x) / max(length(tangent), 0.0001);
        float width = mix(0.26, 0.065, t);
        VERTEX = anchor + mat3(MODELVIEW_MATRIX) * offset + vec3(across * width * (UV.y - 0.5), 0.0);
    }
}

void fragment() {
    if (effect < -1.5) {
        float radius = length(UV * 2.0 - 1.0);
        float halo = 1.0 - smoothstep(0.10, 0.95, radius);
        float core = 1.0 - smoothstep(0.07, 0.33, radius);
        ALBEDO = mix(vec3(1.0, 0.30, 0.055), vec3(1.0, 0.91, 0.67), core);
        ALPHA = min(halo * halo * 0.40 + core * 0.92, 1.0);
    } else if (effect < 0.0) {
        vec2 p = abs(UV * 2.0 - 1.0);
        float core = pow(max(1.0 - length(p), 0.0), 3.0);
        float rays = pow(max(1.0 - max(p.x, p.y), 0.0), 2.0) * exp(-18.0 * min(p.x, p.y));
        ALBEDO = vec3(1.0, 0.73, 0.29);
        ALPHA = min(core + rays, 1.0) * (1.0 - flash_age);
    } else {
        float center = 1.0 - abs(UV.y * 2.0 - 1.0);
        float fade = (1.0 - UV.x) * (1.0 - UV.x);
        float wisp = mix(0.58, 1.0, sin(UV.x * 53.0) * 0.5 + 0.5);
        vec3 team_glow = mix(faction_color, vec3(1.0), 0.16);
        float head = (1.0 - smoothstep(0.0, 0.22, UV.x)) * center * center;
        ALBEDO = mix(team_glow, vec3(1.0, 0.64, 0.30), head * 0.65);
        ALPHA = center * fade * wisp * 0.92;
    }
}
