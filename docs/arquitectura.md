# Arquitectura

Concreta RT-010 a RT-015, RT-040 a RT-043 y RT-060 a RT-065.

## Proyectos y dependencias

```
Underleague.sln
├── Sim/            Underleague.Sim         net10.0 (ADR 0008), sin paquetes salvo NCalc
├── Sim.Tests/      Underleague.Sim.Tests   xUnit -> Sim
├── Balance/        Underleague.Balance     consola -> Sim
├── tools/
│   └── DataValidator/                      consola -> JsonSchema.Net, Sim (para compilar NCalc)
├── Game/           proyecto Godot .NET     -> Sim
└── data/           JSON (sin proyecto)
```

Reglas:

- `/Game -> /Sim`. **Nunca** `/Sim -> /Game`, ni `/Sim -> Godot`, ni `/Sim -> cualquier API de presentación` (RT-011). Se hace cumplir con un test en `Sim.Tests` que inspecciona las referencias del ensamblado `Underleague.Sim` y falla si aparece `GodotSharp`.
- `/Sim` no referencia `System.IO` para leer ficheros, ni `DateTime`, ni `Environment` (RT-012). Ver `determinismo.md`.
- `/Balance` y `/Game` son los únicos que tocan disco. Leen `/data`, lo entregan a `/Sim` como objetos ya parseados.
- El `Sim.csproj` de la solución raíz **no incluye** `/Game`; Godot gestiona su propio `.csproj` y referencia `../Sim/Sim.csproj` por ruta. Así `dotnet build` en WSL nunca necesita Godot.

## Superficie pública de `/Sim` (RT-013)

Una sola entrada. Todo lo demás es `internal`.

```csharp
namespace Underleague.Sim;

public static class Simulator
{
    /// Ejecuta un partido completo. Puro: mismo (estado, semilla, catálogo, config) => mismo resultado.
    public static MatchResult Run(
        MatchState initialState,
        ulong seed,
        Catalog catalog,            // perks, objetos, consumibles, pesos de IA ya compilados
        SimConfig config);          // ticks/s, profundidad máxima de recursión, etc.
}

public sealed record MatchResult(
    IReadOnlyList<Event> Events,            // ordenados por tick, luego por orden de resolución
    MatchState FinalState,
    MatchReport Report);                    // activaciones de perks con contexto (RT-043), criterio del árbitro
```

Además, para no obligar al llamador a conocer el formato interno, `/Sim` expone parseo **desde string** (no desde fichero):

```csharp
public static class DataLoader
{
    public static Catalog FromJson(IReadOnlyDictionary<string, string> filesByPath); // valida contra esquema, compila NCalc
}
```

Quién lee del disco es siempre el llamador (`/Game`, `/Balance`, `/tools`).

## Eventos (RT-040, RF-066, RF-067)

- El evento (`Event`) es un registro inmutable: `Type` (enum `EventType` con el catálogo de RF-066), `Tick`, y `Context` con ejecutor (`Actor`), receptor (`Target`), rival implicado (`Opponent`), casilla (`Cell`), zona del campo (`Zone`), estado del partido (`MatchPhase`, reglamentario/turba), criterio del árbitro (`Bias`) y distancia a portería (`DistanceToGoal`).
- El bus es propio (~30 líneas): lista ordenada de suscriptores por tipo de evento. Los perks activos se suscriben al empezar el partido.
- Orden de resolución de perks simultáneos (RT-041): rareza descendente, id de jugador ascendente, id de perk ascendente. Documentado aquí y comprobado por test.
- Recursión (RT-042): un efecto que publica un evento incrementa una profundidad; al superar `config.MaxDepth` (por defecto 4) el evento se descarta y se registra `RECURSION_CUT` en el informe, nunca una excepción.
- Cada activación se registra con su contexto (RT-043) para alimentar el informe post-partido (RF-119).

## Render (RT-014, RT-020)

`/Game` reproduce la lista de eventos: interpola posiciones entre ticks, dispara animaciones, highlights (RF-115/116), texto flotante (RF-118) y log (RF-121). No tiene acceso al estado interno del simulador y no puede alterar el resultado. La velocidad x1..x4 y "saltar al resultado" (RF-050) solo cambian el ritmo de consumo de la lista. La repetición (RF-120) es volver a ejecutar `Simulator.Run` con la misma semilla y reproducir el tramo.

## Consumibles manuales durante el partido (RF-082)

Un consumible manual es una entrada del usuario en mitad de un partido determinista. Se modela como parte del **estado inicial**: `Simulator.Run` se llama con la lista de consumibles ya equipados y sus disparadores; el slot manual se resuelve así:

1. El render ejecuta el partido por tramos. Al pulsar el consumible en el tick T, `/Game` vuelve a llamar a `Run` con `initialState + manualActivation(consumableId, tick T)` y descarta los eventos posteriores a T de la ejecución anterior.
2. La activación queda en el estado, por lo que la repetición y el guardado ironman (RT-061) reproducen exactamente lo mismo.

Esta decisión mantiene RT-013 (una sola entrada pura) sin excepciones. Coste: recalcular desde el inicio, que con partidos de 60-90 s a 15 ticks/s es despreciable (RT-051).

## Carga de datos y snapshot por run (RT-031, RT-061b)

- `/data` se lee al arrancar, se valida (RT-032) y se compila. Un error de validación aborta con mensaje que incluye fichero, ruta JSON y regla incumplida.
- Al empezar una run se **congela una copia** de los ficheros de `/data` dentro del guardado. Cargar una run usa su snapshot, no el `/data` actual.

## Persistencia (RT-060 a RT-063)

- JSON local con `schemaVersion`. Un slot por run (ironman): se guarda al completar cada nodo, se borra al cargar. Salir a mitad de partido reproduce el partido desde la semilla al volver.
- Modo de depuración (RT-062): `/Game` y `/Balance` aceptan `--state path.json` para cargar un estado predefinido.
- Steam Cloud sincroniza el directorio de guardado. Fase 4.
- Telemetría (RT-065): el **formato del evento** se define en fase 1 en `modelo-datos.md`; el envío llega en fase 4 y está desactivado por defecto.

## Lo que queda fuera de `/Sim`

Generación de mapa, recompensas, mercado, economía, vínculos entre partidos y progresión meta viven en una librería aparte, `/Run` (candidata; decidir en fase 2 si se funde con `/Sim` o va separada). Usan sus propios flujos de RNG (RT-022) y las mismas reglas de determinismo. Hasta entonces, se implementan en `/Sim` bajo un namespace `Underleague.Sim.Run` para no crear proyectos prematuros.
