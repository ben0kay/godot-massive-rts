// World-space editor overlay matching the 2.1-unit deployment cell boundaries.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled;

uniform float cell_size = 2.1;
uniform vec4 line_color : source_color = vec4(0.59, 0.75, 0.86, 0.36);
varying vec2 world_xz;

void vertex() {
    world_xz = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
}

void fragment() {
    vec2 cells = world_xz / max(cell_size, 0.01);
    vec2 pixel_cell = max(fwidth(cells), vec2(0.00001));
    // At any integer cell coordinate, the distance to its nearest line is zero.
    vec2 distance_to_line = abs(fract(cells - vec2(0.5)) - vec2(0.5));
    float line_pixels = min(distance_to_line.x / pixel_cell.x, distance_to_line.y / pixel_cell.y);
    float line = 1.0 - smoothstep(0.0, 1.15, line_pixels);
    // Avoid solid moire/noise when a grid cell becomes only a few screen pixels.
    float pixels_per_cell = 1.0 / max(pixel_cell.x, pixel_cell.y);
    float zoom_fade = smoothstep(4.0, 12.0, pixels_per_cell);
    ALBEDO = line_color.rgb;
    ALPHA = line_color.a * line * zoom_fade;
}
