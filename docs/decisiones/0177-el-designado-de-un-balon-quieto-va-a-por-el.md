# 0177 — El designado de un balón suelto y quieto va a por él (BC-G)

Fecha: 30 sep 2026 · Estado: **aceptada** · Requisitos: RF-051, RT-096, RT-098 · Ficha: [BC-G](../pendientes/BC-G.md)

## Problema

Con el balón parado en un córner, ningún jugador lo recogía (hasta 656 ticks, 44 s). AW-S deja **un solo** perseguidor
por equipo —el más cercano— y la utilidad lo hacía competir contra las acciones de colocación.

## Causa (CONFIRMED, 30 sep)

Volcado (semilla 79, tick 900): el defensa designado, a 0,50 casillas del balón, puntúa `CoverSpace` 790 y `ChaseBall`
631; los demás **no pueden** perseguir. Clasificación de los 58 episodios de 300 partidos (nearest de cada equipo en
mitad del episodio): los largos (≥ 40 ticks) son todos «defensa designado pierde contra CoverSpace» (hueco 118-280);
los de centrocampista/delantero pierden contra `Retreat` por 5-100 puntos (ciclo M1 de la ficha). Además, en 10 de los
58 el designado era el **portero sin alcance**: BB-G2 pasa de «sin evidencia de activación» a observada (episodios
cortos, los resuelve el rival).

## Decisión

Precondición dura en `Utility.Choose` (`HasLooseBallDuty`): si el balón está suelto **y quieto** (sin dueño, sin
vuelo, sin reanudación, velocidad < 0,02 casillas/tick) y el jugador es el designado con `ChaseBall` viable, las
acciones de colocación (`CoverSpace`, `Retreat`, `MarkOpponent`, `FindSpace`, `OfferSupport`) se descartan. Es la
compuerta dura de los tres motores de referencia (`referencia-motores-futbol.md` §6.3). Si `ChaseBall` no es viable
(límite exterior de zona) no hay deber. No toca ningún peso (`chaseBallLooseBonus` sigue en 410, ADR 0117).

Descartado, **REJECTED bajo esta ADR por medición**: el deber para **cualquier** balón suelto. Elimina los bloqueos
(59 → 3 episodios) pero mueve los tiros de centrocampista de 1,25 a 1,96 por partido (goles 0,27 → 0,48) y quita
tiros al delantero: cambia quién marca. Con el umbral de balón quieto: 59 → 32 episodios, **ninguno de más de 40
ticks** (más largo 448 → 27), tiros de centrocampista 1,37 y de delantero 6,37 (1,25 y 6,38 antes).

## Segunda mitad: el designado es el más cercano de los que pueden llegar

Tras lo anterior quedó un bloqueo de 285 ticks (semilla 141): balón quieto en el borde del área, el portero a 2
casillas como designado sin alcance (`ChaseBall` descartada) y el centrocampista rival persiguiéndolo por fuera de su
límite exterior. **BB-G2 CONFIRMED** (mecanismo observado disparándose). `UpdateContextCaches` sólo cuenta como
candidato al designado de un balón suelto a quien puede llegar: el de campo con el punto dentro de su límite exterior de
zona, el portero con `CanKeeperReachLoose`. Medido tras las dos mitades: 150 partidos, **4 episodios de ≥ 15 ticks, el más
largo de 16** (antes 29 y 656 según la muestra).
