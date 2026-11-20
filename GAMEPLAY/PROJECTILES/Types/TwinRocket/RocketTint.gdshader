// Colors each batched rocket using the firing unit's faction.
shader_type spatial;
render_mode unshaded;

varying vec3 faction_tint;

void vertex() {
    faction_tint = COLOR.rgb;
}

void fragment() {
    ALBEDO = mix(vec3(1.0, 0.92, 0.76), faction_tint, 0.55);
}
