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

## Segunda mitad: el designado es el más cercano de los que pueden llegar (sólo con balón quieto)

Tras la primera mitad quedó un bloqueo de 285 ticks (semilla 141): balón quieto en el borde del área, el portero a 2
casillas como designado sin alcance (`ChaseBall` descartada) y el centrocampista rival persiguiéndolo por fuera de su
límite exterior. **BB-G2 CONFIRMED** (mecanismo observado disparándose). `UpdateNearestToBall` elige, **sólo con el balón
suelto y quieto** (misma condición que el deber), al más cercano de los que pueden llegar —de campo, con el punto dentro
de su límite exterior de zona; portero, con `CanKeeperReachLoose`—. **Si en un equipo nadie puede llegar se conserva el
más cercano de todos**, como en `main` (la designación nunca queda vacía). Se calcula después de la percepción, para
leer `KeeperExitCells` y `BallDead` de este tick y no del anterior (antes había un desfase de un tick).

Cifras contra `main` (rebasado sobre `515e743`, 400 semillas de referencia): los bloqueos largos de `main` están en las
semillas **32 (180 ticks), 173 (276), 355 (471) y 398 (374)**; las cinco semillas (con la 141) son test permanente y
ninguna pasa de 60 ticks. En 150 partidos: 10 episodios de ≥ 15 ticks, el más largo de 22.

## Nota de `game-design-review` (resumen de las diez preguntas)

1 el jugador ve un balón parado y a todos mirándolo; 2 el más cercano va; 3 «un balón sin dueño es de quien llega»,
compuerta dura de `referencia-motores-futbol.md` §6.3; 4 `/Sim`, sin datos nuevos; 5 hermanos BB-G, BB-G2, BB-N; 6 cualquier
designación única sin alternativa; 7 precedente AW-S y ADR 0117; 8 arregla la causa (el designado no comparaba), no el
síntoma; 9 riesgo: cambiar quién tira (medido y descartado el deber para balón en movimiento); 10 test de escenario con
pesos reales y control (CoverSpace puntúa más), semillas nombradas y lote.
