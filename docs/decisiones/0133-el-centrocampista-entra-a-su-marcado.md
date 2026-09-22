# 0133 — El centrocampista entra a su marcado, y el delantero no

Estado: **Aceptada** (23 sep 2026). Cierra la mitad que quedaba de la ADR 0125 D2/D3 y con ella la ficha
`docs/pendientes/BE-A.md`. Cambia comportamiento y `/data`: `game-design-review` (esta nota) y
`balance-measure`.

## Problema

`BE-A` empezó siendo un hallazgo de código: el comentario decía «defensa y centrocampista» y la guarda
decía `Defender`. La ADR 0125 arregló el instrumento (D1) y la estructura (D2/D3: el mapa por puesto, la
guarda por dato), pero **dejó el reparto sin abrir**, porque cada intento rompía algo:

- con el ajuste plano y el contador de enfriamiento compartido, `tacklesPerMatch` caía a **6,26** sobre un
  suelo de 6,00;
- y el mapa por puesto parecía aplanar la diferenciación de builds.

Las dos cosas se resolvieron aparte y **por eso esto se puede cerrar ahora**: la **ADR 0129** separó los
enfriamientos (las disputas del balón dejaron de pagar por los golpes) y la **ADR 0131** hizo que las
puertas de build midan ocho plantillas, con lo que el «aplanamiento» resultó ser ruido de una semilla.

## Decisión

`tackleMarkTargetBonus` pasa a **`Defender: 150 · Midfielder: −40 · Forward: 0`**, con
`OffBallTackleCooldownTicks` en **400**.

**El centrocampista entra con un ajuste negativo, y eso no es una rareza: es la única forma.** Sin balón,
su alternativa (`MarkOpponent`, 450) le gana a la entrada (346) por 104 puntos. Un ajuste **positivo**
cualquiera cruza ese hueco y lo **satura** —medido con `Midfielder: 40`: 1,04 entradas por partido-jugador,
la misma tasa que el defensa, con las lesiones en 1,07 sobre un techo de 0,90—. Un número por debajo de 0
lo deja entrando **de vez en cuando**, que es lo que el reparto pedía.

**El delantero se queda en 0.** No porque el mecanismo no pueda: porque abrirlo cuesta 0,07 del
presupuesto de lesiones (0,84 contra 0,77 en la plantilla más expuesta) **por un comportamiento que existe
solo porque sus acciones sin balón son peores que pegar**: su `Tackle` (211) ya gana a su `MarkOpponent`
(180). Eso no es un delantero agresivo, es un delantero sin nada mejor que hacer, y enseñarlo como identidad
sería enseñar un defecto. Queda como `BF-C`.

## Las diez preguntas, en corto

1. **Qué experimenta**: el centrocampista que te marca ya no solo te sigue: de vez en cuando te entra. El
   defensa sigue siendo el que más reparte, con diferencia.
2. **Qué decide hoy**: nada — el centrocampista era decorado en la fase sin balón.
3. **Qué debería decidir**: alinear a un agresivo de centro del campo es aceptar que repartirá ahí; y la
   fase sin balón deja de ser solo cosa de los defensas.
4. **Qué regla representa**: cierra la ADR 0125 D2/D3 y enmienda la ADR 0105 §2. RF-057 la acota, ahora de
   verdad (ADR 0132: con el balón muerto no hay jugada activa).
5. **Sistemas**: `/data` (`ai/weights.json`, `sim/tuning.json`). **Ni una línea de `/Sim`**: el mecanismo ya
   estaba, faltaba el número.
6. **Alternativas descartadas, todas medidas**: ajuste positivo (satura) · enfriamiento más largo para
   dosificar (satura por abajo: con 2.400 ticks, más que un partido, el 83 % de los centrocampistas sigue
   entrando una vez) · enfriamiento **por puesto** (implementado y retirado: con ajustes negativos el orden
   ya sale, así que no eliminaba complejidad, la movía) · abrir también al delantero (cuesta lesiones por un
   defecto).
