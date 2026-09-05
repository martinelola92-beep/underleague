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
| Tasa de victoria de la run con política razonable | **20-30%** (corregido por la ADR 0040; era 25-40) |
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

## 14. Decisiones de implementación del paquete X

Lo que el paquete X (`Sim/Run/Systems/**`, `data/economy`, `data/items`, `data/rivals`, `data/consumables`) resolvió y por qué. Implementa `IRunSystems` (clase `StandardRunSystems`) sin tocar ningún fichero de W, `Sim/Engine` ni `Sim/Perks`.

**X-1. Ni `Sim/Data` ni `Catalog` se tocan: el paquete X carga sus cuatro ficheros por su cuenta.** `EconomyLoader`, `ItemLoader`, `ConsumableLoader` y `RivalLoader` (en `Sim/Run/Systems/`) parsean `data/economy/economy.json`, `data/items/*.json`, `data/consumables/*.json` y `data/rivals/*.json` directamente desde la instantánea de ficheros, con el mismo patrón de cursor JSON que `DataLoader` (fichero + ruta para `DataException`, sin E/S: reciben el contenido ya leído). `Catalog` no gana ningún campo nuevo. La razón es doble: la lista de ficheros del encargo no incluye `Sim/Data`, y dos agentes tocan `Sim/Data`-adyacente en paralelo (razas/l10n, jefes/balance); no compartir el fichero evita cualquier conflicto de fusión. `StandardRunSystems.FromJson(files)` es el punto de entrada único para tests y para `/Balance`.

**X-2. Los efectos de objetos y consumibles reutilizan literalmente `Sim.Perks.EffectDefinition`.** RF-078 pide "el mismo formato de efectos que los perks"; en vez de inventar un tipo paralelo, `EffectJson.Read` (compartido) construye instancias reales de `EffectDefinition` (mismo enum `EffectType`, `AttributeKind`, `ProbabilityKind` de `Sim.Perks`), recortadas a `modifyAttribute` y `modifyProbability` -sin disparador, condición, alcance ni duración variable: un objeto está siempre activo mientras está equipado, y ni `PlayerDefinition` ni `Simulator.Run` reciben todavía el objeto de un jugador (mismo agujero que W dejó anotado para los consumibles equipados). Wire-arlo al partido exige tocar `Sim/Engine` y `Sim/Model.PlayerDefinition`, fuera de las fronteras del paquete X; queda para un paquete futuro.

**X-3. Las descripciones de objetos y consumibles se generan en código, no con `data/l10n`.** RT-035 exige que no exista un campo `description` escrito a mano, y el generador de perks (`Sim.Perks.DescriptionGenerator`) cumple esa regla leyendo plantillas de `data/l10n/<lang>/templates.json` -territorio del agente que trabaja en paralelo en razas y l10n. `Sim.Run.Systems.Items.ItemDescriptions` genera la frase desde el mismo dato que describe (efectos, arquetipo), con plantillas es/en como constantes de C# en vez de en `data/l10n`. Cumple la letra de RT-035 (nada de texto de efecto escrito a mano por objeto) sin abrir ese fichero compartido.

**X-4. Vender un objeto reutiliza `TransferItem` con `ToPlayerId < 0`.** RF-076b exige poder vender equipamiento, pero la jerarquía cerrada de `RunDecision` que dejó W en `RunSetup.cs` (fichero raíz, fuera de mis fronteras) no tiene un `SellItem`. En vez de eso, `EquipmentSystem.Apply` interpreta `ToPlayerId < 0` como "vender a la fracción de mercado" en lugar de "mover a ese jugador", y exige un nodo de mercado abierto (a diferencia de la transferencia normal, permitida en cualquier momento fuera de partido, RF-075).

**X-5. Sin hueco de objeto "sin asignar": asignar uno a un jugador que ya lleva otro vende el desplazado.** `RunState` no tiene un almacén de objetos sueltos (RF-076: un objeto por jugador, sin más), así que comprar en el mercado, elegir una recompensa de objeto o transferir a un jugador ocupado liquida automáticamente el objeto anterior a la fracción de venta (`EquipmentSystem.AssignPurchasedItem`), en vez de destruirlo gratis o rechazar la operación.

**X-6. El nodo de recompensa es el propio nodo de partido ganado, y "elegido" se marca con un contador de run.** `AfterMatch` deja pendiente `node.Id` tras una victoria (como prevé el contrato de W); `RewardSystem` usa `RunState.Counters["rewardClaimed:<nodeId>"]` (mecanismo genérico de W-11) para que `ChooseReward` y `RerollRewards` se puedan rechazar si la recompensa ya se ha elegido, sin subir la versión del esquema del guardado.

**X-7. El surtido del mercado y el de las recompensas comparten derivación, con índices sintéticos separados.** Ambos usan `RngStreams.Rewards(seed, nodeId * 10.000 + rerollCount)` (`OfferStream`). El mercado no se renueva (RF-114): siempre deriva con `rerollCount = 0`. Las recompensas usan `state.NodeRerolls` (0 o 1, un reroll por nodo, RF-071b). El objetivo de "partido excelente" (X-8) usa `RngStreams.Rewards(seed, nodeId)` sin el `* 10.000`, así que nunca coincide con ningún índice de surtido.

**X-8. El objetivo de "partido excelente" se deriva antes de jugar y se recomprueba después, con el mismo dato.** `ExcellentMatchObjectives.For(seed, node)` es una función pura de (semilla, nodo): el mismo valor sirve para anunciarlo (RF-114h, RF-012d) y para comprobarlo en `AfterMatch`. Los cuatro objetivos (ganar por 3+, portería a cero, ganar en inferioridad -menos de 7 titulares, RF-002d-, gol de un canterano) se leen de datos ya conocidos antes del resultado fino del partido; el bono es una cantidad fija (`economy.excellentMatchBonusGold`), nunca proporcional al margen (RF-114i, comprobado en `EconomyTests`).

**X-9. `data/consumables/` se añade fuera de la lista literal de ficheros del encargo, con un límite declarado.** RF-114 exige "cuatro categorías simultáneas: jugadores, perks, equipamiento y consumibles" y el propio encargo del paquete X (fase2-diseno.md §7) las repite; sin un catálogo mínimo el mercado se queda en tres. Se añaden 4 consumibles (uno por familia de RF-084) con el mismo formato de efecto recortado que los objetos. Límite declarado: comprarlos incrementa `RunState.Counters["consumable_owned:<id>"]`, pero `SetConsumables` (paquete W, `RunEngine.Apply`) no comprueba esa propiedad al equipar -no hay inventario de consumibles en el esquema del estado-, así que hoy comprar y equipar sin comprar tienen el mismo efecto. Es el mismo límite que W dejó anotado para los consumibles equipados (§13), no uno nuevo.

**X-10. Un objeto frágil se rompe por partidos jugados o por lesión, con la señal que ya da W.** El contador `RunPlayer.Counters["item_uses"]` cuenta partidos jugados con el objeto puesto (`EquipmentSystem.ProcessFragileItems`, llamado desde `AfterMatch` para los titulares de `summary.PlayedPlayerIds`). La señal de "el portador se ha lesionado" (RF-077) es que un titular termine el partido con `PhysicalState != Healthy`: por el reseteo de W-10 (los titulares con lesión leve arrastrada salen del partido a cero antes de aplicar las lesiones nuevas), esa condición solo es cierta si la lesión es de este partido.

**X-11. La racha de derrotas de los mercenarios es del equipo, no de cada jugador.** RF-111 dice "si el equipo encadena 3 derrotas", así que vive en `RunState.Counters["mercenaryLossStreak"]` (no en `RunPlayer`) y se reinicia a 0 en cuanto provoca un abandono masivo, para que la siguiente derrota no expulse en cadena a un mercenario recién fichado.

**X-12. RF-093 caso 1 (alinear con lesión grave sin tratar) es inalcanzable con el motor actual, y caso 2 (perk rival letal) está bloqueado aguas arriba.** `RunEngine.ApplyLineup` (paquete W, fuera de mis fronteras) rechaza cualquier alineación con un jugador no disponible, e `IsAvailable` excluye `SevereInjury`: RF-092 se cumple de forma literal como bloqueo duro, no como advertencia, así que el primer camino de RF-093 no tiene forma de dispararse hoy. El segundo depende de un perk con `lethal: true`, y `Sim/Perks/PerkLoader.cs` lo rechaza explícitamente ("en fase 1 no hay muertes"), fichero fuera de mis fronteras. La regla "un jugador sano nunca muere" se cumple por tanto de forma trivial (nadie muere) en vez de por diseño activo del paquete X; corresponde anotarlo en `docs/pendientes.md` y abrirlo con un ADR cuando se levante la restricción de fase 1 de `PerkLoader`.

**X-13. El nodo de jefe nunca usa un rival de `data/rivals/`.** Aunque `MapGenerator` reparte los ids de `MapOptions.OpponentIds` entre todos los nodos de partido -jefe incluido, sin distinguir-, `StandardRunSystems.OpponentFor` ignora `node.OpponentId` cuando `node.Kind == Boss` y delega en el procedural de `DefaultRunSystems` (que ya sube la calidad con `BossQualityBonus`). El paquete Y calibra `data/bosses/` contra la ADR 0033 y sustituirá esta rama sin tocar el resto de `StandardRunSystems`.

**X-14. Entrenamiento y evento: el tratamiento más conservador que cumple lo escrito y nada más.** Ninguno de los dos tiene mecánica propia en `requisitos.md` ni en `fase2-diseno.md`. Entrenamiento da una experiencia fija a la plantilla disponible (mismo mecanismo de `Progression` que un partido, sin azar: entrenar no es una apuesta). Evento da oro dentro de una banda, sorteado con `RngStreams.Rewards(seed, node.Id)` (RF-114j: "determinados eventos"). Los dos se resuelven solos, sin decisión nueva.

**X-15. Los rivales y los jugadores generados usan id -1 o rangos altos, nunca los de la plantilla.** Los jugadores generados para el mercado y las recompensas llevan `Id = -1`: `RunState.WithNewPlayer` les asigna `NextPlayerId` solo si de verdad se compran o se eligen (W-18, ids nunca reutilizados). Los rivales de `data/rivals/` se instancian con `RivalTeamBuilder.OpponentFirstPlayerId = 2.000.000` (separado del 1.000.000 de `DefaultRunSystems` aunque nunca convivan) porque no se guardan en `RunState`: existen solo mientras dura una llamada a `Simulator.Run`.

## 15. Decisiones de implementación del paquete Y

Lo que el paquete Y (`Sim/Run/Boss/`, `data/bosses/`, los cuatro escalones de `data/balance/builds/`, `Sim/Analysis/BossGateMetrics.cs`, `Sim.Tests/Analysis/BossGateTests.cs` y el modo `--boss-gate` de `/Balance`) resolvió y por qué. Las decisiones de regla de juego que no estaban cerradas van marcadas.

### Los tres jefes

| Acto | Jefe | Plantilla | Modificador(es) | Qué invalida |
|---|---|---|---|---|
| 1 | `grimhold_guns` — **Los Cañones de Grimhold** | Enanos, calidad 43, nivel 5, bloque bajo con disparo | `butterfingers` — *Manos de mantequilla*: los perks que suben la **parada** no se aplican | Fiarlo todo al portero |
| 2 | `the_hunt` — **La Cacería** | Orcos, calidad 59, nivel 6, violencia y `pack_mentality` | `death_mark` — *Marca de muerte*: el titular **de campo** con más perks los pierde todos | Canalizarlo todo por un jugador |
| 3 | `eternal_crown` — **La Corona Eterna** | No-muertos, calidad 40, nivel 8, **íntegramente legendaria** (RF-001c), 27 perks | `sealed_goal` — *Portería sellada*: los perks que suben el **remate a puerta** no se aplican · `iron_curtain` — *Cerrojo*: ningún titular empieza más allá de la columna 5 | Depender del rematador y del tercio atacante |

**Y-1. Un modificador es una transformación del `MatchSetup`, no un bono.** *(lectura aplicada, RF-001b)* Los cuatro tipos (`singleCopy`, `markStar`, `banChannel`, `pushBack`) quitan perks o mueven casillas-hogar del once del jugador antes de simular. Es lo único que puede hacerse sin tocar `/Sim/Engine` (RT-011: el motor no sabe que existe un jefe) y a la vez lo que hace la regla **anticipable**: el informe de ojeo construye el mismo `MatchSetup` transformado y enseña el once con el que se va a jugar de verdad (RF-012b, RF-012d). Cada uno invalida un **eje de construcción** distinto —el portero, el jugador estrella, un canal de probabilidad, la colocación adelantada—, que es lo que el espíritu de Balatro que cita la ADR 0033 pide: la build que lo apuesta todo a un eje no pasa, la que tiene plan B sí.

**Y-2. `data/bosses/<id>.json` lleva escrita la fila de la ADR 0033 que tiene que cumplir** (`gate.targets`) y el nivel al que llega la plantilla del jugador a esa puerta (`gate.playerLevel`: 5, 6 y 7, de los 100 de experiencia por partido de `sim/tuning.json` y ~6 partidos por acto). El jefe se diseña **contra** la tabla: el dial de calibración es `template.quality` y la tabla vive en el dato, no en el código de la métrica.

**Y-3. D-9 resuelta: la condición de derrota propia del jefe final es «el campeón conserva el título».** *(decisión de regla de juego)* Llegar **empatado al final del tiempo reglamentario** contra el jefe final es derrota; el gol de oro de la turba (RF-055b) ya no salva la run. Se elige porque (a) es una regla, no una cifra, y se anuncia entera antes de entrar; (b) obliga a **ganar**, no a no perder, que es lo único que distingue al jefe final de un rival más y lo que impide que una build puramente defensiva termine la run aguantando; (c) se mide sin tocar el motor (`MatchReport.WentToGoldenGoal` es exactamente «se llegó empatado al final»). No es una tercera vía de derrota del estado: RF-002b sigue teniendo dos, y la causa registrada es `BossMatchLost`, porque lo que ha pasado es que el partido del jefe no se ha superado. Medido: anula el **18,4%** de las victorias de una build muy buena y el **7,8%** de las de una correcta — pesa más cuanto mejor juegas, que es lo propio de una regla que castiga conformarse.

**Y-4. Cuatro escalones por raza, con la **misma** plantilla y distinta construcción.** Los veinte ficheros nuevos de `data/balance/builds/` (`<raza>_{incoherent,correct,good,excellent}`, las cinco razas de lanzamiento) comparten calidad 50, rareza común y etiqueta de estilo impuesta; lo único que cambia entre escalones es qué perks, dónde colocados y con qué escalado acumulado. Es lo que exige el punto 3 de la ADR 0033: la puerta mide **construcción**, no rareza. En particular `*_excellent` no lleva **ningún legendario** (dos o tres raros), así que la celda (jefe final, muy buena) **es** la salvaguarda de la ADR 0027 y se comprueba como tal.

**Y-5. El escalón «muy buena» se apoya en `counters`, un campo nuevo de la build.** Un perk de acumulación con el contador a cero no vale nada, así que sin contadores «buena» y «muy buena» son la misma build. `data/schemas/balance-builds.schema.json` gana `counters` (slot -> contador -> valor) y `BuildConfig` lo aplica con `PlayerDefinition.WithCounters`, que es de donde `EffectEngine` los siembra. «Buena» entra con 2 (lo acumulado dentro del acto) y «muy buena» con 5, el tope.

**Y-6. `ActMap.BossModifierId` guarda el id del **jefe**, no el de un modificador.** El campo es uno solo y el jefe final tiene dos (RF-001c); el catálogo resuelve el id del jefe a su lista de modificadores, que es lo que devuelve `IRunSystems.BossRuleModifiers` y lo que el compendio registra (RF-014b). `BossRunSystems.AssignBosses(state)` lo sella justo después de `RunEngine.Start`. El **compendio del perfil** de RF-014b (lo que persiste entre runs) no existe todavía —la fase 2 no tiene persistencia de perfil—: lo que hay es el dato con el que rellenarlo, `BossDefinition.ModifierIds` más el `BossModifierRevealed` del mapa, y `BossCatalog.FindModifier` para resolver un id descubierto a su nombre localizado.

**Y-7. `BossRunSystems` es un envoltorio de `IRunSystems`, no una implementación nueva.** Envuelve al del paquete X (o al `DefaultRunSystems` de W) y solo intercepta tres cosas: `OpponentFor` en un nodo de jefe (plantilla del jefe del acto en vez del rival procedural, lo que sustituye la rama que X dejó anotada en X-13), `BossRuleModifiers` y `AfterMatch` (condición de derrota propia). Los dos paquetes se componen sin tocarse.

**Y-8. Hueco: `RunEngine` no aplica los modificadores, y no revela el del jefe si pierdes.** *(**cerrado en §16.1**; lo que sigue es el estado en el que el paquete Y lo dejó)* *(defecto del paquete W, anotado, no corregido aquí)* `RunEngine.BuildMatch` construye el `MatchSetup` sin pasar por `IRunSystems`, así que no hay dónde enganchar `BossRules.Apply`: mientras no se añada esa línea a `EnterMatch`, el partido de jefe que juega el bucle de run va **sin** su modificador. `BossRunSystems.BuildBossMatch` es el punto de entrada correcto y es lo que usan el informe de ojeo y la puerta. Y `EnterMatch` marca `WithBossModifierRevealed(true)` **después** de comprobar el desenlace, así que perder contra el jefe deja el modificador sin registrar — justo el caso en el que el jugador ya ha pagado la sorpresa, contra RF-014b. Las dos son de una línea en `Sim/Run/RunEngine.cs`, fuera de las fronteras de este paquete.

### La curva medida *(sustituida por la de §16.6: la de aquí se midió sin equipamiento, con los perks de un solo objetivo muertos y con los acumuladores realimentándose dentro del partido)*

`Sim.Tests/Analysis/BossGateTests.cs`, categoría `Gate`. Semilla **1**, **32 plantillas × 4 partidos** por celda y raza (local/visitante × reparto de ids alternados), cinco razas: **640 partidos por celda** de la tabla, 7.680 en total, **36 s**. Reproducible con `dotnet run --project Balance -c Release -- --boss-gate --rosters 32 --runs 4 --seed 1`.

