// Material del partido para los modelos de raza (Game/Ui/PlayerModel.cs, Dress).
// - La camisa (o los calzones del orco) se tiñe del color del equipo: la máscara va en el ALFA de la textura del
//   traje (tools/arte/razas.py); el alfa no es transparencia.
// - La piel del no-muerto: desaturada y multiplicada por un gris verdoso.
// - La atenuación del corte de teletransporte (BA-K) va por tramado (alpha hash): sigue siendo opaco, sin ordenar.
// PlayerModel crea una variante con cull_disabled (sustituyendo el texto) para el pelo y las piezas de doble cara.
// - Pase de arte (9 oct, «estilo grease pencil, cómic… Cult of the Lamb, Lucky Tower»): luz en tres bandas planas y un
//   filo de luz en el contorno, en vez del degradado de un render. El contorno de tinta lo pone la vista (InkOutline).
shader_type spatial;
render_mode cull_back, specular_disabled, shadows_disabled;

uniform sampler2D albedo_tex : source_color, filter_linear_mipmap, repeat_enable;
uniform vec4 albedo_color : source_color = vec4(1.0);
uniform bool use_texture = true;
uniform bool tint_mask = false;
uniform vec4 team_color : source_color = vec4(1.0);
uniform bool skin = false;
uniform vec4 skin_color : source_color = vec4(1.0);
uniform sampler2D emission_tex : source_color, hint_default_black;
uniform float emission_energy = 0.0;
uniform float opacity = 1.0;
uniform bool hue_team = false;
// Textura con el sombreado ya pintado (modelos generados desde la ilustración): la luz de escena pesa menos.
uniform bool baked = false;

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
	vec4 t = use_texture ? texture(albedo_tex, UV) : vec4(1.0);
	vec3 c = t.rgb * albedo_color.rgb;
	float lum = dot(c, vec3(0.299, 0.587, 0.114));
	if (tint_mask) {
		// El tono es el del equipo; la textura sólo aporta el pliegue (su luminancia, recentrada).
		vec3 dyed = team_color.rgb * 0.8 * clamp(0.55 + 1.1 * (lum - 0.3), 0.35, 1.15);
		c = mix(c, dyed, t.a);
	}
	if (hue_team) {
		vec3 hsv = rgb2hsv(c);
		float blue = smoothstep(0.5, 0.55, hsv.x) * (1.0 - smoothstep(0.68, 0.74, hsv.x)) * smoothstep(0.12, 0.25, hsv.y);
		c = mix(c, hsv2rgb(vec3(0.995, clamp(hsv.y * 1.8, 0.62, 0.9), min(hsv.z * 1.15, 1.0))), blue);
	}
	if (skin) {
		c = mix(c, vec3(lum), 0.85) * skin_color.rgb * 1.25;
	}
	ALBEDO = c;
	ROUGHNESS = 0.85;
	METALLIC = 0.0;
	EMISSION = texture(emission_tex, UV).rgb * emission_energy;
	ALPHA = opacity;
	ALPHA_HASH_SCALE = 1.0;
}

// Sombreado de cómic: tres bandas (luz, media, sombra) con el borde entre ellas un poco suave para que no rasque, y la
// sombra proyectada respetada (ATTENUATION). Más un filo de contraluz cálido en el borde de la silueta.
void light() {
	float ndl = dot(NORMAL, LIGHT) * ATTENUATION;
	float band = smoothstep(0.28, 0.34, ndl) * 0.5 + smoothstep(-0.12, -0.06, ndl) * 0.32;
	DIFFUSE_LIGHT += LIGHT_COLOR * (baked ? mix(0.62, band, 0.35) : band);
	float rim = smoothstep(0.62, 0.7, 1.0 - dot(NORMAL, VIEW)) * smoothstep(-0.2, 0.2, ndl);
	DIFFUSE_LIGHT += LIGHT_COLOR * rim * 0.25;
}

