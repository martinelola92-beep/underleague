# BA-C — Con seis filas no hay fila central y la alineación queda descentrada.

**Estado:** CERRADA

## Observación

**Con seis filas no hay fila central y la alineación queda descentrada.** Portero, delantero único y pivote único quedan media casilla fuera del eje. Lo anticipó la ADR 0103 como «cosmético en el saque» y el revisor dice que no lo es: «esto no puede quedar así»

## Análisis / estado actual

**CERRADA (14 sep 2026, decisión del revisor): el campo pasa a 16×7.** Número impar, la fila central vuelve a existir (`Rows / 2 = 3`) y el portero, el delantero único y el pivote único quedan centrados. Guardado v4, tres esquemas y seis builds de balance remapeados. El reequilibrio costó lo que la ADR 0103 anticipaba: añadir la fila cuesta **~1,0 tiro y ~0,45 goles** por partido en las dos semillas, y `docs/cierre-siete-filas.md` midió que **ninguna palanca local lo recupera** —los pesos de tiro no muerden, la velocidad va al revés y ensanchar las zonas mata `diagonal_press` y hunde la diferenciación de builds—. De ahí la **ADR 0109**: la banda de tiros se recalibra a la geometría vigente (8-16 → **7-15**) y la formación por defecto **no** se ensancha, con las cuatro formas probadas y descartadas registradas en el ADR. Deuda conocida que queda: **tres puertas rojas** de fase 1 —`elf_brawler` 47,71 sobre un techo de 45, `buildsWinDifferently_passChain` 1,08 sobre 1,11, y su agregador—, que se documentan y no se maquillan. Detrás de la primera está **CAT-E**

## Hermanos

_(por enlazar donde se detecten; ver `README.md` del directorio)_
