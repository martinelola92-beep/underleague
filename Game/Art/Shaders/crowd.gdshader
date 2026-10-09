// La grada del partido con figuras renderizadas (pase de arte, 9 oct 2026): cada espectador es un cuadrado con una
// celda del atlas de tools/arte/publico.py. Los datos de instancia dicen qué figura es (r) y su fase (g). La postura
// cambia a saltos (de pie, brazos arriba, brazos abiertos) con un reloj de pocos fotogramas por segundo: una grada que
// se agita a lo dibujo animado, no un vídeo. `excitement` sube la proporción de brazos en alto. Sólo presentación.
shader_type spatial;
render_mode cull_disabled, unshaded, depth_draw_opaque;

uniform sampler2D atlas : source_color, filter_linear_mipmap;
uniform float per_row = 6.0;
uniform float poses = 3.0;
uniform float rows = 5.0;
uniform float excitement = 0.3;
uniform float fps = 2.5;

void vertex() {
	float figure = INSTANCE_CUSTOM.r;
	float phase = INSTANCE_CUSTOM.g;
	float tick = floor(TIME * fps + phase * 7.0);
	float h = fract(sin((tick + phase * 91.0) * 12.9898) * 43758.5453);
	float pose = h < excitement * 0.55 ? 1.0 : (h < excitement ? 2.0 : 0.0);
	float column = mod(figure, per_row) * poses + pose;
	float row = floor(figure / per_row);
	UV = (vec2(column, row) + UV) / vec2(per_row * poses, rows);
}

void fragment() {
	vec4 c = texture(atlas, UV);
	ALBEDO = c.rgb * 0.92;
	ALPHA = c.a;
	ALPHA_SCISSOR_THRESHOLD = 0.5;
}
