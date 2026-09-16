# BA-N — Equipar ya no vale el escalón que la ADR 0033 exige

**Nota sobre el título** (sexta ronda, `independent-reviewer`): el título describe el síntoma tal como se midió antes de esta ficha (equipar no llegaba al umbral entonces vigente, 2,0). Con el umbral ya recalibrado a 1,0 (ADR 0116) el síntoma literal del título ya no ocurre — la puerta pasa hoy con 3,4. No se renombra la ficha por las mismas razones que la ADR 0116 (enlaces cruzados); manda el contenido del Estado, no el título.

**Estado:** **Cerrada** (16 sep 2026, decisión del orquestador tras seis rondas de `independent-reviewer`).
El umbral (1,0) fue verificado de forma independiente y reproducido correctamente en las siete
mediciones directas hechas durante la revisión (rondas 1 a 6, más la reconfirmación final); ninguna
ronda encontró jamás un problema con el número implementado, solo con la narrativa histórica que lo
rodeaba en la documentación. Se cierra con la **deuda documentada** de la sección de abajo en vez de
seguir persiguiendo cifras de commits de hace dos semanas — decisión explícita del orquestador, no
autoevaluación silenciosa (Regla E: la evidencia de las seis rondas queda escrita, no se descarta).
**Decisión del revisor: Opción B** — recalibrar el umbral de la puerta, no subir precios de objeto.
Umbral 2,0 → **1,0**
(`Sim.Tests/Perks/EquipmentImpactTests.cs`, `docs/decisiones/0116-el-escalon-de-equipar-se-recalibra-contra-94-perks.md`).
El número (1,0) es correcto y no ha cambiado en ninguna de las seis rondas de revisión; lo que se
corrigió cada vez fue la causa y el razonamiento escritos para justificarlo — ver `docs/decisiones/0116-...md`
para el historial completo, incluida la historia real del umbral (bajado **tres** veces desde el valor
inicial: 5,0 → 3,0 → 2,0 → 1,0). **Aviso**: el mensaje del commit `f1ce8b3` —publicado, sin editar—
conserva la redacción de la primera versión (causa del catálogo, "1,7 medido" cuando ese árbol mide 3,4);
manda la ADR 0116, no ese mensaje. La inconsistencia calculado/medido de objetos (ADR 0038 vs ADR 0087)
**sigue sin resolver**, intacta — esta ADR nunca la tocó. Tampoco se ha hecho todavía el pase de
`game-design-review` sobre si el poder de detección de esta puerta (~41 % ante una caída a la mitad) es
aceptable para el escalón que nombra la ADR 0033, ni se comparó en coste contra la alternativa de subir la
muestra en vez de bajar el umbral (precedente propio de las dos bajadas anteriores) — ambas quedan como
preguntas abiertas en `docs/pendientes/BB-P.md`, no como decisiones tomadas, aunque el cambio ya esté en
`main`.

## Deuda documentada al cerrar (no bloqueante)

Seis rondas de `independent-reviewer` encontraron y corrigieron errores reales en la narrativa
histórica de esta ficha y de la ADR 0116 — nunca en el umbral implementado, reproducido
correctamente las siete veces que se midió. Se cierra BA-N con lo siguiente sin verificar al 100 %,
en vez de seguir persiguiendo cifras de commits de hace dos semanas:

- Dos filas de la tabla de catálogo congelado (ADR 0116, "Hipótesis") — `96234de`=2,1 y `ad3c472`=1,7
  — no se han remedido en worktree; las otras tres (`ab129d7`, `99a22c2`, `f1ce8b3`) sí, al menos una
  vez cada una, en distintas rondas.
- Las cifras de dispersión "~3" (8 plantillas) y "~1,8" (24 plantillas) citadas en
  `Sim.Tests/Perks/EquipmentImpactTests.cs` son extrapolaciones teóricas (escala 1/√`Rosters` desde
  el ~0,9 medido con 96), nunca medidas directamente.
- Puede quedar alguna otra cita histórica de un commit de hace 12 días sin verificar byte a byte;
  el patrón de las seis rondas (una cifra nueva mal atribuida cada vez que se corregía la anterior)
  no da garantía de que la séptima no encontraría otra.
