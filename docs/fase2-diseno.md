# Fase 2: bucle de run completo (sin gráficos)

Especificación del bucle de partida. Criterio de salida del plan de fases: *"el jugador dice 'una run más' sin arte terminado"*. Como no hay interfaz todavía, ese criterio se sustituye en esta fase por su equivalente medible: **una run completa se puede jugar de principio a fin desde código, es reproducible, y sus decisiones tienen consecuencias medibles en `/Balance`**.

Convenciones vigentes: enteros, orden determinista, `/Sim` sin E/S ni Godot, identificadores en inglés (`glosario-identificadores.md`), valores en datos.

## 1. Alcance

**Dentro**: mapa por actos, tipos de nodo, rivales estáticos, recompensas y reroll, mercado con canteranos, economía, lesiones y clínica, equipamiento, mercenarios, un jefe con modificador de regla, estado de run versionado, guardado ironman con instantánea de `/data`, modo de depuración, y simulación de runs completas en `/Balance`.

**Fuera**: `/Game` y toda la interfaz (requiere Godot instalado en Windows, pendiente del revisor); taller de prótesis, vínculos, turba, rasgos de árbitro y sobornos (fase 3); divisiones y logros (fase 4).

## 2. Estructura

`Sim/Run/` dentro del proyecto `/Sim` (decisión D-13: no se crea proyecto aparte hasta que haga falta). Sin E/S: la persistencia serializa a string y quien escribe en disco es el llamador.

```
Sim/Run/
  RunState.cs        estado versionado (RT-030), inmutable con métodos With*
  Map/MapGenerator.cs, MapNode.cs, NodeKind.cs
  Economy.cs         oro, precios, objetivos de partido excelente
  Market.cs          surtido, canteranos, ventas, mercenarios
  Rewards.cs         tres opciones y reroll
  Medical.cs         lesiones, clínica
  Equipment.cs       objetos y slots
  Boss.cs            modificadores de regla
  RunEngine.cs       superficie pública: avanzar de nodo, resolver, aplicar decisiones
  Save/RunSave.cs    serialización versionada + instantánea de /data
```

## 3. Superficie pública

```csharp
public static class RunEngine
{
    public static RunState Start(RunSetup setup, ulong seed, Catalog catalog);
    public static IReadOnlyList<MapNode> AvailableNodes(RunState state);
    public static RunState Enter(RunState state, int nodeId, Catalog catalog);      // resuelve el nodo no interactivo o abre el interactivo
    public static RunState Apply(RunState state, RunDecision decision, Catalog catalog);  // compra, reroll, alineación, tratamiento...
    public static RunOutcome Outcome(RunState state);                               // EnCurso | Victoria | Derrota(causa)
}
```

Puro y determinista, igual que `Simulator.Run`. Los partidos se resuelven llamando a `Simulator.Run` con el flujo `RngStreams.Match(seed, nodeIndex)`; mapa y recompensas usan sus propios flujos (RT-022).

## 4. Mapa (RF-010..015)

- Grafo por capas dirigido, sin retroceso, 10-12 nodos por acto y 3 actos (D-2/D-10, valores de partida: 11 nodos, de los cuales **6 partidos como máximo**, RF-003b).
- Tipos: `LeagueMatch`, `EliteMatch`, `Market`, `Clinic`, `Training`, `Event`, `Boss`. (`Workshop` en fase 3.)
- **Un mercado cada 3-4 nodos y alcanzable en dos saltos desde cualquier punto** (RF-011b): el generador lo garantiza por construcción y hay un test que lo comprueba sobre 1.000 mapas.
- Rivales **estáticos por acto**, diseñados a mano en `data/rivals/` (RF-015); lo aleatorio es el mapa y qué rival cae en qué nodo.
- Distintivo de dificultad de 5 niveles (RF-012) e informe de ojeo completo y gratuito (RF-012b), que es un dato derivado del `TeamSetup` del rival, no un texto.
- El nodo de jefe es visible desde el principio; su modificador permanece oculto hasta llegar (RF-014) y queda registrado en el compendio del perfil una vez descubierto (RF-014b).

## 5. Estado, persistencia y derrota

- `RunState` según RT-030 y `modelo-datos.md`, con `schemaVersion`.
- **Ironman** (RT-061): un slot, se guarda al completar cada nodo, se borra al cargar. Salir a mitad de partido reproduce el partido desde la semilla.
- **Instantánea de `/data`** al empezar la run (RT-061b): la run se juega con su copia, no con el `/data` actual.
- Derrota por **dos vías únicamente** (RF-002b): perder un partido de jefe, o bajar de 5 jugadores disponibles en cualquier momento, incluido durante un partido. El contador de disponibles frente al mínimo es parte del estado y debe ser consultable en todo momento (RF-002e).
- Se puede jugar en inferioridad con 5 o 6 (RF-002d).

