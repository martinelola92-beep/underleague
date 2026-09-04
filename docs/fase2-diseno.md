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

**La métrica principal de la fase es la curva de puertas de la ADR 0033**: cada nivel de calidad de build (incoherente, correcta, buena, muy buena) contra cada jefe, con la tabla de exigencia que allí se fija. Es el criterio que define si el juego pide construir bien, y va a la puerta de fase 2. El resto son métricas de apoyo:

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
| **Y. Jefe y cierre de run** | fast-worker | W | `Sim/Run/Boss.cs`, `data/bosses/*` calibrados **contra la tabla de la ADR 0033**, condiciones de victoria y derrota, modo de depuración (RT-062) |
| **Z. Runs en `/Balance` y ajuste** | deep-reasoner | W, X, Y | `--full-runs`, política automática, métricas, ajuste de economía, puerta de fase 2 |

## 13. Decisiones de implementación del paquete W

Lo que el paquete W (`Sim/Run/{RunState,Map/*,Save/*}`, `RunEngine`, `RunSystems`, `RunLineup`, `RunStateBuilder`) resolvió y por qué. Las decisiones de regla de juego que no estaban cerradas en `requisitos.md` van marcadas.

**W-1. Los "10-12 nodos por acto" de RF-001 son los nodos que el jugador *recorre*, no los dibujados.** *(lectura aplicada, RF-001)* Es la única que hace cuadrar el resto de números: §4 fija "11 nodos, de los cuales 6 partidos como máximo", que por tres actos son 18 partidos, exactamente la métrica "18-22 partidos por run" de §10; RF-003b habla de fatiga, que depende de lo jugado; y RF-003 pide runs de 75-100 minutos, inalcanzables con los ~10 partidos que da la lectura contraria. Medido: una run completa recorre **33 nodos y juega 18 partidos**. El precio es la palabra "contiene" de RF-001: el acto *dibuja* entre 18 y 21 nodos. Pendiente de anotar en `pendientes.md`.

**W-2. Esqueleto de mapa fijo, con mercados en las capas 2, 5 y 8.** Un acto son `PathLength` capas (10-12) y el jugador atraviesa una por capa. Las capas de mercado tienen **un solo nodo** y las libres, 2 o 3. La garantía de RF-011b no admite otra cosa: en un grafo por capas, lo alcanzable en dos saltos desde la capa `i` son las capas `i+1` e `i+2`; si una capa mezclara mercado con otro nodo, ese otro nodo necesitaría un mercado en `i+1` o `i+2` y el siguiente está a 3-4 por el propio RF-011b. Luego una capa de mercado es **entera** de mercado. Y la separación tiene que ser **exactamente 3**: con 4, un nodo de la capa `m+1` se queda a tres saltos. Con esto la garantía sale **por construcción y sin reintentos**, y el test la comprueba sobre 1.000 mapas.

**W-3. El tope del 60% de RF-003b se cumple sobre el peor camino, no en promedio.** Cada capa libre es entera de partidos o entera de servicios (clínica, entrenamiento, evento). Así, juegue lo que juegue el jugador, no puede pasar del tope. `MapInvariants.WorstCaseMatches` es la cifra que se compara.

**W-4. Aristas sin cruces por construcción.** Entre dos capas de `a` y `b` nodos se sortean `a-1` cortes ordenados en `[0, b-1]` y la fuente `i` se conecta al intervalo `[corte_i, corte_{i+1}]`. Los intervalos cubren todos los destinos, ninguna fuente se queda sin salida y el destino máximo de una fuente es el mínimo de la siguiente, que es la condición de no cruce. El mapa será dibujable sin cruces cuando llegue la interfaz.

**W-5. Ids de nodo estables: `acto * 100 + índice`.** La semilla del partido es `RngStreams.MatchSeed(runSeed, node.Id)`, así que no puede depender del camino recorrido: entrar en un nodo produce el mismo partido sea el tercero o el quinto de la run. Es lo que hace que RT-061 ("salir a mitad de partido reproduce el partido desde la semilla") no necesite guardar nada del partido en curso.

