# 0085. El valor de un perk se lee al horizonte que tiene, y la run no lo nota

**Fecha:** 2026-09-08
**Estado:** Aceptada e implementada, **encendida** por defecto (`RunPolicyOptions.ValuesPerkByHorizon`; `--values-flat` en `/Balance` es la medida de control)
**Cierra:** AV-B (`docs/pendientes.md`)
**Requisitos:** RT-023, RT-054, RT-057
**Relacionada:** ADR 0070 (la tabla se mide en campaña), ADR 0072 (el listón del slot), ADR 0076 (la muestra que resuelve)

## El aviso (AV-B)

La tabla de valor de la ADR 0070 mide cada perk en una campaña de ocho partidos con el contador recorriendo 0..7, y la política usaba ese único número en todas partes: en la capa 0 del acto 1, donde al perk le quedan trece partidos, y en el último nodo del acto 3, donde no le queda ninguno. Los quince perks con `accumulatesAcrossMatches` son el 29 % del catálogo pero el 56 % de los que pasaban el listón del acto 1, y la campaña les añade 61,4 unidades de media frente a 0,0 a los otros 36.

## Lo que se implementa

Un paquete de instrumento, como lo fue la ADR 0070:

- `data/economy/perk-values.json` gana `referenceHorizon` (8), `rowDeviationByHorizon` y `valuesByHorizon`: la curva de valor de cada perk del eje contra los partidos que le quedan, del 1 al 16, con la columna del horizonte 8 obligada a coincidir con `values` (el cargador lo comprueba y falla en explícito, RT-032). Los 36 perks sin curva valen `values` a cualquier horizonte, y eso está medido, no supuesto. La curva sale de `--perk-values` sin volver a medir: `perk-values-by-match.csv` guarda victorias por índice de partido y el valor a horizonte `R` es el acumulado de los `R` primeros.
- `PerkValueTable` precalcula la distribución de oferta a cada horizonte (`ValueAt`, `ValueAtQuantile(…, horizon)`, media y desviaciones por horizonte). El **peso** del pool no cambia con el horizonte: es la palanca de frecuencia de la ADR 0038, propiedad del perk sobre la run entera.
- `RunPolicy` convierte las capas que le quedan a la run, ya descontadas por las tasas de paso igual que las ofertas (ADR 0072), en partidos que le quedan al perk (`MatchesPerLayerPermille`), y lee el perk que juzga y el listón del slot **en el mismo momento**. `perkHorizon` en `runs.csv` dice dónde se compró cada perk y cuántos partidos le quedaban.

`MatchesPerLayerPermille` llegó declarado como "medido" en 590 sin la medición; el banco de esta ADR mide **546-553** en las tres doctrinas (partidos por nodo visitado, 1.800 runs), y queda en **550**. La diferencia entre las dos constantes mueve la semilla 1 en 1,7 puntos, dentro del ruido que sigue.

## Lo que se mide

`--full-runs 600` por doctrina, tres semillas, el mismo banco con y sin `--values-flat`. Solo la doctrina contextual lee la tabla; la ahorradora y la gastadora salen idénticas en los dos brazos en las tres semillas, que es la comprobación de que el control es un control.

| Semilla | `runWinRate` al horizonte | `runWinRate` plano | `contextualAdvantage` al horizonte | plano |
|---|---|---|---|---|
| 1 | 21,83 | 19,50 | 2,17 | −0,17 |
| 2 | 20,33 | 21,00 | −1,83 | −1,17 |
| 3 | 21,67 | 23,67 | 3,17 | 5,17 |
| **media (1.800 runs por brazo)** | **21,28** | **21,39** | **1,17** | **1,28** |

Una diferencia de −0,11 puntos con un error típico del orden de 1,3: **nula**, y con el signo cambiando de semilla en semilla. Ninguna otra métrica del banco se mueve fuera del ruido: los actos 1 y 2 salen iguales (75,0 / 58,8 frente a 75,0 / 58,5 en la semilla 1) y el acto 3 sube en una semilla y baja en otra.

## Por qué no se nota

El diagnóstico `perkHorizon` lo explica sin conjeturas. En el banco de la semilla 3, la doctrina contextual cobra:

| Acto | Perks cobrados | Partidos que les quedaban (media) |
|---|---|---|
| 1 | 2.871 | 10,25 |
| 2 | 2.248 | 6,93 |
| 3 | 626 | 3,40 |

El 89 % de los perks se compran con siete o más partidos por delante, y las curvas medidas son casi planas de ahí en adelante (`deathless_march` vale 292 a 7, 308 a 8 y 355 a 16; `battle_reader` 156, 168 y 227). El único tramo donde el horizonte muerde de verdad —los primeros cuatro partidos, donde un acumulador vale un tercio de su referencia— es el 11 % de las compras, y ahí la política ya tenía poco slot libre que llenar (`perksAtBossAct3` 11,3 de 12). La tabla de un solo número se equivocaba, pero se equivocaba donde casi nadie compra.

## Decisión

Se deja **implementada y encendida**, al contrario que la corrección de la ADR 0076 (`--slot-gates`), que se apagó porque empeoraba la build buena del acto 2. Aquí no empeora nada: es la lectura correcta —la columna del horizonte 8 *es* `values`, así que el plano es un caso particular de la curva— a coste cero medido, y quita una premisa falsa (que un perk vale 308 sin partidos por delante) que cualquier cambio futuro en la densidad de compras del acto 3 haría aflorar. `--values-flat` queda como control para remedirlo cuando eso pase.

## Alternativas descartadas

- **Apagarla por no mover nada.** Sería confundir "no se nota" con "no es cierto". El precedente de apagar (ADR 0076) tenía un coste medido; esto no.
- **Ampliar el banco hasta resolver un efecto de un punto.** Harían falta del orden de 7.000 runs por brazo (ADR 0076); no cambiaría la decisión, porque el mecanismo ya explica por qué el efecto es pequeño.
- **Recortar el contador por acto (`counterCap`)**, la salida que AV-B apuntaba. La curva por horizonte es la misma idea sin tocar el motor de perks ni el catálogo: el horizonte ya es el contador que el acto permite.

## Consecuencias

- AV-B cerrada. `docs/fase2-diseno.md` §39 recoge la medición.
- Tests unitarios de la curva, el cuantil por horizonte, los fallbacks sin curva y las dos comprobaciones del cargador (`Sim.Tests/Run/PerkValueHorizonTests.cs`).
- Si la densidad de compras del acto 3 sube (por ejemplo por un mercado más generoso al final), esta es la primera métrica que remedir con `--values-flat`.
