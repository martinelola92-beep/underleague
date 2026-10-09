# Arte: cómo se produce y en qué punto está

**Decisión del revisor (9 oct 2026):** Claude crea o descarga arte de mayor calidad, con cuatro referencias pintadas
(una tienda de mazmorra con retrato y diálogo; la plantilla de los orcos dos veces, una anotada; un partido con estadio
de madera, banderolas y jugadores ilustrados). *«No son lo que quiero al 100 %.»* Sustituye a la regla de «Claude no
produce arte» (`CLAUDE.md` regla 10).

## Lo que piden las referencias

- **Material en todo**: pergamino con manchas y bordes tostados, tablones de madera oscura, paños heráldicos. Ninguna
  superficie plana.
- **Contorno de tinta grueso** y sombra dura: el lenguaje que la interfaz ya tenía (`Ink.cs`), ahora con textura.
- **Brochazos rojos** para las cabeceras y los rasgos (TITULARES, SUCIO).
- **Iconos de silueta negra** para atributos, puestos y acciones (puño, pie alado, cerebro, escudo).
- **Ilustración** en retratos de busto y objetos (la bota del berserker), y en el partido jugadores de cada raza en un
  estadio de madera con grada, torres y banderolas.

El *cómic* que se descartó para los acontecimientos (onomatopeyas, estallidos, `docs/ui/README.md` §1) sigue fuera: las
referencias son ilustración con tinta, no viñetas.

## Licencias

Sólo CC0, CC BY (con créditos en `Game/Art/**/CREDITS.md`) u OFL. Lo de pago se pregunta antes (coste económico). Cada
fuente queda anotada aquí.

| Fuente | Licencia | Qué | Dónde |
|---|---|---|---|
| Generado (`tools/arte/materiales.py`) | propio | papel, madera, 4 brochazos | `Game/Art/Textures/` |
| game-icons.net (`tools/arte/iconos.py`) | CC BY 3.0 | 35 siluetas | `Game/Art/Icons/` (+ `CREDITS.md`) |
| Quaternius: Bestiary (Imp), Modular Outfits Fantasy (`Male_Peasant`, `Male_Ranger`), Universal Base Characters (cabeza de `Superhero_Male`, `Hair_*`, `Hair_Beard`) — tiers *Standard* gratuitos, zips en `out/arte/q/` | CC0 | las cinco razas del partido 3D (`tools/arte/razas.py`) | `Game/models/races/` (8,3 MB, + `LICENSE-quaternius.txt`) |
| Mixamo «Soccer Game Pack» (clips) | licencia de Mixamo (uso en juegos) | correr, chutar, entrada, caída, portero… para todas las razas | `Game/models/soccer/` |

## Pipeline

- `out/arte-venv/bin/python tools/arte/materiales.py` — mapas de detalle en gris (media ~0,93) que la interfaz
  multiplica por el color de la paleta: el tono lo decide el código, el grano la textura. Deterministas.
- `out/arte-venv/bin/python tools/arte/iconos.py` — rasteriza los glifos de silueta a 128 px blancos (se tiñen).
- `tools/arte/importar.sh` tras añadir ficheros, antes de capturar (importa y deshace lo de abajo). **Trampa (9 oct):** el import abre el editor y
  re-guarda `.cs` cambiando espacios por tabuladores (se coló `MatchPitchView3D.cs` entero en un commit). Después de
  importar, `git diff -w --stat` y `git checkout -- <ruta>` de todo `.cs` que sólo cambie en blanco.
- **Un único punto de entrada en el código**: `Game/Ui/Art.cs`. Las primitivas (`Ink.Slab/Sheet/Plank/Brush`,
  `Pregon.DrawParchment`, `WoodTable`, `InkStyleBox`, `InkIcons.Draw`) piden ahí la textura y, si no existe, pintan
  como antes. Ninguna pantalla sabe que hay arte.

## Hecho

- **Pase de materiales (9 oct)**: papel, madera y brochazo en todas las primitivas; siluetas de game-icons en
  atributos, puestos, rasgos, estado y pestañas. Capturas `equipo-*.png` (antes en `out/arte/antes/`).

- **Razas en el partido 3D (9 oct)**: las cinco razas con modelo de Quaternius animado con los clips de Mixamo.
  Antes/después en `out/arte/razas/`; sonda de cerca `Scenes/SondaRazas.tscn` (`Game/screenshots/razas/`).

### Razas: cómo está montado

- **Retargeting de importación** (no en código): `Game/models/retarget/{mixamo,ual}_bone_map.tres` son `BoneMap` a
  `SkeletonProfileHumanoid`; los `.import` de los clips (`models/soccer/*.fbx.import`) y de los modelos
  (`models/races/*.gltf.import`) los usan con renombrado de huesos, esqueleto único `GeneralSkeleton`, ejes del perfil
  (`retarget_method` 1), silueta en T y pistas de posición normalizadas. Las pistas quedan `%GeneralSkeleton:Hips`… en
  todos; `PlayerModel` usa nombres del perfil (`Hips`, `LeftToes`, `Head`, `LeftHand`, `UpperChest`, `LeftUpperLeg`…).
- **Preparación** (`out/arte-venv/bin/python tools/arte/razas.py`, determinista): copia los glTF sin normales/ORM, color
  a ≤1024 px, del cuerpo base sólo cabeza y cuello (el traje tapa el resto), sin la maza del Imp, y pinta en el **alfa**
  de la textura de ropa la máscara de lo que se tiñe: la camisa del campesino (región UV de `*_Body`), el paño verde del
  explorador (por tono) y los calzones, collar y cadenas del Imp. Tras regenerar: `godot --headless --path Game --import`
  y la trampa de los tabuladores.
- **Material**: `models/races/team_tint.gdshader` (máscara → color de equipo de la cápsula, `Style.TeamOwn/TeamRival`;
  piel del no-muerto; opacidad BA-K por tramado). En modo silueta el modelo usa el material negro como antes.

| Raza | Piezas | Ancho X/Z | Notas |
|---|---|---|---|
| Humano | campesino + cabeza + pelo (rapado / raya / largo, por dorsal) | 1 | pelo teñido por dorsal (castaño, negro, caoba, rubio) |
| Elfo | explorador (capucha) + cabeza | 0,92 | capucha, capa y túnica del color del equipo |
| Enano | campesino + cabeza + barba + rapado | 1,15 | pelo y barba pelirrojos/castaños |
| Orco | Imp, textura verde (`T_Imp_BaseColor_2`) oscurecida | 1,15 | calzones, collar y cadenas del equipo |
| No-muerto | campesino + cabeza, piel gris verdosa desaturada | 1 | sin pelo |

El alto sigue saliendo de la raza (`bodyRadius` + proporciones de la vista); los anchos y tonos son **provisionales, a
ojo**. Pendiente: retratos con estos mismos modelos; variante femenina; el portero sólo se distingue por su postura
(como antes).

## Siguiente

1. **Retratos** renderizados de esos mismos modelos (busto, luz de tres cuartos, contorno de tinta), para que la cara
   de la plantilla sea la del campo.
2. **Estadio**: grada de madera, torres, banderolas, césped con textura.
3. **Objetos** ilustrados y tipografía de titulares más cercana a la de las referencias.
