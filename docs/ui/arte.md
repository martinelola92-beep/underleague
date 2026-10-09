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

## Hecho (9 oct 2026)

- **Pase de materiales**: papel, madera y brochazo en todas las primitivas; siluetas de game-icons en atributos,
  puestos, rasgos, estado y pestañas; las pantallas antiguas (Mapa, Ojeo, Mercado, Informe…) con el lenguaje de tinta de
  Equipo desde los widgets compartidos.
- **Razas en el partido** (retargeting humanoide de los clips de Mixamo a Quaternius; recetas arriba), figuras a escala
  de miniatura (×1,45, sólo presentación) y árbitro con figura.
- **Estilo cómic** (el revisor: «grease pencil, cómic… Cult of the Lamb, Lucky Tower»): luz en tres bandas en modelos y
  grada (`team_tint.gdshader`, `DiffuseMode.Toon`) y contorno de tinta de pantalla completa por saltos de profundidad
  que se redibuja a 8 fps (`Game/Art/Shaders/ink_outline.gdshader`).
- **Estadio**: césped y tierra con grano (ambientCG), grada y muro de madera, banderolas heráldicas, torres.
- **Público renderizado** (el revisor: «que el público sean renders y no pastillas»): 30 figuras × 3 posturas en un
  atlas (`Scenes/PublicoRender.tscn` → `tools/arte/publico.py` → `Game/Art/Crowd/`), dibujadas como cuadrados con
  `crowd.gdshader`; la grada levanta los brazos en los goles y se calla en las muertes.
- **Retratos renderizados**: 40 bustos (5 razas × 8) de los mismos modelos (`Scenes/RetratosRender.tscn` →
  `tools/arte/retratos.py` → `Game/Art/Portraits/`), en `Portrait.Draw` con el dibujo por código de reserva.

## Siguiente

1. **Que el revisor lo vea**: el salto de lo plano por código a modelos con sombreado de cómic es grande; las caras de
   Quaternius son de dibujo animado «semirrealista», no de Cult of the Lamb. Si no convencen, la palanca es otra
   fuente de modelos más «chunky» o pintar a mano encima de los renders.
2. Dorsal y anillo del suelo todavía pesan más que la figura en el plano general; probar anillo fino y dorsal sólo al
   seleccionar.
3. Objetos ilustrados (la bota del berserker de la referencia) y portero con equipación propia.
4. El no-muerto es un humano gris: falta un modelo propio (KayKit Skeletons, CC0, otro esqueleto).