| Puerta | Incoherente | Correcta | Buena | Muy buena |
|---|---|---|---|---|
| **Acto 1** `grimhold_guns` | **5,5** (< 25) | **46,6** (45-60) | **64,1** (60-75) | **81,9** (70-85) |
| **Acto 2** `the_hunt` | **2,3** (< 15) | **37,0** (30-45) | **61,4** (55-70) | **78,3** (65-80) |
| **Acto final** `eternal_crown` | **1,9** (< 10) | **19,1** (15-30) | **40,3** (35-50) | **58,9** (55-70) |

Las doce celdas caen dentro de su banda. El desglose por raza va como `INFO` en la métrica: la dispersión entre razas es de **±13 puntos** (la misma que la §8.4 de `fase1b-resultados.md` documenta y que la ADR 0029 dejó abierta como D-29), así que la puerta se afirma sobre el **agregado de las cinco razas**, que es lo que la tabla de la ADR pide — la exigencia es de la puerta, no del club.

**Y-9. La puerta admite 2,5 puntos de tolerancia sobre la banda, y hay que decir por qué.** El error de una celda con esta muestra **no** es el binomial (±1,2): lo domina la varianza de generación de plantillas, y la misma configuración se mueve **3-4 puntos** al cambiar de semilla (medido con semillas 1, 2 y 3). Subir la muestra hasta que ese error baje de 2 puntos cuesta más de tres minutos, muy por encima de lo que puede costar una puerta. La tolerancia es el error de medida declarado, no una rebaja del criterio: los valores exactos están en la tabla de arriba y ninguna celda la necesita salvo `grimhold_guns/correcta`, que queda a 1,6 puntos del suelo.

### Lo que no se cumple, y por qué

**Y-10. La fila del acto 1 es la única que no cabe con holgura, y es un problema del catálogo de perks.** La ADR pide, en el acto 1, que una build correcta ronde el 52% y una muy buena el 77%: **25 puntos** de escalera entre el suelo de «correcta» y el techo de «muy buena». La escalera que produce el catálogo actual en ese punto de la curva es de **35 puntos** (46,6 -> 81,9), así que el corredor de calibración es de unos **2 puntos de ancho**: `template.quality` solo puede tomar un valor. Las otras dos filas piden 25+10 y 20+20 puntos de escalón y la escalera medida da 24+17 y 21+19, que encajan sin forzar. No se ha forzado la fila del acto 1 estrechando artificialmente los escalones: se deja documentado que el catálogo de perks produce saltos más grandes de lo que la tabla del acto 1 admite.

**Y-11. Solo hay un perk de escalado de verdad en todo el catálogo, y eso deforma «muy buena».** *(**cerrado en §16.5**; y la afirmación sobre `deathless_march` no era correcta)* De los seis perks con `accumulatesAcrossMatches`, cinco (`clean_sheet_legacy`, `poacher_instinct`, `silky_veteran`, `scar_tissue`, `deathless_march`) **se realimentan dentro del mismo partido** —suben su contador con cada parada, gol o regate—, así que llegan al tope solos y el contador con el que entran apenas importa. El único cuyo contador solo sube **entre** partidos es `battle_reader` (+1 por partido, tope 5). Consecuencia: la única forma de expresar «escalado acumulado durante toda la run» con el catálogo de hoy es **apilar `battle_reader` en varios titulares**, y así está construida `*_excellent` (cuatro copias al tope). Es honesto —es lo que un jugador optimizando haría— pero es un síntoma: el catálogo no tiene curva de progresión propia. Mientras siga así, un modificador `singleCopy` en el acto 1 es inviable (probado: hunde «muy buena» a 5 puntos de «buena» y la fila deja de tener solución), y por eso el jefe del acto 1 usa `butterfingers`.

**Y-12. Falta el equipamiento, y con él la mitad de lo que la ADR llama «muy buena».** *(**cerrado en §16.2**; la recalibración anunciada aquí es la de §16.6)* El escalón superior de la ADR es «buena + **equipada** + escalado acumulado». Los objetos existen desde el paquete X (`data/items/`), pero X-2 deja anotado que ni `PlayerDefinition` ni `Simulator.Run` reciben todavía el objeto de un jugador: **un objeto equipado no surte efecto en el partido**. La celda «muy buena» está medida por tanto **sin equipamiento**, con perks, colocación, contadores y dos o tres raros. Cuando el objeto llegue al motor habrá que **volver a calibrar los tres jefes**, porque el escalón superior subirá y las tres filas se desplazan a la vez.

**Y-13. Hallazgos de medida que condicionan cualquier ajuste futuro.** Aislados quitando un perk cada vez y volviendo a medir (640 partidos por comparación):
- Los perks de alcance **`team`** y los del **portero** son casi todo lo que se mide. `center_conductor` (+5 de intercepción al equipo) vale **10 puntos** de tasa de victoria; los perks del portero, unos 30 entre los dos.
- *(diagnosticado y corregido en §16.4; y el caso de `natural_leader` era un artefacto de la medición)* Los perks de un solo objetivo sobre un jugador de campo son **invisibles**: quitar `covering_shadow`, `high_press_trigger`, `pivot_duo` o `natural_leader` de una build no cambió **ni un partido** de 4.800. Cuadra con el orden de la §8.5 de `fase1b-resultados.md`, pero es más extremo de lo que allí se dice y conviene tenerlo presente antes de escribir el catálogo de lanzamiento.
- `markStar` tuvo que **excluir al portero**: con él dentro, el desempate por suma de atributos lo elegía casi siempre y el modificador pasaba a ser «el jefe te anula el portero», que vale 30 puntos y no mide nada de la construcción.

**Y-14. La medición de `/Balance` y la de la puerta son el mismo código.** `BossGateMetrics.PlayCell` (en `/Sim`, pura, sin E/S) juega la celda y `BossGateMetrics.Compute` la contrasta con `gate.targets`; `/Balance --boss-gate` y `Sim.Tests` solo se diferencian en quién lee los ficheros. Las plantillas de jefe de una misma raza son las mismas en los cuatro escalones (la escalera se compara contra el mismo rival) y distintas entre razas (si no, la varianza de esas plantillas no se promedia al agregar y la celda se mueve 10 puntos al cambiar de semilla).

## 16. Costuras entre paquetes: lo que ninguno podía cerrar solo

Los paquetes W, X, Y y el de equipamiento se construyeron en paralelo con fronteras estrictas. Cada uno dejó anotado lo que veía y no podía tocar; esta sección cierra esas costuras y recalibra lo que quedó descolocado. Cierra **Y-8**, **Y-11**, **Y-12** y las dos primeras viñetas de **Y-13**, y aplica la ADR 0035.

### 16.1. El bucle de run ya juega al jefe con su modificador (cierra Y-8)

**Z-1. `IRunSystems` gana `TransformMatch`, y `RunEngine.BuildMatch` pasa por ella.** El hueco de Y-8 era que `BuildMatch` armaba el `MatchSetup` sin consultar a los sistemas, así que `BossRules.Apply` no tenía dónde engancharse y **el jefe que jugaba el bucle no era el jefe que medía la puerta**. La firma nueva es `MatchSetup TransformMatch(state, node, setup, playerTeamIndex, catalog)`; `DefaultRunSystems` y `StandardRunSystems` devuelven el `setup` tal cual y `BossRunSystems` aplica los modificadores del jefe del acto sobre el equipo del jugador. Se ha implementado explícitamente en las cuatro implementaciones en vez de con un método de interfaz por defecto: un envoltorio que no reenvía a `_inner` es justo el error que este hueco ya produjo una vez.

La consecuencia que importa: **el informe de ojeo y el partido son ahora la misma llamada.** `BossRunSystems.BuildBossMatch` se queda como nombre del ojeo, pero su cuerpo es literalmente `RunEngine.BuildMatch(state, nodeId, catalog, this)`. RF-012d ("nada de lo que pase estaba sin anunciar") deja de depender de que dos caminos de código coincidan. El test `TheBossScoutingReportShowsTheMatchThatWillBePlayed` comprobaba antes una tautología (comparaba `BuildBossMatch` contra `BossRules.Apply(BuildMatch)`, y `BuildMatch` no transformaba nada); ahora contrasta el partido con regla contra el partido **sin** regla, construido con un `IRunSystems` que solo aporta el rival.

**Z-2. El modificador se revela por haber jugado el nodo, no por haberlo sobrevivido.** En `RunEngine.EnterMatch`, `WithBossModifierRevealed(true)` se movió **antes** de la comprobación del desenlace. Perder contra el jefe es exactamente el caso en el que el jugador ya ha pagado la sorpresa; dejarla sin registrar en el compendio (RF-014b) era cobrarla dos veces. El test lo afirma ahora sin la disyunción que Y-8 había tenido que dejar.

### 16.2. `/Balance` equipa (cierra Y-12)

**Z-3. Campo `items` en la build, resuelto con la misma conversión que la run.** `data/balance/builds/*.json` gana `items` (slot → id de `data/items/`), con su entrada en `data/schemas/balance-builds.schema.json`. Lo aplican los dos lectores de builds —`Balance/BuildConfig.cs` y `Sim.Tests/Analysis/BuildFile.cs`— con `players[i] with { Item = RunEquipment.ToMatchItem(definition) }`, es decir con **la misma** conversión que usa el bucle de run: la puerta mide el objeto que el jugador equipa, no una copia paralela. Un id que no está en el catálogo es un error explícito, nunca un objeto que calla.

**Z-4. Las cinco `*_excellent` llevan objeto en los siete titulares**, sin legendarios (rompería la salvaguarda de la ADR 0027, que se mide sobre esta misma celda), sin frágiles (en un partido suelto un objeto frágil es gratis: lo que lo define es gastarse **entre** partidos) y sin restringidos (los cuatro escalones imponen estilo `Neutral`, así que un objeto restringido no aportaría nada). Kit: tres `berserker_totem` (+18 de fuerza) en los dos centrales y un centrocampista, y `focus_lens`, `veteran_armband`, `endurance_belt` y `worn_boots` en el resto.

**Z-5. La contrapartida de `berserker_totem` no era una contrapartida.** El objeto maldito subía `injure`, que es la probabilidad de que el portador **lesione al rival** (`MatchEngine`: la tirada suma `Injure` del que entra y `Injury` de la víctima). Es decir, su maldición era una segunda ventaja. Pasa a `injury` —que te lesionen a ti— y de 1.000 a 300 puntos base, que sobre una base de 40 ya multiplica por 8,5 el riesgo del portador. Es una corrección de dato, no un ajuste: el arquetipo "maldito" de RF-077 exige una contrapartida real.

**Z-6. Lo que vale equipar, medido.** Quitando `items` de las cinco `*_excellent` y volviendo a medir la curva completa (32 plantillas × 4 partidos × 5 razas = 640 partidos por celda): **acto 1 −0,0 · acto 2 −0,6 · acto 3 −6,4** puntos de tasa de superación. No se reproduce el **+8,2** con el que se planificó este encargo, y conviene decir por qué el 0,0 del acto 1 no significa "no sirve de nada": las celdas **por raza** se mueven entre −12 y +9 puntos (p. ej. `human_excellent` 73,4 → 61,7 al equipar y `dwarf_excellent` 67,2 → 76,6), y el agregado de las cinco cae por casualidad en el mismo valor. Es la misma varianza de generación de plantillas que la Y-9 declara, y es la razón de que la puerta se afirme sobre el agregado. Contra el jefe final, donde `sealed_goal` apaga los perks de remate y el once tiene menos con qué contar, el equipamiento sí es visible y vale 6,4 puntos.

### 16.3. La escala de valores por canal (ADR 0035)

**Z-7. El escalón vive en `tuning.probabilityChannels.<canal>.step` y lo comprueba el cargador.** `Tuning` gana `ProbabilityScale` (`Sim/Perks/ProbabilityScale.cs`), `DataLoader` la parsea exigiendo los **trece** canales de `ProbabilityKind` —ni uno de más ni uno de menos— y `PerkLoader.Parse` la recibe y rechaza cualquier `value`, `valuePerCounter` o `maxValue` de `modifyProbability` que no sea el escalón de su canal por 1, 2, 3, 5 o 10. La `PercentScale` única (`5/10/15/20/25/50`) desaparece. El **esquema JSON** no puede expresar "múltiplo del escalón declarado en otro fichero", así que su `enum` se sustituye por una cota de cordura (−100..100) y la comprobación real queda donde la ADR la pone, en el cargador; el `_doc` del bloque lo dice para que nadie lo lea como una relajación.

Escalones elegidos, con el criterio de la ADR (que un paso valga aproximadamente lo mismo en impacto **relativo** sobre la base del canal, hasta donde el punto porcentual entero lo permite): `intercept`, `injure`, `injury`, `foul`, `card` e `interceptEvasion` **1**; `tackle`, `tackleEvasion` y `severeInjury` **3**; `pass`, `dribble`, `save` y `shotOnTarget` **5**. Los cuatro que la ADR no nombraba se derivan del canal al que se oponen o con el que comparten base. La igualdad exacta es imposible con puntos porcentuales enteros —un paso vale +40% relativo en `intercept` y +6,5% en `pass`— y **eso es el resultado, no un fallo**: en los canales de base diminuta 1 pp es el paso más pequeño que el formato admite, y era el valor **mínimo legal de 5** lo que convertía cualquier perk de intercepción en un interruptor.

**Z-8. Qué cambió en el catálogo.** Los treinta y tantos valores de `modifyProbability` se reescribieron a múltiplos legales, bajando fuerte en los canales de base diminuta y prácticamente sin tocar los de base grande, que es exactamente lo que la ADR predecía. Lo más grande:

| Perk | Canal | Antes | Ahora | Por qué |
|---|---|---|---|---|
| `battle_reader` | `intercept` | +5/partido, tope +25 | +2/partido, tope +10 | +25 pp sobre base 250 es multiplicar por 11; el tope legal del canal son 10 pasos |
| `center_conductor` | `intercept` (equipo) | +5 | +3 | valía 10 puntos de tasa de victoria él solo (Y-13) |
| `covering_shadow` | `intercept` | +10 | +5 | y su `else` de −5 a −1 |
| `gentle_giant` | `intercept` | +10 | +3 | legendario, pero el canal no admite un interruptor |
| `shadow_marker` | `injure` | +10 | +2 | +10 pp sobre base 40 es multiplicar por 26 |
| `scar_tissue` | `injure` | +5/lesión, tope +25 | +1, tope +3 | ídem, acumulado |
| `elf_touch` | `tackleEvasion` | +5 | +3 | habilidad racial: un solo paso del canal |
| ocho perks de `tackle` | `tackle` | +20 / +25 | +15 | 20 y 25 no son múltiplos de 3; los `else` van a −30 |
| `natural_leader` | `tackle` (equipo) | +15 | +15 | 15 es legal con escalón 3, así que se queda: es de los pocos perks que separan "muy buena" de "buena" y vale 7,5 puntos contra el jefe final (Z-13) |

Los canales de base grande (`pass`, `dribble`, `save`, `shotOnTarget`) casi no se mueven: sus valores ya eran múltiplos de 5. Sube, como la ADR anticipaba, el **peso relativo** de esos canales frente a la intercepción.

**Z-9. Efecto sobre RT-056 y sobre las puertas de fase 1: ninguno medible.** El lote de referencia (`--runs 2000 --seed 1`) da exactamente los mismos valores que antes del cambio, con las once métricas en su rango o marcadas `INFO`, porque `data/balance/reference.json` no lleva perks: RT-056 mide el **motor**, no el catálogo. Las puertas de fase 1 que sí llevan perks —`BuildGateTests`, `RaceBalanceTests`, `RarityAndBossTests`— siguen verdes sin retocar ningún umbral. Era el riesgo grande del encargo y no se materializó; la razón es que el reajuste **baja** los valores de todas las builds a la vez, así que las comparaciones entre builds (que es lo que esas puertas miden) se mueven poco.

**Z-10. `data/items` y `data/consumables` quedan fuera de la escala por canal, y hay que decirlo.** `EffectJson` (paquete X) lee el `value` de un objeto o consumible **directamente en base 10.000**, no en puntos porcentuales, así que el escalón no le aplica tal cual y el cargador no lo comprueba. Con la tabla de arriba los dos únicos valores de probabilidad del catálogo de objetos son legales si se leen como múltiplos del escalón × 100 (`berserker_totem` 300 = 3 pasos de `injury`, `martyrs_relic` 600 = 2 pasos de `severeInjury`), pero eso es una coincidencia afortunada, no una garantía. Unificar la unidad exige tocar `ItemLoader`/`ConsumableLoader` y reescribir los valores; queda anotado en `pendientes.md`.

### 16.4. Perks de un solo objetivo invisibles (cierra la segunda viñeta de Y-13)

**Z-11. El diagnóstico: `target: "linked"` con un canal que no es `pass` no se aplicaba nunca.** No era alcance estrecho ni condición rara: era imposible por construcción. `EffectEngine` convierte todo efecto con objetivo `linked` en un modificador **por par** (ADR 0021) y `Modifiers.Probability` solo lo suma cuando el otro extremo del par es la contraparte de la resolución en curso. Pero un vínculo une a dos **compañeros**, y la única resolución del motor que enfrenta a dos compañeros es el pase de uno al otro: en `intercept` la contraparte es el pasador rival, en `tackle` el conductor rival, en `dribble` el defensor rival, en `shotOnTarget`/`save` el portero rival. El par (portador, compañero) no se formaba jamás.

**Corrección**: el tratamiento por par se restringe al canal `pass`, que es el ejemplo con el que la propia ADR 0021 lo introduce ("un bono de pase aplicable cuando el receptor es uno de los vinculados"). En cualquier otro canal, `target: "linked"` se lee como lo que dice: **el bono es del compañero vinculado**, un modificador normal sobre él, y el vínculo sigue siendo la condición que lo hace existir —que es lo que convierte la colocación en una decisión con coste—. Con eso `covering_shadow` y `pivot_duo` pasan a existir. El precio, y no es pequeño, es que `eternal_crown` llevaba **cuatro** copias de `pivot_duo` y `grimhold_guns` dos: los jefes se habían calibrado contra perks muertos y por eso el reajuste de 16.5 es tan grande.

