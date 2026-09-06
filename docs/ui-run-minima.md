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

---

## Implementación de la navegación (paquete de mapa, ojeo y nodos)

Lo que existe ya y con qué contrato, para que las demás pantallas se enchufen sin preguntar.

### `RunController`, autoload

`Game/Autoload/RunController.cs` (+ `RunController.tscn`, registrado en `project.godot` como autoload
`RunController`). Cumple el contrato de arriba y añade lo que las pantallas necesitaban de verdad:

| Miembro | Para qué |
|---|---|
| `State`, `Catalog`, `Systems` | el estado de la run, el catálogo de **su** instantánea de `/data` (RT-061b) y los sistemas estándar (economía, mercado, recompensas) |
| `Engine` | los sistemas **compuestos** (`BossRunSystems` envolviendo a `StandardRunSystems`), que es lo que hay que pasarle a cualquier consulta de `/Sim`: si se pasan los de dentro, el jefe no aplica sus modificadores y el ojeo miente |
| `NewRun`, `Continue`, `Abandon` | empezar, retomar el guardado ironman —que se borra al cargarse, RT-061— y abandonar (RF-007) |
| `Available`, `Enter`, `Apply`, `Outcome` | el bucle de `RunEngine`, con el guardado automático al completar cada nodo |
| `SelectedNodeId` | el nodo que el jugador ha elegido y **todavía no ha entrado**: lo pone el mapa, lo lee el ojeo y lo consume la pantalla de partido |
| `LastMatch` | el `MatchEntry` del último partido: resumen y `MatchReport` para la pantalla de partido y el informe (RF-119) |
| `StateChanged`, `PhaseChanged` | señales de redibujado |

La E/S vive aquí y en `Game/Data/GameData.cs` (leer `/data`, escribir `user://run.json`). `/Sim` sigue sin
leer ficheros ni mirar el reloj (RT-012).

### Navegación

`Game/Ui/Nav.cs` es el **único** sitio que decide qué pantalla toca: run terminada → fin de run; nodo
abierto → la pantalla de ese tipo de nodo (partido abierto = recompensa); si no, mapa. Cada pantalla
termina llamando a `Nav.Route(this)` y no sabe quién viene después.

| Escena | Script | Quién la escribió |
|---|---|---|
| `Scenes/Inicio.tscn` | `StartScreen` | navegación |
| `Scenes/Mapa.tscn` | `MapScreen` (+ `Ui/MapView`, `Ui/NodeBadge`) | navegación |
| `Scenes/Ojeo.tscn` | `ScoutScreen` | navegación |
| `Scenes/Nodo.tscn` | `NodeScreen` (clínica, inscripción, entrenamiento, evento) | navegación |
| `Scenes/FinDeRun.tscn` | `RunEndScreen` | navegación |
| `Scenes/Pendiente.tscn` | `PlaceholderScreen` | navegación |
| `Scenes/Equipo.tscn` | `TeamScreen`, la que ya existía | fase 1 |
| `Scenes/Partido.tscn`, `Informe.tscn`, `Recompensa.tscn`, `Mercado.tscn` | `MatchScreen`, `ReportScreen`, `RewardScreen`, `MarketScreen` | partido |

Una escena que **todavía no existe** no bloquea la run: `Nav.Go` la sustituye por `Pendiente.tscn`, que
dice cuál falta y deja seguir (y, si lo que falta es el partido, lo juega y enseña el marcador). En cuanto
el fichero aparece, la ruta lo encuentra sin tocar nada.

### Qué se expuso en `/Sim` y por qué

La pantalla no calcula nada del juego (RT-014); lo que le faltaba se añadió a `/Sim` como método puro:

| Qué | Dónde | Por qué |
|---|---|---|
| `RunEngine.EnterMatch` → `MatchEntry(State, Summary, Outcome)` | `Sim/Run/RunEngine.cs` | `Enter` es `(estado) => estado` y tira el resumen del partido por el camino. La pantalla de partido y el informe necesitan el `MatchReport` y **no pueden volver a simular** para conseguirlo. Es además el único camino por el que el resumen llega cuando el partido termina la run (RF-002b), que es justo cuando `AfterMatch` no se llega a llamar. `Enter` delega en él: mismo estado resultante, mismas semillas |
| `RunSummary.ActsCleared / MatchesPlayed / MatchesWon / NodesVisited / Fallen / Survivors / HopsToMarket` | `Sim/Run/RunSummary.cs` | el fin de run necesita actos superados y caídos, y el mapa la distancia al mercado (RF-011b). Recorrer el historial de nodos para contar jefes batidos es derivar estado de la run, no dibujar |
| `Scouting.Profile` → `TeamProfile` | `Sim/Perks/Scouting.cs` | la **build** del rival (RF-015) no es un campo, es el roster: nivel medio, etiquetas de estilo y rasgos que se repiten. Cuentas enteras y ordenadas (RT-041), junto a `Scouting.LethalPerks`, que ya existía |

### Capturas

