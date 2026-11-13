shader_type canvas_item;
render_mode unshaded;

// Port of the supplied GLSL_FRAG. Color frames and flow are shared, time is per creature.
uniform sampler2D frames : source_color, filter_linear, repeat_disable;
uniform sampler2D flow_fields : filter_linear, repeat_disable;
uniform int frame_index = 0;
uniform float fraction = 0.0;
uniform bool flip_h = false;
const vec2 RES = vec2(592.0, 600.0);
const vec2 GRID = vec2(8.0, 7.0);

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
    return clamp(mix(bic, sample_frame(index, uv), 0.58), 0.0, 1.0);
}

vec4 sharpened_frame(int index, vec2 uv) {
    vec4 c = sample_bicubic(index, uv);
    vec2 step_uv = 0.85 / RES;
    vec3 lap = c.rgb * 4.0
        - sample_frame(index, uv + vec2(0.0, -step_uv.y)).rgb
        - sample_frame(index, uv + vec2(0.0, step_uv.y)).rgb
        - sample_frame(index, uv + vec2(step_uv.x, 0.0)).rgb
        - sample_frame(index, uv + vec2(-step_uv.x, 0.0)).rgb;
    c.rgb = clamp(c.rgb + lap * 0.14, 0.0, 1.0);
    return c;
}

void fragment() {
    vec4 tint = COLOR;
    vec2 uv = vec2(flip_h ? 1.0 - UV.x : UV.x, UV.y);
    vec2 flow_uv = (uv * RES - vec2(24.0, 44.0)) / vec2(536.0, 552.0);
    vec2 flow_px = vec2(0.0);
    if (all(greaterThanEqual(flow_uv, vec2(0.0))) && all(lessThanEqual(flow_uv, vec2(1.0)))) {
        vec2 half_texel = 0.5 / vec2(67.0, 69.0);
        vec2 atlas_uv = (tile(frame_index) + clamp(flow_uv, half_texel, 1.0 - half_texel)) / GRID;
        flow_px = (texture(flow_fields, atlas_uv).rg * 255.0 - 128.0) * 0.5;
    }
    vec2 shift = flow_px / RES;
    vec2 uv0 = clamp(uv - fraction * shift, vec2(0.001), vec2(0.999));
    vec2 uv1 = clamp(uv + (1.0 - fraction) * shift, vec2(0.001), vec2(0.999));
    vec4 c0 = sharpened_frame(frame_index, uv0);
    vec4 c1 = sharpened_frame((frame_index + 1) % 56, uv1);
    // Original occlusion-aware, premultiplied sub-frame blend.
    float a_diff = c1.a - c0.a;
    float weight = abs(a_diff) > 0.22
        ? clamp((fraction + a_diff * 0.42 - 0.16) / 0.68, 0.0, 1.0) : fraction;
    // Preserve exact interval endpoints; alpha-aware bias must not leave a ghost at the loop boundary.
    weight = mix(fraction, weight, 4.0 * fraction * (1.0 - fraction));
    float alpha = mix(c0.a, c1.a, weight);
    vec3 rgb = alpha > 1e-4 ? mix(c0.rgb * c0.a, c1.rgb * c1.a, weight) / alpha : vec3(0.0);
    COLOR = vec4(clamp(rgb, 0.0, 1.0), alpha) * tint;
}
