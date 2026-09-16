# BA-N — Equipar ya no vale el escalón que la ADR 0033 exige

**Estado:** Abierta. Causa **LIKELY** (inferida y consistente en tres mediciones, no aislada por
experimento propio). Requiere **decisión del revisor**, no investigación adicional.

## Observación

`EquippingAGoodBuildIsWorthSeveralPointsOfWinRate` cae a **+1,7 puntos** de tasa de victoria contra un
umbral de ≥2,0. El umbral existe para que la ADR 0033 tenga contenido: «muy buena» = «buena, además
equipada» necesita que equipar valga un escalón real.

## Serie medida (la misma métrica, tres puntos)

| momento | valor |
|---|---|
| HEAD limpio (antes de la tanda 1 y 2) | fallaba (< 2,0) |
| + las seis primitivas de tanda 1 (`a0a8b33`) | **+2,0** — pasaba por cero centésimas |
| + los 13 perks de tanda 1 | **+2,0** |
| + los 20 perks de tanda 2 | **+1,7** — cae por debajo |

## Hipótesis — LIKELY, no CONFIRMED

El catálogo pasó de 61 a 94 perks y se ha vuelto más fuerte en conjunto, así que la contribución
**marginal** de los objetos encoge en comparación: no es que los objetos valgan menos, es que el resto
vale más. Es consistente con las tres mediciones y no tiene hipótesis rival planteada, pero **no se ha
aislado con un experimento propio** (p. ej. medir la misma puerta con el catálogo de 94 perks y los objetos
sin tocar, aislando qué perks concretos explican la caída) — de ahí la etiqueta LIKELY y no CONFIRMED.

## Decisión pendiente — dos salidas excluyentes

- **(a)** Subir los valores de objeto para que el escalón siga existiendo.
- **(b)** Recalibrar el umbral contra un catálogo de 94 perks. Es cambio de rango y **exige ADR** (RT-057).

**Aviso sobre (a):** `docs/analisis/builds-analisis-sistemico.md` ya dejó abierta la decisión de precio de
objeto (calculado **o** medido, nunca las dos cosas a la vez); tocar los valores sin cerrar esa decisión
repite el error ya documentado de la ADR 0038.

## Antecedente

**BA-M** es el primer punto de esta misma serie (el momento en que la puerta pasó de roja a verde por cero
centésimas, con un commit que afirmaba —incorrectamente— que no movía balance). Se pliega aquí porque es la
misma métrica y la misma causa; no se investiga por separado.

## Hermanos

Ninguno con la misma causa. Comparte el catálogo de 94 perks con todo el trabajo de tandas 1 y 2.