**Z-12. `high_press_trigger` se disparaba en el lado equivocado del balón.** Trigger `RECOVERY` con condición "en campo rival" y efecto "+intercepción al equipo durante la jugada": justo después de **recuperar** el balón, el equipo lo tiene, así que un bono de intercepción no interceptaba nada. Cero cambio en 4.800 partidos. Pasa a dispararse en `PASS_FAILED`: cuando el equipo **pierde** un pase en campo rival, todos presionan durante esa jugada. Es gegenpressing, la condición se cumple a menudo y el bono cae ahora sobre una jugada en la que el equipo defiende.

**Z-13. `natural_leader` no estaba muerto: el hallazgo de Y-13 era un artefacto.** Medido aislándolo sobre el catálogo tal y como estaba al empezar este encargo (16 plantillas × 4 partidos × 5 razas = 320 partidos por celda), quitarlo de las builds cuesta **−1,9 / −2,5 / −7,5** puntos de tasa de superación contra los tres jefes. Lo que confunde una ablación de este tipo es `markStar` (jefe del acto 2): quitar un perk de un titular cambia **quién** es el titular con más perks, así que el modificador se lleva a otro jugador y el resultado se mueve por una razón que no es el perk. `pivot_duo` daba lo mismo: −3,5 contra `the_hunt` con la mecánica muerta. Conviene aislar perks contra un jefe **sin** `markStar` o contra rivales procedurales.

**Ninguno de los cuatro se retira del catálogo.** Los tres que estaban muertos se arreglaron; el cuarto nunca lo estuvo.

### 16.5. La acumulación acumula entre partidos (cierra Y-11)

**Z-14. Un límite de una activación por partido, y ya está.** De los seis perks con `accumulatesAcrossMatches`, cuatro (`clean_sheet_legacy`, `poacher_instinct`, `silky_veteran`, `scar_tissue`) se disparaban en `SAVE`, `GOAL`, `DRIBBLE_WON` e `INJURY` **sin límite**, así que dentro de un mismo partido subían su contador con cada parada, gol o regate y **volvían a aplicar el bono cada vez**, sumándolo al anterior. No solo no eran progresión de run: eran una bola de nieve dentro del partido (es lo que hacía que "los perks del portero valgan unos 30 puntos", Y-13). Con `"limit": { "per": "match", "times": 1 }` el perk se activa una vez por partido: aplica el bono que le corresponde por el contador **con el que entró** y sube el contador en uno. El contador crece como máximo +1 por partido, que es lo que RF-070 pide y lo que hace que cuidar a un jugador tenga sentido. Es un cambio de **datos**, con un mecanismo que ya existía; no hizo falta tocar el motor.

Los otros dos (`battle_reader`, `deathless_march`) se disparan en `MATCH_START` y ya acumulaban bien. La afirmación de Y-11 de que `deathless_march` se realimentaba dentro del partido no es correcta.

**Z-15. La build "muy buena" deja de apilar cuatro copias del mismo perk.** Era el síntoma que Y-11 describía, y era honesto solo mientras `battle_reader` fuese el único acumulador real. Ahora el escalado de las cinco `*_excellent` se reparte en **cuatro canales distintos**: parada (`clean_sheet_legacy`), intercepción (`battle_reader` ×2), regate (`silky_veteran` ×2) y remate (`poacher_instinct`), todos con el contador al tope. Que ese reparto sobreviva a los modificadores de los jefes es además lo que sostiene la fila superior de la curva: `butterfingers` apaga la parada y `sealed_goal` el remate, pero nunca los cuatro a la vez.

### 16.6. La curva recalibrada, con equipamiento

Los tres jefes se recalibran contra la tabla de la ADR 0033 con el dial que la Y-2 fija, `template.quality`, después de todos los cambios anteriores: **`grimhold_guns` 43 → 39 · `the_hunt` 59 → 45 · `eternal_crown` 40 → 24**. La caída es grande y tiene una causa concreta: los tres estaban calibrados contra un catálogo en el que `pivot_duo` y `high_press_trigger` no hacían nada (y los llevan cuatro, uno y tres veces respectivamente) y en el que `clean_sheet_legacy` le daba a su portero una bola de nieve. Al arreglar las tres cosas los jefes se volvieron mucho más fuertes de golpe.

Misma muestra que la Y-9: semilla **1**, 32 plantillas × 4 partidos por celda y raza, cinco razas, **640 partidos por celda** y 7.680 en total, 34 s. Reproducible con `dotnet run --project Balance -c Release -- --boss-gate --rosters 32 --runs 4 --seed 1`.

| Puerta | Incoherente | Correcta | Buena | Muy buena |
|---|---|---|---|---|
| **Acto 1** `grimhold_guns` | **7,8** (< 25) | **58,4** (45-60) | **68,1** (60-75) | **71,4** (70-85) |
| **Acto 2** `the_hunt` | **6,9** (< 15) | **37,2** (30-45) | **56,6** (55-70) | **73,1** (65-80) |
| **Acto final** `eternal_crown` | **6,2** (< 10) | **26,1** (15-30) | **45,3** (35-50) | **56,6** (55-70) |

Las doce celdas caen dentro de su banda **sin** usar la tolerancia de la Y-9, y la escalera es monótona en los tres jefes. La condición de derrota propia del jefe final sigue pesando lo que la Y-3 midió y sigue pesando más cuanto mejor juegas: anula el **18,6%** de las victorias de una build muy buena, el **16,7%** de una buena, el **12,2%** de una correcta y el **2,7%** de una incoherente.

**Z-16. Cuatro celdas quedan a menos de dos puntos de su borde**, y hay que decirlo igual que lo dijo la Y-9: `grimhold_guns/correcta` a 1,6 del techo, `grimhold_guns/muy buena` a 1,4 del suelo, `the_hunt/buena` a 1,6 del suelo y `eternal_crown/muy buena` a 1,6 del suelo. El error de medida de una celda agregada es de unos 4 puntos (dominado por la varianza de generación de plantillas, no por el binomial: ver Z-6, donde las celdas por raza se mueven ±10 y el agregado apenas), así que la puerta pasa por la tolerancia declarada de ±2,5 y no por margen real. La fila del acto 1 sigue siendo la más estrecha, por la razón que la Y-10 ya diagnosticó y que este encargo **agrava**: `butterfingers` apaga los dos perks de portero en los cuatro escalones, y uno de ellos (`clean_sheet_legacy` con el contador al tope) era el diferenciador principal entre "buena" y "muy buena". Contra ese jefe la escalera correcta → muy buena es de 13 puntos y la tabla pide 25. Se deja documentado en vez de forzarlo: el catálogo todavía no tiene con qué separar los dos escalones superiores cuando le apagan un canal entero.

### 16.7. Lo que queda para el ajuste de economía (paquete Z)

- **La curva se mide con partidos directos build-contra-jefe, no con runs completas.** Lo que este encargo deja demostrado es que la construcción llega; lo que falta por medir es que la **economía** permita llegar a ella: que una build correcta pueda convertirse en buena antes del primer jefe con el oro y los nodos disponibles (punto 2 de la ADR 0033). Si no, el problema es la economía, no la build.
- **Los contadores de la fila "muy buena" son un instrumento, no una medida.** Entran al tope (5) por declaración en el fichero de build. Con la costura 5 cerrada, ahora se puede comprobar de verdad: 18 partidos de run con `battle_reader` dan +1 por partido, pero `clean_sheet_legacy`, `poacher_instinct` y `silky_veteran` solo suben en los partidos en los que su portador hace la jugada. **Cuántos partidos hacen falta de verdad para llegar al tope es la primera medición del paquete Z.**
- **Cuánto equipamiento tiene un jugador a cada puerta.** La fila "muy buena" asume siete titulares equipados y tres objetos raros. Si la economía no lo permite en el acto 3, la fila hay que rebajarla o el oro hay que subirlo.
- **RF-070 pide al menos 15 perks que acumulen entre partidos y hay 6.** Ahora los seis acumulan de verdad, que era el problema urgente; el número sigue siendo el del catálogo de pruebas, no el de lanzamiento.
- **El agujero de los consumibles equipados** que anotaron W y X sigue abierto en la parte de inventario (X-9): comprar y equipar tienen el mismo efecto porque el estado no lleva inventario de consumibles.

## 17. Decisiones de implementación del paquete Z

Lo que el paquete Z (`Sim/Analysis/{RunPolicy,FullRunMetrics,PerkPlacement}`, `Balance --full-runs`,
`Sim.Tests/Analysis/FullRunGateTests`, `data/economy`, `data/map`, ocho perks de acumulación) resolvió
y por qué. Las medidas y las conclusiones están en **`docs/balance/fase2-resultados.md`**; aquí van las
decisiones.

**Z-17. `--full-runs N` juega N runs con las *tres* doctrinas de compra, no con una política.** La ADR
0037 llegó a mitad del encargo y cambia el criterio: la métrica no es cómo rinde una política, es cuál
gana. `PurchaseDoctrine` (gastadora, ahorradora, contextual) es lo **único** que cambia entre las tres;
nodo, alineación, clínica y elección de recompensa son idénticos, para que la diferencia de tasa de
victoria sea atribuible a la decisión de comprar y a nada más. Las tres juegan las **mismas semillas**.

**Z-18. La política vive en `/Sim`, no en `/Balance`.** Igual que `BossGateMetrics` (Y-14): jugar y
medir son de `/Sim` —puro, sin E/S— y `/Balance` solo lee ficheros y cronometra, de modo que la puerta
de `Sim.Tests` y el modo de `/Balance` sean literalmente el mismo código. Las siete reglas comunes están
en el comentario de clase de `RunPolicy` y en `fase2-resultados.md` §1.

**Z-19. `PerkPlacement`: la política *lee* el perk antes de dárselo a alguien.** Comprar un perk cuya
condición de colocación no se cumple en su portador es construir el escalón "incoherente" de la ADR 0033
a propósito: ocupa un slot y, si el perk castiga (`elseEffects`), resta. El motor compila y evalúa las
condiciones con el contexto del evento, que no existe fuera del partido, así que `PerkPlacement`
reconoce los cinco predicados sobre `owner` que dependen solo de plantilla y colocación —`hasTag`,
`startsIn`, `startsOn`, `linked`, `teammatesWithTag`— y **da por bueno todo lo demás**: solo rechaza
cuando está segura. Es una lectura, no una evaluación, y vive en `Sim/Analysis` por eso; cuando la
pantalla de plantilla quiera dar el mismo aviso al jugador (RF-012d) lo correcto será un evaluador
estático sobre el AST en `Sim/Perks`. Medido: con el filtro la contextual pasa del 4,2% al 10,0% de
victorias de run, y la celda del jefe del acto 2 sube de 26 a 46.

**Z-20. D-2 y D-10 cerradas: 11, 12 y 12 nodos recorridos**, en `data/map/map.json`. Son 35 nodos por
run (RF-003b pide 30-36) y, con el reparto por construcción de `MapGenerator`, **20 partidos** en el
peor camino: 6 en el acto 1 y 7 en los actos 2 y 3, incluido el jefe. El 57,1% de los nodos son de
partido, por debajo del 60% de RF-003b, y los 20 partidos caen en el centro de la banda "18-22" de §10,
que con los 11 nodos de partida quedaba justo en el borde. El acto crece con la run, que es lo que
RF-001 permite y lo que hace que el acto 3 se sienta más largo. El fichero es nuevo porque los nodos por
acto no son economía; `MapLoader` lo carga con el patrón de los demás cargadores del paquete X (X-1) y
`StandardRunSystems.Map` lo expone.

**Z-21. D-3 cerrada: el mercenario no cuesta fichaje, cuesta salario, y el salario compite con una
compra.** `mercenaryBaseWage` 16 más 12 por escalón de rareza: un mercenario raro cuesta 28 por partido
y 196 en un acto de siete, algo más que un objeto común (240 de base con dispersión) y bastante más que
un tratamiento de clínica en proporción. Es la relación que RF-114k necesita para que los cuatro
sumideros sean comparables: sin ella el salario era calderilla (10 por partido, 60 por acto) y "usar el
sumidero de salarios" no significaba nada. **Consecuencia medida y anotada**: con trece jugadores en
plantilla la política nunca ficha un mercenario, así que el sumidero está calibrado pero no se ejercita
(`fase2-resultados.md` §5).

**Z-22. D-6 cerrada: perder contra el jefe final exige una run nueva.** Es la lectura provisional que
`pendientes.md` ya aplicaba y no había ninguna razón para cambiarla: el guardado ironman se borra al
cargar (RT-061) y `RunEngine` graba el desenlace en el estado, así que un reintento exigiría conservar
un estado que la propia regla de guardado destruye. No hay valor en datos que cerrar: la decisión es que
**no existe** ninguna clave de reintento.

**Z-23. D-7 cerrada: de la tienda de Rune Dice se replica la estructura por categorías y nada más.** El
mercado ofrece las cuatro categorías de RF-114 simultáneamente (jugadores, perks, equipamiento,
consumibles) con 3-4 artículos cada una, no se renueva, y el surtido se deriva de la semilla del nodo
(W-12). Lo que **no** se replica: ni reroll del surtido (el reroll es de la recompensa, RF-071b), ni
cupones, ni descuentos, ni artículos que modifican la tienda. El valor en datos es el bloque `market`
de `data/economy/economy.json`, con `playerOffers` 3, `perkOffers` 4, `itemOffers` 4 y
`consumableOffers` 3: quince o dieciséis artículos, que es el tamaño de surtido que la ADR 0037 fija.

**Z-24. Los precios se dispersan dentro de la rareza (`market.priceSpreadPercent`).** Sin dispersión,
los diez u once artículos comunes de un surtido cuestan exactamente lo mismo y "qué fracción del surtido
puedo pagar" salta de 0 a 1 con el oro: la métrica de escasez de la ADR 0037 no tiene ningún valor
intermedio y no se puede calibrar. Con un 70% de dispersión la fracción asequible baja del 82% al 40% y
dentro de una misma rareza hay artículos que se pueden pagar y otros por los que hay que ahorrar, que es
literalmente el dilema que la ADR describe.

**Z-25. Los fichajes, los mercenarios y los jugadores de recompensa entran con el nivel del acto**
(`economy.recruitLevelByAct = [1, 4, 6]`). Un fichaje de pago de nivel 1 en el acto 3 es oro tirado —la
plantilla va por el nivel 7 y ningún criterio razonable lo alinea—, así que el mercado dejaba de ser un
sumidero justo cuando más oro hay. El canterano **no** pasa por aquí: entra siempre en el nivel 1, que es
lo que RF-114b/c describe. Y la experiencia se fija al umbral de su nivel: si no, el primer partido
recalcularía el nivel desde cero y el fichaje se quedaría clavado media run.

**Z-26. RF-070 cumplido: quince perks acumulan entre partidos.** Ocho nuevos —`steady_hands`,
`iron_lungs`, `pit_veteran`, `sharpshooter_drill`, `lane_reader`, `bruised_knuckles`, `captains_voice` y
`scar_veteran`— repartidos por canal (pase, resistencia, entrada, remate, intercepción, lesionar, entrada
de equipo, fuerza), por rareza (3 comunes, 4 raros, 1 legendario) y por tipo de RF-069 (5 `filler`, 3
`conditional`, que dejan la distribución en 64/32/4). Todos llevan `limit: {per: match, times: 1}`
—salvo los de `MATCH_START`, que ya se disparan una vez— para que el contador crezca como mucho +1 por
partido, que es la corrección de la costura 16.5. Los valores respetan la escala por canal de la ADR
0035 y la de puntos de atributo. Viven en `data/balance/builds/human_accum.json`, que no entra en ningún
grupo de `groups.json`, para que `EveryCatalogPerkIsAssignedInSomeBuild` los vea sin mover ninguna
puerta de fase 1.

**Z-27. Medido: cuántos partidos hacen falta para llegar al tope de un acumulador** (la primera pregunta
que §16.7 dejaba). Los contadores acumulados en la plantilla al llegar a cada jefe son **3,3** (acto 1,
tras 6 partidos), **12,1** (acto 2, tras 13) y **30,8** (acto final, tras 20). Con seis acumuladores
repartidos por el once, 30,8 es un contador medio de ~5 por perk: **el tope de la fila "muy buena" se
alcanza justo al final de la run**, que es exactamente lo que la ADR 0033 llama "escalado acumulado
durante toda la run". Los de `MATCH_START` llegan al tope en 4-5 partidos; los que dependen de una
jugada del portador (`clean_sheet_legacy`, `poacher_instinct`) tardan el doble o más.

**Z-28. `EquipmentImpactTests` sube su muestra de 8 a 24 plantillas.** Añadir ocho perks al catálogo
movió la medida de 5,4 a 4,7 puntos **sin tocar un solo objeto**: con 512 partidos por brazo la
diferencia entre brazos tiene una desviación de ~3 puntos y el umbral de 5 estaba dentro del ruido. Con
24 plantillas la desviación baja a ~1,8 y el test avisa de una regresión de verdad.

### Lo que el paquete Z deja abierto

Con número en `docs/balance/fase2-resultados.md` §4 y §7:

- **La build llega tarde**: al jefe del acto 1 el once lleva 4,3 perks contra los 14 de `*_correct`, y
  el hueco es aritmético (6 partidos, 3 mercados, plantilla inicial sin perks). La salida es que el club
  inicial traiga una build, y toca RF-023/RF-005: **exige un ADR**.
- **La banda de tasa de victoria de §10 (25-40%) no es compatible con la tabla de la ADR 0033**, cuyo
  producto máximo es 29,5%. La banda coherente es 20-30%.
- **El criterio de la ADR 0037 (la contextual por encima de las dos puras en 8 puntos) no es medible
  hasta que se aplique la ADR 0036**: el equipamiento no vale hoy casi nada, y es el sumidero que la
  propia ADR 0037 llama la palanca fina. Medido: contextual +5,0 sobre la gastadora, +0,8 sobre la
  ahorradora.
- **Las lesiones han desaparecido del bucle de run** (0,04 por partido, frente a 0,62 del lote de fase
  1): la fórmula de `tuning.injury` se mide contra el nivel 1 y la progresión sube la resistencia. Sin
  lesiones no hay muertes, no hay clínica y no hay desgaste. Es un defecto de fase 1 que solo se ve
  jugando runs completas y **no se ha tocado** porque `tuning.json` es global.