- El barrido que pide `docs/pendientes/BB-P.md` punto 4 ("¿hay otras ADR con un «medido: X»
  anclado a un árbol que ya no es vigente?") no se ha hecho fuera de la propia ADR 0116.

Ninguno de estos puntos afecta al umbral implementado (1,0) ni a las 43 puertas (mismas 4 rojas
preexistentes en las siete ejecuciones de esta revisión, sin regresión nueva atribuible a ningún
commit de esta ficha). Si en el futuro se necesita citar de nuevo el historial de esta puerta,
remedir antes de citar, no confiar en lo escrito aquí sin volver a comprobarlo.

## Observación

`EquippingAGoodBuildIsWorthSeveralPointsOfWinRate` cae a **+1,7 puntos** de tasa de victoria contra un
umbral de ≥2,0. El umbral existe para que la ADR 0033 tenga contenido: «muy buena» = «buena, además
equipada» necesita que equipar valga un escalón real.

## Serie medida (la misma métrica, cuatro puntos)

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
| `f1ce8b3` | primer commit con el umbral en 1,0 | 3,4 |

**El número se mueve 1,7 puntos sin que cambie un solo perk.** La caída que la primera versión de la ADR
0116 atribuía al catálogo se ha citado con dos números distintos, los dos equivocados por la misma
razón — mezclar el instrumento de 24 plantillas con el de 96 —: primero "3,3→1,7", luego "3,0→1,7". La
caída real, remedida en worktree con el instrumento de 96 plantillas vigente hoy, es **3,7→1,7** (quinta
ronda de revisión; el 3,0 pertenece a `54c6b38` remedido con `Rosters=24`, no con la muestra de hoy).
Ocurre igual con el catálogo fijo: es el error de muestreo del instrumento (~0,9 puntos
típico, confirmado empíricamente: sd de 0,78 sobre esas cinco medidas), no una tendencia real del
tamaño del catálogo. **La hipótesis pasa de LIKELY a REJECTED** como explicación de esta caída concreta;
sigue viva como mecanismo teórico plausible (un catálogo más fuerte SÍ podría diluir una aportación
marginal fija) pero sin evidencia de activación — nunca se ha observado por encima del ruido.

## Decisión pendiente — dos salidas excluyentes (histórico, previo a la decisión — superado por el encabezado)

**Registrado antes de que el revisor eligiera la Opción B** (encabezado de arriba); se conserva como el razonamiento del momento en que las dos salidas seguían abiertas, no como el estado actual.

- **(a)** Subir los valores de objeto para que el escalón siga existiendo.
- **(b)** Recalibrar el umbral contra un catálogo de 94 perks. Es cambio de rango y **exige ADR** (RT-057).

**Aviso sobre (a):** `docs/analisis/builds-analisis-sistemico.md` ya dejó abierta la decisión de precio de
objeto (calculado **o** medido, nunca las dos cosas a la vez); tocar los valores sin cerrar esa decisión
repite el error ya documentado de la ADR 0038.

## Antecedente

**BA-M** es el primer punto de esta misma serie (el momento en que la puerta pasó de roja a verde por cero
centésimas). **Corregida en el mismo commit que la primera corrección de la ADR 0116** (`50c2bb1`, la ADR se creó en `f1ce8b3`): BA-M.md acusaba a un
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

**Nota (sexta ronda, independent-reviewer)**: este análisis se escribió cuando la hipótesis del
catálogo (61→94 perks diluye la aportación marginal de equipar) todavía no se había probado, y dos de sus bullets — señalados abajo — la dan por medida en presente. La sección "Hipótesis" de
más arriba la marca **REJECTED** como explicación de la caída concreta que motivó esta ficha;
sigue viva solo como mecanismo teórico sin evidencia de activación. El resto de este análisis —la
inconsistencia calculado/medido de precio de objeto (ADR 0038 vs 0087) y la comparación de
opciones (a)/(b)— no depende de esa hipótesis y sigue vigente.

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
  equipar, y en el momento en que se midió esta caída los objetos rendían por debajo de lo que la ADR
  0033 exige).
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

- **A favor** (bullet escrito antes de la revisión que marcó REJECTED la causa del catálogo; se deja
  el texto original tachado por transparencia y se sustituye el argumento vigente debajo): ~~reconoce
  lo que la medición ya muestra —la contribución marginal de un objeto encoge cuando el resto del
  catálogo se vuelve más fuerte, no porque el objeto valga menos en sí—~~. El argumento que sí se
  sostiene: no toca ninguna tabla ya falsada, y no depende de identificar la causa exacta de la caída
  medida — sea cual sea esa causa, recalibrar el umbral no reabre la inconsistencia calculado/medido.
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

### Decisión pendiente del revisor (histórico, previo a la decisión — superado por el encabezado)

**Este apartado quedó escrito antes de que el revisor eligiera la Opción B** (encabezado de arriba). Se deja intacto como registro del razonamiento en el momento en que ninguna opción estaba aún tomada; no lo contradice el encabezado, lo precede. Un lector que llegue hasta aquí debe releer el encabezado,
no concluir que "ninguna opción se ha implementado" sigue siendo cierto.

Ninguna opción se había implementado en ese momento. Se detiene aquí conforme al criterio de parada: es una decisión de
diseño de economía, no una consecuencia demostrada de la medición. Falta, además, decidir la pregunta
más amplia que las dos opciones comparten: **¿el precio de mercado se calcula o se mide, y quién manda
cuando difieren?** — sin esa respuesta, cualquiera de las dos es un parche sobre una inconsistencia que
seguirá ahí.
