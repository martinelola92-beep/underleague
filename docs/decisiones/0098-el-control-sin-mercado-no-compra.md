# 0098. El control sin mercado no compra, y la contradicción que anuncié no existía

**Fecha:** 2026-09-11
**Estado:** Aceptada e implementada (`Sim/Analysis/RunPolicy.cs`)
**Corrige el instrumento de la ADR 0055** y **retira la conclusión de la ADR 0096 §«Lo que queda abierto»**, que era un error de categoría mío.
**Requisitos:** RT-055, RT-057
**Relacionada:** ADR 0033 (la curva de puertas de jefe), ADR 0053 (el mapa de cuatro carriles), ADR 0055 (el mercado es imprescindible), ADR 0096, ADR 0097

## El defecto del instrumento

`runWinRate_noMarket` mide la política de control de la ADR 0055: la misma contextual, jugando igual de bien
todo lo demás, pero **esquivando los mercados**. La esquiva es una regla de **ruta** (el nodo de mercado vale
`int.MinValue`, así que solo entra cuando no hay otra), y la ADR 0053 decía que con cuatro carriles se puede
esquivar en el 98,9 % de los actos.

Medido: **no**. `marketsVisited_noMarket` vale **0,77** y **la mitad de las runs** entran en un mercado
porque el mapa las obliga. Y al entrar, el control **compraba** como cualquiera: 84 de oro de media. Así que
la métrica no medía «se puede ganar sin comprar», sino la mezcla de dos poblaciones muy distintas (1.200
runs, semilla 1, estado de la ADR 0097):

| Población | Cuántas | Ganan |
|---|---|---|
| Nunca compraron nada | 50,3 % | **4,47 %** |
| El mapa las metió en un mercado y compraron | 49,7 % | **19,13 %** |
| **La métrica publicada (mezcla)** | 100 % | **11,75 %** |

## Decisión

**El control no compra.** `AvoidsMarkets` deja de ser solo una regla de ruta y pasa a ser también una regla
de compra: cuando el mapa lo mete en un mercado, mira y se va. Es lo que su nombre dice y lo que la pregunta
de la ADR 0055 necesita («solo con los perks de las victorias no debería darte opción a ganar»).

Con el instrumento arreglado, y sobre las **1.200 runs enteras** (ya no hay dos poblaciones):

| | contextual | sin comprar |
|---|---|---|
| perks del once | 8,32 | **2,81** |
| objetos | 1,03 | **0,00** |
| contadores acumulados | 17,21 | 5,42 |
| oro sin gastar | 15,03 | **122,10** |
| muertes por run | 1,89 | **1,10** |
| nivel medio | 6,16 | **6,88** |
| plantilla final | 10,37 | 7,92 |
| **tasa de victoria** | **25,25** | **7,83** |

Semilla 7, mismas 1.200 runs: **7,17** (la contextual, 25,33). `marketsVisited_noMarket` no se mueve (0,76 / 0,74): el control sigue entrando donde el mapa le obliga, simplemente ya no compra allí.

## Lo que retiro de la ADR 0096

Allí escribí que la ADR 0033 y la ADR 0055 se contradicen, porque las celdas de la fila «buena» multiplicadas
dan 25,6 % y la banda pide ≤ 5 %. **Era un error de categoría**: la fila «buena» de la ADR 0033 mide una build
**completa** —catorce perks en el once, `data/balance/builds/*_good.json`— que simplemente no tiene maestros;
la run sin mercado no se le parece en nada, porque llega con **2,81**. «Sin maestros» y «sin mercado» no son
lo mismo, y tratarlos como sinónimos hizo que una diferencia de instrumento pareciera una imposibilidad de
diseño. No hay contradicción que decidir.

## Lo que queda de verdad

A la banda le faltan **2,2-2,8 puntos**, no veinte. Y el desglose de arriba dice por dónde, en orden de tamaño:

1. **No comprar es, en parte, una estrategia.** El control acaba con 122 de oro sin gastar que dedica a la
   clínica, y con una plantilla corta (7,92) sin fichajes de nivel 1: **muere la mitad** (1,10 contra 1,89) y
   llega **más veterano** (6,88 contra 6,16). No es solo abstenerse: es otra forma de jugar, y hoy paga.
2. **Las elecciones de élite y jefe bastan para un esqueleto**: 2,81 perks en el once sin pisar una tienda.
3. **El jefe del acto 1 casi no pide build**: la ADR 0033 le da 36-38 % a una build incoherente.

La palanca 1 es la interesante y es nueva: si acumular oro que no se gasta fuera peor —o si la clínica no
pudiera absorberlo todo— la run sin comprar caería sin tocar ni el reparto de recompensas ni las celdas de
los jefes. Queda anotada en AZ-H.
