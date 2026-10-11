# BX-10b — la barrera del saque no aparta al rival pegado a la línea de fondo

**Observación** (10 oct 2026, al mover las semillas con BX-10): `RestartClearanceTests`, semilla 121, falta a 1,24
casillas de la línea de fondo; el rival que estaba en x = 0 se queda a 1,24 del balón con la barrera en 2,0.

## Hipótesis (11 oct 2026)

- **H1, sólo se desliza por la banda.** **CONFIRMED** por lectura y por el caso de la semilla 121.
  - `MatchEngine.SlideAlongPitchToClear` (ADR 0142) deslizaba sólo por el eje X, con la Y fija.
  - Si acotar al campo dejaba al rival dentro de la barrera contra la línea de fondo (x = 0 o x = 16), no lo movía.
- **H2, hermano encontrado al instrumentar el motor: el deslizamiento por la banda falla por redondeo.**
  **CONFIRMED.**
  - El deslizamiento cae justo sobre la circunferencia. En coma flotante queda a menudo a 1,99999 de una barrera de 2,
    y `>= clearance` lo daba por fallido, así que el jugador volvía a donde estaba: sobre la banda (y = 7 o y = 0) a
    1,7-1,9 del balón, tick tras tick.
  - Medido con contadores provisionales en el motor, 2000 partidos: 2934 correcciones fallidas, 2812 de ellas sobre la
    banda.
  - Con la holgura, 100 fallidas: lo que queda son esquinas, que la geometría no permite.

## Medición (sonda de cuenta atrás, 400 partidos, el balón quieto, desde el segundo fotograma)

| | Fotogramas de rival dentro de la barrera | Todos sobre una línea | A menos de 1,5 | Peor |
|---|---|---|---|---|
| Antes | 328 | 328 | 28 | 0,94 |
| Después | 0 | — | — | — |

- El peor caso, a 0,94, está dentro del alcance de la entrada (1,0). La barrera de BB-B existe para que eso no pase.
- `RestartClearanceTests.NoRivalIsEverCloserThanTheActionRangeDuringTheWindow` no lo veía: mide la ventana del
  sacador con el balón ya tocado, no la cuenta atrás.

## Arreglo

`SlideAlongPitchToClear`, con dos funciones nuevas, `SlideOnAxis` y `SlideTo`:
- primero por la banda y, si no basta, por la línea de fondo;
- siempre hacia el lado en que ya está el jugador, nunca por delante del balón: es la regla de la ADR 0142 contra el
  salto de 3,85 de BA-K;
- **si el jugador está alineado con el balón en ese eje** (|desfase| ≤ 0,01), no tiene lado: se prueban los dos, porque
  ninguno le cruza por delante;
- se acepta con una holgura de 0,01 (`SlideToleranceCells`), y un hueco menor que la holgura no se desliza;
- **el portero no se desliza** (RF-057b manda sobre la barrera): ya está acotado a su área.

Sin dato nuevo ni regla nueva: la barrera de RF-052 y BB-B ya pedía 2,0 casillas a todos los rivales salvo el portero
en su área.

## Revisión independiente (Regla E, 11 oct)

La primera versión no traía ni los lados, ni el portero, ni la holgura al principio. Lo que encontró la revisión:

- **CONFIRMED, fallo de signo.**
  - Con el rival alineado con el balón, el deslizamiento iba siempre hacia +.
  - En la banda de arriba (y = 7, donde el saque de banda pone el balón) eso lo sacaba del campo, el campo lo devolvía y
    se quedaba a 1,0. En la de abajo, a 2,0.
  - Corregido; lo cubre un test de casos conocidos.
- **LIKELY raro, sin geometría encontrada que lo dispare: el portero podía salir de su área** al deslizarlo.
  - Corregido sin deslizarlo.
  - El test `TheClearanceNeverTakesTheGoalkeeperOutOfHisArea` pasa también sin el arreglo. Queda como guarda, no como
    prueba de que ocurría.
- **La holgura era asimétrica**: se aplicaba antes de la línea de fondo y no antes de la banda. Ahora se aplica al
  principio, igual para los dos ejes.
- **Lo que no se demuestra, y la ficha lo decía mal:**
  - El peor caso, a 0,94, está dentro del alcance como número, pero en la cuenta atrás el balón no tiene dueño y nadie
    puede entrar al sacador.
  - El daño demostrado es el visual (un rival dentro de la barrera), no una entrada ilegal.
- **El salto del deslizamiento por la banda, que ahora sí se acepta** (antes fallaba por redondeo), no está medido.
  Puede acercarse a 2 casillas en un fotograma.
- **Preocupación de diseño anotada, previa a este paquete:** la barrera recoloca al rival al instante en vez de darle un
  destino al que caminar («eventos explícitos > transiciones invisibles»). Este arreglo amplía los casos en que lo hace.
  Candidato a ficha propia si el revisor lo ve en la retransmisión.
- **Hermano sin efecto:** `EnforceRestartClearance` vuelve a mirar cada tick al que quedó a 1,99999 (`>= clearance`).
  LIKELY inocuo; sólo le quita la velocidad hacia el balón.

