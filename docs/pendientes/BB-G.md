# BB-G — El balón se queda parado en el campo

**Estado:** Resuelta (ADR 0117, 16 sep 2026): `chaseBallLooseBonus` sube de 250 a 410 en
`data/ai/weights.json`. Episodios de balón suelto ≥15 ticks bajan de 107 a 32 en 200 partidos (−70 %), el
más largo de 860 a 38 ticks; 43 puertas 4 rojas → 3, sin regresión atribuible tras remedir con una segunda
semilla (ver "Fase 3 — Resultado" más abajo, corregido tras `independent-reviewer`). Quedan hipótesis
vivas sin probar (penalización de zona local, geometría del radio de recogida, precondición de motor con
estado temporal) — ver el final de esa sección.

## Observación

«El balón se queda parado en el campo (a veces en la línea de banda o fondo). ¿No hay saques?»

## Medición

**MEASURED**, 40 partidos con traza: **23 balones muertos de 15+ ticks en juego abierto, el más largo
1.096 ticks** — 73 segundos, casi el partido entero.

## Hipótesis exploradas, en el orden real en que se probaron

No se ordenaron por poder discriminativo antes de empezar — es la lección que deja este problema para
`gameplay-debug` (ver `docs/analisis/auditoria-organizacion-v2.md` §2).

1. **H1 — el muro de la zona de acción bloquea al perseguidor designado.** *(REJECTED bajo la medición
   actual.)* Se escribió un parche que abría el límite duro de la zona solo para `ChaseBall` sobre balón
   suelto. Medido: el número de balones muertos **no cambió**. Revertido.
2. **H2 — el portero acapara la designación de perseguidor y produce un abrazo mortal** (él no puede
   perseguir fuera de su área, y los diez de campo no pueden por no ser el designado). *(REJECTED bajo la
   medición actual — no descartada en general, ver H2b abajo.)* Se escribió el parche que excluye al
   portero de la designación fuera de su área. Medido: el número de balones muertos **no cambió**.
   Revertido.
3. **H3 — `ChaseBall` está legal y puntuada, pero pierde contra las acciones de colocación.** *(CONFIRMED,
   reproducido mediante volcado de la tabla de utilidad — RT-098 — en el tick 1301 de la semilla 33.)* Con
   el balón quieto y catorce jugadores a menos de 9 casillas: el delantero id6, a 1,58 casillas, puntúa
   `ChaseBall` en **884** y elige `FindSpace`; el defensa id102, a 1,15, la puntúa **340** y elige
   `CoverSpace`. `chaseBallLooseBonus` no compensa lo que valen las acciones de colocación.

**H2b (BB-G2, no confirmada ni descartada, latente):** el abrazo mortal portero-designado que describía H2
es real como mecanismo — `UpdateContextCaches` nombra al más cercano incluyendo al portero, y
`EvaluateChaseBall` lo descarta fuera de su área — pero **no se ha observado disparándose** en 40 partidos.
No se cuenta como descartada: es una hipótesis **sin evidencia de activación**, distinta de una hipótesis
**refutada por medición** (H1, H2 arriba). Reabrir si aparece un balón muerto con el portero como jugador
más cercano.

## Por qué no se ha arreglado todavía

El arreglo de H3 es un cambio de pesos (`data/ai/weights.json`), no de motor, y **exige cautela
demostrada**: es la misma palanca (`ChaseBall`) que la auditoría 5 tocó con `pen=50` y hubo que retirar
porque degradaba la diferenciación de builds (3 puertas rojas → 5 → 7 — ver `docs/decisiones/` y
CLAUDE.md, sección de convenciones). Se mide con **una hipótesis de valor por vez** y mirando **todas** las
métricas de diferenciación, no solo la del balón muerto.

## Hermanos

Ninguno detectado con la misma causa. Comparte instrumento (RT-098, volcado de utilidad) con el método que
resolvió BB-M.

## Fase 3 — Experimento (16 sep 2026, `balance-measure`, pre-registrado antes de tocar `/data`)

**Baseline actual** (árbol con BB-B/ADR 0115 y BA-N/ADR 0116 ya cerradas), instrumento temporal
`Sim.Tests/Analysis/_BBG.cs`, 200 partidos del conjunto de referencia (RT-081, mismas semillas que las
puertas), episodios de balón suelto y quieto (`ball.Owner is null && !ball.InFlight`, en `MatchPhase.OpenPlay`)
de 15+ ticks consecutivos:

| métrica | baseline |
|---|---|
| episodios ≥15 ticks (200 partidos) | **107** |
| partidos con ≥1 episodio | 88/200 (44 %) |
| episodio más largo | 860 ticks |

