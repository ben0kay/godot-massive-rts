// UV.x = 0 legacy, 1 steel, 2 paint, 3 emissive trim; UV.y = baked face brightness.
// Negative kinds mark the Circle sword arm; +4 marks boots. Both retain the base armor kind.
vec3 unit_armor_albedo(vec3 tint, vec2 mask) {
    float kind = mod(abs(mask.x), 4.0);
    if (kind < 0.5) return tint;
    float shade = mask.y;
    // The existing hit-feedback path writes white RGB for one flash.
    if (min(tint.r, min(tint.g, tint.b)) > 0.99) return vec3(shade);
    if (kind < 1.5) return vec3(0.12, 0.15, 0.19) * shade;
    if (kind < 2.5) return tint * shade;
    return mix(tint, vec3(1.0), 0.32) * shade;
}

vec3 unit_armor_emission(vec3 tint, vec2 mask) {
    if (mod(abs(mask.x), 4.0) < 2.5 || min(tint.r, min(tint.g, tint.b)) > 0.99) return vec3(0.0);
    return tint * mask.y * 0.75;
}
