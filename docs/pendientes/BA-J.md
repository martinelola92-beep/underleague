# BA-J — Tras una parada, el equipo defensor debería replegarse

**Estado:** **Implementada, cifras provisionales (30 sep 2026, [ADR 0178](../decisiones/0178-la-parada-se-asienta.md))** — pausa del portero y repliegue de quien tiró; baja la sangre, vigilar la puerta de la ADR 0168

## Observación

**Tras una parada, el equipo defensor debería replegarse** y el portero esperar unos ticks antes de sacar. «El delantero puede quedarse presionando, pero el resto debería replegar»

## Análisis / estado actual

**Abierta.** Hoy no hay fase de repliegue tras `SAVE`: se reanuda en caliente

## Hermanos

_(por enlazar donde se detecten; ver `README.md` del directorio)_

## Diagnóstico y arreglo (30 sep 2026, ADR 0178)

- **CONFIRMED:** el portero suelta siempre a los 5 ticks (300 partidos, 664 paradas retenidas); al soltar 1,8 de los 6
  rivales a < 4 casillas y 4,9 en campo contrario. La transición defensiva existente apenas baja medio paso.
- **Lectura del texto (LIKELY):** «equipo defensor» = el que ahora defiende, o sea el que tiró; «el delantero puede
  quedarse presionando» no se implementa (el repliegue baja también al delantero).
- **REJECTED:** que el síntoma fuera de posesión. El receptor pierde el balón ante quien tiró en 45 ticks el 77 % antes y
  el 76-78 % después; los saques de puerta, 73 %.
- **Arreglo:** `save.holdTicks` 15 y `save.retreatTicks` 20. Un primer intento reutilizando la orden defensiva rompió
  `ShoutTests`/`MobTests`: la orden efectiva la reconstruye la vista de gritos de los eventos.
- **Coste:** la pausa mueve entradas −8 %, lesiones −8 %, tiros +7 %, goles +10 % (sonda); el repliegue, nada.
