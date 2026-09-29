# ADR 0170 — El desgaste es de la run y la run es más corta

Fecha: 29 sep 2026 · Estado: **aceptada**. **Dos decisiones del revisor** (29 sep 2026): «las lesiones se arrastran»
(A) y «de acuerdo con bajar nodos» (B; `docs/plan-diversion.md` §0: «Run más corta: sí, menos nodos por acto. Sin x4
automático»). **Requisitos enmendados:** RF-001 (10-12 → 8-10 nodos por acto), RF-003 (75-100 → 45-60 min), RF-003b
(30-36 → 24-30 nodos por run). **Relacionadas:** ADR 0033 (jefes como puertas de build), ADR 0043 (trampolín y desgaste
por acto: **revierte su `healsRoster` del jefe**), ADR 0053 (mapa de cuatro carriles), ADR 0095 (`matchExperience`),
ADR 0168 (métrica guardiana de la sangre). Cierra la pregunta 5 de `docs/plan-evolucion-knavall.md` (desbloquea F7).

Tres partes, medidas por separado y una sobre otra (`/Balance --full-runs 1200`, semillas 1 y 7, ≈ 2.400 runs por
celda): **A** el jefe no cura, **B** el acto tiene 8/9/9 nodos, **C** una única palanca de compensación.

## Por qué

- **A.** CLAUDE.md dice que el desgaste de la plantilla es el recurso central *de la run*. `economy.json` lo
  implementaba por *acto*: superar el jefe curaba a toda la plantilla (`nodeRewards.boss.healsRoster: true`), así que
  ninguna lesión grave cruzaba de un acto al siguiente (medido, abajo: 0,00 graves al empezar los actos 2 y 3). El
  revisor lo resuelve: las lesiones se arrastran. El alivio son la clínica, el herrero y el matasanos, que cuestan oro.
- **B.** El plan de diversión pide una run de 45-60 min; RF-003 decía 75-100 con 11/12/12 nodos (35 nodos, 20 partidos
  en el peor camino).

## Qué cambia

**A.** `data/economy/economy.json`: `nodeRewards.boss.healsRoster` → `false` (ya era `false` en liga y élite). El
consumidor (`StandardRunSystems.AfterMatch`) no cambia: el campo sigue siendo un mando de datos. El cartel de
recompensa del jefe (`ui.reward.optionsBoss`) dejaba de decir «y cura la plantilla». Test: `WinningTheBossNoLongerHealsTheRoster`
(la grave y las leves siguen tras el jefe; el muerto sigue muerto, RF-093).

**B.** `data/map/map.json`: `nodesPerAct` 11/12/12 → **8/9/9**. Regla I: antes de tocarlo se leyó el generador
(`MapGenerator`) y `MapInvariants`. Rango de RF-001 ahora **8-10** (`MinPathLength` 8, `MaxPathLength` 10,
`DefaultPathLength` 9; esquema `map.schema.json` y `MapLoader` en consonancia). De qué depende el número de capas y qué
pasa con 8/9/9:

| depende del número de capas | con 8/9/9 |
|---|---|
| Capas de mercado (`MarketLayers`): las pares de la 2 a la `n-2`, cada 2 capas | 8 nodos → 2, 4, 6; 9 nodos → 2, 4, 6. RF-011b (mercado a ≤2 saltos) sale por construcción; `MarketLayers` lo comprueba y no falla. El «cada 3-4 nodos» de RF-011b ya se leía como «cada 2 capas» desde la ADR 0053: no cambia |
| Presupuesto de partidos (`n·60/100`, RF-003b sobre el peor camino) | 8 → 4, 9 → 5 (el peor camino juega 14 partidos por run, el mejor 11; antes 20 y 17) |
| Clínica garantizada (primer hueco de servicio, RF-094) | sigue: 500 semillas × 3 actos × 8/9/10 nodos en `ShortActs_KeepTheClinicTheElitesAndEveryInvariant` |
| Élites de `AssignElites` (1 en el acto 1, 2 en los demás, nunca en las capas 0-1) | con 8 y 9 nodos salen; con **8 en el acto 2 o 3** puede salir uno solo (no lo envía `map.json`: 8/9/9 pone 9 en los actos 2 y 3). Con 9 y 10 salen siempre los dos |
| Rivales estáticos por acto, `MapNode.Difficulty` | sin dependencia del número de capas |
| `RunPolicy.SlotHorizonOf` usa `DefaultPathLength` (11 → 9) para estimar las capas de los actos futuros | es una estimación de oferta futura; 9 es ahora más fiel que 11 |
| `Game/Screens/MapScreen` | `PathLength(map)` en el progreso y `/2` en la captura; sin números fijos |

