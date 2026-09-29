# ADR 0168 — La métrica guardiana de la sangre

Fecha: 29 sep 2026 · Estado: **aceptada**. **Decisión del revisor** (`docs/plan-diversion.md` §0: «Métrica guardiana de
la sangre: sí»; §5.9: «se mide en cada paso»). La definición y las bandas son decisión propia, sin consultar, dentro de
esa autorización (memoria: el revisor autoriza mover rangos con ADR y datos, RT-057). **Requisitos:** RT-054, RT-056,
RT-057. **Relacionada:** ADR 0048 (un sano puede morir; `deathsPerRun` 1,5-3), ADR 0095, ADR 0167.

## Por qué

La identidad de Underleague no es el fútbol, es **la carnicería administrada** (CLAUDE.md): el desgaste de la plantilla
es el recurso central. Cada cambio de balance o de diversión puede lavarla sin que nada avise: `deathsPerRun` vigila las
muertes, pero no las lesiones graves ni cuántas runs pasan sin sangre. El plan de diversión lo pidió como puerta.

## Lo que había (Regla G)

`deathsPerRun` (banda 1,5-3, ADR 0048) y, sin banda, `injuriesPerMatchBothTeams`, `ownInjuriesPerMatch` y
`severeInjuriesPerRun`. Nada que mire la sangre **propia** junta (muerte o grave) ni las runs sin ella.

## Decisión

Dos filas nuevas en `FullRunMetrics` (lote de campaña y puerta `FullRunGateTests.TheBloodIsNeverWashedOut`):

| métrica | definición | banda | procedencia (Regla H) |
|---|---|---|---|
| `bloodPerMatch` | (muertes + lesiones graves propias de la run) / partidos jugados | ≥ 0,25 | línea base 0,309-0,318 en seis lotes (abajo); suelo con un 20 % de margen, **provisional** |
| `bloodlessRunShare` | % de runs sin ninguna muerte ni lesión grave propia | ≤ 35 % | línea base 23,9-26,3 %; la puerta mide 240 runs por doctrina (error típico ≈ 2,8 puntos), así que el techo va a ~3,5 errores típicos para no fallar por mala suerte; **provisional** |

**Qué cuenta como lesión grave** (instrumento validado, Regla J): el registro de la política suma, en cada nodo, la
subida del número de jugadores en estado grave, así que incluye las graves de partidos **y** de cartas o eventos
(sacrificios, «La encerrona»…); un grave que muere en el mismo partido cuenta como muerte, no dos veces. Por eso en 88
de 1.800 runs hay más graves que lesiones de partido (`ownInjuries` sólo cuenta partidos): no es un error, es la
definición que se quiere —sangre de la run, venga de donde venga—.

No hay techo para `bloodPerMatch` ni suelo para `bloodlessRunShare`: el exceso de sangre ya lo vigila
`deathsPerRun` ≤ 3, y el objetivo de esta puerta es sólo que la carnicería no se lave.

## Línea base (29 sep 2026, `/Balance --full-runs 600`, 1.800 runs por lote)

| lote | muertes/run | graves/run | sangre/partido | runs sin sangre |
|---|---|---|---|---|
| `main` s1 / s2 | 2,06 / 2,07 | 2,08 / 2,07 | 0,309 / 0,309 | 26,3 % / 25,1 % |
| + ADR 0165 s1 / s2 | 2,06 / 2,13 | 2,11 / 2,10 | 0,311 / 0,315 | 26,2 % / 25,2 % |
| + ADR 0167 s1 / s2 | 2,11 / 2,16 | 2,17 / 2,15 | 0,318 / 0,317 | 25,8 % / 23,9 % |

**Lectura:** una de cada cuatro runs termina sin que la plantilla sufra una sola muerte o lesión grave (22-23 % incluso
en runs de seis partidos o más). Es un dato de identidad que el balance aplazado debería mirar: esta ADR **no** lo
cambia, sólo impide que empeore sin que salte una puerta.

## Las diez preguntas (`game-design-review`, resumidas)

No es una mecánica: es un instrumento. Qué experimenta el jugador: nada directamente; protege que siga viendo sangre.
Qué decide: nada. Regla: la identidad de CLAUDE.md y RF-012d (lo malo previsible) — la puerta no exige más sangre, exige
que no desaparezca. Sistemas: `Sim/Analysis/FullRunMetrics.cs`, `Sim.Tests/Analysis/FullRunGateTests.cs`. Alternativas:
sangre de los dos equipos (la del rival no desgasta la run, que es el recurso), o por acto (más ruido con 600 runs).
Degeneración: una puerta de suelo sobre una media estable (±0,005 entre semillas) no falla por mala suerte. Cómo se
demuestra: la puerta en verde con los datos de hoy y los seis lotes de arriba.
