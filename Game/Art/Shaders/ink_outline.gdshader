// Contorno de tinta de la vista 3D del partido (pase de arte, 9 oct 2026; «estilo grease pencil, cómic… Cult of the
// Lamb, Lucky Tower»). Cuadrado de pantalla completa: busca saltos de profundidad entre vecinos y pinta tinta donde
// los hay (siluetas de jugadores, porterías, grada). La línea «hierve»: se desplaza con un ruido suave que cambia a
// pocos fotogramas por segundo, como un trazo de lápiz redibujado. Sólo presentación (RT-014).
shader_type spatial;
render_mode unshaded, depth_test_disabled, depth_draw_never, cull_disabled, blend_mix, fog_disabled;

uniform sampler2D depth_tex : hint_depth_texture, filter_nearest, repeat_disable;
uniform vec4 ink : source_color = vec4(0.106, 0.082, 0.063, 0.55);
uniform float thickness = 1.2;
uniform float threshold = 0.06;
uniform float boil_fps = 8.0;
uniform float boil_pixels = 0.8;
uniform float boil_scale = 34.0;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

float linear_depth(vec2 uv, mat4 inv_projection) {
	float d = texture(depth_tex, uv).r;
#if CURRENT_RENDERER == RENDERER_COMPATIBILITY
	vec3 ndc = vec3(uv * 2.0 - 1.0, d * 2.0 - 1.0);
#else
	vec3 ndc = vec3(uv * 2.0 - 1.0, d);
#endif
	vec4 view = inv_projection * vec4(ndc, 1.0);
	return -view.z / view.w;
}

void vertex() {
	POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}

void fragment() {
	vec2 px = 1.0 / VIEWPORT_SIZE;
	float frame = floor(TIME * boil_fps);
	vec2 cell = SCREEN_UV * VIEWPORT_SIZE / boil_scale + vec2(frame * 17.31, frame * 7.77);
	vec2 wobble = (vec2(noise(cell), noise(cell + vec2(43.0, 11.0))) - 0.5) * 2.0 * boil_pixels * px;
	vec2 uv = SCREEN_UV + wobble;

	float d0 = linear_depth(uv, INV_PROJECTION_MATRIX);
	float t = thickness;
	float d1 = linear_depth(uv + vec2(px.x * t, 0.0), INV_PROJECTION_MATRIX);
	float d2 = linear_depth(uv - vec2(px.x * t, 0.0), INV_PROJECTION_MATRIX);
	float d3 = linear_depth(uv + vec2(0.0, px.y * t), INV_PROJECTION_MATRIX);
	float d4 = linear_depth(uv - vec2(0.0, px.y * t), INV_PROJECTION_MATRIX);
	float jump = max(max(abs(d1 - d0), abs(d2 - d0)), max(abs(d3 - d0), abs(d4 - d0))) / max(d0, 0.001);
	float edge = smoothstep(threshold, threshold * 1.8, jump);

	ALBEDO = ink.rgb;
	ALPHA = edge * ink.a;
}
