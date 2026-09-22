---
name: build-and-test
description: Los comandos de compilación, prueba y validación del proyecto — fuente única, para no duplicarlos en CLAUDE.md, en cada agente y en cada skill. Usar como referencia siempre que haya que compilar, probar, o validar /data.
---

# Comandos — fuente única

**Siempre `-c Release`, siempre `-m:1`.** Medido: la misma clase de tests tarda 3 m 47 s en Debug y 13 s en
Release (17x). El `-m:1` es por la memoria del contenedor WSL (7,8 GB).

```bash
dotnet build Underleague.slnx -c Release -m:1 -v q                        # /Sim, /Sim.Tests, /Balance, /tools (sin /Game)
dotnet test Sim.Tests -c Release --filter "Category!=Gate" -m:1 -v q      # bucle de desarrollo, ~40 s
dotnet test Sim.Tests -c Release --filter "Category=Gate" -m:1 -v q       # las 43 puertas estadísticas: UNA invocación, antes del commit del hito, ~9 min
dotnet test Sim.Tests -c Release --filter "FullyQualifiedName~X" -m:1 -v q   # solo lo que cubre el cambio, durante el desarrollo
dotnet run --project Balance -c Release -- --runs 10000 --seed 1 --teams data/balance/reference.json --out out/ --quiet
dotnet run --project tools/DataValidator -- data/                         # esquemas de /data, tras CUALQUIER cambio en data/**
dotnet build Game/Underleague.Game.csproj                                 # OBLIGATORIO antes de ejecutar Godot (ver skill visual-review)
```

## Leer resultados sin gastar contexto: resumidores

La salida cruda de `dotnet test` y los `summary.csv` son la mayor fuente de contexto gastado en balde. Por
defecto se leen con estos dos scripts (cero tokens de modelo, deterministas):

```bash
timeout 960 tools/test-resumen.sh Sim.Tests -c Release --filter "Category=Gate" -m:1 -v q
#   TESTS: 43 · Failed 3 · Passed 40 · rc=1 · 7 m 05 s · trx: …   + una línea FALLA por test con su mensaje
#   Si no compila: SIN RESULTADOS + los errores distintos del compilador. Mismos argumentos que dotnet test.
tools/balance-resumen.py out/X/summary.csv [--base out/B/summary.csv] [--top 8] [--metric M ...]
#   Sin --base: las filas OUT con su banda. Con --base: cambios de estado IN<->OUT y las más movidas.
```

`balance-resumen.py` no sabe de ruido entre semillas (no está en `summary.csv`): un Δ grande es un candidato
a mirar, no una conclusión. El log y el `.trx` completos quedan en la ruta impresa por si hace falta abrirlos.

## Reglas que van con estos comandos

- Las puertas (`Category=Gate`) se lanzan **una vez y en una sola invocación**, nunca tras cada edición ni
  troceadas por clase. `summary.csv` se lee con `tools/balance-resumen.py` o con `grep -E "^métrica,"`,
  nunca entero (>150 filas).
- No repitas un build o test cuyo resultado ya conoces.
- El lote de `/Balance` se lanza con una hipótesis concreta que medir — ver skill `balance-measure`.
- Todo proceso largo va envuelto en `timeout`; ver "Disciplina de procesos" abajo para el presupuesto por
  tarea y qué cuenta como "terminado" (un artefacto con marca de tiempo, nunca CPU alta).
- El paralelismo del arnés (`/Balance`, puertas de `Sim.Tests`) usa `Parallel.For` por índice con semilla
  función pura del índice y un `Catalog` por hilo — ver skill `architecture-review` antes de escribir un
  bucle nuevo de partidos independientes.

## Disciplina de procesos

**Nada se lanza sin plazo, y la CPU alta no es señal de progreso.** El 15 sep 2026 se dejó la escena de
capturas 85 minutos con 4 h de CPU al 295 % sin producir nada, confundiendo "el proceso está vivo" con
"está avanzando". Reglas:

- **Todo proceso largo va envuelto en `timeout`**, siempre, sin excepción.
- **Presupuesto por tarea, medido**: capturas ≤ 10 min · las 43 puertas ≤ 11 min (tardan 8 m 41 s desde la ADR 0131, que hace que la puerta de build promedie ocho plantillas en vez de una) · el
  bucle `Category!=Gate` ≤ 2 min (tarda ~40 s). Al doble del presupuesto, se mata y se diagnostica.
- **Se espera un ARTEFACTO, no un latido.** `pgrep`/`%CPU`/`TIME` dicen que el proceso existe, no que
  progrese. La condición de espera es un fichero escrito o una línea de log, con marca de tiempo
  comprobada.
- **Diagnostica por el camino barato antes de esperar más.** Aquel cuelgue se resolvió en 282 ms con un
  test de `/Sim` que descartó la simulación — si existe una medición de segundos que acota el problema,
  va antes que la segunda espera.
- **No toques `/data` mientras corre un lote o una puerta.** Cada clase de test carga el catálogo cuando
  xUnit la inicializa, no al arrancar la ejecución, así que una edición a mitad de pasada contamina solo
  algunas clases y el resultado parece válido. Pasó el 22 sep 2026 (un experimento con
  `tackleMarkTargetBonus` durante las 43 puertas: hubo que tirar la pasada entera y repetirla). Si hay que
  medir con `/data` distinto mientras algo corre, se copia el árbol y se pasa `--data <copia>`.
- **Dos esperas fallidas cierran el asunto.** Se anota en `docs/pendientes/` con lo medido y se sigue.

## Qué NO hace

- No decide CUÁNDO lanzar cada cosa — eso lo dan `gameplay-debug`, `balance-measure` y `visual-review`.
  Esta skill es solo la referencia de comandos para no duplicarlos.
