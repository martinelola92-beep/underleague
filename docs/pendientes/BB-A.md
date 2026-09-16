# BB-A — Los jugadores se teletransportan al reanudar

**Estado:** Resuelta. Dos causas distintas, dos arreglos distintos: **BB-C** (16 sep 2026, `/Sim`) quitó el
teletransporte real de posición que sufría el goleador celebrando; **BA-K** (16 sep 2026, `/Game`) arregló
cómo se **ve** el salto del resto del equipo, que es real en `/Sim` (RT-020: el tick lógico es el entero,
no hay nada que suavizar ahí) pero se enseñaba mal en el render.

## Observación

**Los jugadores se teletransportan al reanudar** (falta, gol...). Debería ser gradual, o una cortinilla que oculte el salto

## Análisis / estado actual

**Confirmado en código.** `BeginRestart` llama a `ResetPositions()` en el saque de centro
(`MatchEngine.cs`), y AZ-A solo congela al **sacador**: los otros diecinueve saltan de verdad, de una
casilla a otra, en un solo tick — eso es correcto y no se toca (RT-020, RT-021: el motor no suaviza nada,
solo el render interpola). **Corregido en `ResetPositions()` (BB-C)**: el jugador que sigue `Celebrating`
ya no es de los que saltan.

**Lo que sí se veía mal era el render** (BA-K, `docs/pendientes/BA-K.md`): `Interpolate`/`PositionOf`
mezclaban con `Lerp` la posición del tick de antes del saque con la de después, así que el salto de varias
casillas se enseñaba como un deslizamiento rapidísimo a través del campo en vez de una reforma. Arreglado:
por encima de 0,6 casillas de salto entre dos ticks, ya no se interpola — se corta en el punto medio del
tick (con una atenuación de opacidad en la vista 3D, la "cortinilla" que pedía el ticket). Verificado con
captura: la reforma pasa de una masa de fichas apiñada a la formación en línea sin ningún fotograma
intermedio deslizante.

## Hermanos

Mismo hilo de causa (el reposicionamiento se resuelve por teletransporte en vez de por transición):
[BA-K](./BA-K.md), [BB-C](./BB-C.md), [BB-D](./BB-D.md), [BB-L](./BB-L.md).
