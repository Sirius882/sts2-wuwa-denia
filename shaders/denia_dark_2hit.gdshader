shader_type canvas_item;
render_mode unshaded;
// Remade dark_2hit: original GLSL interpolation and CAS; no full-canvas ambient stars.
uniform sampler2D frames : source_color, filter_linear, repeat_disable;
uniform sampler2D flow_fields : filter_linear, repeat_disable;
uniform int frame_index = 0;
uniform float fraction = 0.0;
uniform bool flip_h = false;
const vec2 NATIVE_RES = vec2(1728.0, 972.0);
const vec2 GRID = vec2(8.0, 9.0);
vec2 tile(int index) { return vec2(float(index % 8), float(index / 8)); }
vec4 sample_frame(int index, vec2 uv) {
    vec2 half_texel = 0.5 / NATIVE_RES;
    return texture(frames, (tile(index) + clamp(uv, half_texel, 1.0 - half_texel)) / GRID);
}
vec4 sample_flow(vec2 uv) {
    vec2 half_texel = 0.5 / vec2(216.0, 122.0);
    return texture(flow_fields, (tile(frame_index) + clamp(uv, half_texel, 1.0 - half_texel)) / GRID);
}
  vec4 samplePremul(int index, vec2 uv) {
    vec4 c = sample_frame(index, clamp(uv, vec2(0.0), vec2(1.0)));
    return vec4(c.rgb * c.a, c.a);
  }

  // 16-tap analytical Catmull-Rom cubic spline surface evaluation in premultiplied space
  vec4 sampleBicubic(int index, vec2 uv) {
    vec2 texel = uv * NATIVE_RES - 0.5;
    vec2 f = fract(texel);
    vec2 base = (floor(texel) + 0.5) / NATIVE_RES;
    vec2 inv = 1.0 / NATIVE_RES;

    vec2 w0 = f * (-0.5 + f * (1.0 - 0.5 * f));
    vec2 w1 = 1.0 + f * f * (-2.5 + 1.5 * f);
    vec2 w2 = f * (0.5 + f * (2.0 - 1.5 * f));
    vec2 w3 = f * f * (-0.5 + 0.5 * f);

    vec4 sum = vec4(0.0);
    float wy[4] = float[4](w0.y, w1.y, w2.y, w3.y);
    float wx[4] = float[4](w0.x, w1.x, w2.x, w3.x);
    for (int j = -1; j <= 2; j++) {
      float yw = wy[j + 1];
      float v = base.y + float(j) * inv.y;
      for (int i = -1; i <= 2; i++) {
        float u = base.x + float(i) * inv.x;
        sum += samplePremul(index, vec2(u, v)) * (wx[i + 1] * yw);
      }
    }
    sum = max(sum, vec4(0.0));
    float a = clamp(sum.a, 0.0, 1.0);
    vec3 rgb = (a > 0.002) ? clamp(sum.rgb / a, 0.0, 1.0) : vec3(0.0);
    return vec4(rgb * a, a);
  }

  vec4 render_frame(vec2 v_uv) {
    vec2 flowRaw = sample_flow( v_uv).rg;
    vec2 flowPx = (flowRaw * 255.0 - 128.0) * 0.5;
    vec2 flowUV = (flowPx / vec2(float(216), float(122))) * 1.0;

    float t = clamp(fraction, 0.0, 1.0);
    vec2 uv0 = v_uv - t * flowUV;
    vec2 uv1 = v_uv + (1.0 - t) * flowUV;

    vec4 c0 = sampleBicubic(frame_index, uv0);
    vec4 c1 = sampleBicubic((frame_index + 1) % 72, uv1);

    // Ghost-free confidence-gated temporal interpolation:
    // When c0 and c1 agree (high photometric & silhouette consistency), blend smoothly via optical flow;
    // when fast motion or disocclusion causes c0 and c1 to diverge, lock to the dominant keyframe via a steep S-curve
    // so fine facial details, hair strands, and legs never double-expose or blur!
    vec3 u0_rgb = (c0.a > 0.01) ? (c0.rgb / c0.a) : vec3(0.0);
    vec3 u1_rgb = (c1.a > 0.01) ? (c1.rgb / c1.a) : vec3(0.0);
    float rgbDiff = length(u0_rgb - u1_rgb);
    float alphaDiff = abs(c0.a - c1.a);
    float consistency = exp(-(rgbDiff * 3.5 + alphaDiff * 5.5));
    float sharpT = smoothstep(0.36, 0.64, t);
    float effT = mix(sharpT, t, consistency * 1.0);

    vec4 blended = mix(c0, c1, effT);
    if (blended.a < 0.003) {
      return vec4(0.0);
    }

    // Mathematical Gradient-Field Curvature & Contrast-Adaptive Sharpening (CAS) in premultiplied space:
    vec2 off = 0.72 / NATIVE_RES;
    vec4 n = mix(samplePremul(frame_index, uv0 + vec2(0.0, -off.y)), samplePremul((frame_index + 1) % 72, uv1 + vec2(0.0, -off.y)), effT);
    vec4 s = mix(samplePremul(frame_index, uv0 + vec2(0.0,  off.y)), samplePremul((frame_index + 1) % 72, uv1 + vec2(0.0,  off.y)), effT);
    vec4 e = mix(samplePremul(frame_index, uv0 + vec2( off.x, 0.0)), samplePremul((frame_index + 1) % 72, uv1 + vec2( off.x, 0.0)), effT);
    vec4 w = mix(samplePremul(frame_index, uv0 + vec2(-off.x, 0.0)), samplePremul((frame_index + 1) % 72, uv1 + vec2(-off.x, 0.0)), effT);

    // Unpremultiply neighborhood for artifact-free anime line-art & cel-edge sharpening
    vec3 cRgb = blended.rgb / blended.a;
    vec3 nRgb = (n.a > 0.01) ? (n.rgb / n.a) : cRgb;
    vec3 sRgb = (s.a > 0.01) ? (s.rgb / s.a) : cRgb;
    vec3 eRgb = (e.a > 0.01) ? (e.rgb / e.a) : cRgb;
    vec3 wRgb = (w.a > 0.01) ? (w.rgb / w.a) : cRgb;

    vec3 minRgb = min(cRgb, min(min(nRgb, sRgb), min(eRgb, wRgb)));
    vec3 maxRgb = max(cRgb, max(max(nRgb, sRgb), max(eRgb, wRgb)));
    vec3 lapRgb = cRgb * 4.0 - (nRgb + sRgb + eRgb + wRgb);

    // Adaptive CAS weight: stronger on fine hair strands, eyes, dress filigree & boot contours
    float contrast = length(maxRgb - minRgb);
    float casWeight = 1.0 * (0.28 + 0.16 * smoothstep(0.04, 0.35, contrast));
    vec3 sharpUnitRgb = clamp(cRgb + lapRgb * casWeight, minRgb * 0.92, maxRgb * 1.06);
    sharpUnitRgb = clamp(sharpUnitRgb, 0.0, 1.0);

    // Mathematical SDF contour anti-aliasing on the silhouette alpha field
    float lapA = blended.a * 4.0 - (n.a + s.a + e.a + w.a);
    float sdfAlpha = clamp(blended.a + lapA * 0.14, 0.0, 1.0);
    sdfAlpha = smoothstep(0.03, 0.97, sdfAlpha);

    return vec4(sharpUnitRgb, sdfAlpha);
  }
void fragment() {
    vec4 tint = COLOR;
    COLOR = render_frame(vec2(flip_h ? 1.0 - UV.x : UV.x, UV.y)) * tint;
}
