shader_type canvas_item;
render_mode blend_mix, unshaded;

uniform float brilho : hint_range(0.95, 1.25) = 1.08;
uniform float saturacao : hint_range(0.9, 1.4) = 1.14;
uniform float suavizacao : hint_range(0.0, 1.5) = 0.45;

vec4 sample_offset(vec2 offset) {
	return texture(TEXTURE, UV + offset);
}

void fragment() {
	vec2 px = TEXTURE_PIXEL_SIZE * suavizacao;
	vec4 center = texture(TEXTURE, UV);
	vec4 north = sample_offset(vec2(0.0, px.y));
	vec4 south = sample_offset(vec2(0.0, -px.y));
	vec4 east = sample_offset(vec2(px.x, 0.0));
	vec4 west = sample_offset(vec2(-px.x, 0.0));
	vec4 ne = sample_offset(vec2(px.x, px.y));
	vec4 nw = sample_offset(vec2(-px.x, px.y));
	vec4 se = sample_offset(vec2(px.x, -px.y));
	vec4 sw = sample_offset(vec2(-px.x, -px.y));

	vec4 blur = center * 0.36;
	blur += (north + south + east + west) * 0.135;
	blur += (ne + nw + se + sw) * 0.03;

	float alpha = center.a;
	float blendFactor = smoothstep(0.0, 0.7, alpha) * 0.75;
	vec4 t = mix(center, blur, blendFactor);

	float luma = dot(t.rgb, vec3(0.299, 0.587, 0.114));
	vec3 cor = mix(vec3(luma), t.rgb, saturacao) * brilho;
	COLOR = vec4(clamp(cor, 0.0, 1.0), t.a);
}
