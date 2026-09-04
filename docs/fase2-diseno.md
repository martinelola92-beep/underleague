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

**Y-8. Hueco: `RunEngine` no aplica los modificadores, y no revela el del jefe si pierdes.** *(defecto del paquete W, anotado, no corregido aquí)* `RunEngine.BuildMatch` construye el `MatchSetup` sin pasar por `IRunSystems`, así que no hay dónde enganchar `BossRules.Apply`: mientras no se añada esa línea a `EnterMatch`, el partido de jefe que juega el bucle de run va **sin** su modificador. `BossRunSystems.BuildBossMatch` es el punto de entrada correcto y es lo que usan el informe de ojeo y la puerta. Y `EnterMatch` marca `WithBossModifierRevealed(true)` **después** de comprobar el desenlace, así que perder contra el jefe deja el modificador sin registrar — justo el caso en el que el jugador ya ha pagado la sorpresa, contra RF-014b. Las dos son de una línea en `Sim/Run/RunEngine.cs`, fuera de las fronteras de este paquete.

### La curva medida

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

**Y-11. Solo hay un perk de escalado de verdad en todo el catálogo, y eso deforma «muy buena».** De los seis perks con `accumulatesAcrossMatches`, cinco (`clean_sheet_legacy`, `poacher_instinct`, `silky_veteran`, `scar_tissue`, `deathless_march`) **se realimentan dentro del mismo partido** —suben su contador con cada parada, gol o regate—, así que llegan al tope solos y el contador con el que entran apenas importa. El único cuyo contador solo sube **entre** partidos es `battle_reader` (+1 por partido, tope 5). Consecuencia: la única forma de expresar «escalado acumulado durante toda la run» con el catálogo de hoy es **apilar `battle_reader` en varios titulares**, y así está construida `*_excellent` (cuatro copias al tope). Es honesto —es lo que un jugador optimizando haría— pero es un síntoma: el catálogo no tiene curva de progresión propia. Mientras siga así, un modificador `singleCopy` en el acto 1 es inviable (probado: hunde «muy buena» a 5 puntos de «buena» y la fila deja de tener solución), y por eso el jefe del acto 1 usa `butterfingers`.

**Y-12. Falta el equipamiento, y con él la mitad de lo que la ADR llama «muy buena».** El escalón superior de la ADR es «buena + **equipada** + escalado acumulado». Los objetos existen desde el paquete X (`data/items/`), pero X-2 deja anotado que ni `PlayerDefinition` ni `Simulator.Run` reciben todavía el objeto de un jugador: **un objeto equipado no surte efecto en el partido**. La celda «muy buena» está medida por tanto **sin equipamiento**, con perks, colocación, contadores y dos o tres raros. Cuando el objeto llegue al motor habrá que **volver a calibrar los tres jefes**, porque el escalón superior subirá y las tres filas se desplazan a la vez.

**Y-13. Hallazgos de medida que condicionan cualquier ajuste futuro.** Aislados quitando un perk cada vez y volviendo a medir (640 partidos por comparación):
- Los perks de alcance **`team`** y los del **portero** son casi todo lo que se mide. `center_conductor` (+5 de intercepción al equipo) vale **10 puntos** de tasa de victoria; los perks del portero, unos 30 entre los dos.
- Los perks de un solo objetivo sobre un jugador de campo son **invisibles**: quitar `covering_shadow`, `high_press_trigger`, `pivot_duo` o `natural_leader` de una build no cambió **ni un partido** de 4.800. Cuadra con el orden de la §8.5 de `fase1b-resultados.md`, pero es más extremo de lo que allí se dice y conviene tenerlo presente antes de escribir el catálogo de lanzamiento.
- `markStar` tuvo que **excluir al portero**: con él dentro, el desempate por suma de atributos lo elegía casi siempre y el modificador pasaba a ser «el jefe te anula el portero», que vale 30 puntos y no mide nada de la construcción.

**Y-14. La medición de `/Balance` y la de la puerta son el mismo código.** `BossGateMetrics.PlayCell` (en `/Sim`, pura, sin E/S) juega la celda y `BossGateMetrics.Compute` la contrasta con `gate.targets`; `/Balance --boss-gate` y `Sim.Tests` solo se diferencian en quién lee los ficheros. Las plantillas de jefe de una misma raza son las mismas en los cuatro escalones (la escalera se compara contra el mismo rival) y distintas entre razas (si no, la varianza de esas plantillas no se promedia al agregar y la celda se mueve 10 puntos al cambiar de semilla).
