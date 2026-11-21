shader_type spatial;
render_mode unshaded, cull_back, depth_draw_never, shadows_disabled;

varying flat vec2 shield_state;

void vertex() {
	shield_state = INSTANCE_CUSTOM.xy;
}

void fragment() {
	float edge = 1.0 - abs(dot(normalize(NORMAL), normalize(VIEW)));
	float rim = edge;
	vec3 tint = mix(vec3(0.30, 0.92, 1.0), COLOR.rgb, 0.10);
	ALBEDO = mix(tint, vec3(0.86, 0.99, 1.0), shield_state.y);
	float charge = shield_state.x;
	// Keep lightly charged shells readable at battlefield zoom, then fade the last sliver away.
	float presence = smoothstep(0.0, 0.04, charge) * mix(0.55, 1.0, sqrt(charge));
	ALPHA = min(0.85, (0.20 + 0.34 * rim) * presence + (0.24 + 0.30 * rim) * shield_state.y);
}
