// Shared shield reactor material. UV.x [32,33] is reserved for the core.
bool shield_reactor_surface(vec2 uv) {
    return uv.x >= 31.99 && uv.x <= 33.01;
}

float shield_reactor_energy(vec2 uv, float time) {
    float u = uv.x - 32.0;
    float v = uv.y;
    // Two helical streams rotate around the core; a second wave climbs upwards.
    float spiral = pow(0.5 + 0.5 * cos(6.283185 * (2.0 * u - v + time * 0.42)), 7.0);
    float rising = pow(0.5 + 0.5 * cos(6.283185 * (2.0 * v - time * 0.72)), 10.0);
    float pulse = 0.5 + 0.5 * sin(time * 3.1);
    return 0.28 + 0.16 * pulse + 0.66 * spiral + 0.33 * rising;
}

vec3 shield_reactor_albedo(vec3 faction, vec2 uv, float time) {
    return mix(faction, vec3(1.0), 0.30 + 0.28 * clamp(shield_reactor_energy(uv, time), 0.0, 1.0));
}

vec3 shield_reactor_emission(vec3 faction, vec2 uv, float time) {
    float energy = shield_reactor_energy(uv, time);
    return mix(faction, vec3(1.0), 0.37) * (0.48 + 1.45 * energy);
}