**W-6. `IRunSystems` es el hueco de los paquetes X e Y.** `RunEngine` resuelve mapa, partidos, alineación, progresión y derrota; todo lo demás entra por esa interfaz, que es un parámetro **opcional** de `Enter`/`Apply`/`Start` para no separarse de la firma de §3. `DefaultRunSystems` genera rivales procedurales con `TeamGenerator` (calidad `45 + 8·(acto-1)`, +7 élite, +12 jefe) y reparte los árbitros; `OpenNode` y `AfterMatch` son **no-ops documentados** (no lanzan: el paquete W tiene que poder jugar una run entera para probar las dos derrotas) y `ApplyDecision` lanza `NotSupportedException` nombrando al paquete que toca.

**W-7. La derrota "durante el partido" se detecta recorriendo la secuencia de eventos.** `Simulator.Run` no se puede interrumpir, así que se aplica el partido evento a evento llevando la cuenta de disponibles; en cuanto baja de 5 se registra el **tick** de esa baja en `RunOutcome.Tick` y **los eventos posteriores no se aplican a la plantilla**. Es la lectura literal de "termina la run al instante" (RF-002b) y le da al render el punto por el que cortar la reproducción.

**W-8. `RunEngine.Outcome` recomprueba el mínimo de plantilla siempre.** No basta con mirar el desenlace grabado: si el paquete X vende un jugador o se le marcha un mercenario, la run tiene que terminar igual que con una lesión (RF-002b, "en cualquier momento"). `RunState.AvailablePlayerCount` es una propiedad consultable en todo momento (RF-002e).

**W-9. Portero de emergencia.** *(decisión fuera de la especificación)* `Simulator.Run` exige exactamente un portero alineado y el club inicial trae uno solo (RF-005), al que una lesión grave aparta hasta la clínica (RF-092). Como RF-002b dice que la run solo termina de dos formas, quedarse sin portero no puede ser una tercera: el jugador de campo disponible de menor id se pone de portero **solo para ese partido**, en el `PlayerDefinition` que recibe el simulador, sin tocar el `RunPlayer` de la plantilla. Al revés igual: un segundo portero titular juega de defensa.

**W-10. Lesión leve: -15% por lesión acumulada, y se gasta al jugar.** RF-091 dice "-15% a todos los atributos durante el siguiente partido, acumulable": dos leves en el mismo partido son -30% en el siguiente. Al terminar un partido, los **titulares** salen con el contador a cero (ya han pagado) y los suplentes conservan el suyo. Se respeta la inmunidad `MinorInjuryPenalty` de la ADR 0026 (no-muertos, RF-035). La constante vive en `RunRules`, no en `/data`, porque es una regla del documento de requisitos; moverla a datos exigiría un ADR (RT-057).

**W-11. `Counters` y `Achievements` de run son `string -> int` ordenados.** Existen para que un sistema nuevo de los paquetes X o Y (coste actual del reroll, oro gastado por sumidero, derrotas seguidas de un mercenario) no obligue a subir la versión del esquema del guardado. Lo mismo a nivel de jugador con `Counters` y `BondProgress`.

**W-12. El contenido de un nodo abierto no se serializa.** El estado guarda `PendingNodeId` y `NodeRerolls`; el surtido del mercado y las tres recompensas se **derivan** de `RngStreams.Rewards(seed, nodeId)` y del número de rerolls. Con esto el guardado ironman (RT-061, "al completar cada nodo") es consistente: salir a mitad de un mercado y volver reproduce el mismo surtido, sin exploit de recarga y sin guardar estructuras que el paquete X todavía no ha diseñado.

**W-13. El guardado se escribe a mano con `Utf8JsonWriter`, no con `JsonSerializer`.** Sin reflexión, con el orden de claves y de diccionarios bajo control (`SortedDictionary` ordinal), de modo que dos estados equivalentes producen el **mismo texto**. La semilla va como cadena de dígitos porque es un `ulong` y no cabe en un número JSON leído como `double`. Los valores de enum van en `camelCase`, como en `modelo-datos.md`. El formato está en `data/schemas/run-save.schema.json` y un test comprueba que el esquema y el escritor no se separen (mismas claves arriba, en cada jugador y en cada nodo).