Bandas y pruebas movidas por B (RT-057; procedencia, Regla H): `matchesPerFullRun` 18-22 → **12-15** (banda de ±10 %
alrededor del peor camino, que pasó de 20 a 14; medido 13,36 y 13,38; **provisional**); `TheMapMatchesTheNodeBudgetOfRf003b`
30-36 nodos y 18-22 partidos → 24-30 y 12-15; `MapTests` (3.000 mapas de longitud 8-10 y 500 semillas por longitud y
acto); `RunEngineTests.ARunCanBePlayedFromStartToFinish` 24-30 nodos y 11-16 partidos.

**C (única compensación).** Con B el nivel al llegar al jefe cae (abajo: 3,6 / 5,9 / 6,9 contra 4,9 / 7,0 / 7,2) y la
puerta de jefes de la ADR 0033 mide con el jugador a nivel 5/6/7. Palanca: `data/sim/tuning.json`
`progression.matchExperience` **140 → 200** (×1,43: los partidos jugados hasta cada jefe pasan de 6/13/20 a 4/9/14, una
razón de 1,5/1,44/1,43). Regla I: se leyó `Progression.AwardExperience`; sólo multiplica la experiencia por partido y
`benchSharePercent` (45 %) sigue igual, así que el banquillo sube en la misma proporción.

## Medición (`/Balance --full-runs 1200`, media de las semillas 1 y 7; salidas en `out/` del worktree)

Semillas: las dos separadas están al final. El error típico de `runWinRate` con 1.200 runs es ≈ 1,1 puntos por lote.

| métrica | antes | A (jefe no cura) | A+B (8/9/9) | A+B+C (`matchExperience` 200) |
|---|---|---|---|---|
| `runWinRate` (banda 20-30) | 16,80 | 17,17 | 13,30 | **14,84** |
| `matchesPerFullRun` | 19,27 | 19,07 | 13,37 | 13,35 |
| `deathsPerRun` (banda 1,5-3) | 2,22 | 2,21 | 1,32 | 1,39 |
| `bloodPerMatch` (≥ 0,27) | 0,36 | 0,35 | 0,35 | 0,36 |
| `bloodlessPastAct1Share` (runs que pasan el acto 1; techo de la ADR 0168) | 3,5 % | 3,7 % | 7,0 % | 7,1 % |
| oro en clínica por run | 17,0 | 20,4 | 11,8 | 12,4 |
| tratamientos del herrero por run | 0,65 | 0,71 | 0,44 | 0,45 |
| tratamientos de matasanos (`riskyTreatmentsPerRun`) | 0,015 | 0,015 | 0,015 | 0,010 |
| tratamientos de plantilla entera por run | 0,25 | 0,35 | 0,18 | 0,20 |
| **graves al empezar el acto 2 / 3** (`severeAtActStartActN`, nueva) | **0,00 / 0,00** | 0,18 / 0,76 | 0,14 / 0,82 | 0,15 / 0,75 |
| graves por run (`severeInjuriesPerRun`) | 2,33 | 2,57 | 1,73 | 1,76 |
| nivel al llegar al jefe 1 / 2 / 3 (`levelAtBossActN`) | 4,90 / 6,95 / 7,16 | 4,90 / 6,95 / 7,19 | 3,55 / 5,90 / 6,85 | **4,14 / 6,96 / 7,53** |
| oro ganado por run | 148,9 | 147,1 | 87,1 | 93,0 |
| `leftoverGoldShare` (techo 15) | 8,6 | 9,1 | 15,3 | 14,9 |

