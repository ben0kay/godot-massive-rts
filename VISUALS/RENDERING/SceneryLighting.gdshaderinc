// Matte sun response for batched scenery. Materials provide their own ambient floor.
void light() {
    float facing = max(dot(NORMAL, LIGHT), 0.0);
    DIFFUSE_LIGHT += ALBEDO * LIGHT_COLOR * (0.72 / PI) * facing * ATTENUATION;
}