**W-14. `schemaVersion` 1 y rechazo explícito.** La versión 0 de `modelo-datos.md` era el borrador sin código; la primera con código es la 1. Cargar otra versión lanza `RunSaveException` con un mensaje que dice que nunca se migra en silencio. `NodeKind.Workshop` está en el enum desde la versión 1 aunque el generador de fase 2 no lo produzca, para que añadir el taller en la fase 3 no obligue a migrar runs.

**W-15. El equipo del jugador siempre es local.** *(decisión fuera de la especificación)* Simplifica el rasgo de árbitro "casero" (RF-061) y no cambia nada medible mientras los árbitros sean neutros. Si la fase 3 quiere alternar campo, es un campo más en el nodo.

**W-16. Árbitros neutros de momento.** `RunState.Referees` guarda los 6-8 árbitros de la run (RF-061b) con su rasgo y sus sobornos recibidos, pero `DefaultRunSystems` los crea todos `Neutral` con criterio 0: los rasgos y los sobornos son de fase 3 y el balance de la fase 1 se midió con árbitro neutro. El paquete Y puede rellenarlos desde `data/referees/` sin tocar la forma del estado.

**W-17. Modo de depuración (RT-062) por dos vías.** `RunStateBuilder` salta a una situación arbitraria (`AtAct(2)`, `BeforeBoss()`, `WithAvailablePlayers(5)`, `WithRoster(...)`) partiendo de un `Start` real, así que el estado resultante siempre es consistente; y `RunSave.Load` admite un estado **sin instantánea de `/data`**, que es lo que necesita el `--state fichero.json` de `arquitectura.md`.

**W-18. Los ids de jugador no se reutilizan.** `RunState.NextPlayerId` solo sube, también tras una venta o una muerte: un id reutilizado rompería el historial y los vínculos (`determinismo.md`, "Orden").

### Interfaces que quedan abiertas

| Hueco | Firma | Paquete |
|---|---|---|
| Surtido y decisiones de mercado, clínica, entrenamiento y evento | `IRunSystems.OpenNode(state, node, catalog)` + `ApplyDecision` | X |
| Oro por partido y recompensas de RF-071 | `IRunSystems.AfterMatch(state, node, summary, catalog)` | X |
| Rivales estáticos de `data/rivals/` | `IRunSystems.OpponentFor` y `MapOptions.OpponentIds` (el generador los reparte entre los nodos de partido) | X |
| Árbitros con rasgo | `IRunSystems.CreateReferees` / `RefereeFor` | X/Y |
| Modificadores de regla del jefe | `IRunSystems.BossRuleModifiers` y `ActMap.WithBossModifier` | Y |
| Decisiones nuevas | añadir un `record` a la jerarquía `RunDecision` y resolverlo en `ApplyDecision` | X/Y |
| Estado nuevo sin subir de versión | `RunState.WithCounter` / `WithAchievement`, `RunPlayer.WithCounters` / `WithBondProgress` | X/Y |

### Lo que el paquete W deja pendiente

- **Sin clínica ni mercado, una run larga se queda sin plantilla.** Con los sistemas por defecto, 18 partidos dejan de media 1-3 lesionados graves sin tratar. Es exactamente el agujero que tapa el paquete X (RF-094, RF-114b); no es un fallo del bucle.
- **Tensión entre el mercado como cuello de botella (W-2) y RF-002d**, que habla de "desviarse hacia un mercado" como si fuera un desvío opcional. Con la garantía dura de RF-011b el mercado no se puede esquivar; la decisión de RF-002d sigue existiendo, pero es "gastar o no gastar", no "pasar o no pasar". Si se quiere recuperar el desvío, hay que relajar RF-011b (por ejemplo a tres saltos) con un ADR.
- Vínculos (RF-101..104), prótesis (RF-095), taller, sobornos y rasgos de árbitro: los campos existen en el estado y en el esquema, pero nadie los rellena todavía (fase 3).
- Consumibles equipados: el estado los guarda y los valida (máximo 3, mínimo 1 manual), pero `MatchSetup` todavía no los recibe, así que no surten efecto en el partido.
