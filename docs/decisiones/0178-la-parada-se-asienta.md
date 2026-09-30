# 0178 — La parada se asienta: el portero espera y el equipo que tiró se repliega (BA-J)

Fecha: 30 sep 2026 · Estado: **aceptada, cifras provisionales hasta el lote** · Requisitos: RF-057b, RT-096 · Ficha: [BA-J](../pendientes/BA-J.md)

## Problema

Tras una parada atrapada el portero soltaba el balón **siempre a los 5 ticks** (mediana, p10 y p90 = 5) con 1,8 de los
seis rivales a menos de 4 casillas y 4,9 en campo contrario (300 partidos, 664 paradas retenidas).

## Decisión

Dos datos en `tuning.save`: `holdTicks` = **15** (el portero conduce sin decidir, estado `Dribbling` con contador, el
compromiso de la ADR 0137) y `retreatTicks` = **20** (repliegue del equipo que tiró). El repliegue baja la casilla-hogar
a donde la pondría `mentalityShift.Defensive` (−1/−2/−3 por puesto) **sea cual sea la orden**, sin tocar
`_context.Order`: la vista de gritos la reconstruye de los eventos y un repliegue por la orden la desmentía
(`ShoutTests`/`MobTests` lo detectaron en el primer intento). Una orden ya defensiva no baja dos veces.

Procedencia (Regla H): 15 es el menor valor con el que al soltar quedan ≤ 0,5 rivales a menos de 4 casillas (0,45
medido; 3,3 de 6 en campo contrario, antes 4,9). El repliegue dura hasta que suelta (hold + 5 del armado).

## Coste medido (sonda, 3.000 partidos de referencia, dos términos medidos cada uno con el otro apagado)

| celda | posesión | entradas | lesiones | tiros | goles |
|---|---|---|---|---|---|
| sin nada | 26,66 | 8,99 | 0,390 | 8,05 | 1,98 |
| sólo pausa (20) | 26,12 | 8,27 | 0,358 | 8,59 | 2,18 |
| sólo repliegue (25) | 27,14 | 9,12 | 0,397 | 8,01 | 1,97 |
| ambos (20/25) | 26,20 | 8,27 | 0,347 | 8,56 | 2,17 |

La pausa es lo que mueve el balance (−8 % entradas y lesiones, +7 % tiros, +10 % goles, atribuido al portero que decide con
el área ya despejada: reparte mejor); el repliegue es casi gratis. **Baja la sangre**: hay que vigilar la puerta de la
ADR 0168. El receptor pierde el balón ante el equipo que tiró en 45 ticks el 77 % antes y el 76-78 % después: el
síntoma «se reanuda en caliente» es visible, no de posesión.
