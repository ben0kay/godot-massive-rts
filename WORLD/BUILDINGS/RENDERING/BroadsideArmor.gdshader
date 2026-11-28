// Broadside's baked RGB mask selects neutral armor, faction paint or bright trim.
shader_type spatial;
#include "res://WORLD/BUILDINGS/RENDERING/TowerLighting.gdshaderinc"

#include "res://VISUALS/FEEDBACK/UNIT_HITS/SurfaceCracks.gdshaderinc"
#include "res://VISUALS/RENDERING/ShieldReactor.gdshaderinc"

varying vec3 armor_mask;
varying vec2 plate_point;
varying float plate_top;
varying flat float fracture_stage;
varying flat vec3 faction_tint;
varying flat float status_value;
varying float local_y;
uniform int status_mode = 0;

void vertex() {
    armor_mask = COLOR.rgb;
    status_value = COLOR.a;
    local_y = VERTEX.y;
    faction_tint = INSTANCE_CUSTOM.rgb;
    plate_point = VERTEX.xz * 2.0;
    plate_top = smoothstep(0.1, 0.65, NORMAL.y);
    fracture_stage = round(INSTANCE_CUSTOM.a * 3.0);
}

global uniform float battlefield_ambient_fill;

void fragment() {
    vec3 steel = vec3(0.12, 0.15, 0.19);
    vec3 paint = faction_tint;
    vec3 indicator = mix(paint, vec3(1.0), 0.30);
    vec3 steel_armor = steel * armor_mask.r;
    vec3 painted_armor = paint * armor_mask.g;
    if (fracture_stage >= 1.0 && plate_top > 0.0) {
        float crack = surface_crack_mask(plate_point, fracture_stage) * plate_top;
        float wear = 1.0 - crack * (0.56 + fracture_stage * 0.12);
        steel_armor *= wear;
        painted_armor *= wear;
    }
    ALBEDO = steel_armor + painted_armor;
    // Lift neutral steel in shade while keeping faction paint and trim distinct.
    float steel_fill = 0.78 * sqrt(battlefield_ambient_fill);
    EMISSION = steel_armor * steel_fill + painted_armor * 0.55 * battlefield_ambient_fill
        + (indicator + paint * 1.15) * armor_mask.b;

    // Damaged base lights flicker without extra per-instance state or uploads.
    if (fracture_stage >= 3.0 && armor_mask.b > 0.0) {
        float phase = TIME * 7.0 + dot(NODE_POSITION_WORLD.xz, vec2(0.37, 0.23));
        float flicker = mix(0.32, 1.0, smoothstep(-0.5, -0.25, sin(phase) * sin(phase * 2.7)));
        EMISSION -= (indicator + paint * 1.15) * armor_mask.b * (1.0 - flicker);
    }

    // The shield generator marks only its exposed central reactor via mesh UV.
    // Every other tower and ordinary armor face retains the existing mask behavior.
    if (shield_reactor_surface(UV)) {
        ALBEDO = shield_reactor_albedo(paint, UV, TIME);
        EMISSION = shield_reactor_emission(paint, UV, TIME);
    }

    // Railgun: white induction rings progress from rear to muzzle, using the
    // real recovery fraction. The other trim and faction panels stay unchanged.
    if (status_mode == 1 && armor_mask.b > 0.0 && abs(plate_point.y) < 3.0) {
        float progress = 0.5 - plate_point.y / 7.6;
        float charge = smoothstep(progress - 0.15, progress + 0.06, status_value);
        EMISSION = steel_armor * steel_fill + painted_armor * 0.55 * battlefield_ambient_fill
            + vec3(0.22 + charge * 2.2) * armor_mask.b;
    }
    // Barrage: illuminate the existing recessed socket floors, not extra meshes.
    if (status_mode == 2 && plate_top > 0.95 && local_y > 1.318 && local_y < 1.326 && armor_mask.r < 0.25) {
        vec2 p = plate_point * 0.5;
        vec2 cell = vec2(round(p.x / 0.19 + 1.5), round(p.y / 0.22 + 1.0));
        vec2 center = vec2((cell.x - 1.5) * 0.19, (cell.y - 1.0) * 0.22);
        vec2 offset = abs(p - center);
        if (cell.x >= 0.0 && cell.x < 4.0 && cell.y >= 0.0 && cell.y < 3.0 && max(offset.x, offset.y) < 0.043) {
            float packed = round(status_value * 255.0);
            float ammo = min(floor(packed / 12.0), 12.0);
            float next_socket = mod(packed, 12.0);
            float socket = cell.y * 4.0 + cell.x;
            float loaded = 1.0 - step(ammo, mod(socket - next_socket + 12.0, 12.0));
            float glow = (1.0 - smoothstep(0.022, 0.043, max(offset.x, offset.y))) * loaded;
            EMISSION += mix(paint, vec3(1.0), 0.25) * glow * 1.5;
        }
    }
}
