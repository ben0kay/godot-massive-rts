shader_type spatial;
uniform vec4 faction_tint : source_color = vec4(0.4, 0.5, 1.0, 1.0);
varying vec2 plate;
varying float top;
varying float phase;

void vertex() {
    plate = VERTEX.xz;
    top = step(0.9, NORMAL.y);
    phase = dot(MODEL_MATRIX[3].xz, vec2(0.32, 0.47));
}

void fragment() {
    float emitter = 1.0 - smoothstep(0.08, 0.14, length(abs(plate) - vec2(0.62)));
    float charge = plate.y - sin(plate.x * 14.0 + TIME * 6.0 + phase) * 0.11
        - sin(plate.x * 37.0 - TIME * 11.0) * 0.045;
    float arc = (1.0 - smoothstep(0.017, 0.052, abs(charge))) * (1.0 - smoothstep(0.58, 0.7, abs(plate.x)));
    ALBEDO = mix(vec3(0.085, 0.10, 0.15), faction_tint.rgb * 0.5, 0.2);
    ROUGHNESS = 0.68;
    EMISSION = mix(vec3(0.65, 0.56, 1.0), faction_tint.rgb, 0.45)
        * top * (emitter * 0.62 + arc * (0.45 + 0.33 * sin(TIME * 5.0 + phase)));
}
