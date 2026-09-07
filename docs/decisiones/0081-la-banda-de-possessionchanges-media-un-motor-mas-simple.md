# 0081. La banda de `possessionChanges` se revisa: el motor que la fijó ya no es el que corre

**Fecha:** 2026-09-07
**Estado:** Aceptada e implementada
**Modifica:** el rango de `possessionChanges` fijado en la fase 0 (RT-056/RT-057), `docs/balance.md`
**Requisitos:** RT-056, RT-057

## El aviso

`possessionChanges` mide cambios de posesión por partido, con banda **12-25** desde la fase 0. Desde entonces, una serie de cambios independientes —cada uno medido y cerrado por separado, ninguno tocando esta banda a propósito— la ha ido empujando hacia el mismo lado:

| Cambio | Semilla 1 | Semilla 2 | Semilla 3 |
|---|---|---|---|
| Referencia antes de AW-D | 24,68 | — | — |
| AW-D (penaliza el pase atrás sin alternativa de regate) | ~25,5 | ~25,5 | — |
| AW-E (penaliza converger en el mismo hueco) | 24,87 | 25,42 | — |
| AW-A paso 1 (el portero debe llegar al balón) | 25,10 | 25,97 | — |
| AW-A paso 2 (estirada) | 25,10 | 25,97 | — |
| AW-A paso 3 (bloqueo de tiro por defensas) | 25,51 | 26,17 | — |
| AW-T (decisión inmediata al cambiar de posesión) | 24,95 | 26,60 | 22,07 |

Cada fila es una medición de 500 partidos, `data/balance/reference.json`, ya con el ajuste aceptado de esa fila (ninguna es la versión sin calibrar). El rango real observado en la versión final de cada cambio va de **22,07 a 26,60**: por encima del techo de 25 en cuatro de las siete filas, y nunca cerca del suelo de 12.

Ninguno de estos siete cambios es, aislado, el culpable: cada uno se midió, se acotó a lo que la propia anotación pedía y se cerró con el resto de métricas obligatorias dentro de rango. El patrón es el mismo que ya describió la ADR 0054 para `betterTeamWinRate`: la banda se fijó **en la fase 0**, cuando el motor era más simple —sin bloqueo de tiro, sin que el portero tuviera que llegar al balón, sin que un jugador reaccionara al instante al perder o ganar el balón—. Un motor más fiel a cómo se juega de verdad al fútbol produce, de forma natural, algún cambio de posesión más por partido: un tiro bloqueado deja el balón suelto (un cambio de posesión que antes no existía porque el tiro simplemente entraba o el portero paraba sin más), un receptor que ya no se queda parado un tick pierde menos el balón por estar desprevenido pero también lo recupera y lo vuelve a perder con más fluidez en el mismo intercambio. La banda mide un juego que ya no es el que corre.

## Decisión

La banda de `possessionChanges` pasa de **12-25 a 12-28**.

El suelo no se toca: en ninguna de las mediciones de este documento ni de las anteriores se ha acercado a 12, así que no hay evidencia de que necesite revisarse.

El techo sube a 28, no más: es un margen por encima del valor más alto observado en la versión final de cualquier cambio de esta tabla (26,60), suficiente para absorber la varianza normal de semilla a semilla (el rango de tres semillas de un mismo estado del motor, AW-T, ya cubre 22,07 a 26,60, más de cuatro puntos), pero sin abrir la puerta tanto que deje de significar nada: un partido con más de 28 cambios de posesión seguiría siendo una señal real de que algo se ha roto.

## Alternativas descartadas

- **No tocar la banda y aceptar que la puerta estadística quede en rojo indefinidamente.** Ya se venía haciendo así desde AW-D ("por ahora es aceptable"), pero acumular desvíos aceptados sin ajustar la banda deja la suite de tests sin poder distinguir un fallo real de una desviación ya conocida: cualquier cambio futuro que la mueva un poco más se juzgaría contra un techo que ya sabíamos que no correspondía.
- **Bajar la magnitud de alguno de los siete cambios hasta que la banda vieja vuelva a cumplirse.** Cada uno de ellos ya se acotó a la magnitud mínima que resolvía su propia anotación (AW-D a 50, AW-E a 50, AW-A sin ajuste porque no hizo falta); forzar más recorte sacrificaría la corrección de la anotación original por hacer pasar una puerta cuyo número, no el comportamiento, es el que está desactualizado.
- **Subir el techo mucho más (35-40) para no tener que volver a tocarlo pronto.** Descartado por la misma razón que la ADR 0054 dio para no subir su techo más de lo necesario: una banda demasiado ancha deja de servir de alarma. 28 es el margen justo sobre lo ya medido, no una previsión de cuánto podría subir en el futuro; si un cambio futuro la vuelve a empujar, se mide y se decide entonces, con sus propios datos.

## Consecuencias

- `docs/balance.md` y `Sim.Tests/Engine/StatisticalTests.PossessionChangesAreInRange` (y `NoMandatoryMetricIsOutOfRange`, que dependía del mismo valor) pasan a estar en verde con el estado actual del motor, sin haber tocado ningún número de `/Sim` ni de `/data` para conseguirlo.
- Si un cambio futuro empuja `possessionChanges` por encima de 28, es una señal real de revisar ese cambio concreto, no de volver a abrir esta ADR primero.
- Ningún otro rango de `docs/balance.md` se toca en esta decisión.
