shader_type canvas_item;
render_mode unshaded;
uniform sampler2D screen_texture : hint_screen_texture, filter_linear;
uniform vec2 center = vec2(0.5);
uniform float blur_amount = 0.0;
uniform float wash = 0.0;
uniform float zoom = 1.0;
void fragment() {
    vec2 uv = center + (SCREEN_UV - center) / zoom;
    vec2 direction = uv - center;
    vec3 c = vec3(0.0);
    for (int i = 0; i < 12; i++) {
        c += texture(screen_texture, uv - direction * blur_amount * float(i)).rgb / 12.0;
    }
    float glow = exp(-length((SCREEN_UV - center) * vec2(1.0, 0.65)) * 3.0);
    c = mix(c, vec3(0.62, 0.30, 0.98) + glow * vec3(0.40, 0.40, 0.35), wash);
    COLOR = vec4(c, 1.0);
}
