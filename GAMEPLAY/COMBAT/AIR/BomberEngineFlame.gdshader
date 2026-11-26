shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled;

varying vec3 faction_tint;
varying float tint_weight;

void vertex() {
    faction_tint = INSTANCE_CUSTOM.rgb;
    // Mesh spans z = 0 at the hot nozzle to z = -1.2 at the tip.
    tint_weight = smoothstep(0.02, 0.50, -VERTEX.z);
}

void fragment() {
    vec3 outer_flame = mix(COLOR.rgb, faction_tint, 0.82);
    vec3 flame = mix(COLOR.rgb, outer_flame, tint_weight);
    ALBEDO = flame;
    EMISSION = flame * 0.85;
}
