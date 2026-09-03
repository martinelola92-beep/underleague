# Determinismo

Concreta RT-020 a RT-024, RT-041, RT-097. Requisito: **misma semilla + mismo binario => mismo resultado**. El determinismo entre plataformas no es requisito de lanzamiento (RT-023), pero la CI lo mide (RT-024) para activar RT-023b si algún día hace falta.

## Reloj

- El simulador avanza en ticks lógicos a **15 por segundo** (RT-020). Un partido de 60-90 s son 900-1350 ticks; la turba añade los que hagan falta hasta el gol de oro.
- Toda duración (estados de jugador, animaciones lógicas, límites de perks) se expresa en ticks enteros, nunca en segundos ni milisegundos.
- La interpolación visual entre ticks ocurre solo en `/Game`.

## Generador aleatorio

- Propio, PCG32 (ver ADR 0004). Salida de 32 bits, estado de 64 bits, semilla `ulong`. `System.Random` queda prohibido: su secuencia no es estable entre versiones de .NET.
- API mínima: `Next()`, `Range(int minInclusive, int maxExclusive)`, `Chance(int probability)` (devuelve `true` con `probability` en 0-100), `Pick<T>(IReadOnlyList<T>)`, `Shuffle<T>(IList<T>)`. Todo entero.
- **Flujos separados** (RT-022), derivados de la semilla de la run con un mezclador (splitmix64): `matchRng(nodeIndex)`, `mapRng(act)`, `rewardRng(nodeIndex)`, `eventRng(nodeIndex)`. Cambiar recompensas no altera el partido con la misma semilla.
- El RNG del partido se pasa explícitamente a quien lo necesita; no hay instancia global ni estática (RT-021).

## Aritmética

- Atributos (1-99), probabilidades (0-100 o 0-10000 según precisión), contadores, criterio del árbitro (-100..+100), oro y experiencia: **`int`** (RT-023). Los porcentajes se calculan como `value * p / 100` con división entera, siempre en el mismo orden de operaciones.
- Posiciones y vectores de movimiento: `float` (RT-023). Se aceptan porque, con el mismo binario, la misma máquina y el mismo orden de operaciones, `float` es determinista. Reglas para que siga siéndolo:
  - No usar `Math.Sin/Cos/Pow/Exp` en `/Sim`. Solo suma, resta, multiplicación, división y `MathF.Sqrt` (IEEE exacta).
  - No convertir una comparación en probabilidad: la distancia se compara con umbrales, no se mezcla con el RNG.
  - Sin `Parallel`, sin `Task`, sin SIMD en `/Sim`.
- Si RT-024 detecta divergencia Windows/Linux, se migra a punto fijo Fix64 (RT-023b) mediante ADR; no antes.

## Orden

- Perks simultáneos: rareza desc, id de jugador asc, id de perk asc (RT-041).
- Empates de utilidad: id de jugador asc (RT-097). Nunca RNG para desempatar.
- Iteración de jugadores en un tick: por id ascendente.
- Cualquier colección que afecte al resultado se recorre en orden definido: `List` o `SortedDictionary`. `Dictionary` y `HashSet` solo para búsqueda, nunca para iterar.
- Los ids de jugador son enteros asignados en orden de creación dentro de la run; los ids de perk son strings comparados con `StringComparer.Ordinal`.

## APIs prohibidas en `/Sim`

`System.Random`, `Random.Shared`, `Guid.NewGuid`, `DateTime`, `DateTimeOffset`, `Stopwatch`, `Environment.TickCount`, `HashCode.Combine` (semilla aleatoria por proceso), `string.GetHashCode()` como valor (aleatorizado por proceso), `Parallel`, `Task`, `Thread`, `System.IO.File`, `Console`. Se refuerza en fase 0 con un test que escanea el ensamblado y con un analizador `BannedApiAnalyzers` (`BannedSymbols.txt` en `/Sim`).

## Pruebas obligatorias

| Test | Qué comprueba | Referencia |
|---|---|---|
| `DeterminismTests.SameSeedSameEvents` | Dos ejecuciones con misma semilla producen secuencias de eventos idénticas (comparación elemento a elemento, no solo hash) | RT-024, RT-082 |
| `DeterminismTests.CrossPlatformFingerprint` | Escribe un hash de la secuencia para 100 semillas; la CI compara el artefacto de Windows con el de Linux | RT-024 |
| `DeterminismTests.IndependentStreams` | Cambiar la semilla de recompensas no altera los eventos del partido | RT-022 |
| `ArchitectureTests.SimDoesNotReferenceGodot` | El ensamblado `Underleague.Sim` no referencia `GodotSharp` ni `System.IO.File` | RT-011, RT-012 |
| `OrderingTests.SimultaneousPerks` | Tres perks disparados en el mismo evento se resuelven en el orden de RT-041 | RT-041 |

La CI ejecuta `dotnet test` en `windows-latest` y `ubuntu-latest` en cada commit que toque `/Sim` o `/data` (RT-054). Una divergencia de huella entre ambos abre automáticamente la decisión RT-023b.
