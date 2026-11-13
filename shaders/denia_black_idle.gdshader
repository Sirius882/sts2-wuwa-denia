shader_type canvas_item;
render_mode unshaded;

// Port of the supplied GLSL_FRAG. Color frames and flow are shared, time is per creature.
uniform sampler2D frames : source_color, filter_linear, repeat_disable;
uniform sampler2D flow_fields : filter_linear, repeat_disable;
uniform int frame_index = 0;
uniform float fraction = 0.0;
uniform bool flip_h = false;
uniform float clock = 0.0;
uniform vec3 bubble = vec3(265.0, 388.0, 76.0);
uniform vec2 halo = vec2(319.43, 94.53);
const vec2 RES = vec2(592.0, 600.0);
const vec2 GRID = vec2(8.0, 6.0);

vec2 tile(int index) {
    return vec2(float(index % 8), float(index / 8));
}

vec4 sample_frame(int index, vec2 uv) {
    // Clamp within each tile, including the sharpening taps, to prevent atlas bleed.
    return texture(frames, (tile(index) + clamp(uv, 0.5 / RES, 1.0 - 0.5 / RES)) / GRID);
}

// Original 4-tap B-spline bicubic + bilinear blend.
vec4 sample_bicubic(int index, vec2 uv) {
    vec2 p = uv * RES - 0.5;
    vec2 i = floor(p);
    vec2 f = p - i;
    vec2 f2 = f * f, f3 = f2 * f;
    vec2 w0 = (1.0 - 3.0 * f + 3.0 * f2 - f3) / 6.0;
    vec2 w1 = (4.0 - 6.0 * f2 + 3.0 * f3) / 6.0;
    vec2 w2 = (1.0 + 3.0 * f + 3.0 * f2 - 3.0 * f3) / 6.0;
    vec2 w3 = f3 / 6.0;
    vec2 g0 = w0 + w1, g1 = w2 + w3;
    vec2 h0 = (i + w1 / max(g0, vec2(1e-5)) - 0.5) / RES;
    vec2 h1 = (i + w3 / max(g1, vec2(1e-5)) + 1.5) / RES;
    vec4 t00 = sample_frame(index, vec2(h0.x, h0.y));
    vec4 t10 = sample_frame(index, vec2(h1.x, h0.y));
    vec4 t01 = sample_frame(index, vec2(h0.x, h1.y));
    vec4 t11 = sample_frame(index, vec2(h1.x, h1.y));
    vec4 bic = (t00 * g0.x + t10 * g1.x) * g0.y + (t01 * g0.x + t11 * g1.x) * g1.y;
    return clamp(mix(bic, sample_frame(index, uv), 0.55), 0.0, 1.0);
}

vec4 sharpened_frame(int index, vec2 uv) {
    vec4 c = sample_bicubic(index, uv);
    vec2 step_uv = 0.82 / RES;
    vec3 lap = c.rgb * 4.0
        - sample_frame(index, uv + vec2(0.0, -step_uv.y)).rgb
        - sample_frame(index, uv + vec2(0.0, step_uv.y)).rgb
        - sample_frame(index, uv + vec2(step_uv.x, 0.0)).rgb
        - sample_frame(index, uv + vec2(-step_uv.x, 0.0)).rgb;
    c.rgb = clamp(c.rgb + lap * 0.15, 0.0, 1.0);
    return c;
}

// Explicit form also preserves the source's descending smoothstep ranges.
float smooth_range(float a, float b, float x) {
    float t = clamp((x - a) / (b - a), 0.0, 1.0);
    return t * t * (3.0 - 2.0 * t);
}

vec3 dark_iridescence(vec2 px) {
    vec2 d = (px - bubble.xy) / bubble.z;
    float r2 = dot(d, d);
    if (r2 > 1.05) return vec3(0.0);
    float r = sqrt(r2);
    float nz = sqrt(max(0.01, 1.0 - min(r2, 1.0)));
    float fresnel = pow(1.0 - nz, 2.8);
    // Concentric golden-cyan water-ripple shimmer in lower-left hemisphere of dark bubble.
    float wave = sin(r * 28.0 - clock * 3.4 + d.x * 6.0) * cos(d.y * 18.0 + clock * 2.1);
    float mask = smooth_range(0.92, 0.25, r) * smooth_range(-0.2, 0.75, d.y) * smooth_range(0.45, -0.35, d.x);
    vec3 caustic = mix(vec3(0.18, 0.75, 1.0), vec3(0.95, 0.88, 0.55), 0.5 + 0.5 * wave)
        * max(0.0, wave) * mask * 0.09;
    // Electric-cyan & deep-violet Fresnel rim.
    float rim_mask = smooth_range(0.76, 0.99, r) * smooth_range(1.05, 0.96, r);
    vec3 rim = mix(vec3(0.22, 0.68, 1.0), vec3(0.68, 0.88, 1.0), rim_mask)
        * (fresnel * 0.16 + rim_mask * 0.26);
    return caustic + rim;
}

void fragment() {
    vec4 tint = COLOR;
    vec2 uv = vec2(flip_h ? 1.0 - UV.x : UV.x, UV.y);
    vec2 flow_uv = (uv * RES - vec2(76.0, 64.0)) / vec2(328.0, 536.0);
    vec2 flow_px = vec2(0.0);
    if (all(greaterThanEqual(flow_uv, vec2(0.0))) && all(lessThanEqual(flow_uv, vec2(1.0)))) {
        vec2 half_texel = 0.5 / vec2(41.0, 67.0);
        vec2 atlas_uv = (tile(frame_index) + clamp(flow_uv, half_texel, 1.0 - half_texel)) / GRID;
        flow_px = (texture(flow_fields, atlas_uv).rg * 255.0 - 128.0) * 0.25;
    }
    vec2 shift = flow_px / RES;
    vec2 uv0 = clamp(uv - fraction * shift, vec2(0.001), vec2(0.999));
    vec2 uv1 = clamp(uv + (1.0 - fraction) * shift, vec2(0.001), vec2(0.999));
    vec4 c0 = sharpened_frame(frame_index, uv0);
    vec4 c1 = sharpened_frame((frame_index + 1) % 48, uv1);
    // Original occlusion-aware, premultiplied sub-frame blend.
    float a_diff = c1.a - c0.a;
    float weight = abs(a_diff) > 0.35
        ? clamp(fraction + a_diff * 0.18 * sin(fraction * 3.14159), 0.0, 1.0) : fraction;
    float alpha = mix(c0.a, c1.a, weight);
    vec3 rgb = alpha > 1e-4 ? mix(c0.rgb * c0.a, c1.rgb * c1.a, weight) / alpha : vec3(0.0);
    rgb = clamp(rgb + dark_iridescence(uv * RES) * alpha, 0.0, 1.0);
    // Subtle Starry Halo Glow around the Eye-Halo.
    float r_halo = length((uv * RES - halo) * vec2(0.55, 1.25));
    float glow = exp(-r_halo * 0.085) * (0.14 + 0.05 * sin(clock * 3.6));
    vec3 halo_color = vec3(0.32, 0.78, 1.0) * glow;
    float bloom_alpha = clamp(glow * 0.35, 0.0, 1.0);
    float combined_alpha = clamp(alpha + bloom_alpha * (1.0 - alpha), 0.0, 1.0);
    vec3 combined_rgb = combined_alpha > 1e-4
        ? (rgb * alpha + halo_color * (1.0 - alpha * 0.4)) / combined_alpha : vec3(0.0);
    COLOR = vec4(clamp(combined_rgb, 0.0, 1.0), combined_alpha) * tint;
}
