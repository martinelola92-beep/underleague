# BB-B — En el saque de centro los defensores van a robar el balón antes de que esté en juego.

**Estado:** Abierta

## Observación

**En el saque de centro los defensores van a robar el balón antes de que esté en juego.** Deberían estar quietos hasta el primer pase

## Análisis / estado actual

**Confirmado en código.** Durante la ventana de reanudación solo el sacador queda congelado; el resto sigue pasando por `UpdatePlayer`. Y **no existe regla de despeje para el saque de centro**: `RestartTuning` solo tiene `FreeKickClearanceCells`, para la falta. Falta el equivalente del círculo central

## Hermanos

_(por enlazar donde se detecten; ver `README.md` del directorio)_