`godot --path Game --rendering-driver opengl3 --audio-driver Dummy -- --tour` bajo Xvfb (receta de
`docs/entorno.md`) juega solo desde el inicio hasta el ojeo, pasa por Equipo y deja `inicio.png`, `mapa.png`, `ojeo.png` y
`equipo-run.png` en `Game/screenshots/`. Es también una prueba de humo: si el recorrido llega hasta el final, empezar
una run, generar los mapas, elegir nodo, **construir el partido** y enseñar la plantilla de la run en la
pantalla de Equipo funcionan de verdad. Las capturas de la pantalla
de Equipo se siguen regenerando con `--screenshots`, como dice `ui-equipo.md` §13.

### Huecos conocidos

- **Mando**: solo la pantalla de Equipo tiene los dos flujos completos de UI-006. Las nuevas van con ratón
  y la línea de ayuda del pie lo dice en vez de prometerlo.
- **Club**: `data/clubs/` no existe, así que el inicio elige **raza** (RF-004: un club, una raza) y el oro
  de partida sale de `economy.startingGold` por división.
- **Árbitros**: neutros y con nombre generado hasta el paquete de árbitros (RF-061); el ojeo lo dice.
- **Alineación**: la pantalla de Equipo mueve jugadores por la cuadrícula y eso ya entra por
  `SetLineup`, pero no hay todavía una forma de **elegir el once** distinta de intercambiar en el campo, ni
  el indicador de riesgo por jugador dentro de la cuadrícula (RF-012c): el número está en el ojeo.

---

## Implementación de partido, informe, recompensa y mercado (paquete de partido)

Las cuatro pantallas que van del saque a la tienda. Se enchufan al `RunController` de arriba y no llaman
a `/Sim` ni calculan nada del juego (RT-014): cada una recibe una **vista** ya compuesta por un método
puro de `Sim.Run.View`, y devuelve decisiones (`RunDecision`) por `RunController.Apply`.

| Escena | Script | Qué resuelve |
|---|---|---|
| `Scenes/Partido.tscn` | `Screens/MatchScreen` | marcador, resultado y log con scroll (RF-121) |
| `Scenes/Informe.tscn` | `Screens/ReportScreen` | informe post-partido (RF-119) |
| `Scenes/Recompensa.tscn` | `Screens/RewardScreen` | elección de recompensa (RF-071, RF-071b, RF-072, ADR 0049) |
| `Scenes/Mercado.tscn` | `Screens/MarketScreen` | las cuatro categorías, compra y venta (RF-114..114f) |

Componentes compartidos nuevos: `Ui/OptionCard.cs`, la tira de 24 px de UI-011 aplicada a lo que no es un
jugador (artículo de mercado, opción de recompensa, perk del informe), con el mismo patrón de inspección
—activar expande, activar otra vez colapsa (UI-001)— y una sola expandida a la vez, que la impone la
pantalla. `Autoload/RunControllerMatch.cs` es la parte parcial del controlador donde viven las cuatro
consultas: `MatchLog()`, `PostMatch()`, `Reward()` y `Market()`, más `PlayMatch(nodeId)`.

### Decisiones de diseño

- **El partido ya está jugado, así que leer el log es opcional.** La pantalla de Partido no anima nada:
  revela el log a la velocidad que el jugador quiera (pausa, x1/x4/x16, mostrar todo) y tiene siempre a
  mano "Ir al informe". El marcador solo enseña lo ya revelado, para que leerlo tenga sentido.
- **Lo que no se puede perder de vista no vive en el scroll.** Goles, tarjetas, lesiones, muertes, turba y
  final salen además en la columna izquierda de 376 px —la de las fichas en Equipo—, que es la que el ojo
  ya sabe mirar.
- **El informe se lee de arriba abajo en el orden en que duele**: bajas primero, con las muertes las
  primeras y en rojo; después los perks; el oro al final. Un informe que empieza por el dinero enseña a
  mirar el dinero.
- **Cada perk del informe lleva su descripción generada al lado de lo que cayó en sus activaciones.** Es
  lo que convierte la lista en una explicación: el informe es donde se aprende qué hace un perk.
- **La advertencia de RF-072 va pegada a la lista de portadores**, en el momento de asignar, no en un
  tutorial: es una decisión irreversible que se toma con un clic.
- **El objeto enseña su arquetipo antes de comprarse** (RF-012d): el maldito dice que baja algo, el frágil
  su probabilidad de rotura y el exclusivo de qué raza es.
- **El canterano se enseña abierto**, sin tener que pulsarlo (RF-114b): es la red de seguridad de una run
  mala y solo la coge quien pasa por el nodo.

### Qué se expuso en `/Sim` y por qué

