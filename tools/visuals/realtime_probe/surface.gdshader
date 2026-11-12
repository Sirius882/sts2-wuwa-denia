shader_type canvas_item;
render_mode unshaded;

// Analytic normals + local lighting demonstrate code-authored surfaces, not a production material.
uniform int profile = 0;
uniform vec3 light_direction = vec3(-0.6, -0.4, 0.8);

void fragment() {
	vec2 p = vec2(fract(UV.x * 4.0), UV.y);
	vec3 base = COLOR.rgb;
	float grain = sin(p.x * 115.0 + sin(p.y * 9.0) * 0.6);
	float fold = sin(p.x * 31.4159 + p.y * 0.8);
	vec3 normal = normalize(vec3((p.x - 0.5) * 0.9, (p.y - 0.45) * 0.28, 1.0));
	float gloss = 24.0;
	if (profile == 0) {
		normal = normalize(normal + vec3(grain * 0.12, 0.0, 0.0));
		gloss = 55.0;
	} else if (profile == 2) {
		normal = normalize(normal + vec3(fold * 0.28, 0.0, 0.0));
		gloss = 34.0;
	}
	vec3 light = normalize(light_direction);
	vec3 halfway = normalize(light + vec3(0.0, 0.0, 1.0));
	float diffuse = max(dot(normal, light), 0.0);
	float specular = pow(max(dot(normal, halfway), 0.0), gloss);
	vec3 result = base * (0.48 + diffuse * 0.53);
	result += vec3(1.0, 0.83, 0.92) * specular * (profile == 3 ? 0.18 : 0.31);
	COLOR = vec4(result, COLOR.a);
}
