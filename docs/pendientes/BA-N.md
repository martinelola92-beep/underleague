# BA-N — Equipar ya no vale el escalón que la ADR 0033 exige

**Estado:** Decisión aplicada, corregida dos veces por `independent-reviewer` (16 sep 2026), pendiente de
una tercera pasada de confirmación antes de darla por resuelta del todo — RT-054/RT-057 y la Regla E de
este proyecto piden esa confirmación antes de cerrar, no una autoevaluación. **Decisión del revisor:
Opción B** — recalibrar el umbral de la puerta, no subir precios de objeto. Umbral 2,0 → **1,0**
(`Sim.Tests/Perks/EquipmentImpactTests.cs`, `docs/decisiones/0116-el-escalon-de-equipar-se-recalibra-contra-94-perks.md`).
El número (1,0) es correcto y no ha cambiado en ninguna de las dos rondas de revisión; lo que se corrigió
ambas veces fue la causa y el razonamiento escritos para justificarlo: primero, que el catálogo de 94
perks diluye la aportación marginal de equipar (esa hipótesis pasa de LIKELY a **REJECTED**, ver más
abajo); segundo, que el cierre de la fuga del penalti de BB-B causó el movimiento de esta puerta (también
refutado — la propia tabla de commits muestra que la mayor parte del movimiento ocurrió antes de que ese
arreglo existiera) y que la puerta necesitaba adoptar comparación emparejada (ya la tiene). La causa real,
en ambas rondas: la puerta tiene un error típico de ~0,9 puntos y un umbral de 2,0 tenía del orden de 34 %
de probabilidad de salir rojo por puro muestreo. La inconsistencia calculado/medido de objetos (ADR 0038
vs ADR 0087) **sigue sin resolver**, intacta — esta ADR nunca la tocó.

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

## Hipótesis — REJECTED (corregida tras `independent-reviewer`)

**La hipótesis original**: el catálogo pasó de 61 a 94 perks y se ha vuelto más fuerte en conjunto, así
que la contribución **marginal** de los objetos encoge en comparación. Era consistente con las tres
medidas de la tabla de arriba y parecía no tener hipótesis rival planteada — pero nadie comprobó si el
mismo movimiento ocurría **sin** tocar el catálogo antes de escribirla en una ADR.

**Lo comprobó el revisor, con el catálogo de 94 perks congelado** (mismo `/data/perks/`, verificado byte
a byte) **en cinco commits distintos que solo cambiaban código de `/Sim` ajeno a objetos y perks**:

| commit | qué cambió | medido |
|---|---|---|
| `ab129d7` | (donde se escribió esta hipótesis) | 1,7 |
| `96234de` | fix de build (BB-J) | 2,1 |
| `ad3c472` | revert de la inmunidad del saque de centro | 1,7 |
| `99a22c2` | tras revertir la barrera generalizada de BB-B | 3,0 |
| `f1ce8b3` | HEAD | 3,4 |

**El número se mueve 1,7 puntos sin que cambie un solo perk.** La caída 3,3→1,7 que la ADR 0116 atribuía
al catálogo ocurre igual con el catálogo fijo: es el error de muestreo del instrumento (~0,9 puntos
típico, confirmado empíricamente: sd de 0,78 sobre esas cinco medidas), no una tendencia real del
tamaño del catálogo. **La hipótesis pasa de LIKELY a REJECTED** como explicación de esta caída concreta;
sigue viva como mecanismo teórico plausible (un catálogo más fuerte SÍ podría diluir una aportación
marginal fija) pero sin evidencia de activación — nunca se ha observado por encima del ruido.

## Decisión pendiente — dos salidas excluyentes

- **(a)** Subir los valores de objeto para que el escalón siga existiendo.
- **(b)** Recalibrar el umbral contra un catálogo de 94 perks. Es cambio de rango y **exige ADR** (RT-057).

**Aviso sobre (a):** `docs/analisis/builds-analisis-sistemico.md` ya dejó abierta la decisión de precio de
objeto (calculado **o** medido, nunca las dos cosas a la vez); tocar los valores sin cerrar esa decisión
repite el error ya documentado de la ADR 0038.

## Antecedente

**BA-M** es el primer punto de esta misma serie (el momento en que la puerta pasó de roja a verde por cero
centésimas). **Corregida en el mismo commit que cierra esta ficha** (`50c2bb1`): BA-M.md acusaba a un
commit (`a0a8b33`) de afirmar —incorrectamente— que no movía balance, apoyándose en que "la métrica es
determinista, así que no es ruido" — premisa falsa (el determinismo garantiza que la misma build da el
mismo número, no que el muestreo de 6.144 partidos no tenga varianza). El valor de 2,0 que BA-M midió
queda a solo ~0,4 errores típicos (~0,9) del valor verdadero estimado con las cinco medidas de esta ADR
(~2,4) — indistinguible de ruido de muestreo, no evidencia de que `a0a8b33` moviera nada. La acusación
queda retirada en `docs/pendientes/BA-M.md`.

## Hermanos

**`docs/pendientes/BB-P.md`** (nuevo, abierto de esta misma revisión): calibración de puertas
estadísticas de un solo partido/semilla contra su error típico. Esta puerta y las 4 puertas rojas actuales
que salieron de cerrar BB-B (`TheThreeDoctrinesBuyDifferently`, `CoherentBuildsBeatTheirBaseline`,
`BadBuildsLoseToTheirBaseline`, `NoGateMetricIsOutOfRange`) comparten la misma firma: un valor que se mueve
por cambios de `/Sim` ajenos a su contenido, con un margen menor que el ruido de su propio instrumento.
BA-N deja de ser un caso aislado en cuanto se mira así.


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
