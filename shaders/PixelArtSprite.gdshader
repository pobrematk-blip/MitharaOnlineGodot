shader_type canvas_item;
render_mode blend_mix, unshaded;

uniform float brilho : hint_range(1.0, 1.5) = 1.14;
uniform float saturacao : hint_range(1.0, 1.6) = 1.28;
uniform float contraste : hint_range(1.0, 1.35) = 1.1;
uniform float gamma : hint_range(0.85, 1.15) = 0.93;

void fragment() {
	vec4 tex = texture(TEXTURE, UV);
	if (tex.a < 0.01) {
		COLOR = tex;
		return;
	}

	float luma = dot(tex.rgb, vec3(0.299, 0.587, 0.114));
	vec3 cor = mix(vec3(luma), tex.rgb, saturacao);
	cor = (cor - 0.5) * contraste + 0.5;
	cor = pow(max(cor, vec3(0.0)), vec3(gamma));
	cor *= brilho;
	COLOR = vec4(clamp(cor, 0.0, 1.0), tex.a);
}
