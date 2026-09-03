# Balance

Concreta RT-050..057 y las métricas obligatorias dispersas por los requisitos (RF-024, RF-064e, RF-064g, RF-114k, RT-055, RT-081). `/Balance` existe desde la fase 0 y es el criterio de salida de cada fase, no una herramienta de final de proyecto (riesgo "balanceo inabordable con 150 perks").

## Herramienta `/Balance` (RT-050..053)

Consola .NET que ejecuta N partidos sin Godot y vuelca CSV.

```
dotnet run --project Balance -- \
  --runs 10000 \
  --seed 1 \
  --teams data/balance/referencia.json \      # parejas o pool de equipos
  --perks sed_de_sangre,cara_de_inocente \    # filtro opcional
  --out out/2026-09-03/ \
  [--estado ruta.json]                        # RT-062: estado predefinido
  [--dump-utility idJugador:tick]             # RT-098
```

Rendimiento: 10.000 partidos en menos de 60 s en máquina de desarrollo (RT-051). Se mide en cada ejecución y se imprime al final.

Salida (`resumen.csv` + `partidos.csv` + `perks.csv`):

- `partidos.csv`: semilla, equipoA, equipoB, golesA, golesB, ganador, ticks, alternancias, cadenaMediaPases, tiros, entradas, faltas, tarjetas, lesiones, muertes, turba (bool), criterioFinal, tiempoBalonPorTercio.
- `perks.csv`: idPerk, activaciones, partidosConActivacion, contribucion (goles, lesiones, recuperaciones).
- `resumen.csv`: cada métrica de la tabla siguiente con valor, rango y `DENTRO|FUERA`.

## Métricas de sensación de fútbol (RT-056)

Criterio de salida de la fase 0 e indicador permanente del equilibrio fútbol/agresividad.

| Métrica | Rango objetivo | Cómo se mide |
|---|---|---|
| Alternancias de posesión por partido | 12-25 | Cambios de equipo poseedor |
| Longitud media de cadena de pases | 2-4 | Pases completados consecutivos por posesión |
| Tiros por partido (ambos equipos) | 8-16 | Eventos `TIRO` |
| Distribución de resultados | Mayoría entre 1-0 y 3-2; < 5% con más de 5 goles totales; < 15% de empates **al final del reglamentario** | Marcador antes de la turba |
| Tiempo del balón por tercio | Ningún tercio > 50% | Ticks con el balón en cada tercio de columnas |
| Entradas por partido | 6-14 | Eventos `ENTRADA` |
| Lesiones por partido | 0,3-0,8 | Eventos `LESION` |

Los rangos son puntos de partida. **Cambiar un rango es una decisión explícita** (RT-057): ADR en `decisiones/` con los datos que lo motivan y actualización de esta tabla en el mismo commit.

## Puertas de CI (RT-054, RT-055)

En cada commit sobre `/Sim` o `/data`:

1. `dotnet test Sim.Tests` incluida la prueba estadística de 1.000 partidos (RT-081) contra los rangos anteriores.
2. Lote de balance con el conjunto de referencia. **El build falla** si alguna build catalogada supera el 70% o baja del 30% de tasa de victoria contra la referencia (RT-055).
3. Validación de `/data` (RT-083).

## Métricas de diseño obligatorias

Se añaden a `resumen.csv` cuando el sistema correspondiente existe:

| Métrica | Requisito | Condición de aprobado | Fase |
|---|---|---|---|
| Común nivel 8 con buenos perks vs legendario nivel bajo | RF-024 | El común gana más del 50% | 1 |
| Común superviviente competitivo ante jefe final | RF-023b | Tasa de victoria del equipo con comunes de nivel 8 dentro de 30-70% contra el jefe final | 2 |
| Build de violencia con sobornos vs sin sobornos | RF-064e | Viable (>=40%) con sobornos, inviable (<30%) sin ellos | 3 |
| Build de violencia con sobornos + 2 mitigaciones | RF-064g | Alcanza la tasa de referencia sin depender de una sola mitigación (retirar cualquiera no la hunde por debajo del 30%) | 3 |
| Oro medio por acto | RF-114k | Permite usar 2-3 sumideros, nunca todos | 2 |
| Cada raza sostiene 3 builds viables distintas | RF-032 | Tres configuraciones con tasa 30-70% y perks mayoritariamente distintos | 2-3 |
| Distribución del catálogo de perks | RF-069 | 60/30/10 ±5 puntos | 1+ |
| Perks que acumulan entre partidos | RF-070 | >= 15 en el catálogo de lanzamiento | 2+ |
| Mejores equipos ganan más con sorpresas creíbles | Fase 0 | Equipo +10 en todos los atributos gana 65-80% | 0 |

## Definición de "build" para `/Balance`

Un fichero en `data/balance/builds/<id>.json`: club, plantilla con niveles, perks asignados, objetos, consumibles. Las builds catalogadas son las que RT-055 vigila. Toda build nueva que se diseñe (una raza, un arquetipo) entra aquí antes de darse por terminada.

## Procedimiento al ajustar un número

1. Cambia el valor en `/data` (nunca en código si el valor debería ser dato).
2. Ejecuta el lote de referencia con la misma semilla base que la última medición.
3. Compara `resumen.csv` con el anterior. Anota en el commit qué métricas se movieron y por qué.
4. Si una métrica sale de rango y crees que el rango está mal, no toques el rango: abre un ADR.