- **Dos de los cuatro sumideros son contenido muerto** (clínica y mercenarios), consecuencia del punto
  anterior y del tamaño de plantilla.

## 18. Decisiones de implementación del paquete AA: las cinco ADR de cierre

Este paquete aplica las ADR **0036** (el objeto sube atributos), **0038** (el precio y la frecuencia como
palancas), **0041** (las fórmulas se miden contra el rival), **0040** (la curva se mide con la build que
cabe en cada acto) y la parte de escala de la **0039** (tres rarezas generables, sin legendarios), y
vuelve a medir el criterio de la **0037**. Las mediciones están abajo, con su lote y su semilla.

### 18.1. La escala de rarezas (ADR 0039, solo el cambio de escala)

**AA-1. `Rarity` pasa a tener cuatro entradas y la generación solo produce tres.** `Common`, `Uncommon`,
`Rare` (2, 3 y 4 slots de perk) y `Legendary`, que **nada genera**: ni `TeamGenerator`, ni
`GeneratedPlayers` (fichajes, canteranos, mercenarios y recompensas), ni el pool de objetos. Existe en el
enum para que el escalón esté reservado y para que precios, presupuestos y slots ya tengan su entrada
cuando la fase 4 escriba los personajes. `RarityWeights` pierde su tercer campo y pasa a repartir entre
las tres generables con **los mismos pesos** que tenía.

**AA-2. La migración es un renombrado, no un reajuste.** Lo que antes se llamaba `rare` pasa a llamarse
`uncommon` y lo que se llamaba `legendary` pasa a llamarse `rare`, en el enum y en los 57 ficheros de
`/data` que declaran rareza. Los números no se mueven: `budgetByRarity` sigue siendo 250 / 275 / 300 para
las tres generables (con 325 reservado para el legendario), los slots siguen siendo 2 / 3 / 4, y los
precios y los pesos de sorteo son los mismos. **Por eso el cambio de escala no necesita remedición**: el
juego que se mide después es el mismo juego con otros nombres, más un escalón vacío arriba. La
salvaguarda de la ADR 0027 —"un equipo sin legendarios tiene que poder ganar al jefe final"— deja de ser
una tensión: ahora **ningún** equipo tiene legendarios.

### 18.2. El objeto es un paquete de atributos (ADR 0036)

**AA-3. `data/items/*.json` cambia de forma y el validador rechaza `effects`.** Un objeto declara
`attributeBonus` y nada más: las entradas positivas son lo que sube, la única negativa es la
contrapartida del maldito. `ItemLoader` rechaza explícitamente cualquier `effects` o `drawbackEffects`
—el formato anterior, que permitía a un objeto hacer exactamente lo mismo que un perk— y comprueba, de un
vistazo, que el número de atributos coincide con el que la rareza permite y que la magnitud es la de la
escala. `MatchItem` sigue recibiendo `EffectDefinition`s, pero ya no salen del dato: los construye
`RunEquipment.ToMatchItem` a partir de los bonos, uno por atributo no nulo, y manda los negativos a
`DrawbackEffects` para que el informe pueda medirlos por separado.

**AA-4. La escala vive en `data/equipment/equipment.json`, no en código.** Magnitud (+10 por atributo),
multiplicador del maldito (×2), cuántos atributos toca cada rareza (1 / 2 / 3 / 4) y cuántos el
restringido (3). En el mismo fichero va la **tabla de valor marginal por atributo** que la ADR 0038
necesita, en milésimas de punto de tasa de victoria por cada +20 repartidos entre los diez jugadores:
fuerza 111, técnica 75, velocidad 66, resistencia 30 y **correa 40**, que no estaba en la medida de
fase 1b y entra como valor provisional entre la resistencia y la velocidad. Hay que remedir la tabla
cuando cambie el motor, y este paquete lo ha cambiado: queda anotado.

**AA-5. El frágil se rompe por probabilidad y al terminar el partido.** `usesLimit` desaparece;
`breakChancePercent` (20-30% en el catálogo) se resuelve en `EquipmentSystem.ProcessFragileItems`, que
se llama desde `AfterMatch`, con el flujo de recompensas del nodo (RT-022) y **tirando el dado siempre**,
se rompa o no, para que el flujo de RNG no dependa del resultado. La rotura se anuncia dos veces: la
probabilidad está en la descripción generada desde antes de equiparlo (RT-035, RF-012d) y la rotura
queda en el contador de run `itemsBroken`, que es lo que el informe post-partido leerá cuando exista
interfaz.

**AA-6. El restringido es exclusivo de raza, no de etiqueta.** Declara `race` y no declara rareza; el
cargador le asigna la banda de raro —tres atributos con magnitud normal es exactamente lo que vale— y le
pone como `requiredTag` la etiqueta de especie. `ItemCatalog.OfferableTo(raza)` es lo que hace que **solo
aparezca en runs de esa raza**, y `MatchItem.AppliesTo` lo que hace que **solo funcione sobre ella**.

**AA-7. Catálogo: 19 universales y 3 por raza de lanzamiento.** Los universales cubren las tres rarezas y
los tres arquetipos obligatorios: cinco comunes normales (uno por atributo), dos comunes frágiles, cuatro
poco comunes normales, un poco común frágil, dos raros normales, un raro frágil y cuatro malditos
(`brutes_pauldron` +20 fuerza / −20 velocidad, `glass_cannon_spikes` +20 velocidad y técnica / −20
resistencia, `berserker_totem` +20 fuerza, velocidad y resistencia / −20 técnica, `martyrs_relic` +20
técnica, resistencia y correa / −20 fuerza). Cada maldito baja algo que **le importa a su portador
natural**, que es lo que lo convierte en una decisión de colocación: el tótem es una ganga en un central
y tira la build a la basura en el organizador.

De los tres restringidos de cada raza, **uno abre una build que esa raza no puede permitirse**, y está
dicho en el `_doc` del propio fichero: `deep_road_boots` (velocidad, correa y técnica) le da al enano el
bloque adelantado que sus sesgos −14 y −18 le prohíben; `moonsteel_bracer` (fuerza, resistencia y correa)
le da al elfo el bloque bajo que su −12 de fuerza le niega; `shaman_beads` (técnica, correa y velocidad)
le da al orco la build de pase que su −10 de técnica le niega; `swiftrot_tendons` (velocidad, técnica y
correa) le da al no-muerto el contraataque que su −10 de velocidad le niega; y `heralds_pennant` (correa,
velocidad y resistencia) le da al humano —que no tiene sesgo y por eso no puede especializarse— el fútbol
de zonas amplias.

**AA-8. Lo que vale equipar, medido.** Siete titulares con un juego realista de acto 3 (dos raros, tres
poco comunes y dos comunes, el maldito en un central) valen **+3,3 puntos** de tasa de victoria sobre un
espejo sin equipar (`EquipmentImpactTests`, 24 plantillas × 64 partidos por brazo). La aritmética de la
ADR 0036 predecía 5,8: **la tabla de valor marginal sobrestima por un factor de ~1,6 cuando el bono va
entero a un jugador** en vez de repartido entre los diez, y conviene decirlo porque el precio de los
objetos se calcula con ella. Lo que confirma la magnitud de partida (+10) no es ese número sino la curva
de puertas: con ella, la fila "muy buena" cae dentro de su banda en los tres jefes (§18.5). Se probó
subirla a +14 y **empeora**: el precio se deriva del valor, así que los objetos buenos se salen del
presupuesto y la doctrina que ahorra gana a la que compra (medido: contextual 15,5 contra ahorradora
21,0). Se deja en 10.

### 18.3. El precio se calcula y la frecuencia se mide (ADR 0038)

**AA-9. `ItemPricing`: precio = precioBase(rareza) × valor / valorMedio(rareza).** El valor sale de la
tabla marginal, así que un común de +10 de fuerza cuesta 3,7 veces lo que un común de +10 de resistencia,
y el precio de venta (RF-076b) sale de la misma cuenta. La contrapartida del maldito entra con signo
negativo y por eso un maldito que baja algo caro **cuesta menos**: es la misma aritmética, sin regla
aparte. El frágil paga `fragilePricePercent` (55%) y pesa el doble en los sorteos
(`fragileOfferWeightPercent` 200), que es la compensación que la ADR 0036 exige para que no sea
estrictamente peor.

**AA-10. Los perks se miden, y la medición es un modo de `/Balance`.** `--perk-values` enfrenta, perk a
perk, una plantilla que lo lleva contra su espejo que no lo lleva —misma raza, calidad 50, nivel 4, ida y
vuelta— y devuelve lo que sube sobre el 50%. El portador **rota** entre los titulares que pueden llevarlo
(mismo filtro de `PerkAssignment.Eligible` que usa el juego): medir siempre sobre el primero elegible
ponía los 53 perks en el portero, que es exactamente donde ninguno significa nada. La tabla resultante
—45 perks medidos con 384 partidos cada uno, semilla 5— vive en `data/economy/perk-values.json`, **en
datos y no en código**, y de ella sale el peso de cada perk en el pool de recompensas (RF-071) y en el
surtido del mercado:

```
peso(perk) = clamp(pesoBase × valorReferencia / max(valor + desplazamiento, suelo), 25, 250)
```

El desplazamiento existe porque el valor medido es una diferencia sobre un espejo y **sale negativo en
media tabla**; sin él, "inversamente proporcional" no está definido. Con los valores medidos
(−125 a +198 milésimas) los pesos van de **71** (`steady_hands`, el más caro) a **133**
(`spearpoint` y `bulwark_stance`, los que menos aportan): una relación de 1,9 entre el perk que menos
sale y el que más. La desviación por fila es de unos 2,5 puntos, así que **la tabla ordena, no
dictamina**, y por eso el peso está acotado por los dos lados.

### 18.4. Las fórmulas se miden contra el rival (ADR 0041)

**AA-11. Cinco fórmulas dejan de comparar contra el 50.** No solo la de lesión: la ADR pedía revisarlas
todas y eran cinco.

| Fórmula | Antes | Ahora |
|---|---|---|
| **Lesión** | `40 + falta + 5·(fuerza−50) − 5·(resistencia−50)` | `onTackleBase + falta + relativeFactor·(fuerza del que entra − resistencia de la víctima)` |
| **Entrada** | `2800 + 12·(fuerza−50) + 8·(velocidad−50) − 14·(técnica−50)` | `2800 + pressureFactor·(presión del que entra − técnica del conductor)`, con `presión = (60·fuerza + 40·velocidad)/100` |
| **Falta** | `320 + 5·(fuerza del que entra − 50)` | `320 + 5·(fuerza del que entra − fuerza del conductor)` |
| **Entrada dura** | `fuerza ≥ 65` | `fuerza − fuerza del conductor ≥ 15` |
| **Regate** | `7200 + 18·(técnica−50) − 9·(velocidad−50) − 9·(fuerza−50)` | `7200 + 18·(técnica del conductor − cobertura del defensor)`, con `cobertura = (50·velocidad + 50·fuerza)/100` |
| **Parada** | `50 + (relevante−50)·20/50 − (calidad−50)·60/100` | `54 + (relevante − técnica del rematador)·8/50 − (calidad − qualityPivot)·60/100` |
| **Intercepción** | `250 + 14·(técnica−50)` | `250 + 10·(técnica del que intercepta − técnica del pasador)` |

Las tres primeras y la última son **exactamente invariantes** al nivel: subir a los dos equipos por igual
no mueve la probabilidad. La parada deja un resto —la calidad del disparo sigue subiendo con los
atributos del rematador— de algo más de un punto a nivel 8, contra los cuatro que tenía la fórmula
absoluta y en el sentido contrario. El **pase** se deja como estaba y hay que decir por qué: no es un
duelo (la mitad defensiva del pase es la intercepción, que sí se ha hecho relativa), así que no entra en
lo que la ADR pide.

**AA-12. La invariancia, medida.** Ocho plantillas por nivel contra su espejo, 400 partidos por nivel
(semilla 11):

| Nivel | Antes: lesiones/partido | Ahora | Entradas | Faltas | Goles |
|---|---|---|---|---|---|
| 1 | 0,61 | **0,31** | 12,2 | 3,1 | 1,47 / 1,38 |
| 4 | — | **0,30** | 9,8 | 2,2 | 1,70 / 1,64 |
| 6 | **0,05** | **0,19** | 9,3 | 2,0 | 1,75 / 1,74 |
| 8 | — | **0,35** | 9,1 | 2,3 | 1,89 / 2,00 |

Antes, dos equipos de nivel 6 producían **once veces menos lesiones** que dos de nivel 1 (0,05 contra
0,61). Ahora la cifra es plana. Es el defecto que la ADR 0041 describe, y lo que lo causaba no era solo
la constante: la banda de atributos por rareza (40-70) comprime las diferencias según sube el
presupuesto, así que la resta *fuerza − resistencia* se estrecha sola. Con la fórmula relativa eso deja
de importar.

**AA-13. Recalibración de `tuning.json`, con RT-056 revalidado.** El lote de referencia
(`--runs 2000 --seed 1`) sigue con **las once métricas en su rango o marcadas INFO**:
`injuriesPerMatch` **0,63** (banda 0,30-0,80), `tacklesPerMatch` 9,5, `shotsPerMatch` 12,0,
`possessionChanges` 23,7, `betterTeamWinRate_60_vs_40` **75,7** (banda 65-80). Ese último es el que más
se movió y explica dos de los ajustes: al volverse relativas, las fórmulas amplifican la diferencia de
calidad, y la métrica saltó de 69,4 a 85,3 con los valores de partida. Se corrigió bajando el peso del
portero (`save.attributeWeightPercent` 20 → 8, con `basePercent` 50 → 54 para compensar el cambio de
referencia) y el de la intercepción (`interceptTechniqueFactor` 14 → 10), que son las dos fórmulas cuyo
significado cambió más.

De la lesión se movieron dos claves: `onTackleBase` 40 → **175** y el factor relativo 5 → **2**, más
`severeShare` 3000 → **4000**. La razón es el clamp: los centrocampistas rivales, que son la mitad de los
que entran, tienen menos fuerza que resistencia tiene la víctima, así que la probabilidad se acotaba a
cero y **una entrada limpia de un centrocampista no podía lesionar a nadie**. Con la base alta y el
factor bajo, la diferencia sigue contando —un desnivel de 40 puntos multiplica el riesgo por 2,7— pero ya
no lo apaga.

**AA-14. La clínica no estaba muerta por falta de lesiones: lo estaba por la política.** La regla 4 de
`RunPolicy` trataba a un lesionado grave *solo si los disponibles bajaban de ocho*, y con trece jugadores
en plantilla eso no pasa nunca. Se le añade la razón que la mantiene viva: **tratar cuando el lesionado
es una pieza** (`TreatFromValue`, 250 puntos de valor, que cualquier titular de mitad de run supera).
Medido: la clínica pasa de **0 a 60 de oro por run** y de 0 a 0,3 tratamientos.

**AA-15. Lo que la ADR 0041 esperaba y no ha llegado.** Las lesiones propias del bucle de run suben de
**0,04 a 0,10 por partido** —2,5 veces— y las graves de 0,20 a **0,62 por run**, pero siguen por debajo
de las 0,31 por equipo y partido del lote de referencia. La causa está identificada y es de **datos, no
de fórmula**: los rivales de `data/rivals/` son plantillas escritas a mano cuyos centrocampistas tienen
mucha menos fuerza que los generados, y los partidos de run producen **0,33 lesiones por partido entre
los dos equipos** contra 0,63 del lote de referencia. Subir más la base choca con el techo de RT-056
(0,80), así que el resto del camino pasa por los rivales, no por `tuning.json`. Y **las muertes siguen
fuera de banda** (0,02 por run contra 0,5-2): con la vía 1 de RF-093 —alinear a un grave sin tratar y que
vuelva a lesionarse— la aritmética no da, y la banda de §10 pide un orden de magnitud que esa vía sola no
puede producir. Es una decisión de diseño pendiente, no un ajuste.

### 18.5. La curva se mide con la build que cabe en cada acto (ADR 0040)

**AA-16. `Sim.Analysis.BuildDensity` deriva las variantes por acto quitando piezas.** Los perks se
recortan **en rondas por titular** —uno de cada slot, luego otro—, que es como los reparte una run, y
dentro de cada titular se conserva **el último** de la lista: los cuatro escalones escriben primero el
perk de base y después el que define ese escalón, así que quitar por delante deja la build de cinco perks
*bien elegidos* que la ADR describe en vez de cinco perks que los cuatro escalones comparten. Los objetos
se quedan en los slots más bajos (el orden en el que la política equipa) y los contadores se acotan a lo
que cabe en los partidos jugados hasta esa puerta. Lo aplican los dos lectores de builds, el de
`/Balance` y el de `Sim.Tests`.

**AA-17. La densidad es por acto **y por escalón**, y hay que decir por qué se separa de la letra de la
ADR.** La ADR 0040 propone una densidad por acto, la misma para los cuatro escalones. Medido así, en el
acto 1 **"buena" y "muy buena" empatan** (51,2 contra 48,1): con cinco perks las dos builds conservan
exactamente los mismos cinco, y lo único que las separa son dos objetos, que valen medio punto. El
escalón superior deja de existir y la escalera de la ADR 0033 se rompe. La corrección es que la densidad
medida (4,3 perks al primer jefe) es la media de **todas** las runs, buenas y malas: una build mejor gana
más partidos de liga, cobra más recompensas y llega a la misma puerta con más piezas. Así que cada
escalón se instancia con su propia densidad **alrededor de esa media**, declarada en
`data/balance/groups.json`:

| Puerta | Incoherente | Correcta | Buena | Muy buena | Medido en runs |
|---|---|---|---|---|---|
| **Acto 1** | 4 perks · 0 obj | 5 · 1 | 6 · 2 | 8 · 3 | 4,3 · 1,8 |
| **Acto 2** | 5 · 1 | 6 · 2 | 11 · 4 | 13 · 5 | 9,7 · 3,6 |
| **Acto final** | 8 · 1 | 8 · 3 | 14 · 6 | 17 · 7 | 13,9 · 6,1 |

