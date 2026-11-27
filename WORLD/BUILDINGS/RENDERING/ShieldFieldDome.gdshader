// Faction-tinted dome with a contact ripple that travels along the shell.
shader_type spatial;
render_mode unshaded, cull_back, depth_draw_never, shadows_disabled;

uniform float center_opacity : hint_range(0.0, 0.15) = 0.012;
uniform float rim_opacity : hint_range(0.0, 0.35) = 0.12;
varying float local_height;
varying vec3 dome_point;
varying flat vec4 contact;

void vertex() {
    local_height = VERTEX.y;
    dome_point = VERTEX;
    contact = INSTANCE_CUSTOM;
}

void fragment() {
    float grazing = 1.0 - clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0);
    float rim = smoothstep(0.60, 0.98, grazing);
    float base_edge = (1.0 - smoothstep(0.0, 0.018, local_height)) * 0.035;
    ALBEDO = mix(COLOR.rgb, vec3(1.0), 0.16 + rim * 0.14);
    // Slow upward-moving faction-tinted bands; the center stays nearly clear.
    float wave = pow(0.5 + 0.5 * cos(6.283185 * (local_height * 3.0 - TIME * 0.32)), 12.0);
    float band = wave * 0.027 * smoothstep(0.06, 0.25, local_height) * (1.0 - smoothstep(0.84, 1.0, local_height));
    float opacity = max(base_edge, mix(center_opacity, rim_opacity, rim)) + band;
    if (contact.w >= 1.0) {
        bool collapsing = contact.w >= 5.0;
        float weight = floor(contact.w) - (collapsing ? 4.0 : 0.0);
        float age = fract(contact.w);
        float angle = acos(clamp(dot(normalize(dome_point), normalize(contact.xyz * 2.0 - 1.0)), -1.0, 1.0));
        float ripple = (1.0 - smoothstep(0.015, 0.035 + weight * 0.012,
            abs(angle - age * (0.22 + weight * 0.20)))) * (1.0 - age);
        if (weight >= 3.0) opacity *= 0.65 + 0.35 * cos(age * 53.0) * (1.0 - age);
        if (collapsing) opacity *= (1.0 - age) * (1.0 - age);
        opacity += ripple * (0.10 + weight * 0.085);
        ALBEDO = mix(ALBEDO, vec3(1.0), ripple * 0.66);
    }
    ALPHA = opacity;
}
