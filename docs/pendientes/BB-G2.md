# BB-G2 — Riesgo latente encontrado de camino, sin evidencia de que se dispare

**Estado:** Abierta

## Observación

**Riesgo latente encontrado de camino, sin evidencia de que se dispare**

## Análisis / estado actual

**Abierta.** `UpdateContextCaches` nombra perseguidor designado al más cercano al balón **incluyendo al portero**, y `EvaluateChaseBall` descarta al portero si el balón está fuera de su área. Si el portero es el más cercano fuera de su área, se produce un **abrazo mortal**: él no puede perseguir por ser portero, y los diez de campo no pueden por no ser el designado (precondición dura de AW-S). No he encontrado ningún caso real en 40 partidos, así que **no se ha tocado**; se anota para que exista, y el arreglo —excluir al portero de la designación cuando el balón está fuera de su área— es de una línea

## Hermanos

_(por enlazar donde se detecten; ver `README.md` del directorio)_

## 30 sep 2026 — activación observada (BC-G, ADR 0177)

**CONFIRMED.** En 58 episodios de balón quieto (300 partidos) el designado era el portero sin alcance en 10 (episodios
cortos: los resuelve el rival), y la semilla 141 dio un bloqueo de **285 ticks**: balón quieto en el borde del área, el
portero a 2 casillas como designado (`ChaseBall` descartada) y sin derecho de nadie más de su equipo a perseguir.
**Arreglo:** el designado de un balón suelto es el más cercano de *los que pueden llegar* (portero con
`CanKeeperReachLoose`, de campo con el punto dentro de su límite exterior). Tras el arreglo: 4 episodios de ≥ 15 ticks
en 150 partidos, el más largo de 16. Cerrada por ADR 0177; queda como test (`LooseBallStuckTests`, semilla 141).

## Barrido de detectores (3 oct 2026)

**REJECTED bajo la ADR 0177.** Con el detector validado en la build anterior al arreglo (6/400 partidos, semilla 389 tick 877,
483 ticks de balón quieto con el portero como más cercano y nadie persiguiendo), la build actual da **0/2.000** (cota 95 % <
0,15 % por partido). El portero elige `ChaseBall` fuera del área 2,3 ticks por partido (no es el abrazo mortal). Informe: [barrido-detectores-2026-10-03](../analisis/barrido-detectores-2026-10-03.md).
