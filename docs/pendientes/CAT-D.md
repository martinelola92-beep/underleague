# CAT-D — ¿Una fila más de campo (16×5 → 16×6), con las DOS filas centrales como «centro»?

**Estado:** Cerrada (ADR 0103) — ver "Análisis / estado actual". Dejó abierto CAT-E

## Observación

**¿Una fila más de campo (16×5 → 16×6), con las DOS filas centrales como «centro»?** Planteada por el revisor el 13 sep 2026 al decidir la dirección visual. A favor: 16×5 es **3,2:1**, un pasillo; un campo de fútbol 7 real ronda 1,5-1,7:1 y 16×6 lo deja en 2,67:1. Y el centro de dos filas es **más limpio que el actual**: hoy `LinkGeometry.FlankOfHome` hace `Rows / 2`, así que el centro es 1 fila de 5 (20 %) y cada banda 2 (40 %); con seis filas y centro en las filas 2-3 quedan **tercios exactos** (33/33/33). Densidad: 80 → 96 casillas, de 5,71 a 6,86 por jugador (**+20 %**; con dos filas eran +40 %)

## Análisis / estado actual

**CERRADA (ADR 0103, 13 sep 2026): entra la fila.** Lo que la desbloqueó fue la cámara, como el revisor pidió: el rectángulo del campo es 3,20:1 y el 3D en tres cuartos dibuja `16/(filas×sen elevación)`, así que con 5 filas hace falta **80° —ya no es tres cuartos— para llenar el marco**, y con 6 basta 55-60°. Medido: entradas 11,23 → 9,19 (dentro), **lesiones 0,92 → 0,75, que estaban FUERA por arriba y entran**, tiros 8,12 → 8,41, tercio máximo 51,10 → 50,38. La compensación de `bodyRadius` que yo daba por necesaria **se midió y se descartó**: no hace nada (56,04 → 56,46). Quedan dos puertas rojas del **instrumento** de fase 1, no del juego → **CAT-E**

## Hermanos

_(por enlazar donde se detecten; ver `README.md` del directorio)_