**AA-18. Los jefes, recalibrados contra ese material.** `template.quality`: `grimhold_guns` 39 → **31**,
`the_hunt` 45 → **46**, `eternal_crown` 24 → **19**. El del acto 1 baja ocho puntos, que es exactamente
lo que la ADR 0040 anticipaba: estaba ajustado contra una build de catorce perks que en su acto no
existe. Curva remedida con la muestra de la puerta (semilla 1, 32 plantillas × 4 partidos por celda y
raza, 640 partidos por celda, 7.680 en total, 35 s):

| Puerta | Incoherente | Correcta | Buena | Muy buena |
|---|---|---|---|---|
| **Acto 1** `grimhold_guns` | **19,5** (< 25) | **55,3** (45-60) | **62,5** (60-75) | **71,7** (70-85) |
| **Acto 2** `the_hunt` | **7,5** (< 15) | **34,4** (30-45) | **60,5** (55-70) | **66,7** (65-80) |
| **Acto final** `eternal_crown` | **11,9** (< 10) | **32,3** (15-30) | **43,9** (35-50) | **54,7** (55-70) |

Las doce celdas pasan y la escalera es monótona en los tres jefes, pero **tres celdas del jefe final
pasan por la tolerancia declarada de ±2,5 y no por margen** (incoherente 11,9 sobre un techo de 10,
correcta 32,3 sobre 30 y muy buena 54,7 bajo un suelo de 55). La fila del acto final es la más estrecha y
la razón es estructural: la tabla pide 25 puntos de escalón entre "correcta" y "buena" y 20 entre "buena"
y "muy buena", y el catálogo produce 12 y 11. Bajar la calidad del jefe sube las cuatro celdas a la vez,
así que no se puede arreglar con el dial. Queda documentado en vez de forzado.

**AA-19. La banda de victoria de la run pasa a 20-30%**, en §10 y en `FullRunMetrics.RunWinRateMin/Max`,
con el argumento de la ADR 0040: el producto de las tres celdas "muy buena" da 29,5%, así que el techo
antiguo estaba por encima de lo que la propia curva permite aunque se juegue perfecto.

### 18.6. Las tres doctrinas, remedidas (ADR 0037)

**AA-20. La contextual coloca; las puras, no.** Con el equipamiento convertido en atributos, *"¿a quién
le doy las botas?"* se responde mirando la plantilla, y ahí es donde las tres doctrinas dejan de
parecerse. La contextual compra el **par (objeto, portador)** que mejor encaja y las dos puras siguen
comprando por rareza y precio y se lo dan al titular de más valor que no lleve nada. El encaje no se
escribe a mano: es `tuning.generation.positionShare`, el mismo reparto con el que el generador decide en
qué gasta su presupuesto un portero o un delantero, así que el maldito cae solo donde su contrapartida no
duele.

**AA-21. Y aun así el criterio de la ADR 0037 no se cumple.** Lote de referencia: **500 runs por
doctrina, semilla 1**, cinco razas repartidas por igual (1.500 runs, 17.967 partidos, 99 s):

| Doctrina | Run | Compras/mercado | Oro sobrante |
|---|---|---|---|
| **Contextual** | **17,8** | 1,66 | 18,8% |
| **Gastadora** | **12,2** | 1,83 | 17,0% |
| **Ahorradora** | **17,8** | 1,26 | 25,8% |

La contextual gana a la gastadora por **+5,6 puntos** y **empata exactamente con la ahorradora**. Contra
el diagnóstico del paquete Z —"la causa es que el equipamiento no vale nada"— aplicar la ADR 0036 y la
0038 **no ha bastado**, y la razón es la que el propio experimento de AA-8 destapó: **el precio se deriva
del valor, así que cuanto más vale un objeto más cuesta, y la ventaja de saber colocarlo se la come el
precio de comprarlo**. Con los precios de objeto a la mitad (120/260/520 en vez de 240/510/1020) la
contextual llega a +5,0 sobre la mejor de las puras y la tasa de victoria de la run sube a 21,0; con los
precios altos, la ahorradora vuelve a empatar. Y la medida tiene ruido de seed: con la misma
configuración, la ventaja va de **+5,0 (semilla 1)** a **−3,0 (semilla 7)** en lotes de 200 runs.

**El diagnóstico, para la próxima decisión**: la palanca que separa a la contextual no es el
equipamiento, es la **colocación**, y la colocación solo paga si se puede comprar. Mientras el objeto
cueste una fracción grande del oro de un mercado, la ahorradora —que compra la mitad de veces pero
siempre algo caro— rinde lo mismo. Las dos salidas que no rompen nada más son bajar el precio del
equipamiento hasta que el once se pueda equipar entero antes del acto 3 (y aceptar que la fracción
asequible del surtido suba por encima de 35%, que ya está fuera), o darle a la contextual la
**transferencia** de objetos entre jugadores, que hoy no usa: es la decisión que la ADR 0036 declara
como la razón de ser del formato y la política ni la considera.

**AA-22. Las métricas de la run y de escasez, con todo aplicado** (500 runs por doctrina, semilla 1):

| Métrica | Rango | Antes (paquete Z) | Ahora | Estado |
|---|---|---|---|---|
| Tasa de victoria de la run (contextual) | 20-30% | 13,0 | **17,8** | OUT |
| Derrotas por bajar de 5 jugadores | < 35% | 0,0 | **0,0** | IN |
| Duración de una run completa | 18-22 | 20,0 | **20,0** | IN |
| Muertes por run | 0,5-2 | 0,00 | **0,02** | OUT |
| Sumideros que paga el oro de un acto | 2-3, nunca 4 | 2,40 | **2,48** (los cuatro: 0%) | IN |
| Fracción del surtido asequible | 20-35% | 40,5 | **44,1** | OUT |
| Compras por visita al mercado | 1-2 | 1,43 | **1,66** | IN |
| Oro sobrante al terminar la run | < 15% | 23,2 | **18,8** | OUT |
| Runs que llegan a un mercado sin poder comprar | 10-25% | 49,2 | **45,0** | OUT |
| Ventaja de la contextual sobre las dos puras | ≥ 8 puntos | 0,8 | **0,0** | OUT |
| *Lesiones propias por partido* | *—* | *0,04* | ***0,10*** | *INFO* |
| *Lesiones graves por run* | *—* | *0,20* | ***0,62*** | *INFO* |
| *Oro gastado en clínica por run* | *—* | *0* | ***60*** | *INFO* |
| *Objetos en la plantilla al terminar* | *—* | *3,55* | ***4,08*** | *INFO* |

Cinco de las diez siguen fuera. Mejoran la tasa de victoria (+4,8), el oro sobrante (−4,4) y las visitas
en blanco (−4,2); empeoran la fracción asequible (+3,6, por el abaratamiento del equipamiento que la
ventaja de la contextual exigía) y la ventaja de la contextual (−0,8, dentro del ruido). Las dos que la
ADR 0041 prometía —clínica y mercenarios como sumideros— se cumplen **a medias**: la clínica está viva
(60 de oro por run, 0,3 tratamientos), los mercenarios siguen sin usarse porque con trece jugadores nunca
faltan cuerpos, que es un límite de la política y no de la economía.

### Lo que este paquete deja abierto

- **La ventaja de 8 puntos de la ADR 0037 sigue sin conseguirse**, y ya no se puede achacar al
  equipamiento. Diagnóstico y dos salidas en AA-21.
- **Las muertes por run** (0,02 contra 0,5-2). La vía 1 de RF-093 no puede producir ese orden de
  magnitud; hace falta una decisión de diseño, no un ajuste.
- **Los rivales escritos a mano tacklean poco y flojo** (AA-15): es lo que separa las lesiones del bucle
  de run de las del lote de referencia, y se arregla en `data/rivals/`, no en `tuning.json`.
- **La tabla de valor marginal por atributo hay que remedirla**: el motor ha cambiado, y ya se sabe que
  sobrestima por ~1,6 cuando el bono va a un solo jugador (AA-8). De ella salen todos los precios de
  objeto.
- **Tres celdas del jefe final pasan por tolerancia** (AA-18): el catálogo no tiene con qué separar los
  escalones superiores tanto como la ADR 0033 pide.
- **La fracción asequible del surtido y las visitas en blanco siguen oponiéndose** (ya anotado en el
  paquete Z) y ahora además compiten con la ventaja de la contextual, que pide equipamiento barato.
- **Los legendarios de la ADR 0039** (personajes, desbloqueo por división, métrica de dificultad neta)
  son fase 4 y necesitan arte: aquí solo se ha hecho el cambio de escala.

## 19. Decisiones de implementación del paquete AB: la curva por actos (ADR 0043 y 0044)

Los tres actos dejan de ser el mismo juego con números más altos: **taller, gestión y examen**. Entra el
trampolín del jefe (recompensas escalonadas por tipo de nodo), el desgaste creciente por acto, la
posibilidad de **rechazar** la recompensa, el nodo de élite diferenciado en riesgo y premio, y la escala
de oro de 1 a 100 de la ADR 0044.

### 19.1. Los tres jefes, recalibrados contra la curva revisada (ADR 0033)

La fila del acto 1 cambió de examen a taller (65-80% para una build correcta, antes 45-60), así que su
jefe se recalibró entero, y con él las otras dos filas. Muestra: la de la puerta —semilla 1, 32
plantillas × 4 partidos por celda y raza, **640 partidos por celda**, 7.680 en total, 35 s—, reproducible
con `dotnet run --project Balance -c Release -- --boss-gate --rosters 32 --runs 4 --seed 1`.

| Puerta | Incoherente | Correcta | Buena | Muy buena |
|---|---|---|---|---|
| **Acto 1** `grimhold_guns` | **26,7** (20-35) | **71,6** (65-80) | **84,8** (75-88) | **92,2** (85-95) |
| **Acto 2** `the_hunt` | **10,3** (< 15) | **41,7** (35-50) | **66,6** (60-72) | **76,7** (72-85) |
| **Acto final** `eternal_crown` | **6,1** (< 10) | **28,8** (15-28) | **46,2** (40-55) | **57,7** (55-70) |

**Once celdas de doce caen dentro de la banda de la ADR sin usar la tolerancia**, y la escalera es
monótona en los tres jefes. La única que pasa por el margen de medida (±2,5) es *correcta* contra el jefe
final, 0,8 puntos por encima de su techo. Es mejor que el estado anterior (tres celdas por tolerancia,
AA-18) y la causa está medida: **el resultado de una build correcta contra el jefe final no depende de
cuántas piezas lleve**. Medido: con 5 perks y sin contadores pasa el 29,2% y con 7 perks y contadores el
29,1%. Lo que mueve esa celda es el jefe, y bajarlo hunde a la vez *buena* y *muy buena*, que están en el
suelo de su banda.

**AB-1. Qué se movió en cada jefe.**

| Jefe | Antes | Ahora | Por qué |
|---|---|---|---|
| `grimhold_guns` | calidad 31, nivel 5, `butterfingers` (banChannel `save`) | calidad **17**, nivel **4**, **`one_gun_per_port`** (singleCopy) | El acto 1 es el taller: su jefe es el más flojo de los tres y su modificador el más suave |
| `the_hunt` | calidad 46 | calidad **40** | La fila del acto 2 se mueve poco; seis puntos de calidad bastan |
| `eternal_crown` | calidad 24, `iron_curtain` en columna 5 | calidad **28**, `iron_curtain` en columna **6** | El cerrojo vacía el tercio atacante, no medio campo: ensancha la escalera y deja subir la calidad |

**AB-2. El modificador del acto 1 cambia de tipo, y por qué era necesario.** `butterfingers` apagaba el
canal de parada, y los cuatro escalones llevan sus dos perks de portero en el mismo sitio: apagaba el
diferenciador del escalón superior en las cuatro filas a la vez y aplanaba la escalera (es Z-D en
`pendientes.md`; medido aquí: con `butterfingers` y la densidad nueva, *buena* 89,2 y *muy buena* 89,1,
que además rompe la monotonía). `one_gun_per_port` (**«Un cañón por tronera»**, `singleCopy`: una copia
repetida de un perk solo surte efecto en su primer portador) no le cuesta nada a una build repartida y le
cuesta media build a la que copia y pega, que es exactamente lo que un jefe-taller debe castigar. De paso
los **cuatro tipos de modificador quedan repartidos sin repetirse**: `singleCopy` en el acto 1, `markStar`
en el 2, `banChannel` y `pushBack` en el final.

**AB-3. La densidad por acto, remedida y con un hallazgo.** `data/balance/groups.json` se actualiza con lo
que la run produce ahora (500 runs, contextual): **3,8 perks y 2,2 objetos** al jefe del acto 1, **8,2 y
4,7** al del 2, **13,0 y 6,7** al final. Y aparece algo que la ADR 0040 no preveía: **el escalón
incoherente llega con MÁS piezas que el correcto**, no con menos. Es consecuencia directa de poder
rechazar: quien construye bien deja pasar el perk que no encaja y llega con menos y mejores. Medido en las
dos direcciones —en runs completas la doctrina contextual termina con 8,9 perks en el once y la gastadora
con 10,3, y gana 7 puntos más de runs; en la puerta, un once incoherente de 10 perks pasa el jefe final el
9,8% y uno de 5 perks el 12,0%—, así que la densidad del escalón incoherente se sube a propósito.

### 19.2. El trampolín: recompensas escalonadas por tipo de nodo

`data/economy/economy.json` gana `nodeRewards`, con una entrada por tipo de nodo de partido:

| Nodo | Oro | Elecciones | Rareza mejorada | Cura |
|---|---|---|---|---|
| Liga | base del acto | 1 de 3 | — | no |
| **Élite** | **+50%** | 1 de 3 | **65%** de las opciones por encima de común | no |
| **Jefe** | **+100%** | **2** de 3 | 35% | **plantilla entera** |

**AB-4. Dos elecciones en el jefe, no una doble.** Cada elección se resuelve por separado y con surtido
propio: el flujo del nodo se desplaza `100` por elección cobrada (`RewardSystem.PickStreamStep`), así que
la segunda no es la primera repetida. El reroll sigue siendo **uno por nodo** (RF-071b), no uno por
elección. Cobrar dos veces sigue siendo determinista por (semilla, nodo, elección, rerolls) y no hay nada
nuevo que guardar en el estado: basta un contador de elecciones cobradas.

**AB-5. La rareza mejorada se sortea opción a opción y antes de elegir el tipo**, de modo que un nodo de
liga tira exactamente el mismo número que uno de élite y siempre le sale «no»: cambiar el escalón de un
nodo no desplaza el dado de los demás.

**AB-6. La cura del jefe cierra el ciclo del acto.** Al superar un jefe, toda la plantilla vuelve a sano y
las lesiones leves acumuladas se borran; **el muerto no vuelve** (RF-093). Es lo que permite exprimir la
plantilla durante un acto en vez de administrar una ruina uniforme, y es la otra mitad del trampolín.

### 19.3. Rechazar la recompensa (RF-071 cambia)

RF-071 obliga hoy a elegir una de las tres. Con perks irreversibles (RF-072) y slots limitados (RF-023),
quedarse con la menos mala **empeora** la build: ocupa el slot que necesitará el perk que llegue después.
Entra la decisión `DeclineReward`, que consume la elección y no se lleva nada. **El cambio de requisito
queda anotado en `pendientes.md`** (R-13).

**AB-7. Cuándo la usa la política automática.** Cuando ninguna de las tres opciones encaja: ningún perk
que el filtro `PerkPlacement` acepte en un titular, ningún objeto para un titular sin objeto, ningún
cuerpo que haga falta. La doctrina **contextual** añade un segundo listón, y es el que más pesa: un perk
solo merece un slot si su **valor medido** (ADR 0038) no es negativo. La mitad del catálogo mide por
debajo de cero —resta tasa de victoria a quien lo lleva— y el pool los ofrece *más* a menudo, porque su
peso es inversamente proporcional al valor. Las dos doctrinas puras no aplican ese listón: la gastadora
coge lo primero que puede colocar (y **no rechaza nunca**, 0,00 rechazos por run) y la ahorradora mira la
rareza, que no es lo mismo que el valor.

Medido: **0,43 rechazos por run**, el **4,2%** de las elecciones. Es una válvula, no un cambio de ritmo:
que las tres opciones fallen a la vez es raro por construcción.

### 19.4. Desgaste creciente por acto y el nodo de élite

**AB-8. El desgaste es un multiplicador en datos, no una fórmula nueva.** `tuning.injury` gana
`actScalePercent` (**120 / 260 / 420**) y `eliteScalePercent` (**150**), y el bucle de run los pasa al
motor en `SimConfig.InjuryScalePercent`; el motor multiplica la probabilidad **ya calculada** por él. Un
partido suelto usa `SimConfig.Default` (100%) y por tanto RT-056 y las puertas de fase 1 no se mueven. El
motor no puede saber en qué acto está, así que `IRunSystems.MatchConfig` recibe ahora el catálogo.

Medido (500 runs, contextual): **0,21 lesiones propias por partido** (antes 0,10), **1,36 lesiones graves
por run** (antes 0,62) y la clínica pasa de contenido testimonial a sumidero con uso real.

**AB-9. El élite adquiere su función**: más premio (arriba) y **más riesgo**, en dos sitios. Su rival es
el **mismo rival estático del acto subido `map.eliteRivalLevelBonus` = 2 niveles** con la progresión de
RF-027 —no una tabla de dificultad aparte—, y su desgaste va multiplicado por `eliteScalePercent`. Elegir
ruta pasa a ser una decisión: se paga con cuerpos y se cobra en rareza y oro.

### 19.5. La escala de oro (ADR 0044)

Toda la economía se reescribe en decenas. Un volcado real del mercado (semilla 20260906, club orco, primer
nodo de mercado de cada acto) con la escala nueva:

```
oro inicial: 10
jugador Rare 47 · jugador Common 18 · jugador Uncommon 23
perk Uncommon 22 · perk Common 12 · perk Uncommon 21 · perk Rare 31
objeto Uncommon 10 · objeto Uncommon 10 · objeto Rare 32 · objeto Common 6
consumible 9 · 7 · 8
```

