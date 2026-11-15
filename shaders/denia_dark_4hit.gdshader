shader_type canvas_item;
render_mode unshaded;
// Remade dark_4hit: original GLSL interpolation and CAS; no full-canvas ambient stars.
uniform sampler2D frames : source_color, filter_linear, repeat_disable;
uniform sampler2D flow_fields : filter_linear, repeat_disable;
uniform int frame_index = 0;
uniform float fraction = 0.0;
uniform bool flip_h = false;
const vec2 NATIVE_RES = vec2(1536.0, 864.0);
const vec2 GRID = vec2(8.0, 15.0);
vec2 tile(int index) { return vec2(float(index % 8), float(index / 8)); }
vec4 sample_frame(int index, vec2 uv) {
    vec2 half_texel = 0.5 / NATIVE_RES;
    return texture(frames, (tile(index) + clamp(uv, half_texel, 1.0 - half_texel)) / GRID);
}
vec4 sample_flow(vec2 uv) {
    vec2 half_texel = 0.5 / vec2(80.0, 45.0);
    return texture(flow_fields, (tile(frame_index) + clamp(uv, half_texel, 1.0 - half_texel)) / GRID);
}
    vec4 render_frame(vec2 vUv) {
      vec4 f0_raw = sample_frame(frame_index, vUv);
      vec4 f1_raw = sample_frame((frame_index + 1) % 116, vUv);

      float colorDiff = length(f0_raw.rgb - f1_raw.rgb);
      float alphaDiff = abs(f0_raw.a - f1_raw.a);
      float motionMag = colorDiff * 0.75 + alphaDiff * 1.25;

      float flowGate = 1.0 * (1.0 - smoothstep(0.035, 0.12, motionMag));

      vec4 fl = sample_flow( vUv);
      vec2 flow01 = ((fl.rg * 255.0 - 128.0) / 127.0) * 16.0;
      vec2 flow10 = ((fl.ba * 255.0 - 128.0) / 127.0) * 16.0;

      vec2 uv0 = clamp(vUv - (fraction * flowGate) * (flow01 / NATIVE_RES), vec2(0.0), vec2(1.0));
      vec2 uv1 = clamp(vUv - ((1.0 - fraction) * flowGate) * (flow10 / NATIVE_RES), vec2(0.0), vec2(1.0));

      vec4 c0 = sample_frame(frame_index, uv0);
      vec4 c1 = sample_frame((frame_index + 1) % 116, uv1);

      float warpErr = abs(c0.a - f0_raw.a) + abs(c1.a - f1_raw.a);
      if (warpErr > 0.18) {
        c0 = f0_raw;
        c1 = f1_raw;
      }

      float sharpBlend = smoothstep(0.42, 0.58, fraction);
      float fastFactor = smoothstep(0.04, 0.14, motionMag);
      float w = mix(fraction, sharpBlend, fastFactor);

      vec3 pm0 = c0.rgb * c0.a;
      vec3 pm1 = c1.rgb * c1.a;
      vec3 pm = mix(pm0, pm1, w);
      float a = mix(c0.a, c1.a, w);

      vec3 unpm = a > 0.004 ? clamp(pm / a, 0.0, 1.0) : vec3(0.0);

      // 5-Tap FidelityFX-Style Contrast-Adaptive Sharpening (CAS) at 1920x1080 Full HD
      if (a > 0.05) {
        vec2 texel = 1.0 / NATIVE_RES;
        vec3 dC = (fraction < 0.5) ? f0_raw.rgb : f1_raw.rgb;
        vec3 dN = (fraction < 0.5) ? sample_frame(frame_index, vUv + vec2(0.0, -texel.y)).rgb : sample_frame((frame_index + 1) % 116, vUv + vec2(0.0, -texel.y)).rgb;
        vec3 dS = (fraction < 0.5) ? sample_frame(frame_index, vUv + vec2(0.0,  texel.y)).rgb : sample_frame((frame_index + 1) % 116, vUv + vec2(0.0,  texel.y)).rgb;
        vec3 dW = (fraction < 0.5) ? sample_frame(frame_index, vUv + vec2(-texel.x, 0.0)).rgb : sample_frame((frame_index + 1) % 116, vUv + vec2(-texel.x, 0.0)).rgb;
        vec3 dE = (fraction < 0.5) ? sample_frame(frame_index, vUv + vec2( texel.x, 0.0)).rgb : sample_frame((frame_index + 1) % 116, vUv + vec2( texel.x, 0.0)).rgb;
        vec3 highFreq = dC - 0.25 * (dN + dS + dW + dE);
        float casWeight = 0.58 * smoothstep(0.05, 0.55, a);
        unpm = clamp(unpm + highFreq * casWeight, 0.0, 1.0);
      }

      return vec4(unpm, a);
    }

void fragment() {
    vec4 tint = COLOR;
    COLOR = render_frame(vec2(flip_h ? 1.0 - UV.x : UV.x, UV.y)) * tint;
}
