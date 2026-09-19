# Prototipos y mediciones de la fase de UI

Material de trabajo de las fases A–D.3 (19 sep 2026). **No es código del juego ni se compila**: se aplica a una
copia del proyecto fuera del repositorio para reproducir las capturas de `../capturas/`.

## `prototipo-ui.patch`

Diferencias sobre `Game/` en `c728d64` (no cambia `/Sim`). Añade escenas de captura (`B1`, `C1`, `C2P`,
`C2S`, `C2C`, `D1`) y, en la copia, extiende `MatchPitchView3D` con: cámara a campo entero y desplazamiento
vertical, rectángulo del campo en pantalla, césped gastado, vallas y grada procedurales. Cambia
`project.godot` a 1920×1080 con aspecto `expand`.

```bash
C=/ruta/temporal; git archive HEAD Game Sim data global.json Directory.Build.props | tar -x -C $C
cd $C && patch -p1 < <repo>/docs/ui/prototipo/prototipo-ui.patch   # comprobado: aplica y compila sobre c728d64
# Fuentes (OFL/Apache, no están en el repo): IM Fell, Cinzel, Barlow Condensed, Alfa Slab One, Bangers…
#   https://github.com/google/fonts/raw/main/ofl/<familia>/<fichero>.ttf  → sustituye <FUENTES>/ en el parche
cd $C/Game && dotnet build Underleague.Game.csproj && godot --headless --path . --import
timeout 300 xvfb-run -a godot --path . --rendering-driver opengl3 --resolution 1920x1080 \
  --scene res://Scenes/D1.tscn -- P salida.png <base|gol|roja|lesion|turba|muerte|final> [gray]
```

`C2P` graba vídeo (`--write-movie x.avi --fixed-fps 30`) de un gol, una lesión con decisión y una muerte a
1×/4×/16× con los tiempos provisionales.

## `medicion/` (fase A)

- `arnes-Program.cs.txt` + `arnes.csproj.txt`: consola que juega runs completas con la política automática de
  `/Balance` (réplica de `RunPolicy.Play`), captura el estado previo a cada partido y vuelca sus sucesos en
  JSONL (0 discrepancias de ganador en 11.176 partidos). Renombrar a `.cs`/`.csproj` en una carpeta aparte.
- `director.py`: simulación en papel del agrupador y de los niveles (no es el diseño de implementación).
- `a1`–`a4`: sucesos por partido, momentos por ventana, frecuencia temporal y solapes, políticas de velocidad.

```bash
dotnet run -c Release --project <arnes> -- data 400 1 s1.jsonl
python3 a3_tiempo.py . s1.jsonl
```
