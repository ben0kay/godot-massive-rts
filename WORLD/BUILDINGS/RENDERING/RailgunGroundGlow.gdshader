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
    // A soft capsule with its rounded caps INSIDE the clipped beam extent.
    // Use full-beam t, not the local strip UV, to keep terrain strip seams invisible.
    // The existing width/brightness is retained in the middle. Both the halo
    // and bright center use the same radial distance, so neither ends abruptly.
    const float cap_fraction = 0.12;
    float across = UV.x * 2.0 - 1.0;
    float cap = max(0.0, 1.0 - min(t, 1.0 - t) / cap_fraction);
    float radius = length(vec2(across, cap));
    float soft = 1.0 - smoothstep(0.0, 1.0, radius);
    float center = 1.0 - smoothstep(0.0, 0.30, radius);
    ALBEDO = mix(team, vec3(1.0), 0.10 + center * 0.12);
    ALPHA = (soft * soft * 0.43 + center * 0.15) * height_fade;
}
