// Keeps angular fragments depth-writing while they fade through ordered coverage.
shader_type spatial;
render_mode unshaded;

void fragment() {
    if (COLOR.a < 1.0) {
        vec2 cell = mod(floor(FRAGCOORD.xy), 4.0);
        vec2 low_bit = mod(cell, 2.0);
        vec2 high_bit = floor(cell * 0.5);
        float coarse = 2.0 * abs(low_bit.x - low_bit.y) + low_bit.y;
        float fine = 2.0 * abs(high_bit.x - high_bit.y) + high_bit.y;
        if (COLOR.a <= (4.0 * coarse + fine + 0.5) / 16.0) {
            discard;
        }
    }
    ALBEDO = COLOR.rgb;
}
