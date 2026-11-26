// Short, translucent exhaust haze for a healthy bomber's engine plumes.
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, shadows_disabled, depth_draw_never, skip_vertex_transform;

varying flat float age;
varying flat float seed;

void vertex() {
    age = INSTANCE_CUSTOM.x;
    seed = INSTANCE_CUSTOM.y;
    float size = mix(0.48, 0.88, age);
    VERTEX = MODELVIEW_MATRIX[3].xyz + vec3((UV - 0.5) * size, 0.0);
}

void fragment() {
    vec2 p = (UV - 0.5) * 2.0;
    p.x += 0.06 * sin(p.y * 6.0 + seed * 6.283185);
    float density = 1.0 - smoothstep(0.05, 0.95, length(p));
    ALBEDO = vec3(0.55, 0.59, 0.64);
    ALPHA = density * (1.0 - smoothstep(0.04, 1.0, age)) * 0.27;
}