**Instrumento validado (Regla J).** `severeAtActStartActN` sale 0,00 / 0,00 antes de A, que es la respuesta que ya se
sabía (el jefe curaba a toda la plantilla); con A aparecen 0,18 y 0,76 graves de media al empezar los actos 2 y 3. Nace
en esta ADR (`Sim/Analysis/RunPolicy.cs`, `FullRunMetrics`): graves de la plantilla antes del primer partido de cada acto
(la capa 0 es siempre un partido de liga), con las runs que llegan a ese acto como denominador.

**Lectura.**

- **A (LIKELY, dentro del ruido en las tasas, CONFIRMED en el desgaste).** Arrastra 0,76 graves por plantilla al acto 3 y
  sube el gasto en clínica un 20 % y los tratamientos de plantilla entera un 40 %: la carne cuesta oro. `runWinRate`
  (15,7 / 17,9 → 15,4 / 18,9), `deathsPerRun` y `bloodPerMatch` no se mueven fuera del ruido; **A no lava ni intensifica**
  la carnicería según la banda guardiana. El matasanos casi no se usa ni antes ni después (0,015 por run): el arrastre no
  lo empuja, y es un dato para el diseño de F7.
- **B.** Recorta un 31 % los partidos por run (19,3 → 13,4), baja `deathsPerRun` a 1,3 (proporcional: 2,2 × 14 / 20 ≈ 1,5),
  el nivel en el jefe 1 de 4,9 a 3,6, el oro ganado por run de 149 a 87 y sube el `leftoverGoldShare` a 15,3 (techo 15): hay
  menos nodos donde gastarlo. `runWinRate` baja de 17,2 a 13,3 (varios errores típicos: ≈ 0,8 sobre 2.400 runs).
- **C.** Devuelve los niveles en los jefes 2 y 3 (6,96 y 7,53 frente a 6,95 y 7,16) y queda corto en el 1 (4,14 frente a
  4,90: son 4 partidos por acto, y el nivel entre 4 y 5 es un escalón de 250 de experiencia). `runWinRate` 14,8: recupera
  la mitad de lo que B quitó. **No se busca más**: con una sola palanca no se llega a la banda de 20-30 %, y **la base ya
  estaba fuera de ella (16,8)**; el revisor aplazó el balance fino (`plan-diversion.md` §0).

**Comprobación de comportamiento (rondas de balance-measure).** Concentración de la muerte y de la sangre no medida por
jugador ni por posición: no hay hipótesis de comportamiento que discrimine (ni A ni B tocan el motor de partido).
`bloodPerMatch` idéntico a tres cifras con el partido sin tocar es la comprobación de que la sangre por partido no cambia.

## Puertas completas (Category=Gate, 46 pruebas)

Corridas tras A+B+C (rama con la base de `main` del 29 sep). Se mueve **una**, y se re-mide, no se toca a ojo (RT-057):

| puerta | antes | ahora | qué se hizo |
|---|---|---|---|
| `TheBloodIsNeverWashedOut` (`bloodlessPastAct1Share` ≤ 7 %, ADR 0168) | 3,5 % | 7,0-7,9 % | **Techo 7 → 10 %, provisional.** Motivo: con menos partidos entre el inicio y el jefe 1, más runs lo pasan sin una baja de sangre (`bloodPerMatch` intacto en 0,35: la carnicería por partido es la misma). Base re-medida: 7,1 % (342 de 4.821 runs que pasan el acto 1); la puerta ve ~500 runs, error típico ≈ 1,15 puntos, así que 10 queda a ≈ 2,5 errores típicos de la base (la misma vara de la ADR 0168). Ver «Lo que queda abierto» |
| `AFullRunLastsBetween12And15Matches` (antes 18-22) | 19,3 | 13,4 | banda reescrita arriba |
| `TheMapMatchesTheNodeBudgetOfRf003b` | 30-36 nodos | 24-30 | reescrita arriba |
| `TheGoldOfAnActPaysTwoOrThreeSinksAndNeverAllOfThem` (roja en la rama de la ADR 0165) | roja | **verde** | no tocada; se anota: el oro por run cae con la run más corta |
| `BossGateTests.TheGateCurveMatchesTheAdr0033Table` (`eternal_crown_excellent` 41,62; banda 50-70) | roja en `main` | roja | **No la mueve esta ADR** (CONFIRMED por lectura: el lote de jefes no referencia `Progression`, economía ni mapa; mide un partido aislado con el jugador a nivel fijo). Sigue abierta como estaba |
| `BuildGateTests` (`orc_violence`, 2 pruebas) | rojas en `main` | rojas | ídem: no dependen de mapa, economía ni experiencia |

