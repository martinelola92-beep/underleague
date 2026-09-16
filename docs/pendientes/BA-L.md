# BA-L — La escena de capturas (`Scenes/Capturas.tscn`) no produce nada en esta máquina.

**Estado:** Abierta

## Observación

**La escena de capturas (`Scenes/Capturas.tscn`) no produce nada en esta máquina.** Lanzada bajo Xvfb el 15 sep 2026 estuvo **85 minutos con 4 h de CPU al 295 %** sin escribir ni el primer PNG ni imprimir la primera línea de `GD.Print`, dos veces seguidas

## Análisis / estado actual

**Abierta.** **No es la simulación**: un partido de referencia con traza se resuelve en **282 ms, 191 eventos, 1.200 fotogramas** (medido). El cuelgue está antes de la primera captura, así que tampoco es el paso `partido-perk` añadido por la ADR 0112, que va el cuarto. Las últimas capturas buenas son del 14 sep; entre medias entraron el campo de siete filas, la ADR 0109 y la ADR 0110. Mientras siga así, **la pantalla de Partido no se puede verificar visualmente** y el aviso de perk queda cubierto solo por los tests de `/Sim`

## Hermanos

_(por enlazar donde se detecten; ver `README.md` del directorio)_
