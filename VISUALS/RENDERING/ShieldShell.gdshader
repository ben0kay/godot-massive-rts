shader_type spatial;
render_mode unshaded, cull_back, depth_draw_never, shadows_disabled;

varying flat vec2 shield_state;

void vertex() {
	shield_state = INSTANCE_CUSTOM.xy;
}

void fragment() {
	float edge = 1.0 - abs(dot(normalize(NORMAL), normalize(VIEW)));
	float rim = edge * edge;
	vec3 tint = mix(vec3(0.28, 0.82, 0.96), COLOR.rgb, 0.25);
	ALBEDO = mix(tint, vec3(0.86, 0.99, 1.0), shield_state.y);
	ALPHA = (0.05 + 0.21 * rim) * shield_state.x + (0.19 + 0.31 * rim) * shield_state.y;
}
