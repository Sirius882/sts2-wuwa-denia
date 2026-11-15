shader_type canvas_item;
render_mode unshaded;
// Original pink_standup GLSL: 74 clamped native frames, optical flow, bicubic/CAS and thin-film bubble.
uniform sampler2D frames : source_color, filter_linear, repeat_disable;
uniform sampler2D flow_fields : filter_linear, repeat_disable;
uniform int frame_index = 0;
uniform float fraction = 0.0;
uniform bool flip_h = false;
const vec2 GRID = vec2(8.0, 10.0);
vec2 tile(int index) { return vec2(float(index % 8), float(index / 8)); }
vec4 sample_frame(int index, vec2 uv) {
    vec2 half_texel = 0.5 / vec2(504.0, 768.0);
    return texture(frames, (tile(index) + clamp(uv, half_texel, 1.0 - half_texel)) / GRID);
}
vec4 sample_flow(vec2 uv) {
    vec2 half_texel = 0.5 / vec2(63.0, 93.0);
    return texture(flow_fields, (tile(frame_index) + clamp(uv, half_texel, 1.0 - half_texel)) / GRID);
}
float smooth_range(float a, float b, float x) {
    float t = clamp((x - a) / (b - a), 0.0, 1.0);
    return t * t * (3.0 - 2.0 * t);
}


  const vec2 uRes = vec2(504.0, 768.0);
  uniform float uTime = 0.0;      // elapsed animation time (s)
  const vec4 uCropRect = vec4(0.0, 16.0, 504.0, 744.0);
  const vec3 uBubble = vec3(257.22, 539.82, 145.5);

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
    vec4 raw = clamp(mix(bic, bil, 0.56), 0.0, 1.0);
    // Un-attenuate RGB at transparent borders where hardware LINEAR filtering mixed foreground RGB with (0,0,0,0)
    vec3 cleanRgb = (raw.a > 0.03 && raw.a < 0.96) ? clamp(raw.rgb / pow(raw.a, 0.62), 0.0, 1.0) : raw.rgb;
    return vec4(cleanRgb, raw.a);
  }

  vec3 computeIridescence(vec2 px, vec3 bSphere, float t) {
    if (t > 0.70) return vec3(0.0);
    float env = 1.0 - smooth_range(0.56, 0.70, t);
    vec2 d = (px - bSphere.xy) / bSphere.z;
    float r2 = dot(d, d);
    if (r2 > 1.03) return vec3(0.0);
    float nz = sqrt(max(0.01, 1.0 - min(r2, 1.0)));
    float fresnel = pow(1.0 - nz, 2.6);
    float phase = 6.28318 * (1.8 * nz + 0.14 * d.x - 0.11 * d.y) + t * 1.6;
    vec3 specIri = 0.5 + 0.5 * cos(phase + vec3(0.0, 2.094, 4.188));
    vec3 pastelIri = mix(vec3(1.0, 0.74, 0.95), specIri, 0.52);
    float rimMask = smooth_range(0.58, 0.99, sqrt(r2)) * smooth_range(1.03, 0.95, sqrt(r2));
    return pastelIri * (fresnel * 0.10 + rimMask * 0.15) * env;
  }

  void fragment() {
    vec4 tint = COLOR;
    vec2 vUv = vec2(flip_h ? 1.0 - UV.x : UV.x, UV.y);
    vec2 px = vUv * uRes;

    // Sample 2D Optical Flow in the active crop window (0, 16, 504, 744)
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
    vec4 c1 = sampleBicubic(min(frame_index + 1, 73), uv1, uRes);

    // Contrast-Adaptive Sharpening (CAS) for razor-sharp anime line-art & staff filigree
    // Alpha-aware CAS from the remade source: sharpen interiors without ringing at transparent borders.
    vec2 stepUv = 0.80 / uRes;
    vec4 n0 = sample_frame(frame_index, uv0 + vec2(0.0, -stepUv.y));
    vec4 s0 = sample_frame(frame_index, uv0 + vec2(0.0,  stepUv.y));
    vec4 e0 = sample_frame(frame_index, uv0 + vec2( stepUv.x, 0.0));
    vec4 w0 = sample_frame(frame_index, uv0 + vec2(-stepUv.x, 0.0));
    float minA0 = min(min(n0.a, s0.a), min(e0.a, w0.a));
    vec3 lap0 = (c0.rgb * 4.0 - (n0.rgb + s0.rgb + e0.rgb + w0.rgb)) * smooth_range(0.6, 0.95, minA0);

    vec4 n1 = sample_frame(min(frame_index + 1, 73), uv1 + vec2(0.0, -stepUv.y));
    vec4 s1 = sample_frame(min(frame_index + 1, 73), uv1 + vec2(0.0,  stepUv.y));
    vec4 e1 = sample_frame(min(frame_index + 1, 73), uv1 + vec2( stepUv.x, 0.0));
    vec4 w1 = sample_frame(min(frame_index + 1, 73), uv1 + vec2(-stepUv.x, 0.0));
    float minA1 = min(min(n1.a, s1.a), min(e1.a, w1.a));
    vec3 lap1 = (c1.rgb * 4.0 - (n1.rgb + s1.rgb + e1.rgb + w1.rgb)) * smooth_range(0.6, 0.95, minA1);

    if (c0.a > 0.75) c0.rgb = clamp(c0.rgb + lap0 * 0.15, 0.0, 1.0);
    if (c1.a > 0.75) c1.rgb = clamp(c1.rgb + lap1 * 0.15, 0.0, 1.0);

    // Confidence-weighted premultiplied alpha blend
    float wBlend = fraction;
    float aDiff = c1.a - c0.a;
    // Crisp edge contour shaping from the remade animator, shared by forward and reverse playback.
    if (abs(aDiff) > 0.25) {
      wBlend = clamp(fraction + aDiff * 0.24 * sin(fraction * 3.14159), 0.0, 1.0);
    }
    float rawOutA = mix(c0.a, c1.a, wBlend);
    float outA = smooth_range(0.32, 0.82, rawOutA);
    vec3 pm0 = c0.rgb * c0.a;
    vec3 pm1 = c1.rgb * c1.a;
    vec3 outRgb = (rawOutA > 1e-4) ? mix(pm0, pm1, wBlend) / rawOutA : vec3(0.0);

    vec3 iri = computeIridescence(px, uBubble, uTime);
    outRgb = clamp(outRgb + iri * outA, 0.0, 1.0);

    COLOR = vec4(clamp(outRgb, 0.0, 1.0), clamp(outA, 0.0, 1.0)) * tint;
  }
