# CAT-C — La medición no puede ejercitar el consumible manual, así que subestima a la familia.

**Estado:** Abierta

## Observación

**La medición no puede ejercitar el consumible manual, así que subestima a la familia.** RF-082 exige que, si se equipa algo, uno sea **manual**; el manual solo se dispara si alguien lo pulsa, y en `/Balance` no hay quien pulse (`MatchConsumable.ManualTick` a −1). De los dos que equipa la política, solo el condicional produce efecto: el coste en oro se paga entero y el efecto se cobra a medias

## Análisis / estado actual

**Abierta.** Las cifras de la ADR 0101 son por tanto una **cota inferior** del valor de la familia, y el coste real para un jugador que sí pulsa es menor que los −2,5 puntos de `runWinRate` medidos. Corregirlo es darle a la política una **doctrina de activación** (¿a qué tick pulsa?, ¿reactiva al ir por detrás?) y pasar `MatchDecisions` desde `RunPolicy`, que hoy entra por `RunEngine.Enter` sin decisiones. Es una decisión de diseño propia, no parte de cerrar CAT-B

## Hermanos

_(por enlazar donde se detecten; ver `README.md` del directorio)_
