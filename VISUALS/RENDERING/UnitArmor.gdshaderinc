// UV.x = 0 legacy, 1 steel, 2 paint, 3 emissive trim; UV.y = baked face brightness.
// Enhanced Light Artillery and Twin Rocket Launcher write these UVs. Instance COLOR remains unchanged.
vec3 unit_armor_albedo(vec3 tint, vec2 mask) {
    if (mask.x < 0.5) return tint;
    float shade = mask.y;
    // The existing hit-feedback path writes white RGB for one flash.
    if (min(tint.r, min(tint.g, tint.b)) > 0.99) return vec3(shade);
    if (mask.x < 1.5) return vec3(0.12, 0.15, 0.19) * shade;
    if (mask.x < 2.5) return tint * shade;
    return mix(tint, vec3(1.0), 0.32) * shade;
}

vec3 unit_armor_emission(vec3 tint, vec2 mask) {
    if (mask.x < 2.5 || min(tint.r, min(tint.g, tint.b)) > 0.99) return vec3(0.0);
    return tint * mask.y * 0.75;
}
