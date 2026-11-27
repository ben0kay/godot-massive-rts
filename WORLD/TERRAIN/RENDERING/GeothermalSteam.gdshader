// Billboard steam with continuous dormant, pressure and eruption phases.
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled;
uniform float activity = 0.0;

void vertex() {
    vec3 right = normalize(vec3(INV_VIEW_MATRIX[0].x, 0.0, INV_VIEW_MATRIX[0].z));
    vec3 up = vec3(0.0, 1.0, 0.0);
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(vec4(right, 0.0), vec4(up, 0.0),
        vec4(cross(right, up), 0.0), MODEL_MATRIX[3]);
    VERTEX.y = (VERTEX.y + 1.5) * mix(0.24, 2.6, activity);
    VERTEX.x *= mix(0.48, 1.6, activity);
}
void fragment() {
    float height = 1.0 - UV.y;
    float shift = sin(height * 18.0 - TIME * 4.3) * 0.10 * height;
    float edge = max(0.0, 1.0 - abs(UV.x - 0.5 + shift) * 2.2);
    float billows = 0.55 + 0.45 * sin(height * 28.0 - TIME * (2.0 + activity * 9.0));
    float envelope = smoothstep(0.0, 0.10, height) * (1.0 - smoothstep(0.48, 1.0, height));
    ALBEDO = mix(vec3(0.62, 0.30, 0.14), vec3(0.76, 0.72, 0.65), smoothstep(0.0, 0.36, height));
    ALPHA = edge * envelope * mix(0.07, 0.58, activity) * billows;
}
