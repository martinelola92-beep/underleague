# BA-N — Equipar ya no vale el escalón que la ADR 0033 exige

**Estado:** Decisión tomada, aplicada, pendiente de confirmación del `independent-reviewer` antes de
cerrar formalmente. Causa sigue **LIKELY** (nunca se aisló con un experimento propio; no hacía falta para
decidir). **Decisión del revisor (16 sep 2026): Opción B** — recalibrar el umbral de la puerta, no subir
precios de objeto. Umbral 2,0 → **1,0** (`Sim.Tests/Perks/EquipmentImpactTests.cs`), `docs/decisiones/0116-el-escalon-de-equipar-se-recalibra-contra-94-perks.md`.
El umbral deja de afirmar "varios puntos" y pasa a afirmar "un efecto real y medible, no ruido" — 1,0
elegido explícitamente de las dos lecturas posibles del único precedente de calibración disponible (ratio
0,61 sobre 3,3→2,0), no una medida nueva ni una fórmula validada. La inconsistencia calculado/medido de
objetos (ADR 0038 vs ADR 0087) **sigue sin resolver**, intacta.

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


## Análisis de consecuencias (16 sep 2026, orquestación de pendientes técnicos)

Leídos: ADR 0033 (define el escalón "muy buena = buena + equipada"), ADR 0038 (precio calculado de
objeto), `docs/analisis/builds-analisis-sistemico.md` §9 (el valor calculado no predice el medido).

**Hallazgo que agrava el problema, no lo simplifica**: el proyecto **ya aplica las dos vías a la vez**, en
subsistemas distintos. `data/economy/item-values.json` (medido, ADR 0087) alimenta el listón de "vale un
slot" del mercado (`ItemWorthASlot`, ADR 0086). El **precio** de venta y el **peso en el pool** siguen
saliendo de la fórmula calculada de la ADR 0038 (`ItemScale.ValueOf`, confirmado en el `_doc` de
`item-values.json`). O sea que un objeto puede pasar el filtro de "vale la pena" por su valor medido y
costar/aparecer según un valor calculado que el propio análisis sistémico demostró que no se corresponde
—`+10 fuerza` mide 2, `+10 resistencia` mide 61—. Esto **no es exclusivo de BA-N**: es una inconsistencia
estructural que BA-N solo pone de manifiesto.

### Opción (a) — subir precios de objeto

- **A favor**: arregla el síntoma medido directamente (el escalón "muy buena" depende de cuánto rinde
  equipar, y hoy los objetos rinden por debajo de lo que la ADR 0033 exige).
- **En contra, medido**: la fórmula que fijaría cuánto subir cada objeto es la calculada (ADR 0038), que
  el propio análisis sistémico **falsó** dos veces (§9: "el orden medido es el inverso", "la suma de
  atributos no predice el valor: +30 puede valer 0 y +10 puede valer 62"). Subir precios con esa fórmula
  no solo no cierra la inconsistencia de arriba: la **agrava**, porque encarecería objetos por una tabla
  que ya se sabe que no corresponde a lo que miden.
- **Efecto en progresión/economía**: subir precios reduce cuántos objetos se pueden comprar por acto, lo
  que baja la tasa de "muy buena" alcanzada — el mismo problema que se quiere arreglar, por el lado
  contrario. Necesitaría remedir `runWinRate` y `brokeMarketRunShare` (ADR 0099/0100), que ya están en el
  suelo de su banda.
- **Efecto en catálogo de perks**: ninguno directo — los perks se obtienen por frecuencia, no por precio
  (ADR 0038 tabla "Vía de obtención"). No interactúa con la tanda 1/2.

### Opción (b) — recalibrar el umbral de `EquippingAGoodBuild` contra 94 perks

- **A favor**: reconoce lo que la medición ya muestra —la contribución **marginal** de un objeto encoge
  cuando el resto del catálogo se vuelve más fuerte, no porque el objeto valga menos en sí—. No toca
  ninguna tabla ya falsada.
- **En contra**: es un ajuste de rango (RT-057, exige ADR), y el propio criterio de "muy buena = buena +
  equipada" de la ADR 0033 se vuelve más débil en términos absolutos: equipar seguiría sumando lo mismo,
  pero el examen para llamarlo "escalón real" se relaja.
- **Efecto en progresión/economía**: ninguno directo — no cambia oro, precios ni pool.
- **Efecto en catálogo de perks**: ninguno directo.
- **Efecto en coherencia con decisiones existentes**: es la opción que **no** reabre la inconsistencia
  calculado/medido; la deja donde está (documentada, sin agravar).

### Lo que NO se recomienda bajo ninguna opción

Tocar `data/economy/item-values.json` (la tabla **medida**) para forzar el escalón sin decidir antes cuál
de las dos tablas manda en general —eso repetiría literalmente el error ya documentado de la ADR 0038 que
el análisis sistémico corrigió una vez.

### Decisión pendiente del revisor

Ninguna opción se ha implementado. Se detiene aquí conforme al criterio de parada: es una decisión de
diseño de economía, no una consecuencia demostrada de la medición. Falta, además, decidir la pregunta
más amplia que las dos opciones comparten: **¿el precio de mercado se calcula o se mide, y quién manda
cuando difieren?** — sin esa respuesta, cualquiera de las dos es un parche sobre una inconsistencia que
seguirá ahí.
