# ADR 0170 — El desgaste es de la run y la run es más corta

Fecha: 29 sep 2026 (revisada tras la revisión independiente) · Estado: **aceptada**. **Dos decisiones del revisor**
(29 sep 2026): «las lesiones se arrastran» (A) y «de acuerdo con bajar nodos» (B; `docs/plan-diversion.md` §0). Una
tercera parte, C, es la única compensación. **Requisitos enmendados:** RF-001 (10-12 → 8-10 nodos por acto), RF-003b
(30-36 → 24-30 nodos por run); **RF-003 no se enmienda como cumplido** (ver «Duración»). **Enmienda de bandas (RT-057):**
ADR 0048 (muertes), ADR 0168 (sangre). **Relacionadas:** ADR 0033, 0043 (**enmendada**: el jefe ya no cura todo), 0053, 0095,
0163 (apodos). Cierra la pregunta 5 de `docs/plan-evolucion-knavall.md` (desbloquea F7).

## Por qué

- **A.** CLAUDE.md dice que el desgaste es el recurso central *de la run*; `economy.json` lo implementaba por *acto* (el jefe
  curaba toda la plantilla: 0,00 graves al empezar los actos 2 y 3, medido). El revisor: las lesiones se arrastran.
- **B.** El plan de diversión pide una run de 45-60 min; RF-003 decía 75-100 con 11/12/12 nodos.

## Qué cambia

**A (con matiz de la revisión).** `nodeRewards.boss` deja de curar la plantilla entera, pero **cura las lesiones LEVES**
(`healsMinorInjuries: true`, campo opcional nuevo; `healsRoster` sigue siendo «todo»). Motivo (decisión del coordinador): una
leve dura un partido de todos modos, y las **graves** son lo que se arrastra. En código, `RosterHealing {None, Minor, All}`;
un guardado anterior con sólo `healsRoster` se lee igual (y los dos campos a la vez son error explícito). El efecto `heal`
de los eventos (`faith_healer`, `guild_tithe`, `smugglers_cart`) también cura **sólo leves** (antes curaba graves, con lo
que una carta compraba el alivio que A quita); plantillas es/en actualizadas. Tests: `NodeRewardTests` (los tres modos,
guardado antiguo, campos incompatibles), `EventTests` (las tres cartas). El cartel del jefe en `/Game` dice «cura las
lesiones leves».

**B.** `map.json` 11/12/12 → **8/9/9**. Regla I: se leyó `MapGenerator`/`MapInvariants` antes. Con 8/9/9: mercados en las
capas pares 2..n-2 (RF-011b por construcción), presupuesto de partidos n·60/100 (peor camino 4+5+5 = 14, mejor 11; antes
20 y 17), clínica garantizada, élites 1/2/2. **Rangos, tras la revisión:** el `/data` vigente exige 8-10 nodos y **≥ 9 en
los actos 2 y 3** (esquema con `prefixItems` y `MapLoader`): con 8 nodos `AssignElites` puede dejar un solo élite (3 capas
de partido candidatas, la 1 no admite élite); no se arregla en el generador para no mover el RNG de todos los mapas. El
generador y **la instantánea de una run guardada** aceptan el rango histórico 8-12 (`SnapshotMaxPathLength`,
`MapLoader.FromJson(files, fromRunSnapshot: true)`, usado por `RunController`): una run ironman guardada con `map.json` de
11/12/12 **se retoma y se juega** (`MapSnapshotRangeTests`; antes de esta corrección `Continue()` fallaba — CONFIRMED por la
revisión). `MapTests` cubre las 15 combinaciones acto × longitud (8..12), no tres.

**C (única compensación).** `matchExperience` 140 → 200 (×1,43, la razón de partidos hasta cada jefe 6/13/20 → 4/9/14).
Compensa el **nivel**, no la **build**: ver «Efectos de segundo orden».

## Medición (`/Balance --full-runs 1200`, media de semillas 1 y 7; `out/` del worktree)

