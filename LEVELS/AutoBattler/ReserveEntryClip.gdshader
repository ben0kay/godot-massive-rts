// Reveal only the parts of each marching reinforcement inside the battlefield.
shader_type spatial;
render_mode unshaded;

uniform float map_half_width;
varying vec2 battlefield_position;

void vertex() {
    battlefield_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
}

void fragment() {
    if (any(greaterThan(abs(battlefield_position), vec2(map_half_width)))) {
        discard;
    }
    ALBEDO = COLOR.rgb;
}
