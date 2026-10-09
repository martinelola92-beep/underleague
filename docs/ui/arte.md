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
| Quaternius: Bestiary (Imp, Puglin), Modular Outfits Fantasy (campesino, explorador), Universal Base Characters (cuerpos, pelo, barba) — tiers *Standard* gratuitos | CC0 | modelos para las razas del partido y retratos | descargado en `out/arte/q/` (sin integrar) |

## Pipeline

- `out/arte-venv/bin/python tools/arte/materiales.py` — mapas de detalle en gris (media ~0,93) que la interfaz
  multiplica por el color de la paleta: el tono lo decide el código, el grano la textura. Deterministas.
- `out/arte-venv/bin/python tools/arte/iconos.py` — rasteriza los glifos de silueta a 128 px blancos (se tiñen).
- `godot --headless --path Game --import` tras añadir ficheros, antes de capturar. **Trampa (9 oct):** abre el editor y
  re-guarda `.cs` cambiando espacios por tabuladores (se coló `MatchPitchView3D.cs` entero en un commit). Después de
  importar, `git diff -w --stat` y `git checkout -- <ruta>` de todo `.cs` que sólo cambie en blanco.
- **Un único punto de entrada en el código**: `Game/Ui/Art.cs`. Las primitivas (`Ink.Slab/Sheet/Plank/Brush`,
  `Pregon.DrawParchment`, `WoodTable`, `InkStyleBox`, `InkIcons.Draw`) piden ahí la textura y, si no existe, pintan
  como antes. Ninguna pantalla sabe que hay arte.

## Hecho

- **Pase de materiales (9 oct)**: papel, madera y brochazo en todas las primitivas; siluetas de game-icons en
  atributos, puestos, rasgos, estado y pestañas. Capturas `equipo-*.png` (antes en `out/arte/antes/`).

## Siguiente

1. **Razas en el partido** con los modelos de Quaternius: retargeting por `SkeletonProfileHumanoid` para que los clips
   de fútbol de Mixamo animen el esqueleto UAL (hoy sólo los humanos tienen modelo, el resto son cápsulas). Recetas:
   humano = cuerpo base + campesino; elfo = explorador con capucha, más esbelto; enano = cuerpo base achatado + barba;
   orco = Imp verde, más ancho; no-muerto = cuerpo base gris verdoso. Camiseta teñida del color del equipo.
2. **Retratos** renderizados de esos mismos modelos (busto, luz de tres cuartos, contorno de tinta), para que la cara
   de la plantilla sea la del campo.
3. **Estadio**: grada de madera, torres, banderolas, césped con textura.
4. **Objetos** ilustrados y tipografía de titulares más cercana a la de las referencias.