Consistente con la muestra original de BB-G (23/40 = 57,5 % partidos con episodio, aquí 88/200 = 44 % —
mismo orden de magnitud, no la misma medición exacta: distinta semilla y tamaño de muestra).

**Medición barata antes de tocar código** (Regla A): en vez de adivinar cuánto subir
`chaseBallLooseBonus`, se volcó la tabla de utilidad (RT-098) del jugador más cercano al balón en el
punto medio del episodio más largo (partido 172, tick 771, defensa id 403, balón a ~0,6 casillas):
`ChaseBall` puntúa **586** (Context 183 = 250 de `chaseBallLooseBonus` − 67 de penalización de
distancia) y pierde contra `CoverSpace`, que puntúa **738** (Context 150, fijo:
`coverBetweenBallAndGoalBonus`, no depende de la distancia). **Hueco medido: 152 puntos.**

**Hipótesis única**: subir `context.chaseBallLooseBonus` (`data/ai/weights.json`) de 250 a **410**
(+160, el hueco medido de 152 más un margen de 8) cierra ese hueco concreto sin buscar el número por
tanteo. Es la misma palanca que la causa CONFIRMED de H3 (arriba): el bono de balón suelto es plano
(no depende de la distancia ni de la posición táctica), así que subirlo desplaza `ChaseBall` hacia
arriba en **todas** las comparaciones donde compite por un balón suelto, sin tocar `Tackle`, `MarkOpponent`
ni ninguna otra acción.

**Precaución explícita** (precedente `docs/auditoria-ia-jugadores-5.md`/`-9.md`, un `ChaseBall pen`
**distinto** —el de Tackle-vs-ChaseBall, no este— fue aceptado sobre un baseline sucio y tuvo que
rechazarse al remedir sobre uno limpio: 3 puertas rojas → 5 → 7, todas de diferenciación de builds
moviéndose juntas): **si dos o más de las puertas de diferenciación de abajo se mueven a la vez en la
misma dirección, se trata como señal, no como ruido, aunque cada una por separado pareciera aceptable.**

- **Métrica primaria**: episodios ≥15 ticks / 200 partidos (baseline 107).
- **Métricas secundarias**: partidos con ≥1 episodio (88/200), episodio más largo (860 ticks),
  `possessionChanges`/`passChainAvgLength`/`shotsPerMatch`/`tacklesPerMatch`/`injuriesPerMatch` del lote
  de `/Balance` (RT-056), y el clúster de puertas de diferenciación de build
  (`CoherentBuildsBeatTheirBaseline`, `BadBuildsLoseToTheirBaseline`, `BuildsWinDifferently`,
  `TheThreeDoctrinesBuyDifferently`, `NoGateMetricIsOutOfRange`).
- **Gates que no deben degradar**: las 43 puertas no pueden ganar rojas nuevas más allá de las 4
  preexistentes (`TheThreeDoctrinesBuyDifferently`, `CoherentBuildsBeatTheirBaseline`,
  `BadBuildsLoseToTheirBaseline`, `NoGateMetricIsOutOfRange`) salvo que se demuestre con remedición que el
  movimiento es ruido de muestreo (patrón BB-P), nunca por descarte a ojo.
- **Criterio de aceptación**: la métrica primaria baja **al menos el 50 %** (≤53 episodios) Y ninguna
  puerta de diferenciación nueva entra en rojo (o, si entra una sola, se remide para descartar ruido antes
  de acompañarla de una segunda) Y el lote de `/Balance` se mantiene dentro de banda (RT-056).
- **Criterio de rechazo**: la métrica primaria baja menos del 30 %, O dos o más puertas del clúster de
  diferenciación se mueven a la vez en la misma dirección, O una métrica de `/Balance` sale de banda.
- **Sin grid search**: un único valor (410), una única medición, comparación contra este baseline. Si el
  experimento no cumple el criterio de aceptación, se registra qué se aprendió y se reformula la
  hipótesis — no se prueba otro número automáticamente.

## Fase 3 — Resultado: ACEPTADO (ADR 0117), tras corregir un rechazo prematuro

**Historial de esta sección**: la primera versión, escrita antes de una revisión independiente, decía
RECHAZADO. `independent-reviewer` encontró que el rechazo se apoyaba en una única medición (semilla 1) de
dos puertas del clúster de diferenciación, sin la remedición con otra semilla que el propio criterio de
aceptación pre-registrado exigía antes de contar una puerta nueva en rojo como señal. Esa remedición
(§ "Qué corrigió la revisión", abajo) mostró que ninguna de las dos puertas sobrevive a un segundo dato:
ambas fallan también en configuraciones donde `chaseBallLooseBonus` no se ha tocado. La decisión se
invirtió a ACEPTADO. El detalle completo, con todas las tablas, vive en la ADR 0117 —
`docs/decisiones/0117-el-bono-de-balon-suelto-sube-para-que-perseguir-gane.md` —; aquí solo el resumen.

