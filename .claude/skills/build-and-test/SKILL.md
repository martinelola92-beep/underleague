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
dotnet test Sim.Tests -c Release --filter "Category=Gate" -m:1 -v q       # las 43 puertas estadísticas: UNA invocación, antes del commit del hito, ≤8 min
dotnet test Sim.Tests -c Release --filter "FullyQualifiedName~X" -m:1 -v q   # solo lo que cubre el cambio, durante el desarrollo
dotnet run --project Balance -c Release -- --runs 10000 --seed 1 --teams data/balance/reference.json --out out/ --quiet
dotnet run --project tools/DataValidator -- data/                         # esquemas de /data, tras CUALQUIER cambio en data/**
dotnet build Game/Underleague.Game.csproj                                 # OBLIGATORIO antes de ejecutar Godot (ver skill visual-review)
```

## Reglas que van con estos comandos

- Las puertas (`Category=Gate`) se lanzan **una vez y en una sola invocación**, nunca tras cada edición ni
  troceadas por clase. `summary.csv` se lee con `grep -E "^métrica,"`, nunca entero (>150 filas).
- No repitas un build o test cuyo resultado ya conoces.
- El lote de `/Balance` se lanza con una hipótesis concreta que medir — ver skill `balance-measure`.
- Todo proceso largo va envuelto en `timeout`; ver skill `visual-review` y la sección de convenciones de
  `CLAUDE.md` para el presupuesto por tarea y qué cuenta como "terminado" (un artefacto con marca de
  tiempo, nunca CPU alta).
- El paralelismo del arnés (`/Balance`, puertas de `Sim.Tests`) usa `Parallel.For` por índice con semilla
  función pura del índice y un `Catalog` por hilo — ver skill `architecture-review` antes de escribir un
  bucle nuevo de partidos independientes.

## Qué NO hace

- No decide CUÁNDO lanzar cada cosa — eso lo dan `gameplay-debug`, `balance-measure` y `visual-review`.
  Esta skill es solo la referencia de comandos para no duplicarlos.
