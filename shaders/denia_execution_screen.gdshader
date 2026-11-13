shader_type canvas_item;
render_mode unshaded;
uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear;
uniform vec2 center = vec2(0.5);
uniform float blur_amount = 0.0;
uniform float wash = 0.0;
uniform float zoom = 1.0;
uniform float entrance_time = -1.0;
const vec3 BUBBLES[9] = {
    vec3(-0.035, 0.30, 0.32), vec3(0.17, 0.88, 0.20), vec3(0.12, 0.20, 0.12),
    vec3(0.80, 0.05, 0.34), vec3(1.04, 0.68, 0.38), vec3(0.74, 0.75, 0.16),
    vec3(0.31, 0.48, 0.085), vec3(0.59, -0.10, 0.25), vec3(0.94, 0.32, 0.095)
};

float hash(float n) { return fract(sin(n * 127.1 + 311.7) * 43758.5453); }
float mist(vec2 p) {
    return 0.5 + 0.23 * sin(p.x * 5.3 + sin(p.y * 4.1))
        + 0.17 * sin(p.y * 8.2 - p.x * 3.7) + 0.10 * sin(p.x * 13.0 + p.y * 9.0);
}

void fragment() {
    vec2 uv = center + (SCREEN_UV - center) / zoom;
    vec2 direction = uv - center;
    vec3 c = vec3(0.0);
    for (int i = 0; i < 12; i++) {
        c += texture(screen_texture, uv - direction * blur_amount * float(i)).rgb / 12.0;
    }
    if (entrance_time < 0.0) {
        float glow = exp(-length((SCREEN_UV - center) * vec2(1.0, 0.65)) * 3.0);
        c = mix(c, vec3(0.62, 0.30, 0.98) + glow * vec3(0.40, 0.40, 0.35), wash);
    } else {
        float t = entrance_time;
        float aspect = SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x;
        vec2 metric = vec2(aspect, 1.0);
        vec2 p = (SCREEN_UV - vec2(0.5)) * metric;
        float cloud = mist(p * 1.7);
        float edge = smoothstep(0.10, 0.76, length((SCREEN_UV - vec2(0.51, 0.49)) * vec2(1.18, 1.55)));
        // 各处延迟与增强曲线不同，边缘先浓、中央仍透出游戏，而非匀速覆盖整屏。
        float delay = 0.08 + 0.30 * (1.0 - edge) + 0.17 * cloud;
        float growth = smoothstep(delay, delay + 1.05 - 0.27 * edge, t);
        float veil = clamp(growth * (0.12 + 0.63 * edge + 0.17 * cloud), 0.0, 0.91);
        float rose = 0.5 + 0.5 * sin(p.x * 2.9 - p.y * 3.8 + cloud * 1.8);
        vec3 tint = mix(vec3(0.49, 0.32, 0.82), vec3(0.98, 0.65, 0.83), rose);
        c = mix(c, tint, veil);
        c += vec3(0.23, 0.14, 0.24) * growth * cloud * cloud * (0.25 + edge * 0.75);

        vec2 bend = vec2(0.0);
        vec3 sheen = vec3(0.0);
        float glass = 0.0;
        // 不同深度的局部球面：折射来自背景采样偏移，反光来自薄膜边缘与弧形高光。
        for (int i = 0; i < 9; i++) {
            float id = float(i);
            float seed = hash(id + 8.0);
            float radius = BUBBLES[i].z;
            vec2 origin = BUBBLES[i].xy;
            origin += vec2(sin(t * 0.65 + id * 2.1) * 0.026, -t * mix(0.018, 0.065, seed));
            vec2 q = (SCREEN_UV - origin) * metric / radius;
            float r = length(q);
            float angle = atan(q.y, q.x);
            float birth = smoothstep(0.10 + seed * 0.34, 0.68 + seed * 0.40, t);
            float inside = 1.0 - smoothstep(0.92, 1.0, r);
            float rim = exp(-pow((r - 0.97) / 0.025, 2.0));
            float halo = exp(-pow((r - 0.98) / 0.13, 2.0));
            float crescent = pow(max(0.0, cos(angle + 2.2 + id * 0.12)), 12.0);
            float opposite = pow(max(0.0, cos(angle - 0.75)), 18.0);
            float fresnel = pow(clamp(r, 0.0, 1.0), 5.0) * inside;
            vec3 film = 0.66 + 0.34 * cos(vec3(0.0, 2.1, 4.2) + r * 15.0 - angle * 1.8 + id);
            sheen += birth * (film * rim * (0.10 + 0.36 * crescent)
                + mix(vec3(0.65, 0.72, 1.0), vec3(1.0, 0.76, 0.91), seed) * halo * (0.035 + 0.30 * crescent)
                + vec3(1.0, 0.91, 0.98) * exp(-pow((r - 0.87) / 0.044, 2.0)) * crescent * 0.80
                + vec3(0.44, 0.77, 1.0) * rim * opposite * 0.28);
            bend += birth * q / metric * inside * (0.003 + 0.013 * fresnel);
            glass += birth * inside * 0.15;
            vec2 spec = (q - vec2(-0.38, -0.70)) / vec2(0.24, 0.07);
            sheen += birth * vec3(1.0, 0.92, 0.99) * exp(-dot(spec, spec) * 1.5) * 0.36;
        }
        vec2 refracted = clamp(uv + bend, vec2(0.001), vec2(0.999));
        vec3 refraction = vec3(texture(screen_texture, refracted + bend * 0.12).r,
            texture(screen_texture, refracted).g, texture(screen_texture, refracted - bend * 0.12).b);
        c = mix(c, mix(refraction, tint, veil), clamp(glass, 0.0, 0.65));
        c += sheen;
        // 宽而柔的失焦光斑，避免把梦幻层画成规则圆圈阵列。
        for (int i = 0; i < 5; i++) {
            float id = float(i);
            vec2 origin = vec2(hash(id + 81.0), hash(id + 112.0));
            vec2 q = (SCREEN_UV - origin - vec2(sin(t + id) * 0.018, -t * 0.018)) * metric;
            float radius = mix(0.065, 0.17, hash(id + 131.0));
            float bloom = exp(-dot(q, q) / (radius * radius));
            c += bloom * vec3(0.26, 0.18, 0.27) * smoothstep(0.15 + id * 0.07, 1.35, t);
        }
    }
    COLOR = vec4(c, 1.0);
}
