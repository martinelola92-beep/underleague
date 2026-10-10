# ADR 0193 — La Corona Eterna baja a calidad 25 tras el saque de puerta de BX-8

Fecha: 10 oct 2026 · Estado: **aceptada** (autónoma, RT-057; el revisor autoriza mover rangos con ADR y datos).
Recalibra `data/bosses/eternal_crown.json` (`template.quality` 31 → 25). No toca ninguna banda (la tabla de la ADR 0033
con la enmienda de la ADR 0089, que bajó `excellent` de este jefe a 50-70) ni el margen de medida de `BossGateTests`. Relacionadas: ADR 0033 (curva de jefes), 0049 y 0050 (recalibraciones anteriores
del mismo jefe), 0027 (salvaguarda sin legendarios), BX-8 (`docs/pendientes/BX-8.md`).

## Contexto

- **El arreglo del saque de puerta (BX-8)** quita el despeje regalado del portero: −0,2/−0,3 goles por partido en
  `/Balance`.
- **Eso endurece a la Corona Eterna en los tres niveles de build, CONFIRMED con dos semillas.**
  - `correct` 12,3/11,4; `good` 37,1/34,6; `excellent` 39,0/38,8.
  - Bandas: 15-28, 40-55 y 50-70 (esta última, de la ADR 0089), con ±2,5 de margen.
- **Mecanismo (LIKELY, `/Balance --boss-gate`, semilla 1).** La caída sale mitad y mitad de los dos canales del jefe:
  - menos victorias en el campo (−1,4 a −1,7 puntos);
  - más empates que «El campeón conserva el título» convierte en derrota (+0,4 a +1,7).
  - Es el jefe del cerrojo (`pushBack` a la columna 6, `banChannel` del remate a puerta): menos goles le favorecen
    por los dos lados.
- `grimhold_guns`, que estaba rojo, entra en banda con el mismo cambio.
- **La celda `excellent` ya estaba roja antes de BX-8** (41-42 desde las ADR 0184 y 0188), así que el jefe pedía
  recalibración igualmente.

## Decisión

`template.quality` 31 → **25**. Es la palanca de las dos recalibraciones anteriores (ADR 0049 y 0050): sube las cuatro
celdas a la vez sin tocar la regla del jefe.

Barrido, puerta completa (5.120 partidos por celda):

| Calidad | Semilla | incoherente (≤ 10) | correct (15-28) | good (40-55) | excellent (50-70) |
|---|---|---|---|---|---|
| 31 | 1 / 2 | ok / ok | 12,3 / 11,4 | 37,1 / 34,6 | 39,0 / 38,8 |
| 28 | 1 | 4,0 | ok | ok | 47,19 (fuera del margen) |
| 27 | 1 / 2 | 3,95 / 3,34 | 16,84 / 17,29 | 45,33 / 43,48 | 48,85 / **46,72** |
| 26 | 1 / 2 | 3,96 / 3,52 | 19,22 / 16,88 | 46,62 / 45,80 | 49,55 / 48,30 |
| **25** | 1 / 2 | 4,57 / 3,98 | 19,41 / 18,20 | 47,70 / 46,72 | **51,50 / 50,16** |

- Las cifras de 28 que faltan no se volcaron: entonces la puerta sólo enseñaba las celdas que fallaban.
- 26 y 27 dejan `excellent` apoyada en el margen de medida, y 27 lo rompe con la semilla 2.
- **25 es la primera calidad con las cuatro celdas dentro de banda, en las dos semillas.**
  - «Sin margen» sería exagerar: `excellent` da 50,16 con la semilla 2, frente a un mínimo de 50. Esa diferencia está
    por debajo del error típico de una celda (0,20-0,89, `BossGateTests`).
  - Se eligió la primera calidad que cruza: es un ajuste al borde, y el margen de ±2,5 sigue haciendo falta.
- La escalera sigue siendo monótona (`EveryBossRewardsABetterBuild`). La salvaguarda de la ADR 0027 (una build muy
  buena sin legendarios gana al jefe final) queda con más holgura.

## Consecuencias medidas

- **Puertas completas** (`Category=Gate`, semilla 1, rama `bx8-sobre-bx10`): queda **una** métrica roja,
  `coherentBuildsBeatNone_orc_violence`, con 57,45 frente a 58; ya era roja en `main` (54,77).
  - La puerta de jefes entra entera en banda.
  - `badBuildsLoseToNone_elf_brawler` y todas las de runs completas, en verde.
- **Runs completas** (`/Balance --full-runs 240`, semillas 1/2, calidad 31 → 25):
  - `runWinRate`: 14,17 → 15,42 y 15,42 → **20,83**. La banda es 20-30: con la semilla 2 entra.
  - `bossWinRateAct3`: 42,5 → 46,3 y 39,8 → 53,8.
  - **En contra:** `runWinRate_noMarket` (la política que no usa el mercado) sube de 7,92 a 10,42 y de 8,75 a
    13,33. Su techo es 5, y ya estaba fuera. El jefe final más blando también lo es para quien no construye.
  - **Sin evidencia de efecto, por lo que se ha medido:** `contextualAdvantage`, −2,9 con una semilla y +2,5 con la
    otra.
- `docs/catalogo-perks-y-objetos.md` no cambia: el jefe no es contenido del catálogo.

## Lo que esta ADR no explica

- **La calidad del jefe hace de sumidero de los cambios del motor.**
  - Historial: ADR 0049 (28 → 24), 0050 P2 (24 → 29), 0050 P1 (29 → 31, `bb8d249`) y ahora 31 → 25, el mayor
    movimiento.
  - `excellent` ya llevaba 41-42 desde las ADR 0184 y 0188 sin explicación. Esta ADR lo absorbe junto con BX-8.
- **Nadie ha decidido si «el cerrojo se fortalece cuando se marcan menos goles» es la identidad del jefe o un
  acoplamiento que habría que cortar.** Es una pregunta de diseño (`game-design-review`), no de calibración.
- **Las runs completas son 240 por semilla**: el error típico de `runWinRate` ronda los 2,6 puntos. «Entra en banda
  con la semilla 2» (20,83 frente a 20) no es evidencia por sí solo.

## Pendiente

- `runWinRate_noMarket` fuera de techo es el problema de la economía (las sondas de la ADR 0182 y el mercado), no de
  este jefe. Queda anotado y no se compensa aquí.
