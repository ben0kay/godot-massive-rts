// Receives Benchmark shadows while retaining the plateau's vertex colors.
shader_type spatial;

void fragment() {
    ALBEDO = COLOR.rgb;
    EMISSION = ALBEDO * 0.48;
}

void light() {
    DIFFUSE_LIGHT += ALBEDO * LIGHT_COLOR * (0.52 / PI) * ATTENUATION;
}
