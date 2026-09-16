# BA-L — La escena de capturas (`Scenes/Capturas.tscn`) no produce nada en esta máquina.

**Estado:** RESOLVED (ruta operativa recuperada). Un fallo secundario, menor, queda anotado sin arreglar

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


## Causa real — CONFIRMED (16 sep 2026, orquestación de pendientes técnicos)

**El comando que documentaba `CLAUDE.md` para "sin flag" nunca lanzaba `Capturas.tscn`.** `Game/project.godot`
fija `run/main_scene="res://Scenes/Inicio.tscn"`, y el comando de siempre
(`godot --path Game --rendering-driver opengl3 --audio-driver Dummy`, sin argumento de escena) lanza
**esa** escena — la pantalla de inicio, que simplemente construye su UI y se queda esperando que alguien
elija club y pulse un botón. Sin entrada de teclado/ratón bajo Xvfb, se queda ahí para siempre: sin error,
sin `GD.Print`, con la CPU alta de renderizar una pantalla idle. Es exactamente el síntoma observado
—85 minutos, 295 % de CPU, cero PNG— y **no** tiene nada que ver con `/Sim`, con la ADR 0112 ni con el
campo de siete filas, que eran las hipótesis que se venían barajando.

**Experimento discriminativo**: Godot expone `--scene <path>` (confirmado con `godot --help`) para forzar
qué escena arranca en vez de la principal del proyecto. Probado con timebox de 90 s:

```
timeout 90 xvfb-run -a --server-args="-screen 0 1280x800x24" godot --path Game \
  --scene res://Scenes/Capturas.tscn --rendering-driver opengl3 --audio-driver Dummy
```

Produjo en segundos, con marca de tiempo comprobada: `partido.png`, `partido-correa.png`,
`partido-marcaje.png`, **`partido-perk.png`** (primera verificación visual real del aviso de la ADR 0112:
se ve «Sangre caliente» y «Perro de presa» sobre las fichas en el saque), `partido-3d*.png` (cuatro
variantes) e `informe.png`.

**No se toca ningún código de `/Game` para llegar a esto**: es una corrección de **comando**, documentada
mal desde el principio. `CLAUDE.md` y la skill `visual-review` quedan corregidos con el flag.

## Fallo secundario encontrado, menor, sin arreglar (queda anotado, no bloquea)

Tras `informe.png` el proceso lanza dos excepciones seguidas (`Parameter "data.tree" is null` en
`CaptureRunner.Show`, `CaptureRunner.cs:293`, al hacer `GetTree()` sobre un `CaptureRunner` que parece
haber sido desconectado del árbol de escena entre la captura de `informe` y la de `recompensa`) y no llega
a producir `recompensa.png` ni `mercado.png`. No se ha investigado más allá de localizar la línea: es un
problema de ciclo de vida en la transición entre pantallas de `CaptureRunner`, independiente del hallazgo
de arriba. Anotado como **BA-L2** si hace falta capturar esas dos pantallas antes de que alguien lo mire.
