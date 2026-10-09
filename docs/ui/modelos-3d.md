# Modelos 3D desde las ilustraciones (plan del 9 oct 2026)

El revisor quiere **3D con aspecto de dibujo a lápiz** que se parezca a los retratos de Nano Banana. Reparto realista:

| Pieza | Quién | Cómo |
|---|---|---|
| Aspecto (rayado en sombra, contorno que hierve, papel, sombreado por bandas) | Claude | shaders: `Game/Art/Shaders/grease_pencil.gdshader`, `ink_outline.gdshader`, `team_tint.gdshader` (hecho, se ajusta con los modelos nuevos) |
| Forma de los cuerpos (que se parezcan a las ilustraciones) | revisor, con generador de imagen a 3D | Tripo (tripo3d.ai) o Meshy (meshy.ai), nivel gratuito por web: subir `out/arte/para-3d/<raza>_frente.png` (de frente, fondo blanco, recortado de la hoja de modelo) → «image to 3D» con textura → descargar `.glb` |
| Esqueleto | revisor | Tripo trae «auto rig» humanoide; si no, Mixamo (mixamo.com, gratis) → subir el `.fbx`/`.obj`, colocar los marcadores, descargar **sin animación** (T-pose) |
| Animación | Claude | los 15 clips de fútbol de Mixamo se reasignan a cualquier esqueleto humanoide con el `BoneMap` de `Game/models/retarget/` (ya funciona con Quaternius) |
| Integración (equipo teñido, alturas por raza, IK de pies, balón al pie) | Claude | `PlayerModel` cambia de receta; nada en `/Sim` |

**Qué dejar a Claude:** los `.glb` (o `.fbx`) en `out/arte/para-3d/entrada/<raza>.glb`. Con uno (el orco) basta para
probar el camino entero antes de hacer los otros cuatro.

**Lo que Claude no puede hacer bien:** esculpir modelos ni animar a mano fotograma a fotograma. El prototipo 2D de
figuras de papel (`PaperFigure`, `MatchPitchView3D.PaperFigures`) queda desactivado y de reserva.

## Hecho (9 oct 2026): las cinco razas, sin pasar por el revisor

No hizo falta Tripo ni Mixamo: todo se hace desde WSL con herramientas gratuitas.

| Raza | Forma | Textura | Esqueleto |
|---|---|---|---|
| orco, elfo, no-muerto | TRELLIS (`tools/arte/trellis.py <raza> [semilla]`) | la de TRELLIS | `rig_blender.py`, difusión de calor |
| humano | TRELLIS desde el recorte limpio; se borró a mano el panel de fondo que se le pegó a la espalda (caras planas en el plano medio) | la de TRELLIS (quedan motas verdes del panel) | `rig_blender.py`, respaldo por distancia |
| enano | TRELLIS lo saca como una lámina plana con cualquier semilla y recorte; la forma sale de **Hunyuan3D-2** (Space `tencent/Hunyuan3D-2`, sólo forma) | `tools/arte/proyectar_textura.py`: frente, perfil y espalda de la hoja de modelo proyectados por la normal de cada cara | `rig_blender.py`, respaldo por distancia |

- **Cuota.** Los Spaces van con ZeroGPU: sin cuenta da ~1 modelo al día; con un token gratuito de Hugging Face
  (`HF_TOKEN` en el entorno, nunca en el repositorio) da unos 6-7. Se agota igual; se recarga a diario.
- **`rig_blender.py`**: proporciones por raza (`clave=valor`) medidas sobre un render de frente con regla.
  - `straight_legs=1` pone rodilla y cadera sobre el tobillo, porque manos, barba o faldón a esa altura tiran del
    centroide.
  - Si la difusión de calor deja sin peso más del 10 % de la malla, reparte por distancia al segmento de cada hueso.
  - Fracciones usadas (**provisionales, a ojo** sobre el render; todas con `straight_legs=1` salvo el orco):
    - humano `hips=0.28,spine=0.40,chest=0.58,shoulder=0.77,neck=0.79,elbow=0.54,wrist=0.35,fingertip=0.25,knee=0.17`
    - elfo `hips=0.47,shoulder=0.79,neck=0.82,elbow=0.61,wrist=0.43,fingertip=0.33,knee=0.25`
    - no-muerto `hips=0.46,spine=0.55,chest=0.64,shoulder=0.75,neck=0.79,elbow=0.54,wrist=0.39,fingertip=0.27,knee=0.24`
    - enano `hips=0.25,spine=0.38,chest=0.52,shoulder=0.66,neck=0.72,elbow=0.47,wrist=0.31,fingertip=0.21,knee=0.14,ankle=0.05`
    - orco: los valores por defecto del script
- **`PlayerModel`**: cualquier raza con `Game/models/races/<raza>_gen.glb` lo usa (receta `gen_<raza>`), sin escalar
  los huesos por raza, porque las proporciones ya vienen dibujadas. El rival lleva `<textura>_rival.png`
  (`rival_textura.py`).
- **Material** (`Game/Art/Shaders/generated_figure.gdshader`): la textura ya trae el dibujo, así que se pinta sin luz de
  escena. Con la cámara cenital de la retransmisión, los humanos salían casi negros con la luz normal. Lleva dos tonos
  planos con una luz fija, tinta en los bordes interiores y algo de luz y color en los oscuros. El árbitro es el humano
  generado con `<textura>_referee.png` (`rival_textura.py … arbitro`).
- **«Corren agachados»** (medido el 9 oct, con `Running.fbx` sobre el humano generado y sobre `peasant_male`):
  - **REJECTED** — pose de reposo en zigzag. Se probaron `straight_legs`/`straight_spine` en `rig_blender.py`: apenas
    cambia, y en el orco y el no-muerto estropea los pesos. Queda como opción, apagada por defecto, y los modelos
    siguen con el rig anterior.
  - **REJECTED** — ejes de reposo. Tras reasignar coinciden con los de Quaternius.
  - **CONFIRMED** — la animación es la misma: la cadera baja a 0,88 de su altura de reposo frente a 0,86, y la rodilla
    pasa por 53°/80°/125° frente a 46°/74°/118°.
  - Lo que cambia son las **proporciones**: la cadera está al 28 % de la altura en el humano generado y al 53 % en
    Quaternius. La misma flexión sobre piernas cortas y un torso grande se lee como agachado.
  - Remedio posible, sin decidir: suavizar la flexión de piernas o la inclinación del clip en las razas de piernas
    cortas.
- **Pendiente**: rehacer el humano con cuota limpia (motas del panel).
