// Broadside's baked RGB mask selects neutral armor, faction paint or bright trim.
shader_type spatial;
render_mode unshaded;

varying vec3 armor_mask;
varying flat vec3 faction_tint;

void vertex() {
    armor_mask = COLOR.rgb;
    faction_tint = INSTANCE_CUSTOM.rgb;
}

void fragment() {
    // Faux bevel lighting is already baked in the mask brightness.
    vec3 steel = vec3(0.115, 0.135, 0.165);
    vec3 paint = faction_tint;
    vec3 indicator = mix(paint, vec3(1.0), 0.34);
    ALBEDO = steel * armor_mask.r + paint * armor_mask.g + indicator * armor_mask.b;
}