| métrica | antes | A (jefe no cura) | A+B | A+B+C (+leves del jefe, apodos) |
|---|---|---|---|---|
| `runWinRate` (banda 20-30; **la base ya estaba fuera**) | 16,8 | 17,2 | 13,3 | 16,0 |
| `matchesPerFullRun` (sólo runs que llegan al jefe 3) | 19,3 | 19,1 | 13,4 | 13,4 |
| partidos medios por run (`matchesPerRun`, todas) | 13,6 | | | 9,1 |
| `deathsPerRun` | 2,22 | 2,21 | 1,32 | 1,31 |
| `deathsPerMatch` (nueva, muertes / partidos) | 0,163 | | | 0,146 |
| `bloodPerMatch` (≥ 0,27) | 0,36 | 0,35 | 0,35 | 0,34 |
| `bloodlessPastAct1Share`, contextual (puerta) | ≈ 3,5 % | | | 6,9 % |
| graves al empezar acto 2 / 3 | 0 / 0 | 0,18 / 0,76 | 0,14 / 0,82 | 0,15 / 0,74 |
| oro en clínica / herrero / matasanos por run | 17,0 / 0,65 / 0,01 | 20,4 / 0,71 / 0,01 | 11,8 / 0,44 / 0,01 | 12,4 / 0,44 / 0,01 |
| nivel al llegar al jefe 1 / 2 / 3 | 4,9 / 7,0 / 7,2 | 4,9 / 7,0 / 7,2 | 3,6 / 5,9 / 6,9 | 4,1 / 7,0 / 7,6 |
| oro ganado por run / `leftoverGoldShare` | 149 / 8,6 | 147 / 9,1 | 87 / 15,3 | 93 / 14,5 |

(«A+B+C» del lote final incluye ya las leves del jefe; A y A+B son las tandas anteriores, con el jefe sin curar nada. Nota:
el lote final está sobre la base de `main` de antes de las ADR 0171/0174/0169; los deltas de A, B y C se midieron cada uno
sobre esa misma base.) Instrumento validado (Regla J): `severeAtActStartActN` da 0/0 antes de A, lo sabido.

**Lectura.** A arrastra ~0,75 graves al acto 3 y sube el gasto de clínica un 20 %, sin mover tasas fuera del ruido (ET de
`runWinRate` ≈ 1,1 por lote); B recorta un 31 % los partidos (19,3 → 13,4) y `runWinRate` ≈ −4; C devuelve niveles en los jefes
2 y 3 y recupera `runWinRate` a 16,0 (LIKELY, sin celda propia que aísle C de las leves del jefe). **`runWinRate` sigue fuera de
la banda 20-30 como antes de esta ADR.** El matasanos casi no se usa antes ni después (0,01).

## Efectos de segundo orden medidos (antes → final, media de 2 semillas)

| efecto | antes → después |
|---|---|
| maestros de build por run (`mastersPerRun`) | 0,23 → 0,12 (−48 %); ofrecidos 0,82 → 0,44, desbloqueados 0,70 → 0,35 |
| gasto en mercado por run | 113,6 → 69,7 oro; objetos en plantilla 6,4 → 4,3; objetos al llegar al jefe 1 / 2 / 3: 3,6 / 5,9 / 6,6 → 2,1 / 4,0 / 5,8 |
| perks al llegar al jefe 1 / 2 / 3 | 2,8 / 6,7 / 8,8 → 2,6 / 6,0 / 8,8 |
| némesis y venganzas por run | 0,93 y 0,23 → 0,68 y 0,12 |
| contadores acumulados por run | 10,4 → 5,5 |
| eventos tomados por run | 0,37 → 0,66 (+78 %) |
| objetos recuperados de muertos, prótesis | 1,60 → 0,59; 0,39 → 0,26 |

**C compensa el nivel, no la build**: se llega al jefe con el mismo nivel pero con menos objetos (−40 % en el jefe 1), menos
maestros y menos contadores. No se compensa (el revisor aplazó el balance fino). El «valor del mercado 9,2 → 5,8» citado por la
revisión no es una métrica del lote de `/Balance`: no lo he reproducido; la cifra más cercana medida es el gasto en mercado.
Además: la entrada de los actos 2 y 3 es un partido forzoso y, según la revisión (no re-medido aquí), en ~62 % el rival lleva
un perk letal con la plantilla tocada; con las graves arrastradas esa entrada es más peligrosa (las leves las cura el jefe).

