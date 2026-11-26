// Soft additive ground illumination along a live, already-clipped railgun beam.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;

varying flat vec3 team;
varying flat vec4 span;

void vertex() {
    team = COLOR.rgb;
    span = INSTANCE_CUSTOM;
}

void fragment() {
    float t = mix(span.x, span.y, UV.y);
    float height_fade = mix(span.z, span.w, UV.y);
    float across = abs(UV.x * 2.0 - 1.0);
    float soft = 1.0 - smoothstep(0.0, 1.0, across);
    float center = 1.0 - smoothstep(0.0, 0.30, across);
    float ends = smoothstep(0.0, 0.045, t) * (1.0 - smoothstep(0.955, 1.0, t));
    ALBEDO = mix(team, vec3(1.0), 0.10 + center * 0.12);
    ALPHA = (soft * soft * 0.43 + center * 0.15) * height_fade * ends;
}
