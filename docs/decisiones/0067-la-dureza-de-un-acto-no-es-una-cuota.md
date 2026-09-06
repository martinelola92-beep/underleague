# 0067. La dureza de un acto no es una cuota

**Fecha:** 2026-09-06
**Estado:** Aceptada e **implementada**. **Cierra AP-B**
**Corrige:** el guardarraíl `defeatShareAct1 ≤ 29,74%`, que vigilaba una cifra que no es la que decía
vigilar
**Requisitos:** RT-055, RT-056, RT-057, RF-002c, RF-032
**Relacionada con:** ADR 0033, ADR 0043 (taller, gestión y examen), ADR 0054 (la banda de
`betterTeamWinRate`), ADR 0056 (objetivos), ADR 0057, ADR 0064, ADR 0065 (que lo detectó), ADR 0066

## El defecto, con la aritmética delante

Desde el paquete AK se vigila la dureza del acto 1 con `defeatShareAct1 ≤ 29,74%`. Ese número **no es la
tasa de derrota del acto 1**: es la **cuota** de las runs perdidas que se pierden en el acto 1,

```
defeatShareAct1 = (1 − P₁) / (1 − P₁·P₂·P₃)
```

y en esa expresión el acto 1 sólo aparece en el numerador. El denominador es el complemento de la tasa de
victoria de la run, así que **la cuota sube sola en cuanto la run se gana más**, con el acto 1 sin tocar:

| tasa de la run | 17,00 (hoy) | 18,67 | 19,83 | 20,00 |
|---|---|---|---|---|
| `defeatShareAct1` con `P₁` = 75,33 | **29,74** | 30,33 | 30,77 | **30,84** |

> **El techo del 29,74% y la banda 20-30% de `runWinRate` no pueden cumplirse a la vez** mientras la
> puerta del acto 1 deje pasar al 75,33%: haría falta `P₁ ≥ 76,21`. Las dos cifras son la misma aritmética
> leída dos veces (ADR 0065 §4).

Y el defecto es de forma, no de magnitud: **cualquier** techo sobre una cuota tiene el mismo problema. Con
la cuota como guardarraíl, arreglar el acto 3 rompe el acto 1.

## Decisión

**1. El guardarraíl pasa a medir una tasa dentro del acto.** Entra
`ordinaryDefeatRateAct1` = partidos ordinarios perdidos ÷ partidos ordinarios jugados en el acto 1, con
**techo 30,0**. Numerador y denominador viven los dos dentro del acto, así que la métrica **no se mueve**
cuando cambian los actos 2 y 3 ni cuando cambia la tasa de victoria de la run. Es la dureza del acto 1
para el jugador, que es lo que el guardarraíl quería vigilar.

**2. Se publica también para los actos 2 y 3, como INFO**, porque los tres juntos son la curva de la ADR
0043 —taller, gestión, examen— y leerla no debería exigir restar a mano:

| | acto 1 | acto 2 | acto 3 |
|---|---|---|---|
| `ordinaryDefeatRateAct{n}`, build buena (1.200 runs) | **24,90** | 39,67 | 56,70 |

**3. `defeatShareAct1` se queda publicada al lado, como INFO**, exactamente como la ADR 0066 hizo con
`winRateAct{n}_withBoss`: sigue diciendo algo cierto —dónde caen las derrotas de run, que es la pregunta
de la ADR 0043— y permite comparar con los ocho paquetes anteriores al decimal. Lo que pierde es la banda,
porque nunca debió tenerla. Su comentario en el código dice ahora por qué.

**4. Y entran, gratis, las tres puertas.** `bossWinRateAct{n}` publica `BossWinsByAct / BossSamplesByAct`
(ADR 0066): la identidad de la ADR 0064 —la run es el producto de las tres puertas— se leía hasta ahora
estimándola desde `bossesBeaten`, y la estimación se come las runs que ganan la puerta y se quedan sin
plantilla en ese mismo nodo. `runs.csv` gana las seis columnas correspondientes.

## De dónde sale el 30, que no es de donde está hoy la cifra

