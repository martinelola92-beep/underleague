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
