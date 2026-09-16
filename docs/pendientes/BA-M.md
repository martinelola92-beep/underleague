# BA-M — Origen de BA-N: la puerta de equipar pasó de roja a verde por cero centésimas

**Estado:** Plegada en **BA-N** (misma métrica). **Corregida (16 sep 2026) tras `independent-reviewer`
sobre ADR 0116**: la premisa de este fichero era falsa, y con ella la acusación al commit `a0a8b33`.

## Observación

`EquippingAGoodBuildIsWorthSeveralPointsOfWinRate` pasa por cero centésimas: +2,0 puntos contra un umbral
de ≥2,0.

## Análisis — corregido

**Lo que este fichero afirmaba**: pasó de roja a verde con el commit `a0a8b33` (las seis primitivas de
tanda 1), cuyo mensaje afirmaba que no movían balance por construcción — "era falso, y medible: la métrica
es determinista (semilla fija, 6.144 partidos por brazo), así que no es ruido".

**Por qué era un error de método**: el determinismo garantiza que *la misma build, jugada otra vez, da el
mismo número* — no dice nada sobre el **error de muestreo** de una diferencia de tasas estimada sobre
6.144 partidos. Esa diferencia tiene un error típico de ~0,9 puntos (medido en `docs/decisiones/0116-el-escalon-de-equipar-se-recalibra-contra-94-perks.md`,
congelando el catálogo de perks en cinco commits y viendo el número moverse 1,7 puntos solo). Con ese
error, pasar de "por debajo de 2,0" a "+2,0 justo" es una diferencia de ~0,4 errores típicos —
indistinguible de ruido de muestreo—. **La acusación a `a0a8b33` no se sostiene**: no hay evidencia de que
ese commit moviera la métrica; el movimiento observado es del tamaño que este instrumento produce por azar
en cualquier commit, se toquen perks o no.

**Causa inferida entonces** (`ScoreGoal`/`Kill` de `Emit` a `EmitCancellable`) queda sin evidencia a favor
ni en contra — nunca se aisló, y ya no hace falta intentarlo: el efecto que se quería explicar no está
demostrado que exista.

## Hermanos

**BA-N** (continuación directa; ahí vive el estado vigente y la corrección completa de la causa).
**`docs/pendientes/BB-P.md`**: esta ficha es el ejemplo más antiguo del mismo patrón (puerta de un solo
partido/semilla, error de muestreo confundido con una causa de diseño).
