shader_type canvas_item;
render_mode unshaded;
// Port of dark_1hit_animator's original GLSL; 96 full-canvas frames, no effect crop.
uniform sampler2D frames : source_color, filter_linear, repeat_disable;
uniform sampler2D flow_fields : filter_linear, repeat_disable;
uniform int frame_index = 0;
uniform float fraction = 0.0;
uniform bool flip_h = false;
const vec2 GRID = vec2(8.0, 12.0);
vec2 tile(int index) { return vec2(float(index % 8), float(index / 8)); }
vec4 sample_frame(int index, vec2 uv) {
    vec2 res = vec2(960.0, 480.0);
    return texture(frames, (tile(index) + clamp(uv, 0.5 / res, 1.0 - 0.5 / res)) / GRID);
}
vec4 sample_flow(vec2 uv) {
    vec2 half_texel = 0.5 / vec2(120.0, 60.0);
    return texture(flow_fields, (tile(frame_index) + clamp(uv, half_texel, 1.0 - half_texel)) / GRID);
}
float smooth_range(float a, float b, float x) {
    float t = clamp((x - a) / (b - a), 0.0, 1.0);
    return t * t * (3.0 - 2.0 * t);
}


  const vec2 uRes = vec2(960.0, 480.0);

  uniform float uTime = 0.0;        // elapsed animation time (s)
  uniform vec4 uBubble = vec4(479.89, 349.12, 34.77, 1.0);       // (bubbleCx, bubbleCy, bubbleR, bubbleAlpha)
  uniform vec2 uHalo = vec2(504.0, 215.0);         // (haloX, haloY) in pixels
  uniform float uSlashAlpha = 0.0;  // 0..1 during giant dark claw phase
  const vec4 uCropRect = vec4(0.0, 0.0, 960.0, 480.0);

  // 4-tap B-Spline / Catmull-Rom Bicubic Filter
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
    return clamp(mix(bic, bil, 0.55), 0.0, 1.0);
  }

  // Phase-Aware Dark-Abyss Bubble Electric-Cyan Fresnel & Caustic Water-Ripple Shimmer
  vec3 computeDarkBubbleIridescence(vec2 px, vec4 bSphere, float t) {
    if (bSphere.w <= 0.01) return vec3(0.0);
    vec2 d = (px - bSphere.xy) / max(bSphere.z, 1.0);
    float r2 = dot(d, d);
    if (r2 > 1.05) return vec3(0.0);
    float r = sqrt(r2);
    float nz = sqrt(max(0.01, 1.0 - min(r2, 1.0)));
    float fresnel = pow(1.0 - nz, 2.8);
    float rippleWave = sin(r * 28.0 - t * 3.4 + d.x * 6.0) * cos(d.y * 18.0 + t * 2.1);
    float causticMask = smooth_range(0.92, 0.25, r) * smooth_range(-0.2, 0.75, d.y) * smooth_range(0.45, -0.35, d.x);
    vec3 causticCol = mix(vec3(0.18, 0.75, 1.0), vec3(0.95, 0.88, 0.55), 0.5 + 0.5 * rippleWave) * max(0.0, rippleWave) * causticMask * 0.09;
    float rimMask = smooth_range(0.76, 0.99, r) * smooth_range(1.05, 0.96, r);
    vec3 rimCol = mix(vec3(0.22, 0.68, 1.0), vec3(0.68, 0.88, 1.0), rimMask) * (fresnel * 0.16 + rimMask * 0.26);
    return (causticCol + rimCol) * bSphere.w;
  }

  void fragment() {
    vec4 tint = COLOR;
    vec2 vUv = vec2(flip_h ? 1.0 - UV.x : UV.x, UV.y);
    vec2 px = vUv * uRes;
    vec2 flowUv = (px - uCropRect.xy) / uCropRect.zw;
    vec2 flowPx = vec2(0.0);
    if (flowUv.x >= 0.0 && flowUv.x <= 1.0 && flowUv.y >= 0.0 && flowUv.y <= 1.0) {
      vec2 rawRG = sample_flow(flowUv).rg;
      flowPx = (rawRG * 255.0 - 128.0) * 0.25;
    }
    // Sample unwarped first to measure inter-frame silhouette/color change (confidence gating against ghosting/tearing)
    vec4 base0 = sample_frame(frame_index, vUv);
    vec4 base1 = sample_frame((frame_index + 1) % 96, vUv);
    float motionDiff = max(abs(base1.a - base0.a), length(base1.rgb * base1.a - base0.rgb * base0.a) * 1.15);
    float flowGate = 1.0 - smooth_range(0.08, 0.22, motionDiff);
    vec2 flowShiftUv = (flowPx * flowGate) / uRes;

    vec2 uv0 = clamp(vUv - fraction * flowShiftUv, vec2(0.001), vec2(0.999));
    vec2 uv1 = clamp(vUv + (1.0 - fraction) * flowShiftUv, vec2(0.001), vec2(0.999));

    vec4 c0 = sampleBicubic(frame_index, uv0, uRes);
    vec4 c1 = sampleBicubic((frame_index + 1) % 96, uv1, uRes);

    // Contrast-Adaptive Sharpening (CAS)
    vec2 stepUv = 0.82 / uRes;
    vec4 n0 = sample_frame(frame_index, uv0 + vec2(0.0, -stepUv.y));
    vec4 s0 = sample_frame(frame_index, uv0 + vec2(0.0,  stepUv.y));
    vec4 e0 = sample_frame(frame_index, uv0 + vec2( stepUv.x, 0.0));
    vec4 w0 = sample_frame(frame_index, uv0 + vec2(-stepUv.x, 0.0));
    vec3 lap0 = c0.rgb * 4.0 - (n0.rgb + s0.rgb + e0.rgb + w0.rgb);

    vec4 n1 = sample_frame((frame_index + 1) % 96, uv1 + vec2(0.0, -stepUv.y));
    vec4 s1 = sample_frame((frame_index + 1) % 96, uv1 + vec2(0.0,  stepUv.y));
    vec4 e1 = sample_frame((frame_index + 1) % 96, uv1 + vec2( stepUv.x, 0.0));
    vec4 w1 = sample_frame((frame_index + 1) % 96, uv1 + vec2(-stepUv.x, 0.0));
    vec3 lap1 = c1.rgb * 4.0 - (n1.rgb + s1.rgb + w1.rgb + e1.rgb);

    c0.rgb = clamp(c0.rgb + lap0 * 0.16, 0.0, 1.0);
    c1.rgb = clamp(c1.rgb + lap1 * 0.16, 0.0, 1.0);

    // Anti-ghosting temporal blend: smooth linear blend on subtle interior motion, crisp S-curve handoff on fast moving silhouettes
    float sharpFrac = smooth_range(0.42, 0.58, fraction);
    float fastWeight = smooth_range(0.10, 0.24, motionDiff);
    float wBlend = mix(fraction, sharpFrac, fastWeight);
    float outA = mix(c0.a, c1.a, wBlend);
    vec3 pm0 = c0.rgb * c0.a;
    vec3 pm1 = c1.rgb * c1.a;
    vec3 charRgb = (outA > 1e-4) ? mix(pm0, pm1, wBlend) / outA : vec3(0.0);

    // Add Dark-Abyss Bubble Electric-Cyan Iridescence & Water-Ripple Caustics when bubble is active
    vec3 iri = computeDarkBubbleIridescence(px, uBubble, uTime);
    charRgb = clamp(charRgb + iri * outA, 0.0, 1.0);

    // Subtle Abyssal Violet-Cyan Rim Boost on Giant Dark Claw & Character during 1-Hit Strike (uSlashAlpha)
    if (uSlashAlpha > 0.01 && outA > 0.05) {
      float edgeFactor = smooth_range(0.95, 0.25, outA) * smooth_range(0.02, 0.35, outA);
      vec3 slashRim = mix(vec3(0.35, 0.85, 1.0), vec3(0.92, 0.32, 0.88), 0.5 + 0.5 * sin(px.y * 0.06 - uTime * 12.0));
      charRgb = clamp(charRgb + slashRim * edgeFactor * uSlashAlpha * 0.38, 0.0, 1.0);
    }

    // Starry Halo Glow around the Eye-Halo (uHalo)
    vec2 dHalo = (px - uHalo) * vec2(0.95, 2.15);
    float rHalo = length(dHalo);
    float haloGlow = exp(-rHalo * 0.14) * (0.14 + 0.05 * sin(uTime * 3.6));
    vec3 haloCol = vec3(0.32, 0.78, 1.0) * haloGlow;

    float bloomA = clamp(haloGlow * 0.35, 0.0, 1.0);
    float combA = clamp(outA + bloomA * (1.0 - outA), 0.0, 1.0);
    vec3 combRgb = (combA > 1e-4) ? (charRgb * outA + haloCol * (1.0 - outA * 0.4)) / combA : vec3(0.0);

    COLOR = vec4(clamp(combRgb, 0.0, 1.0), combA) * tint;
  }