# BF-B — Tres puertas que deciden con una semilla o sin margen

Estado: **abierta, medida**. Es el mismo patrón que la ADR 0131 arregló en las puertas de build, en las
tres que se quedaron fuera de aquel commit.

## Síntoma

Tres puertas cambian de estado con movimientos que están dentro de su propio error de muestreo:

| puerta | valor | rango | margen | muestra |
|---|---|---|---|---|
| `RarityAndBossTests.CommonAtMaxLevelMatchesALegendaryAtLevelTwo` | 43,75 | 45-55 | **1,25** | 480 partidos, **semilla 1** (error típico ~2,3) |
| `badBuildsLoseToNone_orc_misplaced` | 45,18 | ≤ 45 | **0,18** | 8 plantillas (la familia tiene sd 1,6-3,1 → error típico ~0,6-1,1) |
| `FullRunGateTests.TheThreeDoctrinesBuyDifferently` | ahorradora 9,01 contra contextual **9,13** | `>` estricto | **0,12** | 240 runs, **semilla 1** |

La tercera no es una banda: es una **desigualdad estricta entre dos números que empatan**. Con 0,12 puntos
de diferencia sobre un 9 %, el signo lo decide el ruido, no la economía.

## Por qué importa

La ADR 0131 ya pagó esta factura: tres conclusiones equivocadas en una sesión por leer una sola plantilla
como si fuera el juego. Estas tres puertas siguen midiendo así, y cada vez que alguien toca el motor
producen una investigación que acaba en «era ruido». La de las doctrinas además **no dice por cuánto
falla** —eso sí se arregló, ahora imprime el margen— pero sigue sin tenerlo.

## Qué hacer (el patrón ya está decidido, es el de la ADR 0118 y la 0131)

1. **`RarityAndBossTests`**: promediar varias bases de semilla como hace `BuildGateTests`. Su coste actual
   es bajo, así que cabe.
2. **`orc_misplaced`**: ya se mide sobre ocho plantillas; lo que falta es decidir si el techo de 45 es el
   número correcto para las tres builds malas, que es la misma pregunta de [BF-A](./BF-A.md). **No se toca
   sin medir la distribución**: las tres rondan 44-47 y eso huele a que el catálogo no tiene perks que
   estorben de verdad, no a que el techo esté mal.
3. **Doctrinas**: la afirmación que vale es «las tres doctrinas compran distinto», y las dos primeras
   comparaciones (gastadora > ahorradora, contextual > ahorradora) sí tienen margen. La tercera necesita
   **un margen explícito** medido —o desaparecer, si resulta que ahorradora y contextual terminan la run
   con el mismo oro sin gastar y eso es correcto por diseño—. Decidirlo es `game-design-review`, no
   calibración: ¿deben esas dos doctrinas diferenciarse en el oro sobrante o solo en qué compran?

## Cómo se destapó

Midiendo la ADR 0133 (el centrocampista entra a su marcado). Las tres cambiaron de estado con un cambio de
comportamiento pequeño y bien medido en todo lo demás, y la primera reacción —recalibrar el juego para que
volvieran a verde— es exactamente el error que la ADR 0131 documenta.

## Hermanos

ADR 0131 (el mismo arreglo en las puertas de build, con su tabla de dispersión) · ADR 0118 (el precedente
original, en la puerta de equipar) · [BF-A](./BF-A.md) (el techo de las builds malas) ·
`CatJSeedDispersionTests` (el instrumento que mide la dispersión).
