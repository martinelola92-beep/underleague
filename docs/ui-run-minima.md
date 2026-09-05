# Interfaz mínima jugable

Objetivo: **jugar una run completa de principio a fin** con ratón, sin arte y sin animación. No es la interfaz del juego: es el esqueleto que permite que una persona juegue lo que hasta ahora solo jugaban las políticas automáticas.

## Qué entra y qué no

**Entra**: elegir ruta en el mapa, alinear (la pantalla de Equipo ya existe), jugar un partido y ver qué pasó, elegir recompensa, comprar en el mercado, tratar en la clínica, ampliar plantilla, enfrentarse a los jefes y terminar la run con un resumen.

**No entra**: el partido animado sobre el campo (se resuelve y se muestra el resultado con su log), arte, sonido, navegación con mando, y las pantallas de fase 3 (taller, vínculos, memorial ilustrado).

## Contrato: `RunController`

Un único nodo autoload que envuelve `RunEngine` y guarda el estado. Todas las pantallas hablan **solo** con él; ninguna llama a `/Sim` directamente y ninguna calcula nada del juego (RT-014).

```csharp
public partial class RunController : Node
{
    public RunState State { get; }
    public Catalog Catalog { get; }
    public StandardRunSystems Systems { get; }

    public void NewRun(Race clubRace, ulong seed);      // RunEngine.Start
    public IReadOnlyList<MapNode> Available();          // RunEngine.AvailableNodes
    public void Enter(int nodeId);                      // RunEngine.Enter
    public void Apply(RunDecision decision);            // RunEngine.Apply
    public RunOutcome Outcome();                        // EnCurso | Victoria | Derrota(causa)

    [Signal] public delegate void StateChangedEventHandler();   // cualquier pantalla se redibuja
    [Signal] public delegate void PhaseChangedEventHandler(int phase);
}
```

Regla que evita el error clásico: **la interfaz nunca decide**. Si una pantalla necesita saber algo que `RunState` no expone, se expone en `/Sim` como método puro, no se calcula en la escena.

## Pantallas

| Pantalla | Contenido mínimo |
|---|---|
| **Mapa** | Nodos disponibles como botones con su tipo, el distintivo de dificultad de los partidos y el contador de jugadores frente al mínimo (RF-002e). Los mercados destacados (RF-011b) |
| **Ojeo** | Antes de un partido: plantilla rival, su build, el árbitro y **los perks letales destacados** (RF-013). Botón de empezar |
| **Equipo** | La que ya existe: alineación, fichas, zona de acción, vínculos |
| **Partido** | Marcador, resultado y el log de eventos con scroll. Sin campo animado |
| **Informe** | Perks activados con sus contribuciones, lesiones, muertes y oro ganado (RF-119) |
| **Recompensa** | Las dos opciones (o tres en élite y jefe) con su descripción generada, a quién asignar si es perk, reroll y **rechazar** |
| **Mercado** | Las cuatro categorías con precio, oro disponible, comprar y vender |
| **Clínica / Inscripción** | Coste y confirmación |
| **Fin de run** | Victoria o derrota con su causa, actos superados, plantilla final y caídos |

## Criterio de terminado

Una persona arranca el juego, elige club, juega los 35 nodos y termina —ganando o perdiendo— sin tocar el código ni la consola. Las decisiones que el juego ofrece son las mismas que toman las políticas automáticas.
