shader_type canvas_item;
render_mode unshaded;
// Supplied pink_1hit GLSL: bidirectional optical flow, bicubic/CAS and premultiplied alpha.
uniform sampler2D frames : source_color, filter_linear, repeat_disable;
uniform sampler2D flow_fields : filter_linear, repeat_disable;
uniform int frame_index = 0;
uniform float fraction = 0.0;
uniform bool flip_h = false;
const vec2 uRes = vec2(744.0, 540.0);
const vec4 uCropRect = vec4(24.0, 12.0, 712.0, 464.0);
const vec2 GRID = vec2(8.0, 6.0);
vec2 tile(int index) { return vec2(float(index % 8), float(index / 8)); }
vec4 sample_frame(int index, vec2 uv) {
    return texture(frames, (tile(index) + clamp(uv, 0.5 / uRes, 1.0 - 0.5 / uRes)) / GRID);
}
vec4 sample_flow(vec2 uv) {
    vec2 half_texel = 0.5 / vec2(89.0, 58.0);
    return texture(flow_fields, (tile(frame_index) + clamp(uv, half_texel, 1.0 - half_texel)) / GRID);
}
  // 4-Tap B-Spline / Catmull-Rom Bicubic Filter (modeled on gBicubic in 几猫国产3A大作_V1.html)
  vec4 sampleBicubic(int index, vec2 uv, vec2 res) {
    vec2 p = uv * res - 0.5;
    vec2 i = floor(p);
    vec2 f = p - i;
    vec2 f2 = f * f, f3 = f2 * f;
    vec2 w0 = (1.0 - 3.0 * f + 3.0 * f2 - f3) / 6.0;
    vec2 w1 = (4.0 - 6.0 * f2 + 3.0 * f3) / 6.0;
    vec2 w2 = (1.0 + 3.0 * f + 3.0 * f2 - 3.0 * f3) / 6.0;
    vec2 w3 = f3 / 6.0;
    vec2 g0 = w0 + w1, g1 = w2 + w3;
    vec2 h0 = (i + w1 / max(g0, vec2(1e-5)) - 0.5) / res;
    vec2 h1 = (i + w3 / max(g1, vec2(1e-5)) + 1.5) / res;
    vec4 t00 = sample_frame(index, vec2(h0.x, h0.y));
    vec4 t10 = sample_frame(index, vec2(h1.x, h0.y));
    vec4 t01 = sample_frame(index, vec2(h0.x, h1.y));
    vec4 t11 = sample_frame(index, vec2(h1.x, h1.y));
    vec4 bic = (t00 * g0.x + t10 * g1.x) * g0.y + (t01 * g0.x + t11 * g1.x) * g1.y;
    vec4 bil = sample_frame(index, uv);
    return clamp(mix(bic, bil, 0.56), 0.0, 1.0);
  }

  void fragment() {
    vec4 tint = COLOR;
    vec2 vUv = vec2(flip_h ? 1.0 - UV.x : UV.x, UV.y);
    vec2 px = vUv * uRes;

    // Sample 2D Optical Flow in the active crop window (24, 12, 712, 464)
    vec2 flowUv = (px - uCropRect.xy) / uCropRect.zw;
    vec2 flowPx = vec2(0.0);
    if (flowUv.x >= 0.0 && flowUv.x <= 1.0 && flowUv.y >= 0.0 && flowUv.y <= 1.0) {
      vec2 rawRG = sample_flow(flowUv).rg;
      flowPx = (rawRG * 255.0 - 128.0) * 0.25;
    }
    vec2 flowShiftUv = flowPx / uRes;

    // Bidirectional Optical-Flow Warped UVs for Frame f0 and Frame f1
    vec2 uv0 = clamp(vUv - fraction * flowShiftUv, vec2(0.001), vec2(0.999));
    vec2 uv1 = clamp(vUv + (1.0 - fraction) * flowShiftUv, vec2(0.001), vec2(0.999));

    vec4 c0 = sampleBicubic(frame_index, uv0, uRes);
    vec4 c1 = sampleBicubic(min(frame_index + 1, 47), uv1, uRes);

    // Contrast-Adaptive Sharpening (CAS) for razor-sharp anime line-art & staff filigree
    vec2 stepUv = 0.82 / uRes;
    vec4 n0 = sample_frame(frame_index, uv0 + vec2(0.0, -stepUv.y));
    vec4 s0 = sample_frame(frame_index, uv0 + vec2(0.0,  stepUv.y));
    vec4 e0 = sample_frame(frame_index, uv0 + vec2( stepUv.x, 0.0));
    vec4 w0 = sample_frame(frame_index, uv0 + vec2(-stepUv.x, 0.0));
    vec3 lap0 = c0.rgb * 4.0 - (n0.rgb + s0.rgb + e0.rgb + w0.rgb);

    vec4 n1 = sample_frame(min(frame_index + 1, 47), uv1 + vec2(0.0, -stepUv.y));
    vec4 s1 = sample_frame(min(frame_index + 1, 47), uv1 + vec2(0.0,  stepUv.y));
    vec4 e1 = sample_frame(min(frame_index + 1, 47), uv1 + vec2( stepUv.x, 0.0));
    vec4 w1 = sample_frame(min(frame_index + 1, 47), uv1 + vec2(-stepUv.x, 0.0));
    vec3 lap1 = c1.rgb * 4.0 - (n1.rgb + s1.rgb + e1.rgb + w1.rgb);

    c0.rgb = clamp(c0.rgb + lap0 * 0.14, 0.0, 1.0);
    c1.rgb = clamp(c1.rgb + lap1 * 0.14, 0.0, 1.0);

    // Confidence-weighted premultiplied alpha blend
    float wBlend = fraction;
    float aDiff = c1.a - c0.a;
    if (abs(aDiff) > 0.35) {
      wBlend = clamp(fraction + aDiff * 0.16 * sin(fraction * 3.14159), 0.0, 1.0);
    }
    float outA = mix(c0.a, c1.a, wBlend);
    vec3 pm0 = c0.rgb * c0.a;
    vec3 pm1 = c1.rgb * c1.a;
    vec3 outRgb = (outA > 1e-4) ? mix(pm0, pm1, wBlend) / outA : vec3(0.0);

    COLOR = vec4(clamp(outRgb, 0.0, 1.0), clamp(outA, 0.0, 1.0)) * tint;
  }