// Receives Benchmark shadow maps while keeping the Steel Slate surface pattern.
shader_type spatial;

#define RECEIVE_BATTLEFIELD_SHADOWS
#include "res://WORLD/TERRAIN/RENDERING/SteelSlateSurface.gdshaderinc"

void light() {
    DIFFUSE_LIGHT += ALBEDO * LIGHT_COLOR * (0.52 / PI) * ATTENUATION;
}
