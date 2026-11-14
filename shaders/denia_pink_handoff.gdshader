shader_type canvas_item;
render_mode unshaded;
uniform sampler2D first : filter_nearest, repeat_disable;
uniform sampler2D second : filter_nearest, repeat_disable;
uniform float weight = 0.0;
void fragment() {
    // 透明视口读回为预乘色，直接混合后只做一次背景合成，避免半透明叠播导致人物变暗。
    vec4 color = mix(texture(first, UV), texture(second, UV), weight);
    if (color.a > 0.0001) color.rgb /= color.a;
    COLOR *= color;
}
