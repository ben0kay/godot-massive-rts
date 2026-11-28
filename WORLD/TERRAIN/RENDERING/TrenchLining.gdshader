// Steel retaining panels, ribbed floor and rough asphalt ramps.
shader_type spatial;
render_mode unshaded, shadows_disabled;

uniform vec2 start;
uniform vec2 axis;
uniform vec4 dimensions;
varying vec2 trench_uv;

void vertex() {
    vec2 offset = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz - start;
    trench_uv = vec2(dot(offset, axis), dot(offset, vec2(-axis.y, axis.x)));
}

void fragment() {
    float along = trench_uv.x;
    float side = abs(trench_uv.y);
    float half_floor = dimensions.y;
    float end_distance = max(-along, along - dimensions.x);
    bool wall = end_distance <= 1.65 && side <= half_floor + 1.65;
    bool ramp = side >= half_floor - 0.03 && side <= half_floor + dimensions.z &&
        ((trench_uv.y > 0.0 && along >= 0.0 && along <= dimensions.w) ||
         (trench_uv.y < 0.0 && along >= dimensions.x - dimensions.w && along <= dimensions.x));
    if (!wall && !ramp) { discard; }
    float grain = fract(sin(dot(floor(trench_uv * 27.0), vec2(12.9898, 78.233))) * 43758.5453);
    bool floor_panel = end_distance <= 0.0 && side < half_floor;
    vec3 color = vec3(0.20, 0.24, 0.27);
    if (ramp) {
        color = mix(vec3(0.075, 0.083, 0.089), vec3(0.16, 0.17, 0.17), grain);
        if (side > half_floor + dimensions.z - 0.2) { color = vec3(0.56, 0.41, 0.20); }
    } else {
        float panel = fract(along * 0.42);
        float rib = step(0.88, fract((floor_panel ? along : side) * 1.9));
        color *= floor_panel ? 0.68 : 1.15;
        color *= 0.88 + grain * 0.20 + rib * 0.33;
        if (panel < 0.045) { color *= 0.42; }
        if (!floor_panel && side > half_floor + 1.47) { color = vec3(0.40, 0.32, 0.19); }
    }
    ALBEDO = color;
}