**AB-10. El rango se acota dentro de la rareza, no solo se escala.** El precio de un objeto nace de su
valor medido (ADR 0038) y se dispersa dentro de su rareza (ADR 0037): las dos cosas juntas daban **18:1**
dentro de una misma categoría. Se añade `market.priceBandPercent` (**25**), que acota el precio final al
±25% del precio base de su rareza. Resultado: **1,7:1 dentro de una rareza** y unas 5:1 dentro de una
categoría, con la diferencia grande entre rarezas y entre categorías, que es lo que la ADR 0044 pide.

**AB-11. La relación perk/objeto se corrige.** Antes un perk poco común costaba 460 y un objeto de dos
atributos 104: cuatro veces más caro el perk, así que comprar perks no compensaba nunca. Ahora, a igual
rareza, un perk cuesta **1,25 veces** lo que un objeto (base 10/18/32 frente a 8/14/26).

**AB-12. El club ya no empieza con 0 de oro.** `RunSetup.StartingGold` es un `init` que valía 0 si nadie
lo rellenaba y `data/clubs/` no existe, así que quien montaba un `RunSetup` a mano llegaba al primer
mercado sin poder comprar nada. Se cierra con `StandardRunSystems.NewRunSetup`, que arma el `RunSetup` con
lo que dicen los datos —oro de partida, nodos por acto y rivales— y es lo que usan `/Balance` y la puerta.
**El oro de partida es 10**: da para un objeto común o un perk común, no para los dos.

**AB-13. Lo que la escala obligó a mover, con su medida.** La tabla de la ADR 0044 es de *valores de
partida* y dos de sus filas no sobrevivieron a la medición:

- `goldAct` es **5/6/7**, no 3/5/7, porque el multiplicador de dificultad de RF-012 se aplica encima: con
  base 5 y el 70% del acto 1, una victoria de liga paga exactamente los **3** de la tabla. Lo que la ADR
  fija es lo que el jugador cobra, no la base.
- `rerollBaseCost` es **1**, no 2. Con 2, el sumidero de rerolls de un acto costaba más que una clínica y
  `sinksAffordablePerAct` (RF-114k) caía a **1,81**, fuera de su banda 2-3.

Con eso, el oro ganado por acto es **23 / 35 / 47** y una run completa gana del orden de **100**, que es
lo que la ADR pide.

### 19.6. Un defecto de bucle que la carnicería destapó

**AB-14. Un portero suplente que juega de defensa se llevaba sus perks de portero al campo.** `RunLineup`
recoloca al portero sobrante como defensa (el simulador solo admite un portero alineado) y al jugador de
campo que hace de portero de emergencia, pero les dejaba los perks con `positionOnly` de la posición que
acababan de perder. `Simulator.Run` rechaza el equipo entero —*«asigna al jugador 12 (Defender) el perk
'clean_sheet_legacy', que solo admite Goalkeeper»*— y la run se cae. Estaba latente y salió en cuanto el
trampolín repartió más perks: ahora `Repositioned` quita los perks que la posición nueva no admite. **No
se pierden**: siguen en el estado y vuelven en cuanto el jugador juegue en su sitio.

**AB-15. `spearpoint` describía mal lo que hace.** Generaba *«probabilidad de tiro a puerta +25% hacia el
compañero de delante»*, que no significa nada: el tratamiento **por par** solo existe en el pase (§16.4), y
en cualquier otro canal `target: linked` es un bono normal sobre el compañero vinculado. La descripción se
había quedado en la lectura vieja. Ahora `DescriptionGenerator` aplica **la misma condición que el motor**
(par solo si el canal es `pass`) y sale *«el compañero de delante suma +25% a su probabilidad de tiro a
puerta»*. Revisado el catálogo entero: hay **seis** perks con efecto sobre el vinculado —`covering_shadow`,
`diagonal_press`, `gentle_giant`, `pivot_duo`, `spearpoint`, `wing_overlap`— y **ninguno** usa el canal
`pass`, así que hoy los seis son bonos sobre el compañero y ninguno es un par; la plantilla del par se
conserva porque la mecánica existe (ADR 0021).

### 19.7. Las tres cosas que había que comprobar

Lote de referencia: **500 runs por doctrina, semilla 1**, cinco razas repartidas por igual (1.500 runs,
20.620 partidos, 109 s), `dotnet run --project Balance -c Release -- --full-runs 500 --seed 1`.

**1. Dónde se pierde.** Las derrotas se concentran en el **acto 2**, que es lo que la ADR 0043 pide:

| Doctrina | Derrotas en el acto 1 | **en el acto 2** | en el acto 3 |
|---|---|---|---|
| Contextual | 30,9% | **52,1%** | 17,0% |
| Ahorradora | 33,2% | **45,6%** | 21,1% |
| Gastadora | 30,2% | **54,7%** | 15,1% |

El 100% de las derrotas siguen siendo contra un jefe (RF-002b vía 1); la vía de quedarse sin plantilla no
se ejercita ni con el desgaste nuevo.

**2. La separación entre doctrinas: no se ha abierto, y la causa está medida.**

| Doctrina | Puerta 1 | Puerta 2 | Puerta 3 | **Run** | Compras/mercado | Perks en el once |
|---|---|---|---|---|---|---|
| **Contextual** | 76,4 | 47,9 | 64,5 | **23,6** | 1,35 | 8,9 |
| **Ahorradora** | 74,8 | 53,7 | 60,2 | **24,2** | 1,05 | 9,7 |
| **Gastadora** | 74,8 | 39,0 | 56,8 | **16,6** | 1,53 | 10,3 |

La contextual gana a la gastadora por **+7,0 puntos** (antes +5,6) y **sigue empatada con la ahorradora**
(−0,6, dentro del ruido de ±2 puntos con 500 runs). Tres cosas que la medición sí dice:

- **La puerta que separa es la del acto 2**, exactamente el acto que la ADR 0043 llama «gestión»: 47,9%
  frente a 39,0%. En la primera puerta las tres doctrinas están dentro de dos puntos, y en la tercera lo
  que se ve es supervivencia (solo llegan las runs que construyeron bien).
- **El trampolín diluye la doctrina de compra.** Con las recompensas escalonadas, la run cobra **9,8
  elecciones gratuitas** y compra unas 6 piezas en el mercado: lo único en lo que las tres doctrinas se
  diferencian pasa a ser el 40% de la build, y la mitad de eso son objetos. La ventaja de 8 puntos de la
  ADR 0037 es ahora **estructuralmente más difícil**, no menos.
- **Lo que separa no es cuánto compras, es dónde lo pones.** La gastadora compra *más* (1,53 por mercado)
  y termina con *más* perks en el once (10,3 frente a 8,9) y gana 7 puntos menos. La ahorradora compra la
  mitad y rinde igual que la contextual. La conclusión que la aritmética de la ADR 0033 respalda: la run
  la decide la **calidad** de la build, y la política automática que sabe colocar ya está cerca del
  escalón «buena» (producto de la curva: 26,1%) mientras que la que no sabe se queda entre «correcta»
  (8,8%) y «buena».

**3. Cuánto cuesta perder.** Una run ganada dura **35 nodos y 20 partidos**; una perdida, **21,3 nodos y
12,0 partidos**: el **61%**. La referencia del género es un tercio (23 minutos frente a 64). **No se llega
a un tercio, y no se puede llegar sin contradecir la directriz**: si la mayoría de las derrotas tienen que
caer en el acto 2 —a dos tercios del recorrido— una run perdida cuesta por fuerza ~60% de una ganada. Las
dos exigencias (`curva-de-dificultad.md` §2.2 y la ADR 0043) son incompatibles y gana la ADR, que es
posterior y es directriz del revisor. En minutos, con los 60-90 s por partido de RF-003, una run perdida
son unos **25-30 minutos** frente a los 45-55 de una ganada: el número absoluto sí está en el punto dulce
del género aunque la proporción no lo esté.

### 19.8. Las métricas de §10 y de escasez, con todo aplicado

| Métrica | Rango | Antes (AA) | **Ahora** | Estado |
|---|---|---|---|---|
| Tasa de victoria de la run (contextual) | 20-30% | 17,8 | **23,6** | **IN** |
| Derrotas por bajar de 5 jugadores | < 35% | 0,0 | **0,0** | IN |
| Duración de una run completa | 18-22 | 20,0 | **20,0** | IN |
| Muertes por run | 0,5-2 | 0,02 | **0,06** | OUT |
| Sumideros que paga el oro de un acto | 2-3, nunca 4 | 2,48 | **2,09** | IN |
| Fracción del surtido asequible | 20-35% | 44,1 | **53,8** | OUT |
| Compras por visita al mercado | 1-2 | 1,66 | **1,35** | IN |
| Oro sobrante al terminar la run | < 15% | 18,8 | **17,7** | OUT |
| Runs que llegan a un mercado sin poder comprar | 10-25% | 45,0 | **78,2** | OUT |
| Ventaja de la contextual sobre las dos puras | ≥ 8 puntos | 0,0 | **−0,6** | OUT |
| *Lesiones propias por partido* | *—* | *0,10* | ***0,21*** | *INFO* |
| *Lesiones graves por run* | *—* | *0,62* | ***1,36*** | *INFO* |
| *Oro gastado en clínica por run* | *—* | *60 (de 1.390)* | ***3,5 (de 64)*** | *INFO* |
| *Recompensas rechazadas por run* | *—* | *—* | ***0,43*** | *INFO* |

**AB-16. Las muertes siguen fuera de banda, y ahora se sabe por qué no basta el desgaste.** Con el
desgaste al 420% en el acto 3 las lesiones se han doblado, pero las muertes van de 0,02 a **0,06**. La
sensibilidad está medida: **duplicar el desgaste no mueve las muertes** (con 200/400/620 salen 0,11 y con
300/650/950 salen 0,30, con 1,29 lesiones por partido entre los dos equipos, el doble de lo que RT-056
admite en un partido suelto). La razón es que la única vía viva de RF-093 —alinear a un grave sin tratar—
es una **decisión** que una política razonable no toma: con trece jugadores nunca faltan siete sanos, y
cuando falta uno el oro cubre la clínica. Se confirma además al revés: la doctrina **gastadora**, que gasta
el oro y no puede pagar tratamientos, tiene **el triple de muertes** (0,19 frente a 0,06). La banda 0,5-2
necesita la segunda vía de RF-093 (perks rivales letales, hoy inexistentes en `/data`) o una plantilla más
corta; no es un ajuste de datos.

**AB-17. La escasez empeora por aritmética entera.** «Llegar a un mercado sin poder comprar nada» sube del
45% al 78% de las runs: con precios de 6 a 47 y un acto que gana 23, quedarse con 4 de oro y no llegar al
objeto común más barato es habitual. **Por visita** sigue siendo el 15%, que es la cifra razonable; la
métrica está contada por run y una run visita 9 mercados (ya anotado como Z-K). Las cotas de no regresión
de la puerta se ensanchan a 88% y 62% con este motivo escrito.

## 20. Decisiones de implementación del paquete AC: la plantilla corta (ADR 0046)

El desgaste pasa a ser el recurso central que el documento dice que es. Tres cambios de la ADR 0046
—plantilla base de diez con techo de doce, nodo de inscripción, y los primeros perks letales del
catálogo— y una palanca añadida por el revisor: el oro inicial baja al subir de división (ADR 0044).

**Resultado de una línea**: las muertes por run pasan de **0,06 a 0,64** (banda 0,5-2, **dentro**), la
segunda vía de derrota de RF-002b deja de ser teórica (**2,07%** de las derrotas), la tasa de victoria
sigue en banda (**22,6%**) y la curva de puertas de la ADR 0033 **no se mueve ni un decimal**.

### 20.1. La plantilla base es de diez (RF-020)

**AC-1. El límite se comprueba en un único embudo.** `RunState.WithNewPlayer` es por donde entra todo
jugador nuevo —mercado, canterano, mercenario, recompensa— y ahí es donde lanza si la plantilla está
llena. La comprobación no está repartida por los sistemas a propósito: el fallo que la ADR 0045
diagnosticó era precisamente que *alguien* ampliaba la plantilla en silencio, y con el embudo cerrado
ningún camino nuevo puede volver a hacerlo sin darse cuenta. Encima de él, cada sistema añade su mensaje
("hay que vender o descartar a alguien primero"), que es lo que la interfaz necesita enseñar.

**AC-2. El techo vive en un contador, no en el esquema.** `RosterCapacity = 10 + Counter("enrollmentSlots")`,
acotado a 12. Va en `RunState.Counters` porque es exactamente para lo que ese diccionario existe (añadir
un sistema sin subir la versión de esquema, RT-030), y porque así una run guardada antes de este paquete
se carga con cero huecos, que es la lectura correcta.

**AC-3. El muerto no ocupa plantilla.** `RosterSize` cuenta los vivos; el caído sigue en `Roster` para el
memorial (RF-122) pero deja su sitio libre. Morir cuesta un jugador, no un jugador **y** su hueco.

**AC-4. Descartar (`ReleasePlayer`) existe porque vender no basta.** Vender solo se puede en el mercado
(RF-114f) y hacer sitio no puede depender de estar en uno. El descarte no cobra nada y nunca puede dejar
los disponibles por debajo del mínimo de RF-002b: perder la run desde un menú no es una decisión, es un
error de diseño.

**AC-5. Lo que se llevó por delante: el canterano.** Con diez, el canterano gratuito compite por un hueco
y lo pierde a menudo: solo entra cuando la plantilla tiene sitio, es decir, tras una baja o tras comprar
un hueco. Medido: la política ficha **3,31 canteranos por run antes y 1,23 después**. Es la consecuencia
más visible del cambio y vuelve en el apartado de economía (AC-18). Gratis en oro nunca quiso decir
gratis en plantilla; ahora se nota.

### 20.2. El nodo de inscripción (amplía RF-011)

**AC-6. Entra quitándole sitio a los servicios, no al mercado.** Las capas de mercado son cuellos de
botella de un nodo y de ellas sale la garantía de RF-011b por construcción (§4), así que tocarlas habría
puesto en riesgo lo único que el mapa garantiza de verdad. El nodo de inscripción entra en el sorteo de
las **capas de servicio**, junto a clínica, entrenamiento y evento: el reparto pasa de tres tipos a
cuatro. La garantía de mercado sigue saliendo por construcción y el test de los 1.000 mapas
(`MapTests.MarketGuarantee_HoldsOnAThousandMaps`) sigue en verde sin tocarlo.

**AC-7. Uno por acto, garantizado, como la clínica.** La primera capa de servicios lleva siempre una
clínica y la última lleva siempre un nodo de inscripción. Si dependiera del sorteo, comprar un hueco
sería una opción que el dado puede no ofrecer nunca, y una decisión que a veces no existe no es una
decisión. Un test nuevo lo comprueba sobre 600 mapas y además que el nodo **nunca ocupa una capa entera**:
siempre hay otro servicio al lado, así que ir a por el hueco es no ir a lo otro.

**AC-8. Coste creciente en datos**: `economy.enrollmentCosts = [12, 25]`, los valores de partida de la
ADR 0046. Sobre los ~105 de oro de una run completa (§19.5) los dos huecos son el **35%**, que es la
"casi la mitad" que la ADR pide. El cargador exige que sean exactamente dos y que el segundo supere al
primero: el coste creciente es una regla, no una costumbre.

**AC-9. Cuántas veces lo usa una política razonable: 0,88 huecos por run** (10,77 de oro), y el reparto
importa más que la media. La política reserva el precio del **primer** hueco igual que reserva la clínica
—si no, llega al nodo con el oro ya gastado en el mercado, que va antes en el acto— y **no reserva para el
segundo**. Las tres variantes que llevaron a esa regla, medidas sobre **la misma exposición letal** (300 runs por doctrina, semilla 1, cinco
rivales con letal de alta conversión) para que lo único que cambie sea la regla de ahorro:

| Política ante el nodo | Huecos/run | Plantilla final | Muertes/run | Tasa de victoria | Oro al mercado |
|---|---|---|---|---|---|
| Sin reservar (compra si le sobra) | 0,25 | 10,6 | **0,59** | 17,7% | 47,7 |
| **Reserva el primero** (elegida) | **0,89** | **11,0** | **0,38** | **25,0%** | **39,9** |
| Reserva los dos | 1,21 | 11,2 | 0,22 | 23,0% | 23,8 |

La fila de abajo es el aviso: **con los dos huecos comprados, la plantilla vuelve a ser ancha y el
desgaste deja de morder** —0,22 muertes, otra vez el mundo de la ADR 0045— y el mercado se hunde de 48 a
24 de oro por run, la mitad. La de arriba es el contrario: sin reserva el nodo es decorado (0,25 huecos) y
la run se hace un 7% más difícil sin que el jugador tenga una salida. El nodo de inscripción es, por
construcción, **el contrapeso de la plantilla corta**; que sea una decisión y no un trámite depende de que
ahorrar para el segundo hueco cueste medio acto sin comprar nada, y a esas alturas un perk raro vale más
que el duodécimo cuerpo. La exposición letal final (siete portadores, AC-13) se calibró **después** de
fijar esta regla, no al revés.

### 20.3. Los cuatro perks letales (RF-093 vía 2)

**AC-10. El hallazgo que decidió su forma: el motor solo deja una ventana.** `EffectEngine.IsLethalVictim`
exige que la víctima esté **en el campo** y **no esté sana**. Pero una lesión sufrida en el partido saca
al jugador del campo en el acto (`MatchEngine.ResolveInjury` → `LeavePitch`), así que el único herido
alcanzable es **el que salta al campo herido**. De ahí que los dos letales de mayor conversión disparen en
`MATCH_START`: no es un adorno temático, es dónde el mecanismo existe. Y de ahí también que sean
perfectamente telegrafiables: la regla que el jugador tiene que leer es *"contra este equipo, quien salga
tocado no vuelve"*.

| id | rareza | trigger | canal (escalón ADR 0035) | escasez | conversión |
|---|---|---|---|---|---|
| `skullsplitter` | legendary | MATCH_START | `injury` +3 (paso 1 ×3) | `tagsRequired: Dirty` | alta |
| `marrow_thirst` | rare | MATCH_START | `injure` +3 y `severeInjury` +9 (pasos 1 y 3) | `Aggressive` + empezar en el tercio rival | alta |
| `second_wound` | rare | INJURY | `severeInjury` +3 | solo mientras no van ganando | baja |
| `iron_studs` | rare | TACKLE | `tackleEvasion` −9 (paso 3 ×3) | solo presionando en el tercio rival | baja |