## 6. Economía (RF-114g..k)

Oro por partido ganado, fijo por acto y multiplicado por dificultad; élite y jefe pagan más; perder no paga. **El oro nunca escala con el rendimiento dentro del partido** (RF-114i). Bonus de *partido excelente* por objetivos anunciados antes de jugar (RF-114h). Sumideros: mercado, clínica, rerolls, salarios de mercenarios. `/Balance` verifica que el oro medio por acto permite usar **dos o tres** sumideros, nunca todos (RF-114k).

## 7. Mercado (RF-114..114f), canteranos y mercenarios

- Única tienda del juego, cuatro categorías simultáneas con 3-4 artículos: jugadores, perks, equipamiento y consumibles. Surtido generado al llegar, sin renovación.
- Los perks comprados se asignan al instante a un jugador con slot libre (RF-114e). **El pool respeta `race`** (ADR 0023): los exclusivos de otra raza no aparecen.
- 1-2 **canteranos gratuitos** por mercado, comunes de la raza del club, atributos muy bajos, +33% de experiencia (RF-114b/c). Son jugadores completos (RF-114d).
- Venta de jugadores con precio por rareza, nivel, perks y vínculos (RF-114f).
- **Mercenarios** (RF-110..113): otra raza, estadísticas superiores, salario por partido, no forman vínculos, cuentan como `Stranger`, y abandonan tras 3 partidos sin jugar o 3 derrotas seguidas. Con la ADR 0024, su etiqueta de estilo se sortea con la distribución de **su** raza.

## 8. Lesiones, clínica y equipamiento

- Estados físicos y su persistencia entre partidos (RF-090..092, RF-097). Clínica: coste alto, resultado garantizado (RF-094).
- **Muerte solo por las dos vías de RF-093**: alinear a alguien con lesión grave sin tratar, o perk rival letal telegrafiado. Un jugador sano nunca muere.
- Equipamiento: un objeto por jugador (RF-076), transferible fuera de partido, vendible (RF-076b), con los tres arquetipos obligatorios (RF-077). 12 objetos en esta fase.

## 9. Recompensas (RF-071, RF-071b)

Tras cada partido ganado, 3 opciones entre perk, jugador y objeto; si es perk, el jugador elige portador. Un reroll por nodo, coste creciente dentro de la run.

## 10. Simulación de runs en `/Balance`

Modo nuevo `--full-runs N`: juega runs completas con una **política automática** (una IA de jugador sencilla y explícita: prioridad de compra, criterio de alineación, cuándo tratar a un lesionado) y vuelca `runs.csv` con: acto alcanzado, causa de derrota, oro ganado y gastado por sumidero, muertes, lesiones, tamaño final de plantilla, nivel medio, y si pasó por mercado. Métricas nuevas:

| Métrica | Rango objetivo de partida |
|---|---|
| Tasa de victoria de la run con política razonable | 25-40% |
| Runs perdidas por bajar de 5 jugadores | < 35% de las derrotas |
| Oro medio por acto frente al coste de los sumideros | permite 2-3, nunca todos (RF-114k) |
| Duración de la run en partidos | 18-22 |
| Muertes por run | 0,5-2 |

La política automática no pretende jugar bien: pretende ser **reproducible y explicable**, para que un cambio en la economía se lea en la métrica y no en el criterio del que mide.

## 11. Decisiones pendientes que esta fase cierra

D-2 y D-10 (nodos por acto y distribución), D-3 (salario de mercenarios frente a coste de tienda), D-6 (reintento del jefe final: **nueva run**, coherente con ironman), D-7 (de la tienda de Rune Dice solo se replica la estructura por categorías), D-9 (condición de derrota propia del jefe final). Cada una se cierra con un valor en datos y una línea en `pendientes.md`.

## 12. Paquetes

| Paquete | Agente | Depende de | Contenido |
|---|---|---|---|
| **W. Estado, mapa y persistencia** | deep-reasoner | — | `Sim/Run/{RunState,Map/*,Save/*}`, esquema versionado, tests de mapa (mercado alcanzable, sin retroceso, reproducible) |
| **X. Economía, mercado y plantilla** | fast-worker | W | `Sim/Run/{Economy,Market,Rewards,Medical,Equipment}`, `data/economy/*`, `data/items/*` (12 objetos), `data/rivals/*` |
| **Y. Jefe y cierre de run** | fast-worker | W | `Sim/Run/Boss.cs`, `data/bosses/*`, condiciones de victoria y derrota, modo de depuración (RT-062) |
| **Z. Runs en `/Balance` y ajuste** | deep-reasoner | W, X, Y | `--full-runs`, política automática, métricas, ajuste de economía, puerta de fase 2 |