**Resultado final (rama rebasada sobre `main`, techo de sangre 10 %): 43 verdes de 46; las tres rojas son las de `main`** (curva de jefes y `orc_violence` ×2, mismos valores: 41,62 y 53,36). Suite `Category!=Gate`: 1.655, todas en verde tras corregir `LethalRiskTests.BeingHurtMultipliesTheNumberOfTheSamePlayerInTheSameCell` (con el mapa nuevo el primer partido letal de la búsqueda dejaba a todos en el techo de 8000 del indicador; ahora busca un escenario donde el multiplicador se vea).

Los datos que ADR 0033 (curva de jefes) mide con «nivel 5/6/7» siguen siendo comparables: con C el nivel al llegar al
jefe es 4,1 / 7,0 / 7,5, contra 4,9 / 7,0 / 7,2 antes.

## Duración estimada — **provisional, sin medir en reloj**

La duración real sólo se mide con la interfaz y un jugador. Estimación con el modelo del revisor: partido ≈ 135 s de
reloj (60-90 s de simulación más los cortes de UI-003) y el tiempo de los nodos no-partido (mercado, clínica, evento,
entrenamiento) **calibrado con el propio RF-003**: 35 nodos con 20 partidos (45 min) en 75-100 min implican 2,0-3,7 min
por nodo no-partido.

| run completa | partidos | nodos no-partido | partidos × 135 s | + no-partido | total |
|---|---|---|---|---|---|
| antes (11/12/12) | 20 | 15 | 45 min | 30-55 min | 75-100 min (RF-003) |
| ahora (8/9/9) | 14 | 12 | 31,5 min | 24-44 min | **≈ 55-76 min** |

**El 45-60 min del plan queda cerca pero no se alcanza en la banda alta**: 8/9/9 es el punto que se ha probado (el rango que admite el generador desde esta ADR arranca en 8; **bajar de 8 no se ha probado**: con RF-003b, RF-011b, la clínica garantizada y dos élites por acto el espacio de capas es estrecho). Lo que quedaría sin ir más allá en nodos es reducir el tiempo por nodo (UI) o la duración del partido. No se toca aquí. La media medida de partidos
por run es 13,4, porque el 85 % de las runs terminan antes del final: la duración *de una run típica* es menor que la
del camino completo.

## Lo que queda abierto

- `runWinRate` 14,8 con banda 20-30 y `deathsPerRun` 1,39 con banda 1,5-3 y `leftoverGoldShare` 14,9 (techo 15) están en
  el borde o fuera; el revisor aplazó el balance fino. La base ya estaba fuera de banda en `runWinRate` (16,8).
- El techo de `bloodlessPastAct1Share` se ha movido con una sola configuración de mapa: provisional.
- `deathsPerRun` 1,39 < 1,5 se mueve por la cuenta de partidos (proporcional); una banda por partido sería más estable
  que una por run. No se toca sin decisión del revisor (ADR 0048).

## Detalle por semilla

| | base 1 | base 7 | A 1 | A 7 | A+B 1 | A+B 7 | A+B+C 1 | A+B+C 7 |
|---|---|---|---|---|---|---|---|---|
| `runWinRate` | 15,67 | 17,92 | 15,42 | 18,92 | 11,92 | 14,67 | 14,25 | 15,42 |
| `matchesPerFullRun` | 19,30 | 19,23 | 19,05 | 19,08 | 13,36 | 13,38 | 13,30 | 13,40 |
| `deathsPerRun` | 2,22 | 2,22 | 2,19 | 2,22 | 1,29 | 1,35 | 1,41 | 1,37 |
| `bloodlessPastAct1Share` (runs.csv) | 3,92 | 3,13 | 4,04 | 3,33 | 7,07 | 6,94 | 6,76 | 7,43 |
