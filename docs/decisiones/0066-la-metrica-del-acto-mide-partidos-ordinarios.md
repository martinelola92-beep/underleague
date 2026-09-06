# 0066. La métrica del acto mide partidos ordinarios

**Fecha:** 2026-09-06
**Estado:** Aceptada (**decisión del revisor**) e **implementada**. **Cierra AO-D**
**Corrige:** la implementación de `winRateAct{n}` y `matchesLostAct{n}`, que no medían lo que su propia
ADR describe
**Requisitos:** RT-055, RT-056, RT-057, RF-002b, RF-002c, RF-032
**Relacionada con:** ADR 0033, ADR 0056 (objetivos), ADR 0057, ADR 0064 (que lo detectó), ADR 0065

## El desajuste, tal y como la ADR 0064 lo dejó escrito

`winRateAct{n}` es la métrica con la que se publican los objetivos 1 y 2 de la ADR 0056 desde que esa ADR
existe. Su tabla dice, literalmente:

> Sobre 900 runs, partidos **ordinarios** (perder uno no termina la run, RF-002c)

y la métrica **incluía el partido de jefe**, que es exactamente el partido cuya derrota **sí** termina la
run (RF-002b). Lo mismo le pasaba a `matchesLostAct{n}`, cuyo propio comentario en el código decía
"partidos ordinarios perdidos por acto: perder uno no termina la run (RF-002c)".

Las dos cifras, sobre las mismas 1.200 runs (300 × semillas 1/1001/2001/3001):

| perfil | acto 2, con jefe | acto 2, **ordinarios** | acto 3, con jefe | acto 3, **ordinarios** |
|---|---|---|---|---|
| Build **buena** | 57,97 (ET 0,71) | **60,33** (ET 0,85) | 44,43 (ET 0,53) | **43,30** (ET 0,73) |
| Build **mediocre** | 47,94 (ET 0,70) | **50,42** (ET 0,98) | 40,67 (ET 0,25) | **38,65** (ET 0,16) |
| **Hueco del acto 2** | **10,03** (ET 0,50) | **9,91** (ET 0,87) | | |

## Decisión del revisor

**La métrica mide lo que su propia ADR describe: partidos ordinarios.** El partido de jefe ya está
cubierto —y con mucho más detalle— por la curva de puertas de la ADR 0033 y por la identidad de la ADR
0064 (la run es el producto de las tres puertas), y contarlo dos veces hace la métrica ambigua: mezcla en
un solo número el examen y el taller, que la ADR 0057 separó a propósito.

**1. `winRateAct{n}` pasa a medir sólo partidos ordinarios**, y la cifra vieja se publica **al lado**, con
su propio nombre `winRateAct{n}_withBoss`, para que el cambio sea auditable contra los siete paquetes
anteriores sin tener que volver a medirlos. Lo mismo con `matchesLostAct{n}` y
`matchesLostAct{n}_withBoss` (acto 1: 1,19 ordinarios frente a 1,43 con la puerta).

**2. La separación es exacta, no inferida.** `RunPlayResult` ya llevaba `BossSamplesByAct` —los jefes
jugados por acto, de la ADR 0049— y gana `BossWinsByAct`, los jefes **superados** por acto. Con los dos,
`MatchesByAct − BossSamplesByAct` y `WinsByAct − BossWinsByAct` son el partido ordinario exacto.
`BossesBeaten` no vale para esto: sólo cuenta las puertas que dejan la run viva, así que una run que gana
al jefe y se queda sin plantilla en ese mismo nodo sumaría la victoria y no la puerta.

**3. Esto hace que el objetivo 1 pase a estar alcanzado en el acto 2 y no en el acto 3.**

| Objetivo 1 (ADR 0056): build buena al 60% en los actos 2 y 3 | acto 2 | acto 3 |
|---|---|---|
| Como se venía publicando (con jefe) | 57,97 — falta 2,0 | 44,43 — falta 15,6 |
| **Como su ADR lo describe (ordinarios)** | **60,33 — alcanzado** | 43,30 — falta 16,7 |

## Por qué esto no es relajar nada (RT-057)

RT-057 prohíbe el ajuste silencioso, y en particular mover un umbral para que un número lo pase. Aquí no
se ha movido ningún umbral ni ningún número de balance:

- **El objetivo sigue siendo 60%.** No baja a 58, ni se ensancha a una banda.
- **Lo que cambia es el conjunto de partidos que se cuenta**, y cambia hacia el que la decisión describía
  desde el principio. La métrica pasa a medir lo que la decisión siempre dijo; no es la decisión la que se
  ajusta a la métrica.
- **El cambio no se elige por su signo.** Sube el acto 2 (57,97 → 60,33) y **baja** el acto 3
  (44,43 → 43,30), porque en el acto 2 la puerta es más dura que el partido ordinario y en el acto 3 es
  más blanda. Si el criterio fuera "que pase", el acto 3 no se habría tocado.
- **Y se paga un precio, que no se compensa**: el hueco del acto 2 —el guardarraíl que la ADR 0060
  consiguió por primera vez— pasa de **10,03** (ET 0,50) a **9,91** (ET 0,87) sobre un suelo de 9,8. Sigue
  por encima, pero con menos margen y más error, y queda anotado como tal en vez de arreglado con otro
  número.
- **La cifra vieja no desaparece.** `winRateAct{n}_withBoss` la publica en el mismo `summary.csv`, así que
  cualquier comparación con los siete paquetes anteriores sigue siendo posible al decimal.

## Consecuencias

- `summary.csv` del modo `--full-runs` gana seis filas INFO (`winRateAct{1,2,3}_withBoss` y
  `matchesLostAct{1,2,3}_withBoss`). Ninguna métrica con banda cambia, así que **ninguna de las seis
  puertas se mueve**: 598/598 en Release.
- Los objetivos 1 y 2 de la ADR 0056 se publican a partir de ahora en partidos ordinarios. La tabla de
  estado de esa ADR se actualiza con las dos columnas.
- El objetivo 2 (build mediocre en 42-45) se lee ahora en **50,42** en vez de 47,94: se pasa **5,4** puntos
  en vez de 2,9. La elección de fondo sigue siendo la de AM-A/ADR 0063 y no la cambia esta ADR.

## Qué falsificaría esta decisión

- **Que aparezca una razón para volver a contar la puerta dentro del acto.** La habría si la curva de la
  ADR 0033 dejara de medir el jefe por separado; hoy lo mide con doce celdas y un instrumento propio
  (`--boss-gate`), así que el partido de jefe está cubierto dos veces y era la métrica del acto la que
  sobraba.
- **Que el hueco del acto 2 baje de 9,8 medido en ordinarios.** Hoy mide 9,91 con ET 0,87. Si baja, el
  guardarraíl de la ADR 0060 deja de cumplirse con la métrica nueva, y la respuesta correcta es una
  palanca que lo devuelva —no volver a la métrica vieja.
