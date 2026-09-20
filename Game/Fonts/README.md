# Fuentes provisionales (ADR 0120)

Licencia SIL Open Font License 1.1 (texto en `*-OFL.txt`), descargadas de `github.com/google/fonts` el
19 sep 2026. **Provisionales**: la elección definitiva (personalidad y licencias) es una decisión abierta de
`docs/ui/README.md` §9.

| Fichero | Familia | Voz |
|---|---|---|
| `IMFellEnglishSC.ttf` | IM Fell English SC (Igino Marini) | **sin uso** desde el 20 sep 2026 (cargable, ver enmienda) |
| `IMFellGreatPrimerSC.ttf` | IM Fell Great Primer SC | **sin uso**, ver arriba |
| `IMFellEnglish-Italic.ttf` | IM Fell English Italic | **sin uso**, ver arriba |
| `BarlowCondensed-Bold.ttf`, `-SemiBold.ttf` | Barlow Condensed | datos |
| `Cinzel-Variable.ttf` | Cinzel | cifras del marcador |
| `GrenzeGotisch-Variable.ttf` | Grenze Gotisch | **titular**: lo que proclama (estandarte, bando, acta, sellos, cabeceras de bandeja, escudo del tablero, títulos de pantalla) |
| `Grenze-Variable.ttf` | Grenze | **cuerpo y subtítulo**: lo que se lee seguido (etiqueta de sección, subtítulo) |
| `Grenze-Italic-Variable.ttf` | Grenze Italic | la misma voz de cuerpo, en cursiva (crónica, pie de bando/estandarte) |

**20 sep 2026, decisión del revisor** («letra gótica en algún sitio, o una serif intermedia»): la voz de
proclamar pasa de IM Fell (romana pura) a **Grenze Gotisch**, diseñada expresamente como punto intermedio
entre la romana y la gótica; el resto de la voz serif pasa a **Grenze** (y su cursiva). Deja sin efecto el
«nunca gótica» de la ADR 0120 §1 (ver su enmienda) — RA-026 sigue prohibiendo la iconografía gótica
(calaveras, marcos), que es un problema distinto del de la tipografía. IM Fell se queda en el repositorio,
cargable desde `Pregon.Fell`/`FellBig`/`FellItalic`, sin llamadas en el resto del árbol, por si el revisor
quiere volver a ella.
