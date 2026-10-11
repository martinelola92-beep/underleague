# BX-7 — En la turba, el delantero se queda «bloqueado» y el saque tarda mucho

**Observación del revisor (partida del 3 oct 2026, `BX-playtest-3oct.md`):** «En una turba, el delantero se queda
bloqueado sin poder volver a su campo porque el saque tarda mucho».

**Estado: CERRADA (11 oct 2026), `/Sim`.**

## Medición (Regla A)

Sonda temporal sobre las semillas 1..200 de `TestMatches.Reference`, con traza: duración de cada saque de centro
(fotogramas con `RestartKind.Kickoff` pendiente).

| | n | p50 | p90 | máx | al tope (195) |
|---|---|---|---|---|---|
| Saque que abre la turba | 71 | 64 | 195 | 195 | **12** |
| Saque tras gol | 267 | 45 | 45 | 45 | 0 |

Doce turbas de 71 se quedaban **13 s parados** (15 de cuenta atrás + 180 de tope de espera, `kickoffMaxWaitTicks`).

## Hipótesis

- **H1 — el paso acotado a la banda impide moverse en horizontal a quien está en una fila invadida.** REJECTED por
  lectura: `PlayBand.ClampStep` sólo acota la fila (Y); la columna queda libre.
- **H2 — un derribado o lesionado bloquea la espera.** REJECTED: `EveryoneInPlace` los excluye, y en la semilla 10
  todos estaban en `Positioning`.
- **H3 — caminata larga legítima (gente repartida por todo el campo).** Explica la cola que queda (p50 38, máx. 67
  tras el arreglo), no el tope.
- **H4 — la barrera de la reanudación atrapa a quien vuelve desde campo contrario.** **CONFIRMED.**
  `EnforceRestartClearance` devuelve cada tick al rival al borde de un círculo de 2 casillas alrededor del balón, y
  `WalkTo` anda en línea recta. Si su casilla queda detrás del balón, la recta cruza el círculo y se queda clavado en
  su borde. Semilla 10, tick 1360: el centrocampista local llega a (9,90; 2,89), a 1,99 del centro, y no se mueve
  una centésima en 150 ticks. En las **12 de 12** turbas que llegaban al tope hay un jugador del equipo que no saca
  inmóvil en el borde de la barrera **en campo contrario** (instrumento validado: la primera versión contaba también
  a los que esperan legítimamente en su campo pegados a la barrera, 4 en todas, y se corrigió).

  Por qué sólo en la turba: tras un gol, `ResetTeamsAfterGoal` coloca a todos de golpe bajo la cortinilla (ADR 0151),
  y en el saque inicial están todos en su casa. El saque que abre la turba es el único que se recorre andando desde
  campo contrario (ADR 0147, ADR 0175).

## Arreglo

`MatchEngine.RestartWalkWaypoint` + `DetourAroundDisc` (función pura): en el saque de centro, quien no saca y
sigue en campo contrario, si su recta cruza el círculo de la barrera, va al punto de tangencia del lado de su destino
(o por la tangente si ya está en el borde) y la barrera le hace deslizar. Ninguna regla del juego cambia: es el
camino. Sin trigonometría (`docs/determinismo.md`): el ángulo de tangencia sale de `radio/distancia` y `MathF.Sqrt`.

- Turbas al tope: 12/71 → **0/71**; p50 64 → 38; p90 195 → 61; máximo 67 (semillas 1..200). El número de turbas no
  cambia porque el reglamentario no cambia.
- Al sacar, nadie del equipo que no saca queda en campo contrario (la revisión midió 13 → 0).
- El reglamentario queda **byte a byte igual** (`TheRegulationMatchIsByteForByteTheOneBeforeTheChange`). Una primera
  versión sin la condición «en campo contrario» lo cambiaba: tras el reinicio de un gol, los que esperan en su campo
  pegados a la barrera rodeaban por la cuerda.
- Tests: `MobKickoffDetourTests` (cuatro de la función y `NoMobKickoffWaitsUntilTheCap`, que **falla sin el arreglo**:
  semilla 10, 195 ticks, y comprueba también que nadie queda en campo contrario al sacar). Huellas de «todos los
  partidos» renovadas en `MobNarrowingTests`.
- `EmergentChainTests` exigía más de 20 duelos aéreos en las semillas 1..200: en la base hay 28 y con el arreglo 20.
  **No es un efecto del arreglo**: en 2.000 partidos son 344 → 333 (−3 %, dentro de la desviación de Poisson, ~18), y
  por bloques de 200 la base va de 28 a 39 y el arreglo de 19 a 43. Las turbas distintas sólo barajan qué bloque cae
  bajo. El umbral estaba a −2 desviaciones de la media; como prueba de existencia baja a 1 de cada 20 partidos.
- Puertas (`Category=Gate`): rojas `orc_violence` 56,90 (ya roja en la base, 57,45, ADR 0193) y
  `betterTeamWinRate_human_60_vs_human_40`. Ésta **ya estaba fuera de banda en la base** en 3 de 4 semillas (1.000
  partidos por semilla, `/Balance`): base 89,76 / 98,80 / 93,98 / 90,36, arreglo 92,17 / 98,80 / 95,18 / 90,96. La
  semilla 1 del test pasaba por 0,24 puntos. Δ medio +1,1, dentro del ruido entre semillas: LIKELY sin efecto. Sigue
  siendo la puerta roja de BV-C; no se mueve la banda.

## Hermanos anotados (revisión independiente, 11 oct)

- `KickoffFormationTests.TheKickoffWaitEndsBecauseEveryoneArrives` admite una minoría de saques al tope y por eso no
  vio esto. Podría exigir cero en la turba.
- **Salto en el primer tick del saque de la turba** (ya existía): `EnforceRestartClearance` desplaza a jugadores
  hasta 2,00 casillas en el fotograma 1 (semillas 8, 79 y 121). Choca con RF-053 y aquí no hay cortinilla (ADR 0151).
  Sin ficha propia todavía.
- La misma recta contra la barrera en las reanudaciones que no recolocan (banda, puerta, córner, falta): el rival se
  mueve por utilidad y la barrera le devuelve al borde. No bloquea ninguna espera (cuenta atrás fija). Sin medir.
- El rodeo se apaga al cruzar el medio campo: si su sitio queda al otro lado del eje del balón, acaba pegado a la
  barrera en su campo en vez de en su sitio. `EveryoneInPlace` lo acepta (es la regla); es legible.
