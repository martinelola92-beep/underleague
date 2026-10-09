// Acabado «grease pencil» de la vista 3D del partido (9 oct 2026; el revisor: «3D pero con ese estilo 2D falso grease
// pencil que simule los retratos»). Pase de pantalla completa sobre la imagen ya pintada: rayado de lápiz en las zonas
// oscuras (una dirección en la penumbra, cruzado en la sombra), grano de papel y una ligera posterización hacia la
// paleta terrosa de las ilustraciones. Las rayas «hierven» (cambian a pocos fps) como un dibujo repasado. Sólo
// presentación (RT-014). Va debajo del contorno de tinta (ink_outline.gdshader).
shader_type spatial;
render_mode unshaded, depth_test_disabled, depth_draw_never, cull_disabled, blend_mix, fog_disabled;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap, repeat_disable;
uniform sampler2D paper_tex : filter_linear_mipmap, repeat_enable;
uniform vec4 ink : source_color = vec4(0.106, 0.082, 0.063, 1.0);
uniform float hatch_spacing = 5.0;
uniform float hatch_strength = 0.55;
uniform float light_band = 0.42;
uniform float dark_band = 0.26;
uniform float paper_strength = 0.12;
uniform float boil_fps = 6.0;
uniform float warmth = 0.06;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float stripes(vec2 px, vec2 dir, float spacing, float jitter) {
	float t = dot(px, normalize(dir)) / spacing + jitter;
	float d = abs(fract(t) - 0.5);
	return 1.0 - smoothstep(0.08, 0.2, d);
}

void vertex() {
	POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}

void fragment() {
	vec3 c = texture(screen_tex, SCREEN_UV).rgb;
	vec2 px = SCREEN_UV * VIEWPORT_SIZE;
	float frame = floor(TIME * boil_fps);
	float lum = dot(c, vec3(0.299, 0.587, 0.114));

	// Paleta: un punto más cálida y algo menos saturada, como tinta sobre papel.
	vec3 grey = vec3(lum);
	c = mix(c, grey, 0.12) * vec3(1.0 + warmth, 1.0, 1.0 - warmth);

	// Rayado: cada raya con un desvío propio por fotograma (el lápiz que repasa) y que se corta a tramos.
	float jitter = hash(vec2(frame, floor(px.y / 37.0))) * 0.4;
	float one = stripes(px, vec2(1.0, -1.0), hatch_spacing, jitter);
	float two = stripes(px, vec2(1.0, 1.0), hatch_spacing * 1.15, jitter * 1.7);
	float breaks = step(0.25, hash(floor(px / vec2(9.0, 3.0)) + frame));
	float shade = (1.0 - smoothstep(dark_band, light_band, lum)) * one + (1.0 - smoothstep(dark_band * 0.5, dark_band, lum)) * two;
	c = mix(c, ink.rgb, clamp(shade * breaks, 0.0, 1.0) * hatch_strength);

	// Grano de papel multiplicado.
	float grain = texture(paper_tex, px / 512.0).r / 0.93;
	c *= mix(1.0, grain, paper_strength * 4.0);

	ALBEDO = c;
}
