// Material de los jugadores generados desde su ilustración (9 oct 2026; el revisor: «3D pero con ese estilo 2D falso
// grease pencil que simule los retratos»). La textura ya trae el dibujo —tinta, sombreado y luces de la hoja de modelo—,
// así que la luz de la escena no la vuelve a sombrear: con la cámara cenital de la retransmisión los humanos quedaban
// casi negros. Aquí se pinta sin luz de escena y se añade sólo lo que un ilustrador añadiría:
// - dos tonos planos (luz y sombra) según una luz fija, sin degradado;
// - tinta en los bordes interiores de la silueta (brazo sobre el cuerpo, nariz, orejas): el contorno exterior ya lo
//   pone el pase de pantalla (ink_outline.gdshader) y el rayado el de grease_pencil.gdshader;
// - un poco de luz en los oscuros para que la camiseta azul marino se lea desde lejos.
// La atenuación del teletransporte (BA-K) va por tramado, como en team_tint.gdshader. Sólo presentación (RT-014).
shader_type spatial;
render_mode unshaded, cull_disabled;

uniform sampler2D albedo_tex : source_color, filter_linear_mipmap, repeat_enable;
uniform float opacity = 1.0;
// Hacia dónde está la luz, en el mundo: arriba, de frente a la cámara y algo a la izquierda, como la del estadio.
uniform vec3 light_dir = vec3(-0.35, 1.0, 0.45);
uniform float shadow_tone = 0.68;
uniform float lift = 0.07;
uniform float saturation = 1.2;
uniform vec4 ink : source_color = vec4(0.106, 0.082, 0.063, 1.0);
uniform float rim_start = 0.68;
uniform float rim_strength = 0.8;

void fragment() {
	vec3 c = texture(albedo_tex, UV).rgb;
	// Oscuros levantados (no los claros): la camiseta de noche se lee, la tinta del dibujo sigue negra.
	c = c + lift * sqrt(c) * (1.0 - c);
	// Algo más de color: los tonos medios levantados tiran a pastel (orco azul lavanda) y el dibujo es de tinta.
	c = clamp(mix(vec3(dot(c, vec3(0.299, 0.587, 0.114))), c, saturation), 0.0, 1.0);
	vec3 n = normalize(NORMAL) * (FRONT_FACING ? 1.0 : -1.0);
	vec3 l = normalize((VIEW_MATRIX * vec4(light_dir, 0.0)).xyz);
	float lit = smoothstep(-0.08, 0.04, dot(n, l));
	c *= mix(shadow_tone, 1.0, lit);
	float grazing = 1.0 - clamp(dot(n, VIEW), 0.0, 1.0);
	c = mix(c, ink.rgb, smoothstep(rim_start, rim_start + 0.14, grazing) * rim_strength);
	ALBEDO = c;
	ALPHA = opacity;
	ALPHA_HASH_SCALE = 1.0;
}
