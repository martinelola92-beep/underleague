// Figura de papel del partido (prototipo 2D, 9 oct 2026): el recorte ilustrado, siempre de cara a la cámara. El rival
// se distingue tiñendo de rojo el azul de la camiseta (los recortes salen de hojas de modelo con el equipo propio, azul
// y oro). Sin luz de escena: la ilustración ya trae su sombreado. Sólo presentación (RT-014).
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_opaque;

uniform sampler2D tex : source_color, filter_linear_mipmap;
uniform bool flip = false;
uniform bool rival = false;
uniform bool silhouette = false;
uniform float opacity = 1.0;

vec3 rgb2hsv(vec3 c) {
	vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
	vec4 p = mix(vec4(c.bg, K.wz), vec4(c.gb, K.xy), step(c.b, c.g));
	vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));
	float d = q.x - min(q.w, q.y);
	float e = 1.0e-10;
	return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

vec3 hsv2rgb(vec3 c) {
	vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
	return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

void fragment() {
	vec2 uv = flip ? vec2(1.0 - UV.x, UV.y) : UV;
	vec4 c = texture(tex, uv);
	if (c.a < 0.5) {
		discard;
	}
	vec3 col = c.rgb;
	if (rival) {
		vec3 hsv = rgb2hsv(col);
		// Azules de la camiseta (tono 0,52-0,7, algo saturados) → gules.
		float blue = smoothstep(0.5, 0.55, hsv.x) * (1.0 - smoothstep(0.68, 0.74, hsv.x)) * smoothstep(0.12, 0.25, hsv.y);
		vec3 red = hsv2rgb(vec3(0.995, clamp(hsv.y * 1.8, 0.62, 0.9), min(hsv.z * 1.15, 1.0)));
		col = mix(col, red, blue);
	}
	ALBEDO = silhouette ? vec3(0.0) : col;
	ALPHA = opacity;
	ALPHA_SCISSOR_THRESHOLD = 0.5;
}
