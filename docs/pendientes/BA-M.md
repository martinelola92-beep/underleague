# BA-M — Origen de BA-N: la puerta de equipar pasó de roja a verde por cero centésimas

**Estado:** Plegada en **BA-N** (misma métrica, misma causa probable). Se conserva como primer punto de la
serie medida.

## Observación

`EquippingAGoodBuildIsWorthSeveralPointsOfWinRate` pasa por cero centésimas: +2,0 puntos contra un umbral
de ≥2,0.

## Análisis

Pasó de roja a verde con el commit `a0a8b33` (las seis primitivas de tanda 1), cuyo mensaje afirmaba que no
movían balance por construcción — **era falso**, y medible: la métrica es determinista (semilla fija, 6.144
partidos por brazo), así que no es ruido. Causa inferida entonces (no confirmada): `ScoreGoal` y `Kill`
pasaron de `Emit` a `EmitCancellable`, publicando a los perks antes de aplicar las consecuencias.

Con la tanda 2 la puerta volvió a caer, esta vez por debajo del umbral (**BA-N**), lo que apunta a una
causa más simple y consistente en las tres mediciones: el tamaño del catálogo, no el orden de eventos de
`EmitCancellable`. Ver BA-N para la serie completa y la decisión pendiente.

## Hermanos

**BA-N** (continuación directa de este problema; ahí vive el estado vigente).
