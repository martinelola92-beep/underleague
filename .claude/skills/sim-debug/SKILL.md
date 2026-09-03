---
name: sim-debug
description: Depurar el simulador - reproducir un partido desde su semilla, volcar la tabla de utilidad de un jugador en un tick (RT-098), leer el log de eventos, y localizar una divergencia de determinismo entre dos ejecuciones o entre Windows y Linux. Usar cuando un partido se comporta raro, un test de determinismo falla, o el usuario pregunta por qué un jugador hizo algo.
---

# Depurar el simulador

Fuentes: `docs/simulacion.md`, `docs/determinismo.md`, `docs/arquitectura.md`.

## Reproducir un partido

Todo partido queda identificado por `(semilla, estado inicial, versión de /data)`. Con eso:

```bash
dotnet run --project Balance -- --runs 1 --seed <seed> --teams <config> --out out/debug/ --log
```

`--log` imprime el log de eventos (RF-121) tick a tick. Si el partido viene de una run guardada, usa `--state <saved.json>` (RT-062), que contiene el snapshot de `/data` (RT-061b).

## "¿Por qué hizo eso el jugador?"

1. Localiza el tick en el log de eventos.
2. Vuelca la tabla de utilidad: `--dump-utility <playerId>:<tick>` (RT-098). Muestra cada acción legal en el estado del jugador con su puntuación y los términos que la componen (peso base por posición, modificadores de rasgo, estado táctico, contexto).
3. Comprueba en orden: ¿estaba el jugador en un estado que permitía la acción esperada (`CanPerform(state, action)`)? ¿La correa la descartó antes de puntuar (RT-095)? ¿Qué término dominó?
4. Si la decisión es correcta según los pesos pero mala para el juego, el arreglo es un dato en `data/ai/`, no un caso especial en código.

## Divergencia de determinismo

Síntoma: `DeterminismTests` falla, o la huella de Windows y Linux difiere en CI.

1. Reproduce en local: ejecuta el mismo partido dos veces y compara las secuencias de eventos con `diff`. El **primer** evento distinto marca el tick sospechoso.
2. En ese tick, busca en orden las causas habituales de `docs/determinismo.md`:
   - Iteración sobre `Dictionary`/`HashSet`.
   - Empate resuelto sin id ascendente (RT-097) o perks sin el orden RT-041.
   - RNG global, `Random.Shared`, `Guid`, `DateTime`, `HashCode`.
   - `float` en una probabilidad o en una comparación mezclada con el RNG.
   - `Math.Sin/Cos/Pow` o cualquier trascendente sobre posiciones.
   - Paralelismo.
3. Si solo diverge entre Windows y Linux y la causa está en `float`, no lo parchees: es el disparador de RT-023b y requiere un ADR.

## Rendimiento

Si `/Balance` supera 60 s por 10.000 partidos (RT-051), perfila con `dotnet-counters`/`dotnet-trace` antes de tocar nada. Sospechosos por orden: asignaciones por tick (records nuevos donde debería mutarse un struct), evaluación de NCalc no cacheada, LINQ en el bucle de utilidad, registro de eventos con contexto copiado.

## Qué entregar

El tick y evento donde empieza el problema, la causa raíz con el fichero y línea, y si la solución es dato o código. Si el arreglo cambia el resultado de partidos ya medidos, avisa de que hay que volver a ejecutar `balance-check`.
