---
name: visual-review
description: Ejecutar Godot, capturar y comparar el resultado visual antes de declarar que algo "se ve bien". Usar ante cualquier cambio en /Game, y antes de afirmar cualquier propiedad geométrica o de legibilidad sin haberla comprobado.
---

# Verificar visualmente, no declarar de memoria

**No declares que algo "se ve bien" sin haberlo mirado**, si tienes forma de mirarlo. Lección con nombre
propio: se afirmó que la cámara en tres cuartos empeoraba la lectura del texto tumbado al subir el ángulo,
y era al revés (lo del suelo se comprime por el SENO — subir mejora; lo que está de pie por el COSENO —
subir empeora). Una línea de aritmética, o una captura, cuesta menos que el rodeo.

## Las tres entradas de captura — ninguna las hace todas

| qué quieres | cómo se saca |
|---|---|
| `equipo*.png` (11, pantalla de Equipo) | `-- --screenshots` |
| `inicio`, `mapa`, `ojeo`, `equipo-run` | `-- --tour` · solo el mapa: `-- --map-tour` |
| **`partido*`, `informe`, `recompensa`, `mercado`** | `godot --path Game --scene res://Scenes/Capturas.tscn ...` |

## El ciclo obligatorio

1. `dotnet build Game/Underleague.Game.csproj` — Godot ejecuta `Game/.godot/mono/temp/bin/Debug/`, no lo
   que compila `dotnet build` en la raíz. Con un `.dll` rancio el juego se cuelga al arrancar sin imprimir
   nada, y parece un fallo de `/data` cuando es un binario viejo.
2. Ejecutar **con `timeout`, siempre** (ver la sección de convenciones de `CLAUDE.md` sobre procesos sin
   plazo): `timeout 600 xvfb-run -a --server-args="-screen 0 1280x800x24" godot --path Game
   --rendering-driver opengl3 --audio-driver Dummy` para `equipo*`/`--tour`/`--map-tour` (que sí navegan
   solos desde la pantalla de inicio), o con `--scene res://Scenes/Capturas.tscn` para `partido*`.
   **BA-L (16 sep 2026): el comando sin `--scene` lanza `Inicio.tscn`** (`run/main_scene` de
   `project.godot`), que se queda esperando entrada de usuario para siempre — 85 minutos de CPU al 295 %
   sin un solo PNG fue exactamente ese fallo, no un problema de `/Sim` ni de `/Game`. `Capturas.tscn`
   **no** es la escena principal: sin `--scene` no se lanza nunca.
3. **Comprobar SIEMPRE la marca de tiempo del PNG** (`ls -la Game/screenshots/x.png`) antes de mirarlo.
   Que el proceso salga con código 0 no significa que haya escrito el fichero.
4. Mirar el fichero.
5. Modificar.
6. Repetir 1-4.
7. **Comparar antes/después**, no solo mirar el resultado nuevo en aislado — un cambio puede arreglar lo
   que se pedía y romper algo que no se estaba mirando.

## Si no produce nada

Antes de esperar más: mide por el camino barato si el problema es de `/Sim` o de `/Game` (un test con
`SimConfig.Trace` te dice en milisegundos si el partido de referencia se resuelve bien, descartando la
simulación). Si el proceso de captura sigue sin producir nada tras dos intentos, anótalo en
`docs/pendientes/` con lo medido y sigue con otra cosa — no te quedes esperando un artefacto que no llega
(ver la sección de convenciones de `CLAUDE.md`: "se espera un artefacto, no un latido").

## Qué NO hace

- No ejecuta nada sin `timeout`.
- No declara una propiedad geométrica (ángulo, legibilidad, proporción) sin comprobarla, ni con una
  captura ni con la aritmética.