7. **Trade-off**: más violencia repartida a cambio de presupuesto de lesiones. La ADR 0132 devuelve parte.
8. **Estrategias**: le da al centrocampista un papel en la fase sin balón, que era un hueco abierto del
   proyecto.
9. **Degeneración**: `injuriesPerMatch`. Medido en cinco plantillas: 0,47 a **0,81**, techo 0,90.
10. **Cómo se demuestra**: el orden pedido en las cinco plantillas, las obligatorias en banda, y las puertas
    de build con el instrumento de ocho de la ADR 0131.

## Lo medido (500 partidos por plantilla, entradas sin balón por partido-jugador)

| semilla | `tacklesPerMatch` | sin balón | `injuries` | DEF | MID | FWD | orden |
|---|---|---|---|---|---|---|---|
| 1 | 7,55 | 4,91 | **0,73** | 0,91 | 0,21 | 0 | DEF > MID > FWD |
| 2 | 8,19 | 3,58 | 0,47 | 0,50 | 0,26 | 0 | DEF > MID > FWD |
| 3 | 8,63 | 2,37 | 0,49 | 0,27 | 0,22 | 0 | DEF > MID > FWD |
| 5 | 10,07 | 1,83 | 0,58 | 0,24 | 0,15 | 0 | DEF > MID > FWD |
| 7 | 8,81 | 3,13 | **0,81** | 0,65 | 0,09 | 0 | DEF > MID > FWD |

**El defensa va por delante en las cinco.** Ninguna métrica obligatoria cambia de estado en ninguna: las
dos `OUT` que aparecen (`betterTeamWinRate` en la 2, `shotsPerMatch` en la 5) ya lo estaban antes en esas
mismas plantillas.

Puertas de build, con el instrumento de ocho plantillas de la ADR 0131 (media, y en cuántas de las ocho
queda fuera):

| métrica | antes | después |
|---|---|---|
| `coherentBuildsBeatNone_orc_violence` (≥58) | 58,44 · 3/8 | **59,45 · 2/8** |
| `coherentBuildsBeatNone_orc_mob` (≥58) | 62,99 · 1/8 | **60,96 · 0/8** |
| `badBuildsLoseToNone_elf_brawler` (≤45) | 46,64 · 7/8 | **45,55 · 5/8** |
| `badBuildsLoseToNone_elf_out_of_zone` (≤45) | 44,53 · 2/8 | 45,34 · 5/8 |
| `buildsWinDifferently_injuries` (≥1,10) | 1,29 | 1,24 |

Tres mejoran, una empeora (`elf_out_of_zone`, 0,81 puntos sobre un error típico de 1,09: el instrumento no
puede distinguirlo de cero) y una baja dentro de banda. **Lo que parecía aplanamiento de identidad de build
era ruido de una sola semilla**, y por eso esta ADR va después de la 0131 y no antes.

## Lo que queda abierto, y con ficha

- **`BF-C`**: el delantero entra sin balón porque sus alternativas fuera de posesión son peores que pegar.
  Hasta que eso se arregle, su ajuste se queda en 0.
- **El orden entre defensa y centrocampista lo sostiene el ajuste, no la tasa.** No hay enfriamiento por
  puesto: si algún día se quiere gobernar *cuántas* por puesto y no solo *si*, esa palanca no existe y
  habría que crearla (se implementó como prueba en esta sesión y se retiró por no hacer falta).

## Hermanos

ADR 0125 (la que decidió el reparto; esta cierra su D2/D3) · ADR 0129 (el enfriamiento propio, que dio el
margen) · ADR 0131 (el instrumento que deshizo el falso aplanamiento) · ADR 0132 (el balón muerto) ·
`docs/pendientes/BE-A.md` (la ficha, que se cierra con esto) · `docs/pendientes/BF-C.md`.