**El síntoma se corrige de sobra.** Con `chaseBallLooseBonus=410` (200 partidos del conjunto de
referencia, semilla 1):

| métrica primaria/secundaria | baseline | experimento | delta |
|---|---|---|---|
| episodios ≥15 ticks | 107 | **32** | −70 % |
| partidos con ≥1 episodio | 88/200 | 27/200 | −69 % |
| episodio más largo | 860 ticks | 38 ticks | −96 % |

**Qué corrigió la revisión**: la primera lectura de las 43 puertas (semilla 1) veía
`CoherentBuildsBeatTheirBaseline` empeorar (56,67→55,62) y `BuildsWinDifferently`/`passChain` entrar en
rojo (verde→1,1077, contra un umbral de 1,1100) y las leyó como "dos puertas de diferenciación moviéndose
juntas" — el criterio de rechazo pre-registrado, aplicado sin la remedición que el criterio de aceptación
exigía primero. Remedido con semilla 2 (4 combinaciones baseline/experimento × semilla 1/2, ver ADR 0117
para la tabla completa): **las tres puertas en cuestión fallan en 3 de las 4 combinaciones, baseline
incluido** — `passChain` falla incluso en `baseline/semilla 2` (1,0931 < 1,11), sin tocar el peso.
`orc_violence` fluctúa 55,62-57,71 en las cuatro celdas, dentro del error típico de 2,3 puntos que la
propia puerta declara. Ninguna de las dos muestra una degradación atribuible al cambio que no se explique
igual de bien por el ruido ya presente en el baseline (mismo patrón que `docs/pendientes/BB-P.md`
documenta para BA-N, aplicado aquí a la decisión de aceptar/rechazar un experimento, no solo a una
narrativa histórica).

**Neto de las 43 puertas** (semilla 1, canónica): 4 rojas → **3 rojas**
(`TheThreeDoctrinesBuyDifferently` y `BadBuildsLoseToTheirBaseline` se arreglan; `CoherentBuildsBeatTheirBaseline`
y `BuildsWinDifferently` siguen/entran en rojo, ambas dentro de su rango de ruido ya medido).

`/Balance` (2.000 partidos, semilla 1): todas las bandas obligatorias de RT-056 dentro de rango.

**Decisión: ACEPTADO**, aplicando el criterio de aceptación tal como se pre-registró: la métrica primaria
baja muy por encima del 50 % exigido, y la única puerta nueva en rojo (`BuildsWinDifferently`) no
sobrevive a la remedición con otra semilla que el propio criterio pedía antes de contarla como señal.

**H3 sigue CONFIRMED** como causa; la corrección de la ADR 0117 encima precisa **cómo** actúa el hueco
medido (27 de distancia + 40 de penalización de zona escalada por disciplina, no "67 de distancia" como
decía la primera redacción) — detalle completo en la ADR.

**Hipótesis que quedan vivas, ninguna medida en este ciclo** (H1 reetiquetada: REJECTED solo para el
**límite duro** de zona, no para la penalización blanda, que sí interviene aquí — ver ADR 0117):

- Eximir o reducir `OutsidePenalty` solo para `ChaseBall` sobre balón suelto, en vez de subir el bono
  plano — más local, no tocaría los casos donde `ChaseBall` ya gana dentro de zona.
- La geometría del equilibrio: el jugador del episodio volcado quedaba a 0,62 casillas, 0,12 fuera del
  radio de recogida (`PickupRadius=0,5`) — parte del atasco podría ser de geometría, no solo de tabla de
  utilidad.
- Una precondición dura de "perseguir es la única opción cuando el balón lleva N ticks suelto": es una
  **primitiva de motor con estado temporal**, dispara `game-design-review` + `architecture-review`
  (Regla B/C), no `balance-measure` — no se implementa como "otro ajuste de pesos" sin pasar por ahí.
- Medir el hueco de varios episodios largos, no solo el más largo, para saber si 410 quedó ajustado,
  corto o sobrado para el caso típico.

**Riesgo de diseño anotado, no resuelto**: `CoverSpace` gana con un bono fijo (150) mientras `ChaseBall`
paga dos penalizaciones variables (distancia, zona escalada por disciplina) — una asimetría estructural
que subir el bono desplaza, no elimina. Pregunta para `game-design-review`, no conclusión de este ciclo.

Instrumento temporal `Sim.Tests/Analysis/_BBG.cs` borrado antes de cerrar este ciclo (convención
`_A5.cs`/`_A9.cs`). `data/ai/weights.json` con el cambio aplicado (ADR 0117): `chaseBallLooseBonus`
250→410.