## Bandas y puertas (RT-057; procedencia, Regla H)

| puerta | antes | ahora | qué se hizo |
|---|---|---|---|
| `matchesPerFullRun` | 18-22 | **12-15** | ±10 % del peor camino (20 → 14); medido 13,4; provisional. Sólo cuenta runs que llegan al jefe 3 (~16 %) |
| muertes | `deathsPerRun` 1,5-3 (ADR 0048) | **`deathsPerMatch` 0,11-0,22** | las cotas de la ADR 0048 ÷ 13,6 (partidos medios por run, `matchesPerRun`, con los que se midieron; **no** los 20 del peor camino). Base 0,163 → 0,146. `deathsPerRun` queda como dato con cota de no regresión 0,8-3. **Enmienda de la banda de la ADR 0048**; provisional |
| `bloodlessPastAct1Share` (ADR 0168) | ≤ 7 % | **≤ 13 %** | **La población estaba mal en la ADR 0168 y en mi primera enmienda (10):** la puerta mide sólo la doctrina **contextual** con 240 runs (~163 pasan el acto 1, ET ≈ 2,0), no las tres mezcladas. Base contextual re-medida 6,85 % (111 de 1.621); techo = base + 3 ET = 12,9 → 13. Provisional |
| `TheMapMatchesTheNodeBudgetOfRf003b` | 30-36 nodos, 18-22 partidos | 24-30, 12-15 | reescrita |
| curva de jefes (ADR 0033) y `orc_violence` ×2 | rojas en `main` | rojas | no las mueve esta ADR (no leen mapa, economía ni experiencia) |

Los tests de gate completos se anotan al final del informe de cierre.

## Apodos (ADR 0163)

Con el peor camino en 14 partidos, `eternal` (20) y `veteran` (15) eran **inalcanzables** (0 % en el censo, máximo 14) y
`customs` (20 entradas ganadas) también (máx. 17); `grubby` pasaba del 40 % (43,7 %). Censo `--nickname-census 300` sobre
runs cortas (semillas 1 y 2 con los umbrales iniciales, semilla 1 en las iteraciones): `customs` 20 → 14 (1,7 %), `grubby` 6 → 8 (32 %), `eternal` 20 → 14, el máximo del peor camino (13 daba 41 %; con 14, 26 %) y `veteran` 15 → 12 (10 daba 40,3 %; con 12, 33,7 %). Todos entre 0 y 40 %. Test nuevo: ningún umbral de `matches` supera los partidos del peor camino de `map.json`.

## Duración — RF-003 NO se enmienda como cumplido

El objetivo del plan es 45-60 min (RF-003 lo recoge como **objetivo sin cumplir ni medir**; la cifra 75-100 anterior queda
superada). Estimación **sin calibración circular** (la versión anterior calibraba el tiempo de los nodos no-partido con el
propio 75-100 de RF-003): partidos 14 × 135 s (modelo del revisor) = 31,5 min, más 12 nodos no-partido de duración **no
medida**, t min cada uno: total = 31,5 + 12·t. Con t = 1 → 43,5; t = 2 → 55,5; t = 3 → 67,5. El objetivo sólo se cumple si t
≤ 2,4 min. **La estimación central (t = 2-3) es 55-68 min y no alcanza 45-60 con seguridad.** Provisional, sin medir en reloj.

## Lo que queda abierto

`runWinRate` 16,0 (banda 20-30, ya fuera antes); `brokeMarketRunShare` 9,6 (banda 10-25, recién fuera); `leftoverGoldShare`
14,5 en el borde; `contextualAdvantage` 2,7 (banda ≥ 8, ya fuera). El revisor aplazó el balance fino. Techos y bandas de esta
ADR: provisionales, con una sola configuración de mapa.
