# BB-A — Los jugadores se teletransportan al reanudar

**Estado:** Abierta

## Observación

**Los jugadores se teletransportan al reanudar** (falta, gol...). Debería ser gradual, o una cortinilla que oculte el salto

## Análisis / estado actual

**Confirmado en código.** `BeginRestart` llama a `ResetPositions()` en el saque de centro (`MatchEngine.cs:2632`), y AZ-A solo congela al **sacador**: los otros diecinueve saltan. Extiende **BA-K**. La salida que propone el revisor está en **BB-D**

## Hermanos

Mismo hilo de causa (el reposicionamiento se resuelve por teletransporte en vez de por transición): [BB-C](./BB-C.md), [BB-D](./BB-D.md), [BB-L](./BB-L.md).
