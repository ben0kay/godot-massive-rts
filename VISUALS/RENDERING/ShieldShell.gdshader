shader_type spatial;
render_mode unshaded, cull_back, depth_draw_never, shadows_disabled;

varying flat vec3 shield_state;
varying vec3 bubble_position;

void vertex() {
	shield_state = INSTANCE_CUSTOM.xyz;
	bubble_position = VERTEX;
}

void fragment() {
	float grazing = 1.0 - clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0);
	float rim = smoothstep(0.34, 0.96, grazing);
	// Two gentle waves bend the plasma bands around the unit's local bubble.
	vec3 p = bubble_position;
	float phase = TIME * 0.9 + shield_state.z * 6.283;
	float wave = sin(16.0 * p.y + 7.0 * p.x + 2.0 * sin(5.0 * p.z - phase) + phase);
	float plasma = smoothstep(0.86, 0.99, wave) * smoothstep(0.24, 0.83, grazing);

	vec3 faction = COLOR.rgb;
	float flash = shield_state.y;
	ALBEDO = mix(faction, vec3(1.0), min(0.75, 0.18 + 0.32 * rim + 0.16 * plasma + 0.20 * flash));
	float charge = shield_state.x;
	float presence = smoothstep(0.0, 0.04, charge) * mix(0.55, 1.0, sqrt(charge));
	ALPHA = min(0.85, (0.035 + 0.58 * rim + 0.18 * plasma) * presence
		+ (0.10 + 0.48 * rim) * flash);
}