**AC-11. La escasez se escribe con `tagsRequired`, no con `condition`.** Un rival construido a mano
siempre lleva `StyleTag.Neutral` (`RivalTeamBuilder`), así que condicionar por etiqueta de estilo habría
hecho que el perk no se disparara nunca en un rival mientras el ojeo lo anunciaba: un peligro anunciado
que no existe, que RF-012d prohíbe tanto como el contrario. Los rasgos (`Dirty`, `Aggressive`) sí viajan
al partido, se ven en el informe y además restringen quién puede llevarlo en la plantilla del jugador
(`PerkPool.EligibleCarriers`).

**AC-12. La descripción lo dice, no solo el ojeo.** `Scouting.LethalPerks` ya destacaba la amenaza
(RF-013), pero la ficha del perk no decía lo peor que puede pasar. Se añade `layout.lethalSuffix` a
`data/l10n/*/templates.json` y `DescriptionGenerator` lo cuelga cuando el perk es letal: sigue siendo
texto **generado desde el dato** (RT-035), no escrito a mano. Sale, por ejemplo: *«Al empezar el partido,
el equipo rival suma +3% a su probabilidad de lesionarse. Si alcanza a un rival que ya no está sano, lo
mata.»*

**AC-13. El reparto en rivales, y por qué siete.** Acto 1: **ninguno** (es el taller, ADR 0043). Acto 2:
tres equipos con un letal de alta conversión y uno con `second_wound`. Acto 3: cuatro con alta conversión
y los dos de baja donde ya estaban. Los elfos quedan limpios en los dos actos, que es una declaración de
identidad y no un hueco.

**La exposición es la palanca**, y está medida punto a punto (300 runs por doctrina cada uno). Los dos
primeros escalones se midieron antes de fijar la reserva del hueco (AC-9) y los dos últimos después, así
que la escalera hay que leerla por tramos y no como una recta:

| Portadores de alta conversión (de 10 rivales) | Muertes/run | Derrotas por plantilla | Tasa de victoria |
|---|---|---|---|
| 4, sin reserva de hueco | 0,42 | 0,0% | 19,0% |
| 5, sin reserva de hueco | 0,59 | 2,0% | 17,7% |
| 6, con reserva del primero | 0,46 | 1,8% | 24,3% |
| **7, con reserva del primero** (elegido) | **0,77** | **3,5%** | **22,7%** |
| 10, sin reserva de hueco | **2,87** | **17,1%** | **12,3%** |

La fila de diez es el techo y el aviso a la vez: con todos los rivales de los actos 2 y 3 matando, las
muertes se disparan a 2,87 —fuera de banda por arriba— y la run se vuelve injugable. Siete es el punto en
el que la banda 0,5-2 se cumple sin que el resto de la curva se caiga. Con la muestra grande de cierre
(500 runs) ese punto mide **0,64**; el 0,77 de la tabla es la misma configuración con 300 runs, y la
diferencia es muestreo (la desviación típica de la cifra es de ±0,04 con 500 runs).

### 20.4. Aniquilación: por qué hoy no es una vía, medido

El riesgo que la ADR 0046 manda vigilar —que una build de violencia gane por incomparecencia (RF-002b)—
**no existe hoy, y la causa es estructural, no de balance**:

1. `RivalTeamBuilder` construye a los diez rivales **siempre sanos**.
2. Una lesión en el partido saca al lesionado del campo, y `IsLethalVictim` exige estar en el campo.

Luego un perk letal del jugador no encuentra víctima nunca. Medido y no supuesto: la build
`orc_butchery` —los cuatro letales repartidos entre cuatro titulares con los rasgos y las rarezas que
hacen falta— dispara los cuatro perks (`skullsplitter` y `marrow_thirst` en el **100%** de los partidos,
`iron_studs` y `second_wound` en el **71%**) y produce **0 muertes rivales en 60 partidos**.
`LethalPerkTests.APlayerViolenceBuildCannotKillAnOpponent` lo deja como guardia de regresión: si algún día
cambia una de las dos condiciones, el test se pone rojo y hay que remedir la aniquilación **antes** de
seguir, que es justo lo que la ADR pide.

Consecuencia para el jugador: los letales le sirven hoy como lo que sus efectos dicen —más lesiones y más
lesiones graves en el rival— y como espejo de la amenaza, no como vía de victoria. Encarecer la letalidad
no hace falta porque no hay nada que encarecer.

### 20.5. La contrajugada, y lo que vale leer el informe

La política automática **lee el informe de ojeo** antes de alinear (RF-013): si el rival lleva algún perk
letal, deja en el banquillo a los tocados mientras le queden siete sanos. Sin esa regla, la medición de
muertes sería la de un jugador que no lee el informe, es decir un techo y no un número. Las dos cifras,
con el lote de referencia completo (500 runs por doctrina):

| | Muertes/run | Derrotas por plantilla | Tasa de victoria |
|---|---|---|---|
| **Lee el ojeo** (política por defecto) | **0,64** | **2,07%** | **22,6%** |
| No lo lee (`--ignore-scouting`) | 1,28 | 0,75% | 20,4% |

**AC-15. Apartar al tocado sale gratis, y eso es un aviso de diseño.** Las dos filas están a dos puntos de
tasa de victoria: la contrajugada no cuesta partidos, porque un jugador con lesión leve ya juega al −15%
(RF-091) y sentarlo casi no penaliza. El letal, por tanto, **no crea un dilema en el momento de alinear**:
lo que castiga es llegar a ese partido sin siete sanos, es decir, la administración de todo el acto
anterior. Es un buen sitio para que castigue —es exactamente el recurso que el juego dice tener— pero
conviene saber que la decisión no está en la alineación, está antes.

### 20.6. Economía remedida (RF-114k) y el oro por división

**AC-16. Cinco sumideros, no cuatro.** El hueco de plantilla es oro que sale de la run y compite con los
demás, así que entra en `FullRunMetrics.SinksAffordable` (se cuenta el **primer** hueco: la métrica
pregunta qué cabe en *un* acto). `sinksAffordablePerAct` queda en **2,08** (banda 2-3) y ningún acto paga
los cinco. RF-114k se sigue cumpliendo con un sumidero más.

**AC-17. Dónde va el oro ahora**, por run (contextual, 500 runs; entre paréntesis, antes del paquete):

| | Ganado | Mercado | Clínica | **Inscripción** | Rerolls | Sobrante |
|---|---|---|---|---|---|---|
| Antes | 66,3 | 58,7 | 3,8 | — | 2,4 | 11,4 |
| **Ahora** | **56,2** | **37,6** | **4,7** | **10,8** | **1,4** | **11,8** |

