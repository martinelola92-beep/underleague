# BA-L — La escena de capturas (`Scenes/Capturas.tscn`) no produce nada en esta máquina.

**Estado:** Abierta

## Observación

**La escena de capturas (`Scenes/Capturas.tscn`) no produce nada en esta máquina.** Lanzada bajo Xvfb el 15 sep 2026 estuvo **85 minutos con 4 h de CPU al 295 %** sin escribir ni el primer PNG ni imprimir la primera línea de `GD.Print`, dos veces seguidas

## Análisis / estado actual

**Abierta.** **No es la simulación**: un partido de referencia con traza se resuelve en **282 ms, 191 eventos, 1.200 fotogramas** (medido). El cuelgue está antes de la primera captura, así que tampoco es el paso `partido-perk` añadido por la ADR 0112, que va el cuarto. Las últimas capturas buenas son del 14 sep; entre medias entraron el campo de siete filas, la ADR 0109 y la ADR 0110. Mientras siga así, **la pantalla de Partido no se puede verificar visualmente** y el aviso de perk queda cubierto solo por los tests de `/Sim`

## Hermanos

_(por enlazar donde se detecten; ver `README.md` del directorio)_


## Prueba de MCP (16 sep 2026, cierre de la auditoría de organización)

**Descartado.** Se investigó `Coding-Solo/godot-mcp` (la familia runtime/captura, la única con encaje
posible para este problema — ver `docs/analisis/auditoria-organizacion-v2.md` §0/§7). Leído su código
fuente, no solo el README:

- `run_project` —la única función relevante para diagnosticar BA-L— lanza Godot con `-d` (debug), **no**
  `--headless`, sin Xvfb y **sin `timeout`**. En esta máquina sin editor gráfico reproduciría el mismo
  cuelgue, dentro de un proceso que además no se controla desde aquí.
- El único `--headless` real del código pertenece a la automatización de editor (crear escenas, tocar
  nodos vía un script), que ya se había descartado: aquí las escenas se editan como texto.
- **No existe capacidad de captura de pantalla** en ningún punto del código — solo texto de log.

No pasa ni el criterio original (ejecutar + capturar + error legible) ni el cuarto que añadió el
revisor (identificar sin ambigüedad qué escena corre y devolver su resultado). **No se instala.** BA-L
sigue sin resolver por el camino ya conocido: instrumentación desde `/Sim` (descarta la simulación en
milisegundos) más capturas por Xvfb con `timeout`, cuando se retome.
