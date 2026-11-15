shader_type canvas_item;
render_mode unshaded;
uniform sampler2D chroma : filter_linear, repeat_disable;
varying vec4 tint;
void vertex() { tint = COLOR; }
void fragment() {
    // Media Foundation NV12 is limited-range BT.709 for this authored HD H.264 source.
    float y = (texture(TEXTURE, UV).r - 16.0 / 255.0) * (255.0 / 219.0);
    vec2 uv = (texture(chroma, UV).rg - vec2(128.0 / 255.0)) * (255.0 / 224.0);
    vec3 rgb = vec3(y + 1.5748 * uv.y, y - 0.187324 * uv.x - 0.468124 * uv.y, y + 1.8556 * uv.x);
    COLOR = vec4(clamp(rgb, vec3(0.0), vec3(1.0)), 1.0) * tint;
}
