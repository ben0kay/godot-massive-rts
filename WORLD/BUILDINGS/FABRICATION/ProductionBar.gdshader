shader_type spatial;
render_mode unshaded, cull_disabled;

uniform float progress : hint_range(0.0, 1.0);
uniform bool blocked;

void vertex() {
    // Keep translation in view space and face the camera.
    vec4 view_origin = MODELVIEW_MATRIX[3];
    MODELVIEW_MATRIX = mat4(1.0);
    MODELVIEW_MATRIX[3] = view_origin;
}

void fragment() {
    bool border = UV.x < 0.015 || UV.x > 0.985 || UV.y < 0.13 || UV.y > 0.87;
    vec3 fill = blocked ? vec3(1.0, 0.49, 0.20) : vec3(0.20, 0.90, 1.0);
    ALBEDO = border ? vec3(0.30, 0.49, 0.57) :
        (UV.x <= progress ? fill : vec3(0.06, 0.09, 0.13));
}
