# BB-A — Los jugadores se teletransportan al reanudar

**Estado:** Parcialmente resuelta. **BB-C** (16 sep 2026) arregló el caso del goleador celebrando —ya no
salta a su casilla-hogar mientras sigue en `Celebrating`—, pero es un sub-caso de esta ficha, no la
ficha entera: los **otros diecinueve** jugadores (y el goleador mismo, una vez termina de celebrar)
siguen saltando instantáneamente a su casilla-hogar en cada saque de centro. Eso es un problema de
**presentación** (RT-014: el render interpola, no decide), no de lógica de `/Sim` — sigue abierto,
extiende **BA-K**.

## Observación

**Los jugadores se teletransportan al reanudar** (falta, gol...). Debería ser gradual, o una cortinilla que oculte el salto

## Análisis / estado actual

**Confirmado en código.** `BeginRestart` llama a `ResetPositions()` en el saque de centro (`MatchEngine.cs:2632`), y AZ-A solo congela al **sacador**: los otros diecinueve saltan. Extiende **BA-K**. La salida que propone el revisor está en **BB-D**. **Corregido en `ResetPositions()` (BB-C)**: el jugador que sigue `Celebrating`/`KnockedDown` ya no es de los diecinueve que saltan; el resto sigue igual, sin cambios de este ciclo.

## Hermanos

Mismo hilo de causa (el reposicionamiento se resuelve por teletransporte en vez de por transición): [BB-C](./BB-C.md), [BB-D](./BB-D.md), [BB-L](./BB-L.md).
