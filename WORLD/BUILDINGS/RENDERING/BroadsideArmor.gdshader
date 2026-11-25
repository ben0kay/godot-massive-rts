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
    vec3 steel = vec3(0.12, 0.15, 0.19);
    vec3 paint = faction_tint;
    vec3 indicator = mix(paint, vec3(1.0), 0.30);
    ALBEDO = steel * armor_mask.r + paint * armor_mask.g + indicator * armor_mask.b;
    // HDR trim only; no lights, shadows, extra passes or per-instance updates.
    // A visible bloom halo depends on the active WorldEnvironment glow settings.
    EMISSION = paint * armor_mask.b * 1.15;
}
