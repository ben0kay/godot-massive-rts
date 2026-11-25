// Directional shading and a restrained satin highlight for tower armor.
render_mode ambient_light_disabled;

void light() {
    float facing = max(dot(NORMAL, LIGHT), 0.0);
    vec3 halfway = normalize(LIGHT + VIEW);
    float highlight = pow(max(dot(NORMAL, halfway), 0.0), 20.0) * step(0.01, facing);
    DIFFUSE_LIGHT += ALBEDO * LIGHT_COLOR * (0.72 / PI) * facing * ATTENUATION;
    SPECULAR_LIGHT += mix(ALBEDO, vec3(0.78, 0.88, 1.0), 0.40) *
        LIGHT_COLOR * (0.23 / PI) * highlight * ATTENUATION;
}
