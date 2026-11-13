shader_type canvas_item;
render_mode unshaded;
// Original black rise GLSL: single play, liquid bubble alignment, transparency only.
uniform sampler2D frames : source_color, filter_linear, repeat_disable;
uniform sampler2D flow_fields : filter_linear, repeat_disable;
uniform int frame_index = 0;
uniform float uFrac = 0.0;
uniform bool flip_h = false;
uniform float uTime = 0.0;
uniform vec4 uBub0;
uniform vec4 uBub1;
uniform vec2 uHalo;
const vec2 uRes = vec2(592.0, 600.0);
const vec4 uCropRect = vec4(64.0, 24.0, 488.0, 544.0);
const vec2 GRID = vec2(8.0, 6.0);
vec2 tile(int index) { return vec2(float(index % 8), float(index / 8)); }
vec4 sample_frame(int index, vec2 uv) {
    return texture(frames, (tile(index) + clamp(uv, 0.5 / uRes, 1.0 - 0.5 / uRes)) / GRID);
}
vec4 sample_flow(vec2 uv) {
    vec2 half_texel = 0.5 / vec2(61.0, 68.0);
    return texture(flow_fields, (tile(frame_index) + clamp(uv, half_texel, 1.0 - half_texel)) / GRID);
}
  // 4-tap B-Spline / Catmull-Rom Bicubic Filter (from 几猫国产3A大作_V1.html gBicubic)
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

    // Per-pixel exact radial affine bubble alignment during liquid squash-and-stretch (f0 >= 26)
    float wBubRegion = 0.0;
    float dBub = 99.0;
    if (uBub0.z > 10.0 && uBub1.z > 10.0) {
      vec2 bCenter = mix(uBub0.xy, uBub1.xy, uFrac);
      vec2 bRad    = mix(uBub0.zw, uBub1.zw, uFrac);
      vec2 normP   = (px - bCenter) / bRad;
      dBub = length(normP);
      vec2 bubFlowPx = (uBub1.xy - uBub0.xy) + normP * (uBub1.zw - uBub0.zw);
      wBubRegion = clamp((1.26 - dBub) * 6.0, 0.0, 1.0) * clamp((bCenter.x + 38.0 - px.x) / 14.0, 0.0, 1.0);
      flowPx = mix(flowPx, bubFlowPx, wBubRegion);
    }

    vec2 flowShiftUv = flowPx / uRes;
    vec2 uv0 = clamp(vUv - uFrac * flowShiftUv, vec2(0.001), vec2(0.999));
    vec2 uv1 = clamp(vUv + (1.0 - uFrac) * flowShiftUv, vec2(0.001), vec2(0.999));

    vec4 c0 = sampleBicubic(frame_index, uv0, uRes);
    vec4 c1 = sampleBicubic(min(frame_index + 1, 47), uv1, uRes);

    // Contrast-Adaptive Sharpening (CAS)
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

    float wBlend = uFrac;
    float aDiff = c1.a - c0.a;
    if (uBub0.z < 5.0 && uBub1.z > 10.0) {
      // Frame 25 -> 26 transition: snap newly spawned bubble cleanly so it never ghosts at 50% opacity
      vec2 d26 = (px - uBub1.xy) / uBub1.zw;
      if (length(d26) <= 1.12 && c0.a < 0.25) {
        wBlend = smoothstep(0.42, 0.58, uFrac);
      }
    } else if (wBubRegion > 0.2 && dBub > 0.86) {
      // Crisp rim lock during liquid oscillation
      wBlend = smoothstep(0.32, 0.68, uFrac);
    } else if (abs(aDiff) > 0.35) {
      wBlend = clamp(uFrac + aDiff * 0.18 * sin(uFrac * 3.14159), 0.0, 1.0);
    }

    float outA = mix(c0.a, c1.a, wBlend);
    vec3 pm0 = c0.rgb * c0.a;
    vec3 pm1 = c1.rgb * c1.a;
    vec3 charRgb = (outA > 1e-4) ? mix(pm0, pm1, wBlend) / outA : vec3(0.0);

    // Subtle Starlight Halo Glow around Hovering Eye-Halo (uHalo)
    vec2 dHalo = (px - uHalo) * vec2(0.55, 1.25);
    float rHalo = length(dHalo);
    float haloGlow = exp(-rHalo * 0.085) * (0.12 + 0.04 * sin(uTime * 3.6));
    vec3 haloCol = vec3(0.32, 0.78, 1.0) * haloGlow;

    float bloomA = clamp(haloGlow * 0.30, 0.0, 1.0);
    float combA = clamp(outA + bloomA * (1.0 - outA), 0.0, 1.0);
    vec3 combRgb = (combA > 1e-4) ? (charRgb * outA + haloCol * (1.0 - outA * 0.4)) / combA : vec3(0.0);
    COLOR = vec4(clamp(combRgb, 0.0, 1.0), combA) * tint;
  }