**Tests:**
- `RestartClearanceTests.DuringTheCountdownNoRivalIsInsideTheClearance` (nuevo, 400 partidos): 328 → 0.
- `TheClearancePushesARivalOnALineOutToItsFullDistance` (nuevo, 7 casos conocidos en las cuatro líneas): 6 fallan sin el
  arreglo.
- `TheClearanceNeverTakesTheGoalkeeperOutOfHisArea` (nuevo, guarda).
- `NoRivalIsEverCloserThanTheActionRangeDuringTheWindow` pierde la excepción de la línea de fondo, ya sin motivo.
- `MobNarrowingTests`: huellas renovadas.
- `GoalKickDecisionTests.OnlyWhenNobodyCanThreatenDoesTheClearanceDropTheDangerBonus`: tope de semillas de 40 a 120.
  Con las semillas movidas, las recogidas se quedaban en 3 de 5; el barrido para en cuanto hay muestra.

## Hermano destapado al mover las semillas: sustitución de un expulsado

Con este cambio, `RunPolicySellTests` se caía con una `ArgumentException` del motor en la run de orcos, semilla 4.
- En el tick 373, el jugador 4 hace una entrada sin balón, se lesiona de gravedad en el choque y ve la roja (perk
  `dirty_play`), todo en el mismo tick.
- `SubstitutionPoints.Pending` veía la lesión y ofrecía la sustitución. El motor la rechazaba porque estaba expulsado
  (ADR 0094), y la run entera caía.
- **Causa CONFIRMED:** reproducida comparando las dos pasadas, que son idénticas hasta T (no es determinismo), y leyendo
  los eventos.
- **Arreglo:** `Pending` no ofrece sustituir a quien vio una roja no anulada en ese tick o antes. Al expulsado no se le
  sustituye, que es la regla del motor.
- **Test:** `SubstitutionSentOffTests`. El caso es demasiado raro para barrer (ninguno en 3000 partidos sueltos), así que
  se construye con un partido real y una roja añadida al lesionado. Falla sin el arreglo.
- Va en commit propio.

## Balance (`--runs 3000`, semillas 1 y 2, base = el lote de BX-10c)

- Ninguna métrica cambia de estado.
- Fuera de banda sólo `betterTeamWinRate_human_60_vs_human_40`, como en la base ([BV-C](./BV-C.md)).
- La que más se mueve, `share_over5goals` (INFO), lo hace un 25-29 % de su banda y con signo distinto en cada semilla:
  −0,17 y +0,27.

## Puertas (`Category=Gate`)

Rojas: `betterTeamWinRate_human_60_vs_human_40` (90,96; ya roja) y `orc_violence` (57,55; ya roja). Tres nuevas:

- **`badBuildsLoseToNone_elf_brawler` = 45,60** (techo 45). Medida por semilla con `BuildGateTests.MetricsWithSeed`:

  | | s1 | s2 | s3 | s4 | Media |
  |---|---|---|---|---|---|
  | `main` | 45,21 | 45,42 | 39,79 | 45,83 | 44,06 |
  | Con el cambio | 44,17 | 44,58 | 45,63 | 47,29 | 45,42 |

  - +1,36, ~0,85 ET de la diferencia; los deltas cambian de signo de una semilla a otra.
  - En `main` ya pasaba del techo en tres de las cuatro semillas (en el filo, [BF-A](./BF-A.md)).
  - **LIKELY ruido.**
  - En la misma tanda, `orc_violence` +0,8 de media y `elf_out_of_zone` −1,7.
- **`MatchOrderTests.DefensiveConcedesLessAndOffensiveScoresMore`**: cuánto menos encaja el defensivo que el neutro,
  con un listón de 0,04.

  | Bloque de 1200 partidos | b0 (el de la puerta) | b1 | b2 | Media |
  |---|---|---|---|---|
  | `main` | 0,085 | 0,156 | 0,093 | 0,111 |
  | Con el cambio | 0,038 | 0,122 | 0,045 | 0,068 |

  - Los tres bloques bajan, pero cada diferencia tiene ~0,05 de error típico (los partidos divergen enteros tras la
    primera diferencia de la barrera), y la media se mueve ~1,4 ET.
  - **Hipótesis del mecanismo REJECTED.** Pensé que el defensivo, con gente amontonada en su línea de fondo, perdía la
    ventaja de una barrera que no le apartaba. Pero los goles de balón parado que encaja el defensivo no suben (0,107 y
    0,089 → 0,097 y 0,091, contando los marcados en los 10 s tras un córner, una falta o un saque de banda del rival);
    la diferencia está en el juego abierto.
  - **LIKELY ruido.** No se toca el listón.
  - La puerta tiene un error típico (~0,037 por bloque) casi igual a su listón: es frágil por construcción. Queda
    anotado para el revisor.
- **`FullRunGateTests.TheThreeDoctrinesBuyDifferently`**: la ahorradora acaba con 13,17 de oro y la contextual con 14,04.
  Es la oscilación conocida de [BU-A](./BU-A.md), LIKELY ruido, que depende además de la economía pendiente de decisión.
