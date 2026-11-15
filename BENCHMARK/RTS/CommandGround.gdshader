shader_type spatial;
render_mode unshaded, shadows_disabled;

uniform float map_half_width = 2500.0;
varying vec2 ground_position;

void vertex() {
    ground_position = VERTEX.xz;
}

void fragment() {
    float distance_to_edge = map_half_width - max(abs(ground_position.x), abs(ground_position.y));
    vec3 exterior = vec3(0.065, 0.075, 0.095);
    vec3 ground = vec3(0.25, 0.26, 0.29);
    vec3 edge = vec3(0.64, 0.72, 0.81);
    float transition = max(fwidth(distance_to_edge), 0.05);
    float inside = smoothstep(-transition, transition, distance_to_edge);
    ALBEDO = mix(exterior, mix(edge, ground, smoothstep(2.0, 2.0 + transition, distance_to_edge)), inside);
}
