// Retains faction vertex colors in the shared non-Broadside tower batches.
shader_type spatial;

#include "res://WORLD/BUILDINGS/RENDERING/TowerLighting.gdshaderinc"

void fragment() {
    ALBEDO = COLOR.rgb;
    EMISSION = ALBEDO * 0.55;
}
