shader_type canvas_item;

uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;
uniform float strength : hint_range(0.0, 1.0) = 0.32;
uniform float saturation : hint_range(0.0, 2.0) = 0.72;
uniform float contrast : hint_range(0.5, 2.0) = 1.12;
uniform vec3 tint = vec3(0.76, 0.82, 1.0);
uniform float vignette_strength : hint_range(0.0, 1.0) = 0.18;

void fragment() {
    vec4 original = texture(screen_texture, SCREEN_UV);
    vec3 color = original.rgb;

    float gray = dot(color, vec3(0.299, 0.587, 0.114));
    color = mix(vec3(gray), color, saturation);
    color = (color - 0.5) * contrast + 0.5;
    color *= tint;

    vec2 centered_uv = SCREEN_UV * 2.0 - 1.0;
    float vignette = smoothstep(1.18, 0.25, length(centered_uv));
    color *= mix(1.0 - vignette_strength, 1.0, vignette);

    color = mix(original.rgb, color, strength);
    COLOR = vec4(color, original.a);
}