RT-057 prohíbe elegir un umbral para que un número lo pase, así que el techo se deriva y luego se mira
dónde cae la medición, no al revés:

- La **ADR 0043** le da al acto 1 la función de **taller**: *"dificultad baja, poco desgaste, recompensas
  frecuentes"*. Un taller es un sitio donde se construye, no donde se filtra; el filtro es su jefe.
- La **ADR 0054** fija en **70-88** la banda de `betterTeamWinRate`: lo que gana **el mejor equipo** de un
  emparejamiento. Es el número con el que este proyecto ya expresa "aquí hay un favorito claro".
- En el acto 1 el jugador **es** el mejor equipo por construcción: los cinco rivales ordinarios del acto
  1 son de nivel 1-2 y suman 121-141 puntos de fuerza+velocidad+técnica por jugador, frente a los 163-188
  de nivel 3 del acto 2 y los 201-235 de nivel 5 del acto 3; el jugador llega con la plantilla generada a
  calidad 50 y subiendo de nivel desde el primer partido.

De ahí: **un acto 1 que sea taller pide que el jugador gane al menos lo que gana el mejor equipo, 70%**, es
decir que pierda como mucho el **30%** de sus partidos ordinarios. Medido hoy: **24,90%** (ET 0,63 sobre
1.200 runs; 23,26% en la muestra de 60 runs de la puerta). Quedan **5,1 puntos** de margen, y el
guardarraíl muerde antes de que el acto 1 deje de ser un taller.

## Por qué esto no es relajar nada (RT-057)

- **No se mueve ningún umbral**: el 29,74% no se sube a 31 ni se ensancha. Se **retira**, porque estaba
  puesto sobre una cifra que no mide la dureza del acto.
- **La cifra vieja no desaparece.** `defeatShareAct1` sigue en `summary.csv` con su valor de siempre
  (29,74%), así que cualquier comparación con los ocho paquetes anteriores sigue siendo posible.
- **El cambio no se elige por su signo.** La métrica nueva **no es más laxa**: es *otra* pregunta. Y viene
  con una banda **de verdad** —`ordinaryDefeatRateAct1` es la primera métrica de acto con banda en el
  proyecto, y entra en la puerta de `Sim.Tests`—, mientras que el techo del 29,74% sólo vivía en el texto
  de los encargos. Se pasa de un guardarraíl imposible y no verificado a uno verificable y verificado.
- **El techo no se ajusta a la medición.** Sale de dos ADR anteriores; si hubiera salido de la medición,
  habría sido 26 o 27, que es donde estaría con el margen de costumbre.
- **Nada de balance se toca.** Ni `/data`, ni el motor, ni ninguna otra banda.

## Consecuencias

- `summary.csv` gana siete filas: `ordinaryDefeatRateAct{1,2,3}` (la primera con banda, las otras dos
  INFO), `bossWinRateAct{1,2,3}` y nada más; `runs.csv` gana seis columnas
  (`bossSamples{1,2,3}`, `bossWins{1,2,3}`).
- `FullRunGateTests` gana una afirmación, `Act1IsTheWorkshop`. La suite completa pasa de 598 a **599/599**
  en Release, todas verdes.
- El encargo del paquete siguiente vigila el acto 1 con `ordinaryDefeatRateAct1 ≤ 30`, no con la cuota.

## Qué falsificaría esta decisión

- **Que el jugador deje de ser el mejor equipo del acto 1.** Si la capa de build o la calidad del rival
  ordinario del acto 1 subieran hasta emparejarlo con el jugador, el 70% de la ADR 0054 dejaría de
  aplicarse y el techo habría que rederivarlo —hacia arriba— con la misma cadena de razonamiento.
- **Que la banda de `betterTeamWinRate` se mueva.** El techo está anclado a su extremo inferior; si la
  ADR 0054 se revisa, este número la sigue.
- **Que el acto 1 deje de tener partidos ordinarios suficientes para medirlo.** Con 5,8 partidos por run
  la tasa es estable a 60 runs (ET ~2,3 puntos); si el mapa acortara el acto 1, habría que subir la
  muestra de la puerta antes que aflojar el techo.
