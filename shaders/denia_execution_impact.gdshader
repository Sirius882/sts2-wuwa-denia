shader_type canvas_item;
render_mode unshaded;
uniform bool inverted = false;
uniform bool whiteout = false;
void fragment() {
    vec3 c = texture(TEXTURE, UV).rgb;
    float luminance = dot(c, vec3(0.2126, 0.7152, 0.0722));
    float ink = step(0.28, luminance);
    if (inverted) { ink = 1.0 - ink; }
    COLOR = vec4(vec3(whiteout ? 1.0 : ink), 1.0);
}
