# BB-G — El balón se queda parado en el campo

**Estado:** Diagnosticada, sin arreglar. La causa está **CONFIRMED**; el arreglo requiere una decisión de
balance, no de motor.

## Observación

«El balón se queda parado en el campo (a veces en la línea de banda o fondo). ¿No hay saques?»

## Medición

**MEASURED**, 40 partidos con traza: **23 balones muertos de 15+ ticks en juego abierto, el más largo
1.096 ticks** — 73 segundos, casi el partido entero.

## Hipótesis exploradas, en el orden real en que se probaron

No se ordenaron por poder discriminativo antes de empezar — es la lección que deja este problema para
`gameplay-debug` (ver `docs/analisis/auditoria-organizacion-v2.md` §2).

1. **H1 — el muro de la zona de acción bloquea al perseguidor designado.** *(REJECTED bajo la medición
   actual.)* Se escribió un parche que abría el límite duro de la zona solo para `ChaseBall` sobre balón
   suelto. Medido: el número de balones muertos **no cambió**. Revertido.
2. **H2 — el portero acapara la designación de perseguidor y produce un abrazo mortal** (él no puede
   perseguir fuera de su área, y los diez de campo no pueden por no ser el designado). *(REJECTED bajo la
   medición actual — no descartada en general, ver H2b abajo.)* Se escribió el parche que excluye al
   portero de la designación fuera de su área. Medido: el número de balones muertos **no cambió**.
   Revertido.
3. **H3 — `ChaseBall` está legal y puntuada, pero pierde contra las acciones de colocación.** *(CONFIRMED,
   reproducido mediante volcado de la tabla de utilidad — RT-098 — en el tick 1301 de la semilla 33.)* Con
   el balón quieto y catorce jugadores a menos de 9 casillas: el delantero id6, a 1,58 casillas, puntúa
   `ChaseBall` en **884** y elige `FindSpace`; el defensa id102, a 1,15, la puntúa **340** y elige
   `CoverSpace`. `chaseBallLooseBonus` no compensa lo que valen las acciones de colocación.

**H2b (BB-G2, no confirmada ni descartada, latente):** el abrazo mortal portero-designado que describía H2
es real como mecanismo — `UpdateContextCaches` nombra al más cercano incluyendo al portero, y
`EvaluateChaseBall` lo descarta fuera de su área — pero **no se ha observado disparándose** en 40 partidos.
No se cuenta como descartada: es una hipótesis **sin evidencia de activación**, distinta de una hipótesis
**refutada por medición** (H1, H2 arriba). Reabrir si aparece un balón muerto con el portero como jugador
más cercano.

## Por qué no se ha arreglado todavía

El arreglo de H3 es un cambio de pesos (`data/ai/weights.json`), no de motor, y **exige cautela
demostrada**: es la misma palanca (`ChaseBall`) que la auditoría 5 tocó con `pen=50` y hubo que retirar
porque degradaba la diferenciación de builds (3 puertas rojas → 5 → 7 — ver `docs/decisiones/` y
CLAUDE.md, sección de convenciones). Se mide con **una hipótesis de valor por vez** y mirando **todas** las
métricas de diferenciación, no solo la del balón muerto.

## Hermanos

Ninguno detectado con la misma causa. Comparte instrumento (RT-098, volcado de utilidad) con el método que
resolvió BB-M.
