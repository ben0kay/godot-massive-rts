// Dark camera-facing engine smoke; same custom data convention as muzzle smoke.
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, shadows_disabled, depth_draw_never, skip_vertex_transform;
varying flat float age;
varying flat float seed;
void vertex() {
    age = INSTANCE_CUSTOM.x;
    seed = INSTANCE_CUSTOM.y;
    float size = mix(0.85, 2.55, sqrt(clamp(age,0.0,1.0)));
    VERTEX = MODELVIEW_MATRIX[3].xyz + vec3((UV - 0.5) * size, 0.0);
}
float puff(vec2 p, vec2 center, float radius) {
    return 1.0 - smoothstep(0.20,1.0,length(p-center)/radius);
}
void fragment() {
    vec2 p = (UV-0.5)*2.0;
    float phase = seed*6.283185+age*2.1;
    p += 0.055*vec2(sin(p.y*12.0+phase),cos(p.x*13.0-phase));
    vec2 roll = vec2(cos(phase),sin(phase))*0.14;
    float density = puff(p,vec2(0.0,0.03),0.64);
    density = max(density,puff(p,vec2(-0.37,0.16)+roll,0.43)*0.87);
    density = max(density,puff(p,vec2(0.34,-0.18)-roll,0.43)*0.87);
    density = max(density,puff(p,vec2(0.08,-0.37)+roll*0.6,0.39)*0.76);
    float light = clamp(0.38-p.y*0.27+age*0.23,0.0,1.0);
    ALBEDO = mix(vec3(0.08,0.09,0.10),vec3(0.34,0.35,0.37),light);
    ALPHA = density*(0.55+0.45*smoothstep(0.0,0.12,age))*(1.0-smoothstep(0.30,1.0,age))*0.64;
}