El mercado cede 21 de oro por run: 11 al sumidero nuevo y el resto a que se gana menos (se llega menos
lejos). La clínica **sube** un 24%, que es la mitad de lo que la ADR 0046 predecía ("fichar y tratar suben
de valor"); la otra mitad, fichar, no sube en volumen sino en precio de oportunidad, porque ahora un
fichaje cuesta además un hueco.

**AC-18. `purchasesPerMarket` baja de 1,39 a 0,76, y la mitad de la caída es el canterano.** Las compras
por run pasan de 8,88 a 4,88 sobre el mismo número de mercados (6,4), y se reparten así:

| | Canteranos | Perks | Objetos | Fichajes | **De pago por mercado** |
|---|---|---|---|---|---|
| Antes | 3,31 | 2,57 | 2,85 | 0,15 | **0,87** |
| **Ahora** | **1,23** | **1,66** | **1,95** | **0,03** | **0,57** |

Dos causas, las dos medidas. La primera es el hueco: los 2,1 canteranos que se pierden no se dejan de
fichar por falta de oro, sino de sitio (AC-5), y la métrica los contaba como compras aunque fueran
gratis. La segunda es aritmética de caja: la run gana 10 de oro menos (se llega menos lejos) y desvía
10,8 al sumidero nuevo, de modo que al mercado le llegan 21 de oro menos y compra 1,8 artículos menos.
Conviene señalar que **la banda 1-2 de la ADR 0037 ya se cumplía gracias a lo gratuito**: contando solo
las compras de pago, el valor de antes era 0,87, también por debajo de 1. La métrica **deja de ser puerta
dura** y pasa al grupo de cotas de no regresión (0,5-2,0) con esta causa escrita, como se hizo con
`affordableShare` en §19; lo que la banda pide —que comprar sea una decisión y no un trámite— lo dice
mejor hoy `contextualAdvantage` (AC-19), que ha mejorado seis puntos.

**AC-19. La separación entre doctrinas mejora sola, y mucho.** `contextualAdvantage` pasa de **−0,6** (§19)
a **+5,6** puntos: la contextual gana el 22,6% de sus runs, la ahorradora el 17,0% y la gastadora el
13,2%. Sigue por debajo de los 8 puntos que pide la ADR 0037, pero es el mejor valor medido de la fase y
la razón es la que la ADR 0037 describía: con la plantilla corta **el oro vuelve a ser escaso de verdad**
y elegir en qué gastarlo separa a quien sabe de quien no. La gastadora, que no reserva ni para la clínica
ni para el hueco, es la que más se hunde.

**AC-20. El oro inicial por división (ADR 0044).** `economy.startingGold` pasa a ser
`startingGoldByDivision`, en el orden del enum `Division`: **10 / 8 / 5 / 3 / 2** (tercera, segunda,
primera, continental, mundial). El criterio es que el club empiece con lo justo para **un artículo común**
de la primera tienda y que en Mundial esa tienda sea solo un escaparate. La ADR no lista Continental, que
va interpolado entre Primera (5) y Mundial (2). El cargador exige que la serie **no crezca**: subir de
división es empezar con menos. La fase 2 juega siempre en tercera, así que el único valor medido es el 10
y todas las cifras de este apartado son a ese valor; el resto se valida cuando existan las divisiones
(fase 4). Es una palanca de **ritmo**: 10 de oro sobre los ~105 de una run completa es el 10% del total y
no mueve la tasa de victoria por sí sola.

### 20.7. Las métricas de §10, antes y después

Lote de referencia: **500 runs por doctrina, semilla 1**, cinco razas repartidas por igual (1.500 runs,
20.202 partidos, 103 s), `dotnet run --project Balance -c Release -- --full-runs 500 --seed 1`.

| Métrica | Rango | Antes (AB) | **Ahora** | Estado |
|---|---|---|---|---|
| **Muertes por run** | **0,5-2** | **0,06** | **0,64** | **IN** (era OUT) |
| **Derrotas por bajar de 5 jugadores** | < 35% | 0,0% de las derrotas | **2,07%** | **IN y ya no es cero** |
| Tasa de victoria de la run (contextual) | 20-30% | 23,6 | **22,6** | IN |
| Duración de una run completa | 18-22 | 20,0 | **19,97** | IN |
| Sumideros que paga el oro de un acto | 2-3, nunca todos | 2,09 | **2,08** | IN |
| Ventaja de la contextual sobre las dos puras | ≥ 8 puntos | −0,6 | **+5,6** | OUT, pero el mejor valor de la fase |
| Compras por visita al mercado | 1-2 | 1,35 | **0,76** | OUT — causa medida en AC-18 |
| Fracción del surtido asequible | 20-35% | 53,8 | **59,3** | OUT |
| Oro sobrante al terminar la run | < 15% | 17,7 | **20,9** | OUT |
| Runs que llegan a un mercado sin poder comprar | 10-25% | 78,2 | **61,2** | OUT, pero mejora 17 puntos |
| *Plantilla al terminar* | *—* | *13,0* | ***10,5*** | *INFO* |
| *Huecos de plantilla comprados por run* | *—* | *—* | ***0,88*** | *INFO* |
| *Lesiones graves por run* | *—* | *1,42* | ***2,08*** | *INFO* |
| *Lesiones propias por partido* | *—* | *0,22* | ***0,31*** | *INFO* |
| *Canteranos fichados por run* | *—* | *3,31* | ***1,23*** | *INFO* |
| *Perks y objetos comprados por run* | *—* | *2,57 y 2,85* | ***1,66 y 1,95*** | *INFO* |

**AC-21. La curva de la ADR 0033 no se ha movido.** Remedida con el mismo lote de la puerta (semilla 1,
32 plantillas × 4 partidos, 7.680 partidos): **26,7 / 71,6 / 84,8 / 92,2**, **10,3 / 41,7 / 66,6 / 76,7**
y **6,1 / 28,8 / 46,2 / 57,7**, cifra por cifra las mismas de §19.1, con las mismas once celdas de doce en
banda sin tolerancia. Era lo esperado —la puerta se mide con partidos directos build-contra-jefe y ni las
builds ni los jefes se han tocado— pero había que comprobarlo antes de dar el paquete por bueno.

**AC-22. Lo que sigue abierto.** La ventaja de la contextual (5,6 de 8) y las tres métricas de escasez
siguen fuera de banda, con las mismas causas de §19 (Z-K: `affordableShare` y `brokeMarketRunShare` se
oponen entre sí, y la aritmética entera de la escala de oro). La novedad de este paquete es que las tres
se mueven **en la dirección correcta** por primera vez, sin tocar un solo precio: acortar la plantilla ha
hecho más por la escasez que la escala de oro entera.

## 21. Decisiones de implementación del paquete AD: morir estando sano y 1 de 2 (ADR 0048 y 0049)

Dos cambios que tocan el mismo equilibrio y por eso se implementan juntos y se miden una sola vez: **un
jugador sano puede morir** (ADR 0048, que sustituye a la ADR 0047) y **el partido de liga ofrece dos
opciones de recompensa en vez de tres** (ADR 0049).

**Resultado de una línea**: las muertes por run pasan de **0,64 a 1,53** y entran en la banda nueva
(1,5-3); la curva de la ADR 0033 se recalibra y vuelve a dejar **once celdas de doce** en banda; el acto 1
sigue siendo el taller; y las dos apuestas que las ADR hacían **no se cumplen y están medidas**: la
ventaja de la doctrina contextual cae de +5,6 a **−0,2** en vez de subir, y la escasez **no se corrige
sola** (58,1 → 56,6 con objetivo 20-35).

### 21.1. La tirada de muerte: alcanzar deja de ser matar

**AD-1. Retirar la condición no bastaba: la habría convertido en una siega.** `IsLethalVictim` exigía tres
cosas —ser rival, estar en el campo y **no estar sano**—. Quitar la tercera a secas convierte
`skullsplitter` en un exterminio: su efecto va sobre `opposingTeam`, así que en el saque inicial habría
matado a **los siete** titulares a la vez. La ADR 0048 pide que la muerte sea *rara y memorable*, no que
el partido empiece con un entierro colectivo, así que alcanzar deja de ser matar y pasa a ser **tirar**:

- Cada perk letal declara su probabilidad base en el dato (`lethalChance`, base 10.000). Es la palanca con
  la que se sube y se baja la letalidad al medir, y el cargador la exige a todo perk con `lethal: true`
  (y la prohíbe al resto): un perk que se anuncia como letal en el ojeo y no puede matar sería el peligro
  inexistente que RF-012d prohíbe tanto como el peligro callado.
- La tirada la resuelve `Sim.Perks.Lethality`, **puro y entero**, y la usan el motor
  (`MatchEngine.LethalChanceAgainst`) y el indicador de riesgo (`RunEngine.LethalRisks`). Que sea el mismo
  código no es comodidad: es lo que impide que el número prometido y el dado real se separen.
- Los tres factores de `tuning.injury.lethality` son, a propósito, **las tres cosas que el jugador decide
  antes de confirmar la alineación** (RF-012c): en qué estado alinea, a quién alinea y dónde lo coloca.

| Factor | Cómo entra | Valor |
|---|---|---|
| Estado de la víctima | multiplicador | sano **100**, lesión leve **800**, lesión grave **2500** |
| Resistencia | multiplicador `100 + 6·(fuerza del portador − aguante de la víctima)`, acotado | **20-500** |
| Cercanía | `100 − 25` por casilla de emparejamiento, con suelo | **100 → 0** a partir de 4 |
| Techo | acotado al final | `maxChance` **8000** |

**AD-2. La resistencia es un multiplicador y no un sumando, y está medido por qué.** Con la fórmula
relativa de la ADR 0041 escrita como suma —`base + 2·(fuerza − aguante)`— sobre una base de miles de
puntos, cuarenta puntos de aguante movían la tirada un 1,6%: **elegir a quién alinear no cambiaba el
número**. Medido así: 1,62 muertes leyendo el indicador y 1,82 sin leerlo, indistinguible. Multiplicando,
un aguante de 75 recibe la mitad que uno de 35.

**AD-3. La distancia de emparejamiento, no la del saque inicial.** `Lethality.Matchup` no mide dónde están
los dos al empezar —ahí cada equipo está en su mitad y todo el mundo está lejos de todo el mundo— sino
**quién se va a encontrar con quién**: refleja al portador sobre el eje de colocación, de modo que la
distancia en columnas es `|columna de la víctima + columna del portador − 7|` sobre las columnas locales
0..7 de RF-040..045. Un delantero rival amenaza a mi portero y a mis centrales; mi delantero amenaza a los
suyos. Las filas no se reflejan: la banda derecha del campo es la misma para los dos equipos. Y depende
**solo de las casillas-hogar**, nunca de dónde estén cuando el perk se dispara, que es lo que hace que el
indicador previo sea exacto y no una estimación.

**AD-4. Un perk letal MARCA a un rival por activación, no siega el equipo.**
`tuning.injury.lethality.victimsPerActivation` vale **1**, y el marcado es el que **peor lo tiene** (mayor
probabilidad de morir; a igualdad, menor id, RT-041). Concentrar el peligro en el eslabón más débil es lo
único que permite que la alineación lo **reduzca** —quitar ese eslabón baja el número— en vez de repartir
un impuesto que nadie puede esquivar. También es lo que hace legible el indicador: en la pantalla de
alineación hay **un** nombre señalado, no siete barras.

**AD-5. La tirada usa los atributos BASE, no los efectivos.** El indicador tiene que poder calcular el
mismo número antes del partido, cuando no se ha aplicado ningún modificador de perk. Si la letalidad
dependiera de lo que pase a mitad de partido, el número prometido y el dado real se separarían sin que
nada se pusiera rojo.

**AD-6. Los perks letales, con su probabilidad y su reparto.** El reparto baja de **cuatro portadores por
acto a tres** por la condición 2 (AD-9).

| id | `lethalChance` | trigger | dónde |
|---|---|---|---|
| `skullsplitter` | **5000** | MATCH_START | acto 3 (orcos) |
| `marrow_thirst` | **3900** | MATCH_START | actos 2 (orcos, no-muertos) y 3 (enanos) |
| `second_wound` | **9000** | INJURY | actos 2 (humanos) y 3 (no-muertos) |
| `iron_studs` | **760** | TACKLE | acto 3 (enanos) |

`iron_studs` es un orden de magnitud más barato porque se dispara en **cada entrada** en el tercio rival,
no una vez por partido: su número no es comparable con los otros tres.

### 21.2. Las cinco condiciones de la ADR 0048, una a una y con su medida

**AD-7. Anticipación (condición 1): se cumplía y se cumple.** `Scouting.LethalPerks` destaca la amenaza en
el ojeo con nombre de portador (RF-013) y `DescriptionGenerator` cuelga el sufijo letal de la descripción
generada (RT-035). El texto **cambia** porque lo que hace el perk ha cambiado: donde decía *«Si alcanza a
un rival que ya no está sano, lo mata»* ahora dice *«Marca a un rival por activación, el que peor lo
tiene, y puede matarlo aunque esté sano»*.

**AD-8. Reducción (condición 3): existe, es numérica, y es la mitad de lo que la ADR necesita.** Es la
condición que la ADR declara más importante y la única que había que construir entera.

- `RunEngine.LethalRisks(estado, nodo, catálogo, sistemas, alineación)` devuelve la probabilidad de morir
  **de cada titular**, en base 10.000, contra el rival concreto de ese nodo y con la colocación concreta
  que se le pase. `RunEngine.LineupWarnings` la incorpora como `LineupWarningKind.LethalOpponentRisk` con
  su `Risk`, que es lo que la pantalla de alineación tiene que pintar.
- **Cambiar la alineación cambia el número**, y se comprueba en las tres palancas por separado
  (`LethalRiskTests`): mover al marcado lejos del portador le baja el riesgo, cambiar quién juega cambia
  el total, y estar tocado multiplica el número del mismo jugador en la misma casilla.
- **Y aquí está el hallazgo incómodo.** La métrica que la propia ADR declara decisiva —dos políticas
  idénticas salvo que una atiende al indicador— **no se separa claramente**. Con el lote de referencia
  (500 runs por doctrina, semilla 1): leyendo el informe y el número, **1,53** muertes por run; sin leer
  nada (`--ignore-scouting`), **1,62**. Son **5,6 puntos porcentuales de mejora**, dentro de dos
  desviaciones típicas de la muestra.

  El barrido de `--risk-aversion` (300 runs, semilla 1, todo lo demás idéntico; el dial es
  `RunPolicyOptions.DeathCostPercent`, cuánto descuenta la política el valor de un titular por su
  exposición) dice **por qué**, y es más informativo que la métrica sola:

  | `--risk-aversion` | qué hace la política | Muertes/run |
  |---|---|---|
  | **−1000** | busca el riesgo: pone al frágil donde muerde | **1,98** |
  | −400 | lo busca a medias | 1,75 |
  | **0** | ignora el número (alinea por valor deportivo) | **1,64** |
  | +150 (por defecto) | lo obedece | 1,58 |
  | +400 | lo obedece más | 1,61 |
  | **+1000** | lo obedece por encima de todo | **1,56** |

  **El número tiene rango —un 27% entre el mejor y el peor uso del mismo indicador— pero está repartido
  de forma asimétrica: exponer al eslabón débil cuesta +21%, y protegerlo solo ahorra −5%.** La causa está
  medida y es que **una alineación elegida por valor deportivo ya está cerca de la más segura**: el valor
  de un jugador incluye su aguante, así que la política que solo quiere ganar partidos ya deja fuera a los
  frágiles sin proponérselo. La agencia existe en la dirección «puedes estropearlo» y está casi saturada
  en la dirección «puedes protegerte».

  Es, literalmente, el caso que la ADR 0048 anticipa: *«si no se separan, el azar no tiene agencia y la
  decisión hay que revisarla»*. La lectura honesta no es que no haya agencia —hay un 27% de rango— sino
  que **la mitad útil de esa agencia es pequeña y el jugador la obtiene gratis**, que es la misma
  advertencia que AC-15 hizo con la contrajugada de la ADR 0046. Queda abierto como **AD-A**.

**AD-9. Evitación (condición 2): se cumplía a medias y ha habido que pagar por ella.** El mapa es
ramificado, pero eso no basta: si **todos** los rivales de una capa matan, no hay ruta que esquivar. Con
el reparto de la ADR 0046 —cuatro rivales letales de cinco por acto— solo el **30%** de los nodos letales
tenía al lado una alternativa que no mata. El reparto baja a **tres de cinco** y sube al **50%**; con dos
de cinco sube al **77%**, pero entonces las muertes se quedan en **0,82 por run** y no llegan a la banda.

El 100% **no es alcanzable** ni con el mejor reparto: `MapGenerator` asigna rivales con un cursor
consecutivo sobre la baraja barajada del acto, y tres marcados en un ciclo de cinco tienen por fuerza dos
adyacentes. Cerrarlo del todo exige bajar a dos letales por acto **y** añadir al generador la restricción
"dos letales nunca en la misma capa", y cuesta la banda de muertes. Se elige el 50% con la banda dentro y
queda anotado como **AD-B**.

**AD-10. Recuperación (condición 4): el objeto del muerto vuelve al inventario.** `RunState` gana un
**almacén** (`itemStock:<id>` en `Counters`, por la misma razón que los huecos de inscripción: añadir un
sistema sin subir la versión del esquema, RT-030) y `MatchResolution` mete ahí el equipamiento del caído
antes de marcarlo muerto. La decisión `EquipStoredItem` lo saca y se lo pone a un vivo **sin pagar nada**
—ya estaba pagado— y en cualquier nodo, porque rehacer una build no puede depender de estar en un mercado.
Medido: **0,89 objetos recuperados por run**, es decir que la recuperación se ejercita en casi todas las
runs y no es una regla de papel.

**AD-11. Rareza (condición 5): la ADR se contradice consigo misma, y hay que decirlo.** La misma ADR pide
dos cosas incompatibles: que las muertes por run estén en **1,5-3** y que haya *«una muerte por perk letal
cada dos o tres runs»*, que son **0,3-0,5 por run**. Un factor de cinco. Y no hay margen para acomodar las
dos, porque la vía 1 de RF-093 (alinear a un lesionado grave) aporta **cero**: `deathsAct1` mide **0,00** y
en el acto 1 no hay ningún rival letal, así que **las 1,53 muertes son todas de perk letal**. Se aplica la
banda —es la que la ADR fija explícitamente y la que mide la puerta— y se anota la contradicción como
**AD-C**: si lo que el revisor quiere es la frase, la banda tiene que bajar a 0,3-0,5 y `lethalChance` con
ella.

### 21.3. La recompensa de liga baja a 1 de 2 (ADR 0049)

**AD-12. Es un número en datos.** `economy.nodeRewards.league.options` pasa de 3 a **2**; élite y jefe
mantienen sus tres opciones, la rareza mejorada y las dos elecciones del jefe. El escalonado de la ADR
0043 deja de ser solo de oro y pasa a ser **de calidad de decisión**, que es lo que la ADR 0049 pide.

**AD-13. Lo que la run trae ahora a cada puerta.** Medido con el lote de referencia:

| | Al jefe del acto 1 | Al del acto 2 | Al final |
|---|---|---|---|
| Antes (§20) | 3,8 perks · 2,2 objetos | 8,2 · 4,7 | 13,0 · 6,7 |
| **Ahora** | **3,2 · 1,8** | **6,2 · 3,7** | **8,5 · 4,8** |

La caída del acto 3 (−34% en perks) es **doble**: menos elecciones gratis y la carnicería de la ADR 0048,
que se lleva jugadores **con sus perks puestos**. Las elecciones cobradas por run bajan de 9,8 a **9,0**
—el número de nodos no cambia, solo la calidad de cada elección— y los rechazos suben de 0,43 a **0,42**
por run (4,4% de las elecciones): con dos opciones falla más a menudo que ninguna encaje, pero también se
llega con menos build montada y por tanto con más slots libres, y las dos cosas se compensan.

**AD-14. La compensación ha ido por el mercado, como la ADR manda, y no por devolver la tercera opción.**
`market.perkOffers` y `market.itemOffers` pasan de 4 a **5**: más surtido, ni mejor rareza ni precios más
bajos, que son las otras dos palancas que la ADR autoriza. Efecto medido (300 runs): `purchasesPerMarket`
0,83 → **0,88**, `sinksAffordablePerAct` 1,99 → **2,00** (vuelve a su banda) y `affordableShareAtMarket`
58,1 → **56,4**. Es una compensación pequeña a propósito: el objetivo de la ADR 0049 es **mover peso de lo
gratis a lo comprado**, no reponer el total.

### 21.4. La curva de la ADR 0033, recalibrada

Bajar la calidad de las recompensas baja la build final, así que la curva había que rehacerla entera.
Muestra idéntica a la de §19.1: semilla 1, 32 plantillas × 4 partidos por celda y raza, **640 partidos por
celda**, 7.680 en total, 34 s, `dotnet run --project Balance -c Release -- --boss-gate --rosters 32 --runs 4 --seed 1`.

| Puerta | Incoherente | Correcta | Buena | Muy buena |
|---|---|---|---|---|
| **Acto 1** `grimhold_guns` | **26,7** (20-35) | **71,6** (65-80) | **82,3** (75-88) | **89,4** (85-95) |
| **Acto 2** `the_hunt` | **10,3** (< 15) | **41,7** (35-50) | **67,3** (60-72) | **72,8** (72-85) |
| **Acto final** `eternal_crown` | **8,9** (< 10) | **30,3** (15-28) ⚠ | **43,4** (40-55) | **57,8** (55-70) |

**Once celdas de doce siguen en banda sin tolerancia**, la escalera sigue siendo monótona en los tres
jefes, y la celda que pasa por el margen de medida (±2,5) es **la misma de siempre**: *correcta* contra el
jefe final, ahora 2,3 puntos por encima de su techo en vez de 0,8 (AB-D).

**AD-15. Qué se movió y por qué solo se movió eso.**

- **`data/balance/groups.json`**: las densidades de los escalones *correcta*, *buena* y *muy buena* bajan
  con la build real (AD-13). El escalón **incoherente se queda como estaba** (6 / 8 / 10 perks), y no es
  un descuido: quien no rechaza nada coge **una** recompensa por nodo tanto si le ofrecen dos como si le
  ofrecen tres, así que la ADR 0049 le baja la **calidad** de lo que coge pero no la **cantidad**. Es la
  misma asimetría que AB-3 documentó al revés.
- **`eternal_crown` de calidad 28 a 24**, el único jefe que ha habido que tocar. Con 28 y la build nueva,
  *muy buena* se quedaba en **49,1** frente al 55 que pide su banda. Ablandarlo sube también la fila
  *correcta*, y no hay forma de evitarlo: está medido que esa celda **no depende de cuántas piezas lleve
  la build** (§19.1) y que subir la densidad de *muy buena* de 11 a 14 perks no la mueve (48,4 contra
  49,1: el escalón superior está **saturado** contra ese jefe). Las dos configuraciones y su coste:

  | `eternal_crown` | Incoherente | Correcta | Buena | Muy buena |
  |---|---|---|---|---|
  | calidad 28 | 7,5 | 28,6 ✓ | 40,8 | **49,1 ✗** (pide ≥55) |
  | **calidad 24** (elegida) | 8,9 | **30,3 ⚠** | 43,4 | **57,8 ✓** |

  Se elige la segunda: que el techo de la escalera sea alcanzable importa más que dos puntos de la fila
  *correcta*, porque es lo que hace que construir muy bien sirva de algo.
- **Los otros dos jefes no se tocan.** El del acto 1 mide 71,6 para una build correcta, exactamente la
  misma cifra de §19.1: **el acto 1 sigue siendo el taller** (ADR 0043) y el jugador sigue llegando con
  build a la primera puerta. Era la condición que la ADR 0049 pone por encima de todo lo demás.

### 21.5. Las métricas de §10, antes y después

Lote de referencia: **500 runs por doctrina, semilla 1**, cinco razas repartidas por igual (1.500 runs,
~19.900 partidos, 100 s), `dotnet run --project Balance -c Release -- --full-runs 500 --seed 1`.

| Métrica | Rango | Antes (AC) | **Ahora** | Estado |
|---|---|---|---|---|
| **Muertes por run** | **1,5-3** (antes 0,5-2) | 0,64 | **1,53** | **IN** con la banda nueva |
| Tasa de victoria de la run (contextual) | 20-30% | 22,6 | **20,2** | IN |
| Derrotas por bajar de 5 jugadores | < 35% | 2,07% | **0,50%** | IN |
| Duración de una run completa | 18-22 | 19,97 | **20,00** | IN |
| Sumideros que paga el oro de un acto | 2-3, nunca todos | 2,08 | **2,00** | IN, en el filo |
| **Ventaja de la contextual sobre las dos puras** | ≥ 8 puntos | +5,6 | **−0,2** | **OUT y peor** |
| Compras por visita al mercado | 0,5-2 (cota) | 0,76 | **0,87** | cota IN |
| Fracción del surtido asequible | 20-35% | 59,3 | **56,6** | OUT |
| Oro sobrante al terminar la run | < 15% | 20,9 | **21,9** | OUT |
| Runs que llegan a un mercado sin poder comprar | 10-25% | 61,2 | **63,4** | OUT |
| *Muertes por acto* | *—* | *—* | ***0,00 / 0,87 / 0,66*** | *INFO* |
| *Objetos recuperados de un muerto por run* | *—* | *—* | ***0,89*** | *INFO* |
| *Plantilla al terminar* | *—* | *10,5* | ***10,4*** | *INFO* |
| *Lesiones graves por run* | *—* | *2,08* | ***1,83*** | *INFO* |
| *Lesiones propias por partido* | *—* | *0,31* | ***0,28*** | *INFO* |
| *Perks en el once al terminar* | *—* | *—* | ***6,5*** | *INFO* |
| *Recompensas cobradas / rechazadas por run* | *—* | *—* | ***9,02 / 0,42*** | *INFO* |

**AD-16. Dónde caen las muertes, y por qué el acto 1 está a cero.** El reparto por acto es **0,00 / 0,87 /
0,66**, y el cero del acto 1 es una propiedad del catálogo, no del azar: la ADR 0046 dejó el acto 1 sin
ningún rival letal porque es el taller. Que sea **exactamente** cero dice además algo que no se sabía: la
**vía 1 de RF-093** —alinear a un lesionado grave sin tratar y volver a lesionarlo— no produce ni una
muerte en 500 runs, porque una política razonable trata o sienta antes de llegar ahí. Las 1,53 muertes son
todas de perk letal.

**AD-17. Las derrotas siguen cayendo donde la ADR 0043 quiere.** 30,8% en el acto 1, **52,4% en el acto
2** y 16,8% en el 3. El 99,75% son contra un jefe; quedarse sin plantilla es el 0,25%, menos que antes
(2,07%) porque el almacén de objetos y el reparto letal más corto compensan la subida de muertes. Una run
perdida cuesta el **60,9%** de una ganada (12,0 partidos frente a 20), igual que antes (AB-C).

**AD-18. La escasez NO se corrige sola, y era la pregunta explícita.** La hipótesis era que con más
muertes y menos recompensas gratis, fichar y curar subirían de valor y `affordableShareAtMarket` bajaría
hacia su banda 20-35. Medido: **59,3 → 56,6**, dos puntos y medio, y el surtido ampliado de AD-14 es
responsable de la mayor parte. El oro por run baja de 56,2 a **52,4** y el gasto en mercado de 37,6 a
**34,0**, así que el jugador compra un poco menos de un surtido un poco mayor: la fracción asequible se
mueve, pero no cambia de orden de magnitud. **Tocar precios sigue pendiente y sigue chocando con
`brokeMarketRunShare`** (Z-K, AB-F): las dos se oponen y ninguna configuración cumple las dos.

**AD-19. La apuesta de la ADR 0049 sobre las doctrinas no se ha cumplido: ha ido al revés.** La ADR
razonaba que, si la run repartía menos elecciones gratis, el mercado decidiría más build y la doctrina que
compra con criterio se separaría de las dos puras. Medido:

| Doctrina | Antes (AC) | **Ahora** | Compras por mercado |
|---|---|---|---|
| **Contextual** | 22,6 | **20,2** | 0,87 |
| Ahorradora | 17,0 | **20,4** | 0,67 |
| Gastadora | 13,2 | **14,8** | 1,17 |

`contextualAdvantage` cae de **+5,6 a −0,2**: la contextual ya no le saca nada a la ahorradora. La causa
que los datos sugieren es que **la ventaja de la contextual no estaba en comprar, estaba en tener con qué
elegir**: su regla distintiva es repartir el oro entre los mercados que le quedan antes del jefe y, dentro
del presupuesto, preferir el raro; con la build más pobre y el surtido igual de caro, el presupuesto casi
nunca alcanza al raro y su regla degenera en la de la ahorradora. La gastadora **sí** se separa de las
otras dos (−5,4 puntos), que es la mitad del criterio de la ADR 0037 que sigue vivo. Queda abierto como
**AD-D**, y es el hallazgo que más pesa en contra de la ADR 0049: la decisión sube el peso del mercado en
la build, pero **no** consigue que comprar con criterio sea distinto de ahorrar.

**AD-20. RT-056 no se mueve, como debía.** Los equipos de referencia no llevan perks, así que ningún
cambio de este paquete puede alcanzarlos, pero había que comprobarlo: 1.000 partidos, semilla 1 —cambios
de posesión 23,76; cadena de pase 2,27; tiros 11,83; entradas 9,78; **lesiones por partido 0,73** (banda
0,30-0,80); reparto de resultados 76,4%; tercio máximo 41,4%— cifra por cifra las de siempre.