| Qué | Dónde | Por qué |
|---|---|---|
| `MatchPlaybacks.Of` → `MatchPlayback(Node, Setup, Result, Seed)` | `Sim/Run/View/MatchPlayback.cs` | `MatchEntry` trae el `MatchReport` (agregados) pero **no la secuencia de eventos**, y el log de RF-121, las bajas con su minuto y su causa, y la contribución de cada perk se componen de eventos. Reproducir el partido desde su semilla antes de resolverlo es determinista (RF-120, RT-061): el que se enseña y el que se juega son el mismo |
| `MatchLogView.Build` → `MatchLogLine[]` | `Sim/Run/View/MatchLogView.cs` | `MatchReport.Log` es el volcado interno del motor, en inglés y con el detalle crudo: sirve para depurar, no para leerlo. La vista devuelve **dato estructurado** (minuto, tipo, bando, nombres, marcador, si un perk lo anuló) y la frase en español la compone la pantalla desde `UiText` y el vocabulario ya localizado de `data/l10n` (RT-073), igual que `LineupWarning` |
| `PostMatchView.Build` → `PostMatchReport` | `Sim/Run/View/PostMatchView.cs` | RF-119 entero: perks activados con activaciones y contribución, objetos, bajas, tarjetas, árbitro y desglose del oro |
| `GoldCalculator.Breakdown` → `GoldForWinBreakdown` | `Sim/Run/Systems/Economy/GoldCalculator.cs` | el informe tiene que decir **por qué** se cobró esa cantidad. `GoldForWin` pasa a ser el total del desglose, no una fórmula paralela: así no pueden divergir |
| `MarketSystem.SalePrice` | `Sim/Run/Systems/Market/MarketSystem.cs` | la pantalla enseña el precio de venta antes de vender y tiene que ser el que se cobra, no una copia |
| `DescriptionGenerator.DescribeEffects` | `Sim/Perks/DescriptionGenerator.cs` | los **consumibles** son efectos sin perk que los envuelva; sin esto el mercado tendría que escribir su texto a mano, que es lo que RT-035 prohíbe |
| `RewardView.Build` → `RewardScreenView` | `Sim/Run/View/RewardView.cs` | opciones con descripción generada, portadores elegibles con sus slots libres, coste del reroll y motivo estructurado de por qué una opción no se puede cobrar |
| `MarketView.Build` → `MarketScreenView` | `Sim/Run/View/MarketView.cs` | las cuatro categorías con precio y asequibilidad, canteranos y mercenarios marcados, arquetipo y rotura de cada objeto, y la lista de venta con su precio |
| `PlayerDescriptions` | `Sim/Run/View/RewardView.cs` | un jugador ofrecido tampoco lleva texto a mano: su línea sale de sus atributos con el vocabulario de `data/l10n` |

**Cómo se mide la contribución de un perk** (RF-119): cada activación queda registrada con su tick
(RT-043), y la contribución es lo que le pasó al partido **en esos mismos ticks** —goles del equipo,
lesiones causadas al rival, recuperaciones, paradas y eventos que un perk anuló—. No es una atribución
causal, y por eso se enseña como "en sus activaciones", que es exactamente lo que el dato dice.

### Capturas

```bash
dotnet build Game/Underleague.Game.csproj
godot --headless --path Game --import
xvfb-run -a --server-args="-screen 0 1280x800x24" godot --path Game \
  --rendering-driver opengl3 --audio-driver Dummy res://Scenes/Capturas.tscn
```

`Screens/CaptureRunner` (escena `Scenes/Capturas.tscn`) juega una run de verdad con semilla fija, instancia
las cuatro escenas y deja `partido.png`, `informe.png`, `recompensa.png` y `mercado.png` en
`Game/screenshots/`. Los estados que necesitan un clic —una opción de recompensa elegida, un objeto del
mercado abierto— se alcanzan **empujando eventos de ratón sintéticos** por el mismo camino que la entrada
real, no llamando a los métodos por dentro, y de paso cobra la recompensa y compra el objeto e imprime el
antes y el después: si la decisión no llegara a `/Sim`, se vería ahí.

### Huecos conocidos

- **Faltas no señaladas**: RF-119 pide el apartado del árbitro "con la evolución del criterio y las faltas
  no señaladas a cada equipo". El criterio inicial y el final están; las **faltas no señaladas no se
  registran** en el motor (no hay evento para una acción sucia que el árbitro deja pasar), así que el
  informe lo dice como hueco en vez de dejarlo en blanco.
- **Mando**: como el resto de pantallas nuevas, ratón. Partido acepta A (mostrar todo) y B (ir al informe).
- **Nombres de equipo**: se enseñan los ids (`underleague_fc`, `act1_elf_swiftwing`) porque `data/clubs/`
  no existe todavía y los rivales de `data/rivals/` no llevan nombre localizado.
- **Surtido repetido**: el mercado y las recompensas pueden ofrecer dos veces el mismo artículo, porque el
  sorteo de `MarketOfferGenerator` y `RewardSystem` es con reemplazo. Se ve en la captura del mercado; es
  de `/Sim`, no de la pantalla.
- **Consumibles**: se compran y se registran en el inventario, pero equipar y usar sigue siendo el hueco
  que dejó el paquete X (`fase2-diseno.md` §13); la pantalla de Partido los narra si se activan.
- **Ganar el partido y perder la run a la vez**: si una baja baja del mínimo (RF-002b), `AfterMatch` no
  llega a pagar, así que el informe **no** enseña desglose de oro aunque el marcador diga victoria. Es una
  comprobación explícita, no un efecto secundario.
