// Material del partido para los modelos de raza (Game/Ui/PlayerModel.cs, Dress).
// - La camisa (o los calzones del orco) se tiñe del color del equipo: la máscara va en el ALFA de la textura del
//   traje (tools/arte/razas.py); el alfa no es transparencia.
// - La piel del no-muerto: desaturada y multiplicada por un gris verdoso.
// - La atenuación del corte de teletransporte (BA-K) va por tramado (alpha hash): sigue siendo opaco, sin ordenar.
// PlayerModel crea una variante con cull_disabled (sustituyendo el texto) para el pelo y las piezas de doble cara.
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

void fragment() {
	vec4 t = use_texture ? texture(albedo_tex, UV) : vec4(1.0);
	vec3 c = t.rgb * albedo_color.rgb;
	float lum = dot(c, vec3(0.299, 0.587, 0.114));
	if (tint_mask) {
		// El tono es el del equipo; la textura sólo aporta el pliegue (su luminancia, recentrada).
		vec3 dyed = team_color.rgb * clamp(0.55 + 1.1 * (lum - 0.3), 0.35, 1.25);
		c = mix(c, dyed, t.a);
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
