// A short, widening smoke wake sampled along the existing flight curve.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled, skip_vertex_transform;

void vertex() {
    float t = UV.x;
    vec3 quadratic = vec3(0.0, -INSTANCE_CUSTOM.w, 0.0);
    mat3 view_basis = mat3(MODELVIEW_MATRIX);
    vec3 offset = view_basis * (t * (INSTANCE_CUSTOM.xyz + quadratic * t));
    vec2 tangent = (view_basis * (INSTANCE_CUSTOM.xyz + 2.0 * quadratic * t)).xy;
    vec2 across = vec2(-tangent.y, tangent.x) / max(length(tangent), 0.0001);
    float width = mix(0.28, 1.05, t);
    VERTEX = MODELVIEW_MATRIX[3].xyz + offset + vec3(across * width * (UV.y - 0.5), 0.0);
}

void fragment() {
    float t = UV.x;
    float cross_section = abs((UV.y - 0.5) * 2.0 + sin(t * 35.0) * 0.13);
    float billows = mix(0.56, 1.0, sin(t * 44.0) * 0.5 + 0.5);
    float fade = smoothstep(0.06, 0.22, t) * (1.0 - smoothstep(0.32, 1.0, t));
    ALBEDO = mix(vec3(0.27, 0.31, 0.38), vec3(0.46, 0.49, 0.53), billows);
    ALPHA = (1.0 - smoothstep(0.08, 0.92, cross_section)) * billows * fade * 0.43;
}
