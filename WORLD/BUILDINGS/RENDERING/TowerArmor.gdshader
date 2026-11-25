// Retains faction vertex colors in the shared non-Broadside tower batches.
shader_type spatial;

#include "res://WORLD/BUILDINGS/RENDERING/TowerLighting.gdshaderinc"

global uniform float battlefield_ambient_fill;

void fragment() {
    ALBEDO = COLOR.rgb;
    EMISSION = ALBEDO * 0.55 * battlefield_ambient_fill;
}
