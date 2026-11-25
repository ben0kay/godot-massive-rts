// Satin highlights on batched units under the Benchmark sun.
shader_type spatial;

#define BATTLEFIELD_LIT
#include "res://VISUALS/FEEDBACK/UNIT_HITS/UnitSurface.gdshaderinc"

void light() {
    float facing = max(dot(NORMAL, LIGHT), 0.0);
    vec3 halfway = normalize(LIGHT + VIEW);
    float highlight = pow(max(dot(NORMAL, halfway), 0.0), 18.0) * step(0.01, facing);
    DIFFUSE_LIGHT += ALBEDO * LIGHT_COLOR * (0.50 / PI) * facing * ATTENUATION;
    SPECULAR_LIGHT += mix(ALBEDO, vec3(0.78, 0.88, 1.0), 0.4) *
        LIGHT_COLOR * (0.36 / PI) * highlight * ATTENUATION;
}
