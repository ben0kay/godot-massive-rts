uniform sampler2D crack_mask : filter_linear_mipmap, repeat_disable;

float surface_crack_mask(vec2 point, float stage) {
    vec3 lines = texture(crack_mask, point * 0.5 + 0.5).rgb;
    return max(lines.r, max(lines.g * step(2.0, stage), lines.b * step(3.0, stage)));
}
