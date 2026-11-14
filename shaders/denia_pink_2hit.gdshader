shader_type canvas_item;
render_mode unshaded;
// Original pink_2hit GLSL, bidirectional RGBA flow and premultiplied frame blending.
uniform sampler2D frames : source_color, filter_linear, repeat_disable;
uniform sampler2D flow_fields : filter_linear, repeat_disable;
uniform int frame_index = 0;
uniform float fraction = 0.0;
uniform bool flip_h = false;
// Source renders at 2x canonical size; flow displacements are measured in that canvas.
const vec2 uRes = vec2(1792.0, 1008.0);
const vec2 GRID = vec2(8.0, 17.0);
vec2 tile(int index) { return vec2(float(index % 8), float(index / 8)); }
vec4 sample_frame(int index, vec2 uv) {
    vec2 half_texel = 0.5 / vec2(896.0, 504.0);
    return texture(frames, (tile(index) + clamp(uv, half_texel, 1.0 - half_texel)) / GRID);
}
vec4 sample_flow(vec2 uv) {
    vec2 half_texel = 0.5 / vec2(224.0, 126.0);
    return texture(flow_fields, (tile(frame_index) + clamp(uv, half_texel, 1.0 - half_texel)) / GRID);
}


    void fragment() {
      vec4 tint = COLOR;
      vec2 vUv = vec2(flip_h ? 1.0 - UV.x : UV.x, UV.y);
      vec4 f0_raw = sample_frame(frame_index, vUv);
      vec4 f1_raw = sample_frame((frame_index + 1) % 132, vUv);

      // Measure local color + alpha difference to detect fast motion vs slow standing transitions
      float colorDiff = length(f0_raw.rgb - f1_raw.rgb);
      float alphaDiff = abs(f0_raw.a - f1_raw.a);
      float motionMag = colorDiff * 0.65 + alphaDiff * 1.15;

      // Gate optical flow smoothly so fast slashes/swings never tear silhouettes
      float flowGate = 1.0 * (1.0 - smoothstep(0.10, 0.28, motionMag));

      vec4 fl = sample_flow(vUv);
      vec2 flow01 = ((fl.rg * 255.0 - 128.0) / 127.0) * 20.0;
      vec2 flow10 = ((fl.ba * 255.0 - 128.0) / 127.0) * 20.0;

      vec2 uv0 = clamp(vUv - (fraction * flowGate) * (flow01 / uRes), vec2(0.0), vec2(1.0));
      vec2 uv1 = clamp(vUv - ((1.0 - fraction) * flowGate) * (flow10 / uRes), vec2(0.0), vec2(1.0));

      vec4 c0 = sample_frame(frame_index, uv0);
      vec4 c1 = sample_frame((frame_index + 1) % 132, uv1);

      // Guard against flow sampling across sharp silhouette boundaries
      float warpErr = abs(c0.a - f0_raw.a) + abs(c1.a - f1_raw.a);
      if (warpErr > 0.32) {
        c0 = f0_raw;
        c1 = f1_raw;
      }

      // Anti-ghosting S-curve blend weight: smooth linear on stationary pixels, crisp on fast slashes
      float sharpBlend = smoothstep(0.42, 0.58, fraction);
      float fastFactor = smoothstep(0.14, 0.34, motionMag);
      float w = mix(fraction, sharpBlend, fastFactor);

      // Premultiplied alpha interpolation to prevent dark fringe halos
      vec3 pm0 = c0.rgb * c0.a;
      vec3 pm1 = c1.rgb * c1.a;
      vec3 pm = mix(pm0, pm1, w);
      float a = mix(c0.a, c1.a, w);

      // Subtle high-frequency unsharp detail boost on high-opacity character core
      vec3 unpm = a > 0.004 ? clamp(pm / a, 0.0, 1.0) : vec3(0.0);

      COLOR = vec4(unpm, a) * tint;
    }
