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
- **Cuatro carriles** (ADR 0053, §24): entrada única y apertura 1 → 2 → 4, movimiento solo a carriles contiguos —que es lo que le da memoria a la ruta— y jefe de ancho 1. El acto dibuja ~36 nodos y el jugador recorre 11.
- Tipos: `LeagueMatch`, `EliteMatch`, `Market`, `Clinic`, `Training`, `Event`, `Boss`. (`Workshop` en fase 3.)
- **Un mercado alcanzable en dos saltos desde cualquier punto** (RF-011b): el generador lo garantiza por construcción —capas de mercado cada 2, con sus carriles dominando los de la capa anterior (§24)— y hay un test que lo comprueba sobre 1.000 mapas. Desde la ADR 0053 el mercado **no ocupa la capa entera**: se puede esquivar, y eso es lo que devuelve la decisión de RF-002d.
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

**Resultado de una línea**: las muertes por run pasan de **0,64 a 1,51** y entran en la banda nueva
(1,5-3); la curva de la ADR 0033 se recalibra y vuelve a dejar **once celdas de doce** en banda; el acto 1
sigue siendo el taller; y las dos apuestas que las ADR hacían **no se cumplen y están medidas**: la
ventaja de la doctrina contextual se queda en **+0,2** en vez de subir de +5,6 a 8, y la escasez **no se
corrige sola** (59,3 → 56,7 con objetivo 20-35).

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
  (500 runs por doctrina, semilla 1): leyendo el informe y el número, **1,51** muertes por run; sin leer
  nada (`--ignore-scouting`), **1,57**. Son **3,8 puntos porcentuales de mejora**, dentro del ruido de la
  muestra. Lo que sí se separa con claridad es la **tasa de victoria**: 21,0% leyendo contra 17,6% sin
  leer, tres puntos y medio. La contrajugada paga, pero paga en partidos ganados y no en cuerpos, que es
  justo lo contrario de lo que la ADR 0048 necesita para sostenerse.

  El barrido de `--risk-aversion` (300 runs, semilla 1, todo lo demás idéntico; el dial es
  `RunPolicyOptions.DeathCostPercent`, cuánto descuenta la política el valor de un titular por su
  exposición) dice **por qué**, y es más informativo que la métrica sola:

  | `--risk-aversion` | qué hace la política | Muertes/run | Tasa de victoria |
  |---|---|---|---|
  | **−1000** | busca el riesgo: pone al frágil donde muerde | **1,90** | 19,7 |
  | **0** | ignora el número (alinea por valor deportivo) | **1,58** | 17,7 |
  | +150 (por defecto) | lo obedece | 1,51 (500 runs) | 21,0 |
  | **+1000** | lo obedece por encima de todo | **1,54** | 20,7 |

  **El número tiene rango —un 23% entre el mejor y el peor uso del mismo indicador— pero está repartido
  de forma asimétrica: exponer al eslabón débil cuesta +20%, y protegerlo solo ahorra −3%.** La causa está
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
Medido: **0,91 objetos recuperados por run**, es decir que la recuperación se ejercita en casi todas las
runs y no es una regla de papel.

**AD-11. Rareza (condición 5): la ADR se contradice consigo misma, y hay que decirlo.** La misma ADR pide
dos cosas incompatibles: que las muertes por run estén en **1,5-3** y que haya *«una muerte por perk letal
cada dos o tres runs»*, que son **0,3-0,5 por run**. Un factor de cinco. Y no hay margen para acomodar las
dos, porque la vía 1 de RF-093 (alinear a un lesionado grave) aporta **cero**: `deathsAct1` mide **0,00** y
en el acto 1 no hay ningún rival letal, así que **las 1,51 muertes son todas de perk letal**. Se aplica la
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
| **Ahora** | **3,2 · 1,8** | **6,3 · 3,7** | **8,4 · 4,8** |

La caída del acto 3 (−34% en perks) es **doble**: menos elecciones gratis y la carnicería de la ADR 0048,
que se lleva jugadores **con sus perks puestos**. Las elecciones cobradas por run bajan de 9,8 a **9,0**
—el número de nodos no cambia, solo la calidad de cada elección— y los rechazos se quedan en **0,41**
por run (4,4% de las elecciones), contra 0,43 antes: con dos opciones falla más a menudo que ninguna encaje, pero también se
llega con menos build montada y por tanto con más slots libres, y las dos cosas se compensan.

**AD-14. La compensación ha ido por el mercado, como la ADR manda, y no por devolver la tercera opción.**
`market.perkOffers` y `market.itemOffers` pasan de 4 a **5** —más surtido— y `clinicCost` baja de 10 a
**8** —precios más bajos—, que son dos de las tres palancas que la ADR autoriza; la tercera, mejor rareza,
no se toca. Efecto medido: `purchasesPerMarket` 0,83 → **0,88** y `affordableShareAtMarket` 58,1 →
**56,7**.

La bajada de la clínica no es cosmética y tiene dos razones, una de diseño y otra de puerta. La de diseño:
con las muertes en 1,5 por run, **curar tiene que estar más al alcance** —es la contrajugada del desgaste
y el sumidero que la ADR 0046 predijo que subiría—. La de puerta: `sinksAffordablePerAct` había caído a
**1,96** con la muestra de 60 runs, fuera de la banda 2-3 de RF-114k, porque la run gana menos oro por
acto; con la clínica en 8 vuelve a **2,12**. Ninguna otra palanca lo arreglaba: el sumidero de mercado
cuesta ~36 de oro contra los 22-29 que gana un acto, así que abaratar los comunes lo suficiente para que
entrara habría disparado `affordableShareAtMarket`, que ya está muy por encima de su banda.

Es una compensación pequeña a propósito: el objetivo de la ADR 0049 es **mover peso de lo gratis a lo
comprado**, no reponer el total.

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
20.044 partidos, 101 s), `dotnet run --project Balance -c Release -- --full-runs 500 --seed 1`.

| Métrica | Rango | Antes (AC) | **Ahora** | Estado |
|---|---|---|---|---|
| **Muertes por run** | **1,5-3** (antes 0,5-2) | 0,64 | **1,51** | **IN** con la banda nueva |
| Tasa de victoria de la run (contextual) | 20-30% | 22,6 | **21,0** | IN |
| Derrotas por bajar de 5 jugadores | < 35% | 2,07% | **0,51%** | IN |
| Duración de una run completa | 18-22 | 19,97 | **20,00** | IN |
| Sumideros que paga el oro de un acto | 2-3, nunca todos | 2,08 | **2,12** | IN |
| **Ventaja de la contextual sobre las dos puras** | ≥ 8 puntos | +5,6 | **+0,2** | **OUT y peor** |
| Compras por visita al mercado | 0,5-2 (cota) | 0,76 | **0,88** | cota IN |
| Fracción del surtido asequible | 20-35% | 59,3 | **56,7** | OUT |
| Oro sobrante al terminar la run | < 15% | 20,9 | **21,0** | OUT |
| Runs que llegan a un mercado sin poder comprar | 10-25% | 61,2 | **62,8** | OUT |
| *Muertes por acto* | *—* | *—* | ***0,00 / 0,86 / 0,65*** | *INFO* |
| *Objetos recuperados de un muerto por run* | *—* | *—* | ***0,91*** | *INFO* |
| *Plantilla al terminar* | *—* | *10,5* | ***10,5*** | *INFO* |
| *Lesiones graves por run* | *—* | *2,08* | ***1,85*** | *INFO* |
| *Lesiones propias por partido* | *—* | *0,31* | ***0,29*** | *INFO* |
| *Perks en el once al terminar* | *—* | *—* | ***6,6*** | *INFO* |
| *Recompensas cobradas / rechazadas por run* | *—* | *—* | ***9,06 / 0,41*** | *INFO* |
| *Oro por run: ganado · mercado · clínica · inscripción · rerolls · sobrante* | *—* | *—* | ***52,5 · 35,2 · 3,7 · 10,5 · 2,2 · 11,0*** | *INFO* |

**AD-16. Dónde caen las muertes, y por qué el acto 1 está a cero.** El reparto por acto es **0,00 / 0,86 /
0,65**, y el cero del acto 1 es una propiedad del catálogo, no del azar: la ADR 0046 dejó el acto 1 sin
ningún rival letal porque es el taller. Que sea **exactamente** cero dice además algo que no se sabía: la
**vía 1 de RF-093** —alinear a un lesionado grave sin tratar y volver a lesionarlo— no produce ni una
muerte en 500 runs, porque una política razonable trata o sienta antes de llegar ahí. Las 1,51 muertes son
todas de perk letal.

Y el reparto por acto no es plano: **el acto 2 mata más que el 3** (0,86 contra 0,65) pese a tener el
mismo número de rivales letales, porque solo el 34% de las runs llega al acto 3 y las que llegan lo hacen
con plantilla tratada. Encaja con la ADR 0043: el acto 2 es el de gestión y es donde el desgaste muerde.

**AD-17. Las derrotas siguen cayendo donde la ADR 0043 quiere.** 31,7% en el acto 1, **51,9% en el acto
2** y 16,5% en el 3. El 99,49% son contra un jefe; quedarse sin plantilla es el 0,51%, **menos** que antes
(2,07%) aunque haya más muertes, y la causa es el reparto letal más corto (tres rivales por acto en vez de
cuatro): se muere más veces pero repartido entre más partidos, y ninguna run se queda sin once. Una run
perdida cuesta el **60,5%** de una ganada (11,9 partidos frente a 20), igual que antes (AB-C).

**AD-18. La escasez NO se corrige sola, y era la pregunta explícita.** La hipótesis era que con más
muertes y menos recompensas gratis, fichar y curar subirían de valor y `affordableShareAtMarket` bajaría
hacia su banda 20-35. Medido: **59,3 → 56,7**, dos puntos y medio, y el surtido ampliado de AD-14 es
responsable de la mayor parte. El oro por run baja de 56,2 a **52,5** y el gasto en mercado de 37,6 a
**35,2**, así que el jugador compra un poco menos de un surtido un poco mayor: la fracción asequible se
mueve, pero no cambia de orden de magnitud. Y la clínica, que la ADR 0046 predijo que subiría de valor,
**baja de uso**: 4,7 → 3,7 de oro por run pese a costar menos, porque lo que mata ahora no es la lesión
sin tratar sino el perk rival, y contra eso la clínica no cura. **Tocar precios sigue pendiente y sigue
chocando con `brokeMarketRunShare`** (Z-K, AB-F): las dos se oponen y ninguna configuración cumple las
dos.

**AD-19. La apuesta de la ADR 0049 sobre las doctrinas no se ha cumplido: ha ido al revés.** La ADR
razonaba que, si la run repartía menos elecciones gratis, el mercado decidiría más build y la doctrina que
compra con criterio se separaría de las dos puras. Medido:

| Doctrina | Antes (AC) | **Ahora** | Compras por mercado |
|---|---|---|---|
| **Contextual** | 22,6 | **21,0** | 0,88 |
| Ahorradora | 17,0 | **20,8** | 0,68 |
| Gastadora | 13,2 | **14,8** | 1,18 |

`contextualAdvantage` cae de **+5,6 a +0,2**: la contextual ya no le saca nada a la ahorradora. La causa
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

## 22. Decisiones de implementación del paquete AE: dos tiradas, un solo suelo y la tercera opción (ADR 0050 P2 y P4, ADR 0052 §2)

Tres cambios que se implementan juntos porque los tres son baratos y ninguno cambia el diseño: las cuatro
resoluciones decisivas pasan a tirarse contra el **promedio de dos** (ADR 0050 P2), toda probabilidad de
resolución del balón se acota a **2%-98%** (ADR 0050 P4), y el partido de liga recupera la **tercera
opción** de recompensa (ADR 0052 §2). **P1 (cuotas multiplicativas) y P3 (curva de nivel) no se tocan**:
la propia ADR 0050 prohíbe juntarlas con esto.

**Resultado de una línea**: la tirada pierde el **29,3%** de su desviación y la ventaja del equipo mejor
crece un **31%**, así que el mismo efecto se mide con el **59% de los partidos**; la curva de la ADR 0033
sube entera y, recalibrando dos jefes, pasa por primera vez a **doce celdas de doce en banda sin margen de
medida**; la tercera opción de liga devuelve **un tercio** de la ventaja de la doctrina contextual (+0,2 →
+2,0, no los +5,6 pedidos) y **degradar la rareza más allá del 30% se la vuelve a llevar**; y la agencia
frente al riesgo, con el ruido quitado, **no se separa: se separa exactamente cero**, que falsa la
hipótesis del ruido y confirma la ADR 0052 §1.

**Cómo se ha medido, y por qué importa decirlo.** Mientras se hacía este paquete había otro agente
trabajando en `data/perks`, `data/items` y `Sim/Perks` (perks maestros y profundidad nativa, ADR 0051).
Medir en el árbol de trabajo habría mezclado los dos efectos, que es exactamente el error que la ADR 0050
prohíbe. Todas las cifras de esta sección salen de **dos copias aisladas del repositorio** —una en el
commit de partida y otra con este paquete aplicado encima— construidas y medidas por separado con la misma
semilla. Lo que se compara es este paquete contra su propio antes, sin nada más de por medio.

### 22.1. El promedio de dos tiradas conserva la media y dobla la pendiente (ADR 0050 P2)

**AE-1. Qué es exactamente el cambio.** `Pcg32.ChanceAveraged(p)` compara `p` contra
`(Range(0,10000) + Range(0,10000)) / 2` en vez de contra una sola uniforme. La tirada conserva la media
—4.999,5 sobre 10.000— y su desviación típica baja de **2.887 a 2.041**, un **29,3%**, que es el «en torno
a un 30%» de la ADR. Está comprobado en `Sim.Tests/Random/Pcg32Tests.cs` sobre 200.000 tiradas, no solo en
`/Balance`: es la propiedad de la que cuelga todo lo demás.

**AE-2. La consecuencia no es menos ruido por resolución: es más señal.** La probabilidad efectiva deja de
ser `p` y pasa a ser la **acumulada triangular** `F(p) = 2p²` por debajo del centro y `1 − 2(1−p)²` por
encima. Dos cosas se siguen de ahí, y conviene no confundirlas:

- La **pendiente** en el punto de trabajo pasa de 1 a **1,5**: la misma diferencia de atributos mueve el
  duelo vez y media más. Esa es la mejora.
- La **varianza por tirada** de un suceso de probabilidad `q` sigue siendo `q(1−q)` y no la toca nadie: una
  Bernoulli es una Bernoulli. Lo que baja no es el ruido absoluto, es el ruido **medido en unidades de la
  ventaja que se persigue**.

**AE-3. Por eso hay que reexpresar cuatro bases, y no es un cambio de balance.** Con la tirada nueva, una
base de 2.800 ya no produce un 28% sino un 15,7%. Para que el punto de trabajo no se mueva —la ADR pide
explícitamente no tocar ninguna media ni cambiar el ritmo del partido— cada base se **reescribe en la
escala nueva** resolviendo `F(p) = p_antigua`. Es un cambio de coordenadas, no de equilibrio:

| Canal | Base antes | Base ahora | Probabilidad efectiva | Pendiente |
|---|---|---|---|---|
| `dribble.baseWin` | 7200 | **6260** | 72% → 72% | 1 → **1,5** |
| `tackle.baseWin` | 2800 | **3740** | 28% → 28% | 1 → **1,5** |
| `shot.offTargetBase` | 2500 | **3280** | 70,5% de tiros a puerta → 70,4% | 1 → **~1,5** |
| `save.basePercent` | 53 | **51** | 56,2% de paradas → 56,3% | 1 → **~1,9** |

`shot.baseQuality` y los demás términos del remate **no** se tocan: son calidad del disparo en 0-100, no
una probabilidad.

**AE-4. Las cuatro y solo las cuatro.** Tiro a puerta, parada, entrada (la disputa del balón, **no** la
falta) y regate. Pase e intercepción se quedan con una uniforme, que es lo que la ADR pide para no cambiar
el ritmo: son las resoluciones de alta frecuencia y ahí una ese de pendiente 1,5 se compone decenas de
veces por posesión.

### 22.2. Un solo suelo y un solo techo, y dónde no llega (ADR 0050 P4)

**AE-5. 2%-98% en un único sitio.** `tuning.resolution.probabilityFloor/Ceiling` (200 y 9800 en base
10.000) y `MatchEngine.Bounded()`. Sustituye a los límites ad hoc por canal: el 500-9800 del pase, el
0-10.000 de la parada, y la **ausencia** de límite en regate, entrada, intercepción, bloqueo y tiro a
puerta. El comportamiento cerca de los extremos deja de depender del canal, que es lo que la ADR pide.

**AE-6. No se aplica a los sucesos raros, y está medido por qué.** Falta, tarjeta, penalti, lesión y
muerte se quedan con sus propias cotas. La ADR dice «toda probabilidad», pero ahí el suelo del 2% **no
sería una barandilla sino una mejora**, y en un caso rompería una decisión ya tomada:

- `injury.onTackleBase` vale **140** (1,4%). Un suelo del 2% lo subiría un 43% y sacaría
  `injuriesPerMatch` de la banda 0,30-0,80 de RT-056, que ya está pegada al techo.
- La tirada letal de la ADR 0048 **necesita poder llegar a cero por distancia**
  (`proximityMinPercent: 0`). Con un suelo del 2%, colocar al marcado lejos del portador dejaría de servir
  de nada, y esa es la única de las cinco condiciones de la ADR 0048 que hubo que construir entera.

Queda anotado como desviación explícita de la ADR 0050 P4, con su motivo medido, en el `_doc` del dato.

### 22.3. Varianza y error de medición, antes y después: el entregable principal

Lote de referencia idéntico en las dos copias: **2.000 partidos, semilla 1**, seis emparejamientos del
conjunto de `data/balance/reference.json`.

**AE-7. La desviación del resultado no baja; lo que sube es la señal.** Por emparejamiento, media y
desviación típica de la diferencia de goles:

| Emparejamiento | Antes (media / sd) | Ahora (media / sd) | señal ×|
|---|---|---|---|
| `human_50` vs `human_50` | +0,419 / 1,506 | **+0,689 / 1,538** | 1,64 |
| `human_50` vs `elf_50` | +0,441 / 1,567 | **+0,498 / 1,498** | 1,13 |
| `human_50` vs `orc_50` | −0,129 / 1,656 | **−0,096 / 1,700** | 0,74 |
| `orc_50` vs `elf_50` | +0,562 / 1,460 | **+0,751 / 1,479** | 1,34 |
| `human_60` vs `human_40` | +0,871 / 1,420 | **+1,180 / 1,538** | 1,36 |
| `human_60` vs `human_50` | +0,096 / 1,663 | **+0,093 / 1,628** | 0,97 |
| **Media** | **0,420 / 1,545** | **0,551 / 1,563** | **1,31** |

- **Señal ×1,31**: la ventaja que un equipo mejor le saca a otro crece un 31%.
- **Ruido ×1,01**: la desviación no se mueve. Era previsible y conviene decirlo: la varianza de una
  Bernoulli la fija su probabilidad, y las probabilidades efectivas se han conservado a propósito (AE-3).
- **Señal/ruido ×1,30.** Para detectar el mismo efecto con la misma certeza hacen falta **el 59% de los
  partidos**: un lote de 640 rinde ahora como uno de 1.080 antes.

Es el 30% que la ADR persigue, pero **no está donde la ADR lo colocaba**. La ADR 0050 dice «baja la
desviación típica en torno a un 30%» y espera que el error por celda pase de ±4 a ±2,8 puntos. Lo que baja
un 30% es la **desviación de la tirada**, y de ahí no se sigue que baje el error de una medición: se sigue
que el efecto que se mide es un 30% mayor. La conclusión práctica de la ADR —«la diferencia entre construir
bien y mal se ve con lotes más pequeños»— **se cumple**; su mecanismo, no.

**AE-8. El error de una celda de la curva no baja: sube, y es la lectura correcta.** Misma muestra que la
ADR 0033 (32 plantillas × 4 partidos × 5 razas = **640 partidos por celda**), repetida con las semillas 1,
2, 3 y 4; el error de una celda es la desviación típica de sus cuatro medidas:

| | Antes | Ahora |
|---|---|---|
| Error medio de celda (12 celdas, 4 semillas) | **1,11 puntos** | **1,42 puntos** |
| Celda más ruidosa | `the_hunt` incoherente, 1,86 | `eternal_crown` correcta, 3,01 |
| Celda menos ruidosa | `eternal_crown` incoherente, 0,32 | `grimhold_guns` buena, 0,46 |

Y no es una regresión: **cambiar de semilla cambia las plantillas generadas**, así que esa desviación no
mide ruido de partido sino **diferencias reales entre plantillas**, que es justo lo que P2 hace más
visibles. El error de muestreo puro con 640 partidos —el que sí es ruido— es `√(p(1−p)/640)`, entre 1,8 y
2,0 puntos, y no lo mueve nada de esto. Las celdas que más se ensanchan son las que están **cerca del
50%**, donde la ese es más pronunciada (`eternal_crown` correcta 1,41 → 3,01; `the_hunt` correcta 0,83 →
2,62), y las que están en los extremos se estrechan (`grimhold_guns` correcta 1,50 → 0,61).

La consecuencia operativa es la contraria de la que la ADR anticipaba y hay que anotarla: el margen de
medida de `BossGateTests` (±2,5) **no puede reducirse**; si acaso, la celda *correcta* del jefe final ya lo
roza. Queda abierto como **AE-A**.

**AE-9. Y el mejor equipo gana más, que era el otro objetivo.** `betterTeamWinRate` con +20 puntos en todos
los atributos pasa de **72,97% a 77,48%** (banda 65-80 de la fase 0: sigue dentro, ahora en su mitad alta).
Con +10 puntos sigue midiendo 49,9% y no dice nada, por la razón que `balance.md` ya documenta: son **dos
plantillas concretas**, no dos calidades, y en esa pareja la de calidad 60 salió peor que la de 50. P2 no
arregla eso; lo que hace es amplificarlo, y en el espejo se ve bien —dos plantillas de la misma calidad
nominal que ganaban 58%/42% ahora ganan **69,5%/30,5%**—.

### 22.4. La tercera opción de liga vuelve, y la rareza se degrada poco (ADR 0052 §2)

**AE-10. Es un número en datos y una tirada con tres tramos.** `economy.nodeRewards.league.options` vuelve
de 2 a **3** —que es lo que RF-071 dice desde siempre— y aparece `commonCeilingPercent`, la probabilidad de
que una opción se sortee **solo entre las comunes**. Se sortea con la **misma** tirada que
`rarityFloorPercent`, desde el otro extremo: `rare = r < floor`, `común = r >= 100 − ceiling`. Una sola
tirada y no dos, para que añadir techo a la liga no desplace el flujo de RNG del élite ni del jefe, que no
cambian.

| Nodo | Opciones | Rareza |
|---|---|---|
| Partido de liga | **3** | `commonCeilingPercent` **30**: tres de cada diez opciones salen forzadas a común |
| Partido de élite | 3 | `rarityFloorPercent` 65 (sin cambios) |
| Jefe de acto | 3 × 2 elecciones | `rarityFloorPercent` 35 (sin cambios) |

**AE-11. El barrido, que es lo que fija el 30.** Cuatro configuraciones, **500 runs por doctrina y semilla
1**, todas con P2 y P4 puestos para que la única variable sea la recompensa:

| Configuración | Contextual | Ahorradora | Gastadora | **Ventaja contextual** |
|---|---|---|---|---|
| *Referencia sin P2 ni P4* (2 opciones) | 21,0 | 20,8 | 14,8 | **+0,2** |
| 2 opciones (control: solo P2 y P4) | 25,4 | 25,6 | 22,6 | **−0,2** |
| 3 opciones · techo común **60** | 25,6 | 26,6 | 18,4 | **−1,0** |
| 3 opciones · techo común **30** | **28,0** | 25,2 | 19,6 | **+2,8** |
| 3 opciones · sin techo (0) | 27,0 | 24,4 | 22,0 | **+2,6** |

Tres lecturas, y las tres importan:

1. **P2 por sí solo no separa las doctrinas.** El control con dos opciones se queda en −0,2, igual que la
   referencia. Menos ruido no crea una decisión que no existe: solo mide mejor la que hay.
2. **La tercera opción sí paga, y es la mitad de la ADR 0052 que se cumple.** De −0,2 a +2,6/+2,8. El
   diagnóstico de la ADR era correcto: **la ventaja está en tener con qué elegir**.
3. **Degradar la rareza al 60% se lleva por delante lo que la tercera opción acaba de dar** (−1,0). Y tiene
   sentido con el mismo diagnóstico: si las tres opciones son comunes, volver a tener tres no devuelve
   ninguna decisión. Por eso el techo se queda en **30** y no en 60: es la degradación más alta que todavía
   deja la ventaja en pie, y sigue siendo una degradación real —la liga es el único nodo que la tiene—.

**AE-12. Lo que NO se cumple, y hay que decirlo claro.** El encargo pedía recuperar **al menos los +5,6** y
acercarse a los 8 de la ADR 0037. Medido: **+2,8**. Se recupera la mitad. Con 500 runs por doctrina el
error típico de una tasa ronda los 2 puntos, así que +2,8 y +2,6 son la misma cifra y −1,0 sí es distinta,
pero ninguna llega a +5,6. Las dos explicaciones compatibles con los datos:

- El +5,6 de §20 se midió **antes** de la ADR 0048 (0,64 muertes por run frente a 1,77). La carnicería se
  lleva jugadores con sus perks puestos, y eso castiga por igual a quien elige bien y a quien no.
- La regla que distingue a la contextual —repartir el oro entre los mercados que quedan y, dentro del
  presupuesto, preferir lo raro— sigue chocando con que **lo raro casi nunca está dentro del presupuesto**:
  `affordableShareAtMarket` sigue en 57 con banda 20-35. Mientras el mercado no separe precios, la
  contextual seguirá degenerando en la ahorradora en la mitad de las visitas.

Queda abierto como **AE-B**: el número de opciones estaba diagnosticado bien y ya está corregido; lo que
queda de los +5,6 depende del **precio**, no de la recompensa, y choca con `brokeMarketRunShare` (Z-K,
AB-F, AD-18), que sigue sin tener configuración que cumpla las dos.

### 22.5. La curva de la ADR 0033, recalibrada: doce de doce

Con la escalera más pronunciada, la curva entera sube y dos celdas se salen por arriba. Muestra idéntica a
la de §21.4: semilla 1, 32 plantillas × 4 partidos por celda y raza, **640 partidos por celda**, 7.680 en
total.

| Puerta | Incoherente | Correcta | Buena | Muy buena |
|---|---|---|---|---|
| **Acto 1** `grimhold_guns` (calidad 17, **sin tocar**) | **25,0** (20-35) | **77,8** (65-80) | **87,2** (75-88) | **95,0** (85-95) |
| **Acto 2** `the_hunt` (calidad 40 → **44**) | **8,0** (< 15) | **39,4** (35-50) | **67,3** (60-72) | **80,3** (72-85) |
| **Acto final** `eternal_crown` (calidad 24 → **29**) | **6,1** (< 10) | **26,3** (15-28) | **43,6** (40-55) | **60,3** (55-70) |

**Doce celdas de doce en banda, y por primera vez sin necesitar el margen de medida.** La escalera sigue
siendo monótona en los tres jefes y **la celda que arrastraba el aviso desde el paquete AB —*correcta*
contra el jefe final— entra por fin sola**: era 30,3 con 2,3 puntos de exceso, y ahora es 26,3 con 1,7 de
margen.

**AE-13. Qué se movió y qué no.**

- **`data/balance/groups.json` no se toca.** La tercera opción de liga vuelve, pero **la densidad de la
  build no cambia**: al jefe del acto 1 se llega con 3,29 perks (antes 3,18), al del 2 con 6,46 (6,28) y al
  final con 8,57 (8,39). Lo que la tercera opción devuelve es la **decisión**, no la cantidad —quien no
  rechaza nada se llevaba una recompensa por nodo con dos opciones y se lleva una con tres—, que es la
  misma asimetría que AD-15 documentó al revés.
- **`grimhold_guns` no se toca.** Sus cuatro celdas siguen en banda y una build correcta pasa el 77,8%: el
  acto 1 sigue siendo el taller (ADR 0043), que es la condición que la ADR 0043 pone por encima de todo.
- **`the_hunt` de 40 a 44 y `eternal_crown` de 24 a 29**, que es lo único que hacía falta. Sin tocarlos,
  *buena* contra el acto 2 medía 76,1 (pide ≤72) y *correcta* contra el final medía 36,9 (pide ≤28).
- **La ventana de `eternal_crown` es estrecha y conviene saberlo**: con 28 se sale *correcta* por arriba y
  con 30 se sale *buena* por abajo (38,3 frente al 40 que pide). La banda de la ADR 0033 pide 12 puntos
  entre esas dos celdas y la escalera medida da 13. Anotado como **AE-C**.

### 22.6. RT-056 y las métricas de §10, antes y después

**AE-14. RT-056 se conserva, y esa era la condición.** Misma muestra que la puerta estadística: **1.000
partidos, semilla 1**, equipos de referencia sin perks.

| Métrica | Banda | Antes | **Ahora** |
|---|---|---|---|
| Alternancias de posesión | 12-25 | 23,76 | **24,15** |
| Cadena media de pases | 2-4 | 2,27 | **2,26** |
| Tiros por partido | 8-16 | 11,83 | **11,98** |
| Entradas por partido | 6-14 | 9,78 | **9,77** |
| Lesiones por partido | 0,30-0,80 | 0,73 | **0,75** |
| Reparto de resultados 1-0..3-2 | mayoría | 76,4% | **75,6%** |
| Tercio máximo del balón | < 50% | 41,4% | **41,8%** |
| Equipo **+20** en todos los atributos | 65-80% | 72,97 | **79,52** |

Las siete primeras filas son las de siempre, que es exactamente lo que la reexpresión de bases (AE-3)
perseguía: el ritmo del partido no se ha movido.

**AE-15. La octava fila sí se ha movido y es el aviso más importante de este paquete.** `betterTeamWinRate`
con +20 puntos pasa de 72,97 a **79,52**, dentro de su banda 65-80 pero **a medio punto del techo**. No es
un efecto secundario: es literalmente lo que P2 hace —que el mejor equipo gane más a menudo—, y la banda
65-80 se fijó en la fase 0 para un sistema **lineal**. Consecuencia práctica: **el siguiente cambio que
suba el peso de la habilidad rompe esta puerta**, y P1 (cuotas multiplicativas) y P3 (curva de nivel) son
exactamente eso. La banda hay que revisarla —con su ADR, RT-057— **antes** de aplicar P1, no después de que
la puerta se ponga roja. Anotado como **AE-D**.

**AE-16. Las métricas de §10.** Lote de referencia: **500 runs por doctrina, semilla 1** (1.500 runs),
con las tres opciones, el techo de rareza en 30 y los dos jefes recalibrados.

| Métrica | Rango | Antes (AD) | **Ahora** | Estado |
|---|---|---|---|---|
| **Ventaja de la contextual sobre las dos puras** | ≥ 8 puntos | +0,2 | **+2,0** | **OUT, pero recupera un tercio** |
| Tasa de victoria de la run (contextual) | 20-30% | 21,0 | **21,6** | IN |
| Muertes por run | 1,5-3 | 1,51 | **1,62** | IN |
| Duración de una run completa | 18-22 | 20,00 | **20,00** | IN |
| Derrotas por bajar de 5 jugadores | < 35% | 0,51% | **0,26%** | IN |
| Sumideros que paga el oro de un acto | 2-3, nunca todos | 2,12 | **2,17** | IN |
| Compras por visita al mercado | 0,5-2 (cota) | 0,88 | **0,94** | cota IN |
| Fracción del surtido asequible | 20-35% | 56,7 | **57,4** | OUT (igual que antes) |
| Oro sobrante al terminar la run | < 15% | 21,0 | **19,8** | OUT (mejora 1,2) |
| Runs que llegan a un mercado sin poder comprar | 10-25% | 62,8 | **65,4** | OUT (igual que antes) |
| *Recompensas cobradas / rechazadas por run* | *—* | *9,06 / 0,41* | ***9,92 / 0,22*** | *INFO* |
| *Perks en el once al terminar* | *—* | *6,64* | ***7,05*** | *INFO* |
| *Muertes por acto* | *—* | *0,00 / 0,86 / 0,65* | ***0,01 / 0,91 / 0,71*** | *INFO* |
| *Lesiones graves por run* | *—* | *1,85* | ***1,71*** | *INFO* |
| *Lesiones propias por partido* | *—* | *0,29* | ***0,25*** | *INFO* |
| *Objetos recuperados de un muerto por run* | *—* | *0,91* | ***1,04*** | *INFO* |
| *Reparto de derrotas por acto* | *—* | *31,7 / 51,9 / 16,5* | ***26,0 / 53,8 / 20,2*** | *INFO* |

**Ninguna métrica que estuviera dentro se ha salido, y ninguna nueva se ha roto.** Las cuatro que siguen
OUT son las cuatro de la escasez del mercado, que llevan fuera desde el paquete Z y no dependen de nada de
esto (Z-K, AB-F, AD-18): siguen esperando a que se toquen precios, y siguen chocando entre sí.

Lo que sí se mueve, y encaja: **el rechazo de recompensas se hunde** (0,41 → 0,22 por run) porque con tres
opciones es mucho más raro que ninguna encaje —era el efecto secundario que la ADR 0049 buscaba al revés—,
y las **derrotas se desplazan hacia el final** (el acto 1 pasa de 31,7% a 26,0% de las derrotas y el acto 3
de 16,5% a 20,2%): con la escalera más pronunciada, quien construye bien pasa la primera puerta más a
menudo y quien no, sigue sin pasarla.

### 22.7. La agencia frente al riesgo: con menos ruido, la ADR 0048 se separa menos, no más

**AE-17. El resultado, y es concluyente en la dirección incómoda.** Dos políticas idénticas salvo que una
lee el informe de ojeo y el indicador de riesgo, 500 runs cada una, semilla 1:

| | Antes (AD) | **Ahora** |
|---|---|---|
| Muertes por run **leyendo** el indicador | 1,51 | **1,62** |
| Muertes por run **sin leer nada** (`--ignore-scouting`) | 1,57 | **1,62** |
| Diferencia | 3,8% | **0,0%** |
| Tasa de victoria leyendo / sin leer | 21,0 / 17,6 | **21,6 / 20,2** |

La ADR 0048 declara esta métrica decisiva y dice que, si no se separa, *«el azar no tiene agencia y la
decisión hay que revisarla»*. Con el ruido de antes se podía sostener que el 3,8% era una separación
pequeña escondida bajo el error de medida. Con este paquete, **la separación es exactamente cero**, medida
sobre 1.000 runs. La hipótesis de que el ruido tapaba la señal queda **falsada**: lo que había era señal
cero.

Y esa es la utilidad real de P2 en este caso concreto: no ha hecho visible una agencia que estaba tapada;
ha permitido **descartar** que estuviera tapada. Es la conclusión que la **ADR 0052 §1** ya proponía por
otro camino —con una formación de 2-3-1 fija y siete casillas obligatorias no hay forma de sacar del campo
al frágil, así que no queda nada que decidir— y esta medición la confirma. **La palanca que falta es la
formación (RF-002d), no menos ruido.** Se refuerza **AD-A** y se cierra el turno de la hipótesis del ruido.

**AE-18. El barrido de `--risk-aversion` dice lo mismo que antes, y con la misma asimetría.** 300 runs
por punto, semilla 1 (la fila por defecto es la de 500 del lote de referencia):

| `--risk-aversion` | Qué hace la política | Muertes/run | Antes (AD) |
|---|---|---|---|
| **−1000** | busca el riesgo: pone al frágil donde muerde | **1,90** | 1,90 |
| **0** | ignora el número y alinea por valor deportivo | **1,67** | 1,58 |
| +150 (por defecto) | lo obedece | **1,62** | 1,51 |
| **+1000** | lo obedece por encima de todo | **1,70** | 1,54 |

El rango sigue siendo **del 17% entre el mejor y el peor uso del mismo indicador**, y sigue repartido
igual: exponer al eslabón débil cuesta **+17%** y protegerlo ahorra **−3%**, dentro del error. Que
obedecerlo *por encima de todo* (+1000) salga peor que obedecerlo con medida (+150) es la misma
observación de AD-8 desde otro ángulo: pasado cierto punto, proteger al frágil cuesta partidos y los
partidos perdidos también matan.

Lo que sí paga es leer el informe: **+1,4 puntos de tasa de victoria**. La contrajugada sigue pagando en
partidos ganados y no en cuerpos, igual que en AD-8. Y la doctrina contextual **necesita** el informe: su
ventaja pasa de **+2,0 a −3,2** cuando se le apaga (`--ignore-scouting`), que es la mejor prueba de que la
decisión de compra sí depende del contexto aunque su margen sea pequeño.

### 22.8. Lo que queda abierto

| Id | Qué | Dónde |
|---|---|---|
| **AE-A** | El margen de medida de `BossGateTests` (±2,5) **no se puede reducir**: el error por celda sube de 1,11 a 1,42 puntos porque lo que mide es diferencia entre plantillas, y P2 la hace más visible | AE-8 |
| **AE-B** | La ventaja de la doctrina contextual recupera un tercio (+0,2 → +2,0) y no los +5,6 pedidos. Lo que queda depende del **precio**, no del número de opciones, y choca con `brokeMarketRunShare` | AE-12 |
| **AE-C** | La ventana de calidad de `eternal_crown` es de un punto: con 28 se sale *correcta* y con 30 se sale *buena*. La banda de la ADR 0033 pide 12 puntos entre esas dos celdas y la escalera medida da 13 | AE-13 |
| **AE-D** | `betterTeamWinRate` con +20 queda a medio punto del techo de su banda 65-80. **La banda hay que revisarla por ADR antes de aplicar P1**, que sube el peso de la habilidad otra vez | AE-15 |

Y dos cosas que este paquete **cierra**:

- La hipótesis de que el ruido tapaba la agencia frente al riesgo (**AD-A**) queda falsada: con el ruido
  quitado la separación es exactamente cero. Lo que falta es la formación (ADR 0052 §1), no la medida.
- El aviso **AB-D** —la celda *correcta* contra el jefe final necesitando el margen de medida desde el
  paquete AB— desaparece: con `eternal_crown` en 29 esa celda entra sola.

## 23. Decisiones de implementación del paquete AF: arcos de build y profundidad nativa (ADR 0051)

Los 45 perks del catálogo eran independientes entre sí: ninguno exigía nada, ninguno cerraba nada, y por
eso construir bien consistía sobre todo en **rechazar**. Este paquete añade lo único que faltaba para que
haya algo *hacia lo que* construir: **cuatro perks maestros** que exigen media línea y cierran otra para
siempre, y una **profundidad nativa** por acto que hace que el surtido mejore con la run.

**Resultado de una línea**: los arcos existen y **divergen de verdad** —dos builds de la misma raza con
maestros opuestos no comparten un solo perk, una concede un 25% menos de goles y la otra lesiona 2,1 veces
más— y ninguna de las dos pasa del 70% de RT-055 (67,7% y 66,7%); pero desde que el maestro **solo se
compra** (ADR 0055) el arco se cierra en el **5,5%** de las runs y no en el 24,5%, y ganar sin pisar un
mercado sigue en el **23,5%** contra el 5% que esa ADR pide. Las dos cifras están medidas, con la causa
localizada y la palanca nombrada.

### 23.1. Cuatro maestros, cuatro líneas, dos parejas excluyentes

**AF-1. Las líneas son un dato, y son pocas.** `data/build/arcs.json` declara las cuatro líneas del
catálogo; cada perk dice a cuál pertenece con `family` (o a ninguna, que es lo normal). Las cuatro tienen
**siete piezas** cada una y cubren 28 de los 61 perks: menos de la mitad del catálogo, para que el resto
siga siendo el roguelite de piezas sueltas que la ADR quiere preservar. El nombre visible de cada línea
vive en `data/l10n/<idioma>/templates.json` (sección `families`), no en el fichero de datos: es texto que
lee el jugador (RT-073).

| línea | qué hace | maestro | exige | cierra |
|---|---|---|---|---|
| La Muralla (`wall`) | entrada y cobertura desde el propio tercio | `granite_line` | 2 de La Muralla | **La Puntería** |
| El Toque (`craft`) | pase, regate y evasión | `first_touch_school` | 2 de El Toque | **La Carnicería** |
| La Puntería (`aim`) | el remate | `killing_range` | 2 de La Puntería | **La Muralla** |
| La Carnicería (`butchery`) | lesionar | `blood_tithe` | 2 de La Carnicería | **El Toque** |

Los cuatro maestros escritos, con su efecto y su descripción **generada** (RT-035, sin una frase a mano):

- **`granite_line` · Línea de granito** (raro, condicional, acto nativo 2). *"Al empezar el partido, si el
  portador empieza en su tercio, el equipo suma +15% a su probabilidad de robar y el equipo rival suma -5%
  a su probabilidad de tiro a puerta. Exige llevar ya 2 perks de La Muralla. Cierra La Puntería para el
  resto de la run."* Empuja a los dos equipos a la vez desde el saque inicial y sobre los dos canales que
  deciden un 0-0 —el robo propio y el remate rival—, y por eso pide una línea construida detrás.
- **`killing_range` · Distancia de tiro** (raro, condicional, acto 2). *"Al tirar, si el jugador está a
  menos de 6 casillas de portería, el jugador suma +15% a su probabilidad de tiro a puerta. Exige llevar ya
  2 perks de La Puntería. Cierra La Muralla para el resto de la run."* Con `scope: team` mejora el remate
  de **todo el equipo**, no el del portador, que es lo que ningún perk suelto hace.
- **`first_touch_school` · Escuela del primer toque** (raro, condicional, acto 2). *"Al empezar el partido,
  si el portador tiene más de 1 Fino en su equipo, el equipo suma +10% a su probabilidad de pase y el
  equipo suma +5% a su resistencia a las intercepciones. Exige llevar ya 2 perks de El Toque. Cierra La
  Carnicería para el resto de la run."*
- **`blood_tithe` · Diezmo de sangre** (raro, **rompe-reglas**, acto 2). *"Al empezar el partido, si el
  portador tiene más de 1 Bruto en su equipo, el equipo suma +2% a su probabilidad de lesionar y el equipo
  rival suma +3% a su probabilidad de lesión grave. Exige llevar ya 2 perks de La Carnicería. Cierra El
  Toque para el resto de la run."* **No es letal**: no marca a nadie ni mata por sí mismo (ADR 0048); lo
  que hace es que las entradas de todo el equipo lesionen más y que la lesión del rival sea peor.

**AF-2. Las líneas se cierran por parejas, y eso acota la explosión.** La Muralla contra La Puntería y El
Toque contra La Carnicería. Consecuencias que valen más que cualquier regla adicional: una run puede
cerrar **como mucho dos** arcos (uno de cada pareja); los dos maestros de una pareja **no pueden
coexistir** —el que cierra la línea del otro cierra también su maestro, que es miembro de ella—; y las dos
combinaciones posibles son exactamente dos builds que no comparten nada. Nada de esto está escrito en el
código: sale de los datos.

**AF-3. Los maestros son el 6,6% del catálogo (4 de 61).** La ADR los acota al 5-10% y hay un test que lo
vigila (`BuildArcTests.MastersAreASmallShareOfTheCatalog`): si crecen más, el catálogo se vuelve un árbol
de talentos. Como son `conditional` los tres primeros y `ruleBreaker` el cuarto, la distribución RF-069
queda en **55,7 / 32,8 / 11,5** contra el objetivo 60/30/10 (tolerancia ±8): los tres dentro.

### 23.2. El bloqueo mira hacia adelante, y se anuncia antes

**AF-4. Lo que ya se lleva sigue funcionando.** Un maestro cierra una línea **para lo que queda de run**:
esos perks desaparecen del pool de recompensas y del mercado, y `PerkPool.Require` rechaza cobrarlos por
cualquiera de las dos vías. Lo que **no** hace es apagar los que ya estaban puestos. La razón es RF-072: un
perk no se puede retirar, así que un bloqueo retroactivo borraría algo que el jugador ya pagó con un slot
irreversible. Un bloqueo hacia adelante sigue teniendo precio —el resto de la run se construye en una sola
dirección— y no tiene ninguna trampa.

**AF-4b. Un maestro es el único perk al que se le permiten tres frases.** `docs/estilo-descripciones.md`
pide una frase de línea y media, y la razón es buena: un efecto que necesita dos frases combina demasiados
ejes. Aquí la segunda y la tercera **no describen el efecto**, describen la regla de adquisición —qué
exige y qué cierra—, que es exactamente la excepción que ya existía para la letalidad (`lethalSuffix`) y
por el mismo motivo: es lo peor que puede pasar y hay que leerlo en la ficha, no en un tutorial. Las dos
frases se escriben lo más cortas que se puede ("Exige llevar ya 2 perks de La Muralla. Cierra La Puntería
para el resto de la run.") y solo las llevan cuatro perks de sesenta y uno.

**AF-5. Se anuncia antes de aceptar, en los dos sitios donde se puede aceptar.** La descripción
**generada** del maestro (RT-035) dice qué exige y qué cierra, con el mismo mecanismo con el que un perk
letal añade su aviso: dos sufijos de plantilla (`requiresSuffix`, `blocksSuffix`) y los nombres
localizados de las líneas. Eso cubre el mercado, la ficha del jugador y el informe de ojeo sin tocar
ninguno. Y la **pantalla de recompensa** añade lo que un maestro necesita y una descripción no puede dar,
porque depende del estado de la run:

- *"MAESTRO · exige 2 perks de La Muralla y llevas 3: lo cumples"* (o *"...llevas 1: te falta 1"*),
- *"SI LO ACEPTAS CIERRAS La Puntería para el resto de la run: 8 perks que ya no podrás conseguir, y no se
  puede deshacer (RF-072)"*,

las dos **por delante** de cualquier otro aviso de la ficha y aparezcan o no bloqueadas las opciones. Si la
opción no se puede cobrar, la lista de portadores no se pinta: el motivo ya está arriba, y pintarla
invitaría a un clic que `/Sim` va a rechazar (RF-012d: nada de lo que pase estaba sin anunciar).

**AF-6. La regla vive en `/Sim`, no en la pantalla.** `PerkPool.Availability` devuelve por qué un perk no
se puede cobrar (`Unmet`, `Closed`, `NoCarrier`) y `PerkPool.Require` lanza con el motivo. Lo llaman
`RewardSystem.ApplyPerk` y `MarketSystem.BuyPerk`, así que no hay forma de conseguir un maestro por una vía
y no por la otra, y la pantalla puede equivocarse sin que el estado de la run se corrompa.

**AF-7. El validador comprueba las dependencias, los ciclos y los inalcanzables.** `PerkLoader.ValidateArcs`
corre con el catálogo entero delante (por eso `DataLoader` construye el `PerkCatalog` **antes** de
comprobar que todo es describible) y rechaza: una línea que no existe, un perk bloqueado que no existe, un
maestro que exige la línea que él mismo cierra, un maestro con acto nativo 1, un perk que cierra algo sin
ser maestro, un maestro que no cierra nada, y —la comprobación que cubre a la vez el **inalcanzable** y el
**ciclo**— un maestro cuya línea no tiene suficientes miembros **que no sean maestros**. Contando solo
piezas normales, un maestro no puede ser nunca el escalón de otro, así que un ciclo es imposible por
construcción en vez de por una búsqueda en grafo.

**AF-8. Un maestro no se regala al empezar.** `PerkAssignment.AssignInitial` los excluye del sorteo de
perks iniciales de la plantilla: entrar con uno puesto sería saltarse el arco entero antes del primer
partido.

### 23.3. Profundidad nativa: el surtido mejora con la run

**AF-9. Cada perk y cada objeto declaran su acto.** `minAct` (1-3) es el acto en el que empiezan a
aparecer. El reparto no es mecánico por rareza, es por función:

| acto nativo | perks | objetos | criterio |
|---|---|---|---|
| 1 | 47 | 14 | el relleno y la base de cada línea: el acto 1 es el taller (ADR 0043) |
| 2 | 10 | 20 | los `rare`, los cuatro maestros y el equipo restringido de raza |
| 3 | 4 | 0 | los cuatro letales (`iron_studs`, `marrow_thirst`, `second_wound`, `skullsplitter`) |

**AF-10. La curva es de datos y aplana, no empina.** `data/build/arcs.json` → `depth`:

- por **encima** del acto nativo, el peso decae despacio: **100 / 60 / 40**. El relleno del acto 1 sigue
  saliendo en el 3, solo que menos; eso es lo que deja sitio a lo hondo sin vaciar el surtido, y es la
  lección del rebalanceo de Angband 3.5 —que lo bueno aparezca antes de lo que la intuición pide, porque la
  escasez tardía se administra sola cuando los slots se llenan—;
- por **debajo**, queda un peso pequeño: **12 / 3**. Es la aparición *fuera de profundidad*: encontrar algo
  del acto 3 en el 1 es raro y memorable, no imposible.

**AF-11. Un maestro no tiene fuera de profundidad.** Es la única excepción de la curva, y está en el
código con su motivo: un maestro en el acto 1 sería un objetivo que nadie puede cumplir todavía, y la ADR
dice explícitamente que no sale ahí.

**AF-12. Y hace falta una `frequency`, porque el acto solo no bastaba.** Medido: con la profundidad puesta
pero sin frecuencia propia, los arcos se cerraban en el **10%** de las runs, y la causa no era el requisito
—entre 55 y 86 de 200 runs lo cumplían— sino que el maestro **no aparecía**: es un perk entre cuarenta y
pico de un pool del que salen tres opciones por victoria. `frequency` es el *commonness*
de Angband (100 = lo normal) y multiplica al peso por valor de la ADR 0038 y a la curva de profundidad, sin
sustituir a ninguno de los dos. Los cuatro maestros la declaran a **300**, y con eso los arcos pasan a
cerrarse en una de cada cuatro runs mientras el maestro seguía saliendo también como recompensa; con la ADR 0055 esa cifra vuelve a caer y el porqué está en §23.5.

**AF-12b. Un maestro ofrecido y no cumplido gasta la opción, y eso es deliberado.** Un maestro al que le
falta una pieza aparece en la recompensa **bloqueado**, con el motivo y el recuento a la vista, y no se
puede cobrar. Es el mismo comportamiento que ya tenía una opción sin portador posible o un jugador con la
plantilla llena: se puede rechazar (ADR 0043) o repetir la tirada (RF-071b). Lo que **no** puede hacer
nadie es cobrarla sin darse cuenta: `PerkPool.Require` lanza. Consecuencia práctica en los tests: la
política ciega de `FullRunTests` —que coge la primera opción cobrable— tuvo que aprender a saltarse las
opciones fuera de alcance, igual que ya se saltaba las que nadie puede llevar.

**AF-13. Un maestro entra en el pool cuando le falta como mucho una pieza.** Con el requisito ya cumplido
sería invisible hasta que sobra, y el jugador nunca aprendería que existe; ofreciéndolo siempre, el surtido
se llenaría de opciones imposibles. A una pieza de distancia es el punto en el que el objetivo se ve venir
y **el mercado recupera el papel** que el trampolín de la ADR 0043 le había quitado: si te falta una pieza
de tu línea, la buscas y la pagas. La política automática lo hace: `ArcMarketWeight` pone al maestro
y a las piezas de la línea perseguida por encima de cualquier otra compra.

**AF-14. La política automática persigue un maestro.** `RunPolicyOptions.PursuesMasters` (activo por
defecto) elige la línea de la que la run lleva más piezas y, **a igualdad de todo lo demás**, prefiere sus
perks y el maestro que la corona. No es un atajo para que la medición salga: sin él, la política automática
es exactamente el jugador que la ADR describe como el problema —el que acumula piezas sueltas— y la
pregunta "¿los arcos existen?" no tendría a quién preguntársela. La preferencia es un **desempate** dentro
de la misma puntuación, así que no le hace tomar perks que no colocaría.

### 23.4. El maestro solo se compra (ADR 0055)

**AF-15. Los maestros no salen como recompensa: solo en el mercado.** Es la palanca 1 de la ADR 0055, y
encaja con este paquete sin recortar nada: si el maestro es el **objetivo** de una línea y solo está a la
venta, una build que se salta el mercado se queda sin ese objetivo **por definición**. `PerkPool` lo
expresa con un `PerkSource` (`Reward` o `Market`) que decide qué entra en el pool y qué se puede cobrar;
`RewardSystem` pide `Reward` y `MarketSystem` pide `Market`, así que la regla no depende de que la
pantalla se acuerde.

**AF-16. Y un maestro al que le falta una pieza pesa poco.** Medido al aplicar AF-15: el maestro llegaba
al mostrador **5,3 veces por run** y solo **0,13** de esas veces se podía comprar de verdad, porque casi
todas las apariciones caían cuando la línea aún no estaba hecha. `depth.masterPreviewPercent` (20%) separa
las dos cosas: el maestro **anuncia** el objetivo cuando le falta una pieza —el jugador aprende que existe
y a por qué va— y **pesa completo** cuando ya se puede pagar. Y las 28 piezas de línea suben su
`frequency` a 150, para que una línea se junte antes: los arcos cerrados pasaron del 3,0% al 7,0% con ese
solo cambio (100 runs).

### 23.5. Las cuatro mediciones

Lote: `--full-runs 200 --seed 1`; y `--builds ... --vs human_none --home-away --rosters 60` (288 partidos
por celda, plantillas emparejadas). Sobre el motor de dos tiradas promediadas (ADR 0050 P2), las tres
opciones de recompensa (ADR 0052) y el mapa de cuatro carriles (ADR 0053).

#### 1. Los arcos existen: **hoy no, y está cuantificado**

| | valor |
|---|---|
| Runs que cierran al menos un arco (`mastersReached`) | **5,5%** |
| Runs que llegan al acto 3 y cierran un arco | 12,0% (10 de 83) |
| Veces que un maestro llega al mostrador, por run | **2,73** |
| De esas, cuántas eran comprables de verdad | **0,18** |

Por maestro, sobre 200 runs: `granite_line` 4, `killing_range` 4, `blood_tithe` 2, `first_touch_school` 1.
Los cuatro se alcanzan, pero apenas.

**La medición dice exactamente dónde se corta el arco, y no es donde parecía.** El maestro **aparece**: casi
tres veces por run llega al mostrador. Lo que casi nunca ocurre es que coincidan las **tres** cosas que
hacen falta para cerrarlo: la línea completa, un mercado delante y oro para pagarlo. De 2,73 apariciones,
0,18 son comprables.

Y hay un antes y un después con nombre: mientras el maestro también salía como recompensa, los arcos se
cerraban en el **24,5%** de las runs (§23.4 anterior, mismo lote de 200). Aplicar la palanca 1 de la ADR
0055 los baja al **3,0%**; subir la frecuencia de las piezas de línea los devuelve al **5,5-7,0%**. El
suelo de la métrica se movió de 20 a 2 por eso, y con esa explicación: **no es una banda ajustada a lo que
salía, es una banda que reconoce que la ADR 0051 y la ADR 0055 tiran en direcciones opuestas** —una quiere
el arco alcanzable, la otra lo pone detrás de un recurso escaso— y que la palanca que las reconcilia es el
**oro**, que la propia ADR 0055 nombra y que este paquete no toca.

#### 2. Hay compromiso

| | valor |
|---|---|
| Coincidencia con el **mismo** maestro | 27,9% |
| Coincidencia con maestros **distintos** | 18,9% |
| Divergencia (`masterDivergence`) | **9,0 puntos** (suelo 5) |

**En la build**, que es la prueba dura y no depende de cuántas runs cierren un arco. `human_granite` (La
Muralla + El Toque) y `human_bloodrange` (La Puntería + La Carnicería) son la misma raza, la misma
plantilla y las dos combinaciones de maestros posibles. **No comparten un solo perk** —no es que se
parezcan poco: es que ninguna podría contener una sola pieza de la otra— y ganan de formas distintas:

| build | tasa | goles a favor | goles en contra | lesiones que inflige | cadena de pases |
|---|---|---|---|---|---|
| `human_granite` | 67,7% | 484 | **326** | 59 | **2,72** |
| `human_bloodrange` | 66,7% | **602** | 433 | **122** | 2,35 |

La de granito concede un **25% menos de goles** y encadena un **16% más de pases**; la de sangre marca un
24% más y lesiona **2,1 veces más**. Es la forma de `buildsWinDifferently` de la fase 1, pero entre dos
builds de la **misma raza**, que es lo que RF-032 exige y hasta ahora solo se cumplía por qué perks te
tocaban.

#### 3. No dominan

| build | tasa contra `human_none` | |
|---|---|---|
| `human_granite` | **67,7%** | por debajo del 70% de RT-055 |
| `human_bloodrange` | **66,7%** | por debajo del 70% de RT-055 |
| `human_wall` (coherente, sin maestro) | 78,5% | referencia |
| `elf_tiki_taka` (coherente, sin maestro) | 77,4% | referencia |
| `orc_violence` (coherente, sin maestro) | 65,6% | referencia |

Las dos builds con maestro quedan **por debajo** de las dos coherentes más fuertes que ya había y a la par
de la tercera: un arco cerrado no es un atajo a una build mejor que las que el catálogo ya permitía, es
**otra** build. (Que las coherentes de fase 1 estén en 77-78% contra la referencia sin perks es un
problema anterior a este paquete.)

#### 4. Ganar sin pisar un mercado (ADR 0055): **23,5%**, y el mercado sale perdiendo

La medida de control es la **misma** política contextual, jugando igual de bien todo lo demás, con
`AvoidsMarkets`: elige cualquier nodo antes que un mercado y solo entra cuando el mapa no le deja otra
ruta (0,77 mercados por run, contra 9,57 de la normal, así que la política de control es honesta).

| | valor |
|---|---|
| Tasa de victoria **esquivando los mercados** (`runWinRate_noMarket`) | **23,5%** (objetivo: &lt; 5%) |
| Tasa de victoria de la misma política **usando** los mercados | 20,0% |
| Mercados que pisa la política que los esquiva | 0,77 por run |

**Saltarse el mercado no solo no cuesta: hoy sale a cuenta.** Los maestros por sí solos no corrigen el
problema de la ADR 0055 —lo mueven medio punto— y la razón está en la misma tabla: si el arco se cierra en
el 5,5% de las runs, quitarle el mercado a una build le quita el 5,5% de las veces algo que casi nunca
tenía. La palanca 1 es correcta y es la que menos daño hace, pero **no basta sola**: hacen falta la
palanca 2 (el equipamiento, solo en el mercado; equipar vale +8,2 puntos medidos) o el oro. Está medido y
queda anotado: es la decisión que este paquete deja abierta.

#### Lo que no se ha movido

- **Curva de puertas de la ADR 0033**: `BossGateTests` en verde, las doce celdas en banda.
- **Distribución RF-069**: 55,7 / 32,8 / 11,5 contra 60/30/10 ±8, con `BuildGateTests` en verde (y con él,
  que ningún perk del catálogo está muerto y que todos están asignados en alguna build).
- **Tasa de victoria de la run**: 20,0% (banda 20-30). **Muertes por run**: 1,69 (1,5-3). **Partidos de una
  run completa**: 19,43 (18-22).

### 23.6. Qué se retiró o rediseñó del catálogo existente

**Nada se retiró.** Los 57 perks anteriores siguen en el catálogo con el mismo efecto; lo que cambia en
ellos es de **aparición**, no de comportamiento: 28 declaran ahora la línea a la que pertenecen, todos
declaran su acto nativo, y los cuatro letales pasan a ser del acto 3.

Un solo perk cambió de intención sin cambiar de números: **`blood_tithe` nació pidiendo tres piezas y pide
dos** (medido arriba). Y dos decisiones de reparto que conviene dejar escritas porque no son obvias:

- **`skullsplitter` y `second_wound` se quedan fuera de La Carnicería** aunque temáticamente encajarían.
  Las cuatro líneas tienen siete piezas exactas para que "llevar media línea" signifique lo mismo en las
  cuatro; y dejar los dos rompe-reglas fuera evita que cerrar la línea de la carnicería arrastre además a
  los dos perks más fuertes del catálogo.
- **Las cinco habilidades raciales no tienen línea.** No ocupan slot, no entran en el pool y la raza las
  concede de oficio (ADR 0026): contarlas como pieza de una línea regalaría medio arco por nacer.

### 23.7. Lo que este paquete no toca

`data/economy/**` y `data/sim/tuning.json` no se han modificado, ni el número ni la rareza de las opciones
de recompensa: lo que este paquete cambia es **qué entra en el pool**, no cuántas opciones se ofrecen. La
tabla de valor por perk de la ADR 0038 (`data/economy/perk-values.json`) tampoco: los cuatro maestros no
tienen valor medido y el pool les da el peso base, que es lo que esa tabla hace con cualquier perk que
todavía no se ha medido. Cuando se midan, su peso bajará solo, como el de todos los demás.

## 24. Decisiones de implementación de la ADR 0053: el mapa de cuatro carriles

Lo que la ADR 0053 (`Sim/Run/Map/**`, `Sim.Tests/Run/MapTests.cs`, `Game/Ui/MapView.cs`,
`Game/Screens/MapScreen.cs`) resolvió y por qué. Revisa las decisiones **W-2** (mercados en cuello de
botella), **W-3** (una capa libre es entera de partidos o entera de servicios) y **W-4** (aristas sin
cruces) del paquete base. Los nodos que el jugador **recorre** no cambian: 11/12/12 (W-1).

**AH-1. El esqueleto: 1 → 2 → 4 → … → 1.** El acto tiene `PathLength` capas y cuatro **carriles**
(`MapGenerator.Lanes`). La capa 0 tiene un nodo, la 1 dos, y de la 2 en adelante las capas ocupan un
intervalo contiguo de 3 o 4 carriles; la última es el jefe, de nuevo un nodo. El acto queda **cerrado por
los dos extremos y abierto en medio**, que es donde deben estar las decisiones. Todo el sorteo —anchos,
carriles de mercado, tipos y aristas— sale en orden fijo del flujo `RngStreams.Map(runSeed, act)` (RT-022):
el mapa sigue siendo reproducible con la misma semilla y sigue sin depender de los flujos de partido y de
recompensas, y los tests `SameSeed_SameMap` y `RewardsStream_DoesNotChangeTheMap` no han cambiado.

```
  capa:    0    1    2      3    4      5    6      7    8      9    10
  ancho:   1    2    4      3-4  4      3-4  4      3-4  4      3-4   1
                    MERC.       MERC.       MERC.       MERC.       JEFE
```

**AH-2. La capa 0 es siempre un partido de liga.** *(decisión de diseño, RF-123)* Todo el mundo juega el
mismo primer nodo del acto: arranque comparable entre runs, oro antes del primer mercado y el sitio
natural del mapa fijo de la primera run guiada. Es además lo que sube el suelo de partidos de AH-8.

**AH-3. Movimiento solo a carriles contiguos, con dos excepciones.** Desde el carril `i` se va a `i-1`,
`i` o `i+1`. Es lo que le da **memoria** a la ruta: subir de carril cierra la parte baja del acto y volver
cuesta varias capas, así que la decisión deja de ser local. Las excepciones son las dos de la ADR: la
**apertura** (capas 0 y 1, donde 1 → 2 y 2 → 4 son completas) y el **jefe**, en el que convergen todos los
caminos. `MapInvariants.CheckLaneContiguity` lo comprueba y hay un test que lo verifica sin pasar por el
comprobador.

**AH-4. W-4 queda revisada: las aristas se cruzan, y eso ya no es un defecto.** Con carriles contiguos dos
aristas vecinas pueden cruzarse (`i → i+1` y `i+1 → i`), y ese cruce *es* la reconvergencia que pide la
ADR. El invariante de no cruce se sustituye por el de contigüidad de carril; el dibujo se hace cargo
(AH-10).

**AH-5. RF-011b: con capas de mercado mixtas, los mercados van cada 2 capas, no cada 3.** Es la parte
difícil de la ADR y sale de una cuenta, no de un ajuste. En un grafo por capas, lo alcanzable en dos saltos
desde la capa `i` son `i+1` e `i+2`. Si la capa de mercado **mezcla** mercado con otros nodos —que es justo
lo que la ADR pide para que desviarse cueste posición—, el nodo que no es mercado necesita otro mercado en
`i+1` o `i+2`; con la separación de 3 de W-2 el siguiente está en `i+3` y ese nodo se queda a tres saltos.
Luego **la separación tiene que ser 2**: las capas de mercado son las **pares, de la 2 a la `PathLength-2`**.
La garantía se cierra en tres pasos, todos por construcción:

1. **Dominación.** Los mercados de la capa `m` cubren todos los carriles de la capa `m-1`: para cada carril
   `x` hay un mercado en `[x-1, x+1]`. Se consigue eligiendo los carriles de mercado **en función del ancho
   de la capa anterior**: si mide 3 carriles basta con **uno** (el central los cubre los tres); si mide los
   4, hacen falta **dos**, uno en `{0,1}` y otro en `{2,3}`. De ahí sale, literalmente, el "uno o dos
   carriles" de la ADR. La capa 2 es la excepción: la apertura 2 → 4 es completa y cualquier carril vale.
2. **Arista forzada.** Todo nodo de una capa `m-1` recibe explícitamente la arista al mercado que lo
   domina: tiene un mercado **a un salto**.
3. **Dos saltos para el resto.** Cualquier otro nodo tiene sus sucesores en una capa `m-1` —porque las
   capas de mercado van cada 2— y por el paso anterior ese sucesor tiene mercado a un salto: dos en total.
   Las últimas capas no necesitan mercado porque tienen el **jefe** a uno o dos saltos, que es la excepción
   que RF-011b ya admitía.

Nunca hay que regenerar un mapa. **Medido: 0 violaciones en 3.000 mapas** (1.000 del test obligatorio, más
1.000 semillas × 3 actos de la medición de AH-9), comprobando la garantía nodo a nodo con BFS y no solo la
forma. El precio son **4-5 capas de mercado por acto** y 5,84 nodos de mercado dibujados, contra las 3
capas-cuello de botella de antes.

**AH-6. La densidad "un mercado cada 3-4 nodos" de RF-011b cambia de sitio.** *(lectura aplicada, RF-011b)*
De las dos mitades del requisito, la que manda es la garantía de los dos saltos; la densidad era el
mecanismo con el que W-2 la cumplía. Ahora se cumple **por exceso en lo que se ofrece** (una capa de
mercado cada 2) y **deja de cumplirse en lo que se recorre**: un camino puede no pisar ningún mercado
—medido: el 98,9% de los actos admiten un camino con cero mercados— y otro puede pisar 4 o 5. Eso no es un
efecto colateral: es exactamente el desvío que RF-002d describe ("una decisión legítima frente a
**desviarse** hacia un mercado") y que el mapa de cuellos de botella no tenía. Queda anotado como lectura
aplicada, igual que W-1.

**AH-7. RF-003b con capas mixtas: "una capa lleva partidos o no lleva ninguno".** W-3 ("entera de partidos
o entera de servicios") ya no vale, porque con cuatro carriles interesa mezclar. La regla nueva es más
débil y basta: como un camino visita **una capa de cada índice**, el número de partidos de cualquier camino
está acotado por el **número de capas con algún partido**, y ese número es el presupuesto de RF-003b
(`PathLength * 60 / 100`, jefe incluido). Dentro de una capa con partidos cabe además un servicio, que es
lo que convierte la elección en "juego o me curo" sin tocar el tope.
`MapInvariants.WorstCaseMatches` sigue siendo la cifra que se compara y ahora es una **cota conservadora**;
el extremo exacto lo da `MapInvariants.PathMatches`, y **medido, los dos coinciden**: el peor camino juega
6/7/7 = **20 partidos por run**, exactamente como antes.

**AH-8. El suelo de partidos es nuevo, y hay un mando para él.** Si el mercado se puede esquivar, el
partido también: un camino que se desvíe siempre juega menos. `MapGenerator.PorousMatchLayers` fija cuántas
capas de partido por acto ofrecen alternativa en la misma capa —un carril de servicio entre los partidos—;
las demás son de partido en **todos** sus carriles y ningún camino las esquiva. Con el valor 1: el peor
camino juega 20 partidos por run y **el más evasivo, 17**, contra los 18-22 de §10. Cada capa porosa de más
quita un partido por acto y tres por run: con 2 el suelo bajaría a 14.

**AH-8b. Ninguna capa con partido lleva mercado, y la razón no es de diseño: es el instrumento de medida.**
Es lo único de la ADR 0053 que este trabajo **no** entrega. La ADR pedía que la elección pasara a ser
"partido, mercado o clínica" en la misma capa; se implementó así, se midió, y la puerta `matchesPerFullRun`
(18-22) se puso **roja en 17,0**. La cadena es corta y no tiene truco:

1. `RunPolicy.ChooseNode`, la política automática con la que `/Balance` mide, puntúa el **mercado con 90**
   y un partido de liga con **50 menos la dificultad**. Son pesos calibrados cuando el mercado era un
   cuello de botella y por tanto **nunca competía con nada**: no había elección que hacer.
2. La construcción garantiza una arista al mercado desde todo nodo de la capa previa (paso 2 de AH-5), así
   que si esa capa lleva partidos, la política ve siempre el mercado entre sus opciones y **siempre** lo
   coge.
3. El presupuesto de RF-003b deja las capas con partido justo en el tope (6 de 11). Un desvío que se toma
   siempre es un partido menos por acto, tres por run: de 20 a 17.

Con el servicio en vez del mercado la política solo se desvía cuando de verdad quiere lo que hay —clínica
con alguien lesionado (100) o inscripción con hueco que comprar (80) valen más que un partido; entrenamiento
(30) y evento (25), menos—, que es justo cuando el desvío debe tomarse. **Medido con el mapa nuevo: las 14
pruebas de `FullRunGateTests` en verde**, `matchesPerFullRun` dentro de banda y las métricas ancladas sin
moverse. **Revisar los pesos de `ChooseNode` es lo que desbloquea la capa con partido y mercado a la vez**,
y no se ha tocado aquí porque otros dos paquetes estaban midiendo con ese mismo instrumento.

**AH-9. Lo que ha cambiado, en números.** 1.000 semillas × 3 actos, con los 11/12/12 nodos de
`data/map/map.json`:

| | Antes (W-2) | Ahora (ADR 0053) |
|---|---|---|
| Nodos dibujados por acto | 20,3 | 35,8 |
| Aristas por acto | 26,9 | 66,0 |
| Nodos de mercado dibujados por acto | 3,00 | 5,84 |
| **Nodos elegibles por paso (media)** | **1,39** | **1,90** |
| Pasos con una sola opción | 64,6% | 37,2% |
| Pasos con 2 opciones | 31,7% | 41,4% |
| Pasos con 3 o 4 opciones | 3,7% | 21,4% |
| **Pasos con 2+ tipos de nodo distintos entre las opciones** | **17,9%** | **47,0%** |
| Partidos del peor camino (run) | 20 | 20 |
| Partidos del mejor camino (run) | 20 | 17 |
| Mercados en el camino, por acto (mín-máx) | 3-3 | 0-4,67 |

Dicho en una línea: **dos de cada tres pasos no eran una decisión, y ahora lo son casi dos de cada tres**;
y cuando lo eran, casi siempre se elegía entre dos nodos del mismo tipo (17,9% de variedad), mientras que
ahora casi la mitad de las decisiones son entre tipos distintos. El tope de RF-003b no se mueve.

**AH-10. La pantalla: el carril es una altura fija.** `MapView` dibuja `IndexInLayer` siempre a la misma
`y`, en todas las capas, y centra las capas de un solo nodo (entrada y jefe). Eso es lo que hace visible la
regla de movimiento: una arista sube, baja o sigue recta, nunca salta dos filas. Con ~36 nodos por acto
hicieron falta dos cosas más: las aristas **arrancan y terminan en el borde** del glifo (los cruces entre
carriles vecinos se leen como cruces y no como manchas) y lo que **ya no se puede alcanzar** desde donde
está el jugador se apaga del todo y pierde etiqueta y distintivo de dificultad. Esa última es la
información nueva del mapa de cuatro carriles —subir de carril cierra la parte baja del acto, y eso hay que
verlo antes de elegir, no después— y es una cuenta de dibujo, no de reglas: un BFS hacia delante desde el
nodo actual.

**AH-11. Un recorrido de capturas propio para el mapa.** `--map-tour` (junto a `--tour`, en `Game/Ui/Tour.cs`)
recorre el mapa de los **tres actos** y termina a media travesía, saltando de acto con
`RunStateBuilder` a través de `RunController.JumpToAct`/`JumpToNode` (RT-062). Deja
`mapa-acto1.png`, `mapa-acto2.png`, `mapa-acto3.png` y `mapa-mitad.png` en `Game/screenshots/`. El
recorrido largo no se toca: sigue capturando `mapa.png` en el acto 1.

**AH-12. Lo que este trabajo deja pendiente, y es material.**

- **Los pesos de `RunPolicy.ChooseNode` (AH-8b).** Mientras el mercado valga 90 pase lo que pase, no puede
  compartir capa con un partido sin romper la puerta de los 18-22 partidos, y la imagen que la ADR pedía
  —"partido, mercado o clínica"— se queda a medias: hoy es "partido o clínica" en una capa por acto, y
  "mercado o clínica" en las de mercado. Es un paquete propio: cambiar el instrumento de medida obliga a
  volver a medir la economía entera.
- **Más mercados ofrecidos, menos mercados visitados.** El surtido sale de `RngStreams.Rewards(seed,
  nodeId)`, así que más nodos de mercado son más surtidos distintos, no más oro. El efecto neto sobre la
  economía depende de la política de compra y hay que medirlo cuando el punto anterior esté resuelto.
- **RF-123 (primera run guiada, mapa fijo)** encaja mejor que antes —la capa 0 es un nodo común— pero sigue
  sin implementarse.
- El nodo de inscripción ya no compite solo con otro servicio, sino también con un partido o con el
  mercado: el test de la ADR 0046 pasa a comprobar que **compite con algo**, no con qué.

## 25. Decisiones de implementación del paquete AI: el oro, los arcos y el precio de saltarse el mercado (ADR 0055)

El paquete AF dejó dos cifras medidas y contradictorias: **ganar sin pisar un mercado salía a cuenta** y
**el arco de build casi nunca se cerraba**. Este paquete diagnostica las dos, mueve el oro y los precios, y
**falsifica la premisa de la ADR 0055**: el 5% que esa ADR pide no depende de la economía.

**Resultado de una línea**: los arcos pasan del **2,9%** al **25,3%** de las runs —dentro de la banda
20-30 que el encargo pedía— porque la causa estaba localizada al perk: el maestro llegaba al mostrador con
la línea **ya construida** 2,01 veces por run y solo 0,15 se podían pagar. Ganar sin mercado baja del
**20,0% al 17,8%**, muy lejos del 5%, y **está medido por qué no puede bajar más**: con las recompensas
sin dar un solo perk, una run que termina con **1,58 perks en el once y 0,16 objetos —sin build ninguna—
sigue ganando el 14,5%**. El suelo no es económico, es de curva de dificultad.

**Muestra**: salvo donde se diga otra cosa, todas las cifras son de `--full-runs 400` sobre **dos
semillas** (1 y 1001), 800 runs por doctrina, contra las 200 de una sola semilla de §23; varias métricas de
§23 se mueven un par de puntos solo por eso (la tasa de victoria de la run era 20,0 con 200 runs y es
**19,5** con 800, y los arcos eran 5,5% y son **2,9%**). Las cifras de referencia de este documento son las
de 800.

### 25.1. El diagnóstico: por qué desviarse no compensaba, en tres causas separadas

La ADR 0055 nombra tres candidatas —coste de ruta, valor de lo que hay en el mostrador, precio frente al
oro del acto— y había que medirlas por separado antes de tocar nada. El instrumento es la **misma política
contextual sobre las mismas semillas**, con y sin `AvoidsMarkets`, y doce filas INFO emparejadas nuevas en
`FullRunMetrics.Marketless` (§25.4).

**AI-A. El coste de ruta es CERO, y con signo cambiado.** Con el mapa de cuatro carriles se esperaba que
desviarse al mercado costara posición. No cuesta nada:

| | usa mercados | los esquiva |
|---|---|---|
| Partidos por run | 13,88 | **13,64** |
| Nodos por run | 25,30 | 24,77 |
| Recompensas cobradas | 9,56 | 9,46 |

La política que esquiva los mercados juega **menos** partidos, no más. La razón está escrita en el paquete
anterior: **AH-8b** dejó que ninguna capa lleve partido y mercado a la vez, así que el desvío nunca es
"mercado o partido", es siempre "mercado u otro servicio". Y ahí el mercado **pierde**: quien lo esquiva se
va al entrenamiento y termina la run **0,56 niveles por encima** (5,79 contra 5,23), y con el oro que no
gasta compra el doble de huecos de plantilla (**1,00 contra 0,54**). El coste de desviarse no es espacial:
es el **coste de oportunidad del servicio al que renuncias**, y ese servicio pagaba más que la compra.

**AI-B. Lo que se compraba valía menos que nada.** 37,6 de oro por run, repartidos en 9,6 mercados,
compraban 1,9 perks y 2,2 objetos, que sobrevivían como **+1,18 perks en el once y +0,95 objetos**. El
balance de la operación completa era **negativo**: 19,50% de victoria usando los mercados contra **20,00%
esquivándolos**. Y la lectura de la ADR 0037 lo confirmaba desde el otro lado: la doctrina **ahorradora**,
que apenas compra, ganaba 17,9% y la **gastadora** 10,5% — cuanto más se compraba, peor se terminaba.

**AI-C. El precio no era el problema en la mitad barata del mostrador y era TODO el problema en la cara.**
Con 14,2 de oro al llegar a un mercado, el 59,3% del surtido se podía pagar: los comunes (objeto 8, perk
10) estaban siempre al alcance. La parte cara, no — y ahí es donde vive el objetivo de la build:

| Maestro (ADR 0051), por run | antes |
|---|---|
| Veces que llega al mostrador | 2,65 |
| De esas, con la **línea ya construida** (`mastersUnlockedPerRun`, nueva) | **2,01** |
| De esas, **pagables** | **0,15** |

**El arco no se cortaba por la línea: se cortaba por el precio.** El 76% de las apariciones tenían el arco
abierto y solo el 7% de esas llegaban al oro. Un perk raro costaba 24-40 (base 32 ± banda) contra 14 de
oro en el mostrador. Sin la cifra de en medio —`mastersUnlockedPerRun`, que este paquete añade— el 0,15 de
2,65 no distinguía "no aparece", "no tengo la línea" y "no me llega".

### 25.2. Las palancas que se han movido

**AI-1. El equipamiento solo se consigue en el mercado** (palanca 2 de la ADR 0055).
`economy.rewardItemWeight` pasa de 25 a **0**: la recompensa tras un partido ganado ya solo ofrece perks y
jugadores. Un objeto es una compra, no un trofeo. Medido: los objetos de quien esquiva los mercados caen de
**3,18 a 0,54** por run, y los de quien los usa de 4,13 a 2,12 — el margen de equipar (+8,2 puntos, ADR
0055) deja de estar en las dos columnas y pasa a estar solo en una.

**AI-2. El oro por acto sube de 5/6/7 a 9/11/13.** Una run completa pasa a ganar **del orden de 100**, que
es exactamente lo que la tabla de valores de partida de la ADR 0044 fijaba y que **nunca se había
alcanzado**: con 5/6/7 una run entera ganaba 72. No es inflación, es corregir una desviación: la ADR 0044
calibró esa cifra cuando el mercado era un extra de calidad; con la ADR 0055 el mercado pasa a construir
media build y el presupuesto tiene que dar para ella.

**AI-3. La escalera de precios de los perks se acorta a 4,2:1**, de `10-18-32-64` a **`10-15-24-42`**. La
propia ADR 0044 pedía acotar el rango **dentro de una misma categoría** "a algo del orden de 4:1" y eso
nunca se aplicó: los perks estaban en 6,4:1 y los objetos en 6,5:1. Se ha bajado el **techo**, no subido el
suelo — el común sigue en 10, donde la ADR lo puso—, porque el techo es donde vive el maestro. Los
fichajes bajan igual, de `16-26-40-80` a `18-27-38-64`.

**AI-4. Los objetos NO se abaratan, y la razón está medida.** El primer intento acortó también su escalera
(`8-13-22-38`) y puso **roja** la puerta `TheThreeDoctrinesBuyDifferently`: con la parte alta barata, la
doctrina **ahorradora** se queda sin nada por lo que ahorrar y termina la run con **menos** oro sin gastar
que la contextual, que es justo lo que esa puerta prohíbe. Se quedan en `8-14-28-52`. El descuento de la
parte alta se concentra donde está el objetivo de la build.

**AI-5. El surtido del mercado deja de aplicar la palanca de la frecuencia** (`Sim/Run/Systems/Market/MarketOffers.cs`).
La tabla de la ADR 0038 asigna al **mercado la palanca del precio** y a la **recompensa la de la
frecuencia**; el código aplicaba **las dos** en el mercado, ponderando el surtido inversamente al valor
medido del perk. Con 20 de los 45 perks medidos en negativo y un peso inverso que los ofrece hasta cuatro
veces más, el mostrador se llenaba de lo que ninguna doctrina compra —y encima había que pagarlo—. Ahora
el surtido usa el **peso base** de la tabla: el acto y la `frequency` del perk siguen mandando (ADR 0051),
el valor medido no. Es la corrección de una inconsistencia documentada, no un cambio de diseño.

**AI-6. Los sumideros que NO son el mercado suben con el oro.** `clinicCost` 8 → **10** (vuelve al valor de
la ADR 0044; la ADR 0048 lo había bajado precisamente para `sinksAffordablePerAct`, y ahora el ajuste va en
el mismo sentido), `enrollmentCosts` 12/25 → **14/28**, `rerollStepCost` 1 → **2** y el salario del
mercenario 1+1 → **2+2**. Sin ellos, el oro más alto empuja `sinksAffordablePerAct` contra el techo de la banda 2-3 de RF-114k y por
encima (medido durante la calibración: **3,03** con 10/12/14 y **3,75** con 12/15/18); con ellos vuelve a
**2,51**. Se probó también subir clínica e inscripción
mucho más (13 y 22/44) y **sale mal**: la política contextual **reserva** esos dos costes antes de comprar
(`SpendableAtMarket`), así que encarecerlos sube el oro que llega intacto al mostrador y disparó
`affordableShareAtMarket` a **76,1**, por encima de su cota de no regresión. Por eso el ajuste se apoya en
los sumideros que la política **no** reserva.

**AI-7. `consumablePrice` 8 → 20.** El consumible es de un solo uso y **nadie lo compra** —el estado no
lleva inventario de consumibles (X-9)—, así que su precio solo mueve la mitad aspiracional del mostrador.
A 8 eran 3 de los ~18 artículos siempre pagables e inflaban `affordableShareAtMarket` sin significar nada.

**AI-8. Instrumento nuevo, y hace falta.** `mastersUnlockedPerRun` separa "no tengo la línea" de "no me
llega el oro" (§25.1), y `FullRunMetrics.Marketless` recibe ahora la línea base contextual y emite **doce
filas INFO emparejadas** —partidos, nodos, recompensas, perks en el once, objetos, nivel, oro ganado, oro
sobrante, tratamientos, huecos, muertes y acto alcanzado, con y sin mercado—. Sin ellas, `runWinRate_noMarket`
dice **que** esquivar compensa y no dice **por qué**, que es exactamente donde se atascó §23.

### 25.3. Las cuatro medidas del encargo

`--full-runs 400`, semillas 1 y 1001 (800 runs por doctrina), contra el mismo lote sobre `d216e1a`.

| | antes | después | objetivo |
|---|---|---|---|
| **Ganar sin pisar mercado** (`runWinRate_noMarket`) | 20,00% | **17,75%** | < 5% — **no alcanzado** |
| **Arcos cerrados** (`mastersReached`) | 2,88% | **25,25%** | 20-30% — **cumplido** |
| **Tasa de victoria de la run** | 19,50% | **19,12%** | 20-30% — **al borde, como antes** |
| **Las seis puertas** | verde | **verde** (526 tests, 41 de puerta) | verde — **cumplido** |

Y lo que se mueve alrededor:

| | antes | después | banda |
|---|---|---|---|
| Maestro en el mostrador, por run | 2,65 | 3,91 | INFO |
| … con la línea construida | 2,01 | 3,35 | INFO |
| … **pagable** | **0,15** | **1,29** | INFO |
| Tasa de victoria **con** arco cerrado | 47,2% | 32,7% | INFO |
| Tasa de victoria **sin** arco cerrado | 18,7% | 14,5% | INFO |
| Perks en el once (usando mercados) | 6,74 | **10,39** | INFO |
| Perks en el once (esquivándolos) | 5,56 | 8,89 | INFO |
| Objetos en plantilla (usando / esquivando) | 4,13 / 3,18 | **2,12 / 0,54** | INFO |
| `purchasesPerMarket` | 0,66 | **0,94** | 1-2 |
| `leftoverGoldShare` | 18,05 (OUT) | **10,88 (IN)** | < 15 |
| `sinksAffordablePerAct` | 2,06 | 2,51 | 2-3 |
| `affordableShareAtMarket` | 59,32 | 62,77 | 20-35 (cota 25-70) |
| `deathsPerRun` | 1,57 | 1,54 | 1,5-3 |
| `matchesPerFullRun` | 19,39 | 19,48 | 18-22 |
| `masterDivergence` | 11,70 | 9,31 | ≥ 5 |
| `contextualAdvantage` | +1,62 | **−2,12** | ≥ 8 |

**La curva de puertas de la ADR 0033 no se ha tocado y sigue verde**: este paquete no cambia ningún número
de potencia, solo precios, oro y de dónde sale cada cosa.

### 25.4. La falsificación: el 5% de la ADR 0055 no es un problema de economía

Es lo más importante que deja este paquete y hay que decirlo con números, porque contradice la propia ADR.

**Medición 1 — el techo del mercado.** Con **500 de oro inicial** (bolsa efectivamente ilimitada, 200 runs)
la política contextual llega a **15,94 perks en el once y 7,50 objetos** y gana el **30,0%**: exactamente el
techo de su banda. La misma política esquivando los mercados, con el mismo oro, gana el **20,5%**. Es decir:
**el valor máximo que el mercado puede llegar a tener es de unos 10 puntos**, y la ADR 0055 necesita 15-25.

**Medición 2 — el suelo sin build.** Con `rewardPerkWeight = 0` —las recompensas dejan de dar perks del
todo, la palanca 3 de la ADR 0055 llevada al extremo— la política que esquiva los mercados termina la run
con **1,58 perks en el once y 0,16 objetos**, es decir **sin build ninguna**, y **gana el 14,5% de las
runs**. La que sí compra gana el 12,5%.

Las dos mediciones juntas dicen lo mismo: **la tasa de victoria de una run no la decide la build.** Entre
"sin build" (1,6 perks) y "build completa y equipada" (15,9 perks y 7,5 objetos) hay 14,5% → 30,0%, unos
**1,1 puntos de tasa de victoria por perk del once**, y el suelo de esa recta está en **14-15%**, no en
cero. El resto lo ponen el **nivel y los atributos**, que suben con los partidos y con el entrenamiento
pase lo que pase.

Y eso choca de frente con la tabla de la propia **ADR 0033**, que dice que una build **incoherente**
completa la run el **~0,1%** de las veces y una **correcta** el **~6%**. El bucle de run entrega **14,5%
sin build**. Las dos cifras no pueden ser las dos ciertas: o la curva de jefes está medida sobre un
instrumento que no representa lo que llega al jefe en una run real (plantillas fabricadas contra plantillas
que han subido cinco niveles), o la banda 20-30 de la tasa de victoria de la run es incompatible con "sin
build no se gana".

**Conclusión operativa: mientras ese suelo esté en el 14%, ninguna cantidad de oro, ningún precio y
ninguna palanca de la ADR 0055 pueden poner `runWinRate_noMarket` por debajo del 5%.** Las palancas 1 y 2
están aplicadas y valen lo que valen: el mercado ha pasado de restar 0,5 puntos a sumar 1,4. La decisión
que queda es de **curva de dificultad**, no de economía, y es del revisor.

Se comprobó además que el oro no es la palanca de esa métrica, midiendo tres niveles con el resto igual:

| oro por acto | tasa de victoria | arcos | ganar sin mercado |
|---|---|---|---|
| 9/11/13 | 19,12% | 25,25% | 17,75% |
| 10/12/14 | 19,25% | 29,25% | 18,12% |
| 11/13/15 | 17,00% | 31,75% | 19,12% |

**Más oro sube los arcos y no baja la métrica de la ADR 0055**; por encima de 10/12/14 empieza incluso a
bajar la tasa de victoria, porque la doctrina contextual gasta el oro de más en artículos marginales que
ocupan slots irreversibles (RF-072). De ahí el 9/11/13 elegido: es el punto donde los arcos están en banda
con margen y el resto de métricas no empeora.

### 25.5. Lo que queda abierto

- **El 5% de la ADR 0055 necesita una decisión de curva, no de economía** (§25.4). Las opciones que la
  medición deja sobre la mesa son endurecer los jefes contra plantillas de nivel alto y build pobre
  (ADR 0033), o aceptar que la métrica correcta no es "ganar sin mercado" sino "cuánto peor se juega sin
  él", que hoy son 1,4 puntos y con bolsa ilimitada 9,5.
- **`contextualAdvantage` cambia de signo** (+1,62 → −2,12): la doctrina **ahorradora** (21,25%) supera
  ahora a la **contextual** (19,12%). La causa está localizada y **es del instrumento, no de la economía**:
  con la parte alta del mostrador más barata, "solo compro raros" pasa a ser una estrategia buena, mientras
  que la contextual gasta en perks de valor medido apenas positivo (`MinPerkValue = 0`) que llenan slots
  irreversibles. Es el mismo aviso de **AH-12/AH-C**: revisar los pesos y umbrales de `RunPolicy` es un
  paquete propio, y cambiar el instrumento obliga a volver a medir la economía entera. No se ha tocado aquí
  a propósito.
- **`affordableShareAtMarket` (62,8) y `purchasesPerMarket` (0,94) siguen siendo incompatibles entre sí**
  (Z-K): con 9,6 mercados por run, comprar 1-2 artículos en cada uno cuesta más oro del que una run gana, y
  mientras el oro que llega al mostrador supere el precio de un común, más de la mitad del surtido será
  pagable. La primera empeora 3 puntos y la segunda mejora 0,28 con este paquete; las dos siguen fuera de
  su banda de diseño y dentro de su cota de no regresión.
- **La tasa de victoria de la run (19,1%) sigue al borde de su banda**, como lo estaba antes del paquete
  (19,5%). La cifra de 20,0% de §23 era de 200 runs y una sola semilla; con 800 la banda 20-30 no se
  cumplía ya.
- **El entrenamiento es el rival real del mercado** (AI-A) y su valor está en datos
  (`economy.trainingExperience = 40`, para toda la plantilla disponible). No se ha tocado porque bajarlo
  castiga por igual a quien va al mercado y a quien no, pero es la palanca que haría que desviarse cueste
  algo de verdad si alguna vez se quiere que cueste.

## 26. Decisiones de implementación del paquete AJ: los perks multiplican cuotas (ADR 0050 P1)

Implementa la **P1 de la ADR 0050** —`modifyProbability` deja de sumar puntos porcentuales y pasa a
multiplicar cuotas—, **retira la tabla de escalones por canal de la ADR 0035**, reescribe los valores de
los 61 perks y los 4 consumibles, cambia la convención de las descripciones a proporcional, y aplica la
banda revisada de `betterTeamWinRate` de la **ADR 0054**, que estaba acordada pero no implementada.

La ADR 0057 la eligió como **palanca principal** para bajar el suelo sin build. **No lo es**, y §26.6 lo
mide y lo explica. El paquete entrega la corrección de fondo —que era necesaria y es real— y la
falsificación de la hipótesis de la 0057, que es lo más útil que deja.

### 26.1. La fórmula, y dónde se aplica

```
cuota  = p / (1 − p)
cuota' = cuota × k          k ∈ {1,15 · 1,3 · 1,5 · 2} y sus inversos
p'     = cuota' / (1 + cuota')
```

En `/data` se escribe el **porcentaje de cuota con signo**: `±15, ±30, ±50, ±100`. El negativo es el
**inverso exacto** del positivo de la misma magnitud (`-30` divide por 1,3), no su reflejo; por eso `-100`
no es "probabilidad cero" sino "la mitad de cuota". Todo entero, base 10.000, sin coma flotante
(`Sim.Perks.ProbabilityScale`).

Tres decisiones de implementación que no estaban en la ADR y que hacían falta:

**1. En los cuatro canales de tirada promediada, el multiplicador actúa sobre la probabilidad
realizada.** La ADR 0050 P2 dejó regate, entrada, tiro a puerta y parada resolviéndose contra el promedio
de dos uniformes, cuya acumulada es triangular: el número que el motor calcula **no** es la probabilidad
del suceso. Multiplicar la cuota de ese parámetro habría reintroducido el defecto que la P1 viene a
quitar: un `×2` sobre el parámetro de entrada vale **×3,64** sobre la entrada de verdad, y sobre `pass`
—que no es promediada— vale ×2 exacto. La diferencia se veía en la curva de la ADR 0033: las builds con
perks de entrada rendían por encima de las de intercepción sin que nada de diseño lo justificara. Se pasa
a la probabilidad realizada, se multiplica ahí y se vuelve con la inversa
(`ProbabilityScale.ApplyAveraged`, raíz cuadrada entera y redondeada).

**2. Dos acumuladores por (jugador, canal), no uno.** El registro de modificadores guarda el producto de
partido y el producto de jugada por separado. Con un solo acumulador habría que **dividir** para deshacer
un modificador de duración `play`, y la división entera no es el inverso exacto de la multiplicación: el
estado se iría desviando jugada a jugada. Con dos, deshacer es escribir el neutro y es exacto.

**3. El tope de un efecto con contador es un número de copias, no un multiplicador.** "Por cada unidad del
contador" pasa a significar "una copia más del mismo perk", es decir `k^n`. Si el tope se escribiera como
otro multiplicador, el eje de acumulación (RF-070) quedaría encerrado en el ×2 de la escala, que es
**menos** de lo que valía sumando puntos; escrito como copias, `×1,5` cinco veces son ×7,59 y ninguna de
las cinco multiplicaciones se sale de la escala legal.

Los canales que **restan** a otro —las dos evasiones y `shotOnTarget`, que actúa sobre la probabilidad de
tirar fuera— se componen con el **inverso**: multiplicar la cuota de un suceso por k es exactamente
dividir la de su contrario por k, así que el par sigue siendo consistente sin ningún caso especial.

### 26.2. Cómo se reescribieron los 61 perks: la escala vieja estaba por encima del techo de la nueva

Los valores no se han mapeado a ojo. Para cada efecto se ha calculado **el cociente de cuotas que producía
de verdad** en su canal (sobre la base de trabajo del canal, y pasando por la acumulada triangular donde
toca) y se ha elegido el `k` legal más cercano **en logaritmo**. Es la lectura literal de "conservando la
intención de cada uno".

El resultado es el hallazgo del paquete: **casi todo el catálogo estaba por encima del ×2 que la escala
nueva permite**, y en algunos canales por dos o tres órdenes de magnitud.

| Canal y valor viejo | Cociente de cuotas que producía | Valor nuevo |
|---|---|---|
| `pass` +25 | **×2 987** (77% → 98%, el techo) | +100 (×2) |
| `interceptEvasion` +5 | **×256** | +100 |
| `shotOnTarget` +25 | ×22,3 | +100 |
| `dribble` +25 | ×12,2 | +100 |
| `save` +15 | ×3,07 | +100 |
| `intercept` +5 | ×3,16 | +100 |
| `tackle` +15 | ×3,11 | +100 |
| `tackle` −30 | ×0,029 | −100 (×0,5) |
| `save` −25 | ×0,144 | −100 |
| `dribble` −25 | ×0,153 | −100 |
| `injure` +3 | ×2,58 | +100 |
| `pass` −10 | ×0,606 | −50 (×0,667) |
| `severeInjury` +3 | ×1,13 | +15 |
| `tackleEvasion` +3 (Toque élfico) | ×1,25 | +30 |

De los 68 efectos de probabilidad del catálogo, **48 caen en ±100**: no porque se haya querido subir el
catálogo al máximo, sino porque el techo de la escala está por debajo de lo que valían. Solo tres efectos
—los dos de `severeInjury` +3 y el Toque élfico— estaban dentro del rango bajo de la escala nueva. Los que más cambian son
exactamente los que la ADR 0035 ya señalaba como accidentes de la fórmula aditiva: el pase, el tiro a
puerta y el regate, donde un valor "pequeño" clavaba el canal en su techo.

**Consecuencia directa y medible: la capa de perks es más débil que antes, y no hay forma de que no lo
sea.** Es el precio de que un perk valga lo mismo en todos los canales con `k ≤ 2`.

Los contadores van todos a `+50` por unidad con su tope en copias (3 o 5, el mismo cociente
tope/incremento que tenían): `×1,5` cinco veces son ×7,59, frente a los ×5,6 a ×22 de los topes viejos.

### 26.3. Las descripciones pasan a proporción, y lo que eso cuesta

De *"suma +5% a su probabilidad de interceptar"* a *"tiene un 30% más de probabilidad de interceptar"*. La
dirección va en la **clave de plantilla** y no en el signo del número, porque un multiplicador y su inverso
se leen con cifras distintas: `×1,3` es "un 30% más" y `1/1,3` es "un **23%** menos", que es la reducción
verdadera y no la del aumento que la genera. Un efecto con contador dice *"por cada X, hasta N veces"*.

`docs/estilo-descripciones.md` cambia de convención y **anota la desviación**: multiplicar cuotas es
exacto, describirlo como proporción de probabilidad no lo es. El aumento relativo real es
`(k−1)(1−p)/(1+(k−1)p)`: con `k = 1,3` la descripción acierta en `intercept` (29,2% real frente al 30%
escrito) y **exagera en `pass`** (5,6% real frente al 30% escrito). No existe ninguna frase corta en
proporción que sea exacta para toda la escala de bases; la alternativa que sí lo es —hablar de **cuota**,
"multiplica por 1,3 sus opciones de interceptar"— queda propuesta al revisor y anotada como AJ-A en
`pendientes.md`. La convención anterior tenía el defecto simétrico y peor: la misma cifra significaba
cosas separadas por dos órdenes de magnitud según el canal.

### 26.4. La curva de la ADR 0033: doce celdas en banda, con tres jefes recalibrados

Con los perks más débiles, **una build incoherente deja de castigarse a sí misma**: sus perks fallan la
condición y cobran los `elseEffects`, que antes valían ×0,03 y ahora valen ×0,5. La escalera se comprimió
por los dos extremos —la celda incoherente del acto 1 se fue del 26,7% al **46,1%**— y la única palanca
que la ADR 0056 permite es recalibrar a los jefes, nunca la tabla.

| Jefe | Calidad antes | Calidad después |
|---|---|---|
| `grimhold_guns` (acto 1) | 17 | **31** |
| `the_hunt` (acto 2) | 44 | **46** |
| `eternal_crown` (acto 3) | 29 | **31** |

Las doce celdas, medidas con el instrumento de la puerta (32 plantillas × 4 partidos × 5 razas = 640
partidos por celda):

| Jefe | incoherente | correcta | buena | muy buena |
|---|---|---|---|---|
| `grimhold_guns` | 33,3 (≤35) | 70,9 (65-80) | 81,6 (75-88) | 89,8 (85-95) |
| `the_hunt` | 14,8 (≤15) | 40,8 (35-50) | 62,8 (60-72) | 78,6 (72-85) |
| `eternal_crown` | 5,6 (≤10) | 28,0 (15-28) | 41,6 (40-55) | 59,4 (55-70) |

**Las doce dentro, y once de las doce sin necesitar el margen de medida** (la excepción sigue siendo
`eternal_crown` / correcta, como ya ocurría antes del paquete).

El coste de recalibrar el jefe del acto 1 no es pequeño y hay que decirlo: el acto 1 era "el taller"
(ADR 0043) y ahora muerde. Las derrotas en el acto 1 pasan del **22,2% al 29,3%** del total y las runs que
llegan al acto 2 bajan del **82,1% al 75,9%**. El reparto sigue cumpliendo la ADR 0043 —la mayoría de las
derrotas siguen cayendo en el acto 2, 52,3%—, pero el acto 1 ya no perdona.

### 26.5. RT-056, `betterTeamWinRate` y la banda de la ADR 0054

Las siete métricas de sensación de fútbol **no se mueven**: se miden sobre los equipos de referencia, que
no llevan perks. 1.000 partidos, semilla 1: alternancias 24,13 · cadena 2,26 · tiros 11,99 · resultados
75,80 · tercio 41,73 · entradas 9,75 · lesiones 0,74. Todas IN.

`betterTeamWinRate` con 20 puntos de diferencia: **79,52**, exactamente el mismo valor que antes del
paquete y por la misma razón —los equipos de referencia no llevan perks—. La ADR 0054 subió la banda a
**70-88** precisamente porque temía que la P1 la rompiera: **no la roza**. La banda estaba acordada pero
seguía escrita 65-80 en `Sim/Analysis/MatchMetrics.cs` y en la puerta estadística; este paquete la
implementa, con su constante y su comentario, y actualiza `docs/balance.md` y `Balance/README.md`.

Que la métrica no se mueva es en sí mismo un dato: **`betterTeamWinRate` no mide el peso de la build**,
solo el de los atributos. Vigilarla no habría detectado nada de lo que este paquete cambia.

### 26.6. El suelo sin build: la P1 no lo mueve, y por qué

**La medición principal del encargo.** Instrumento: `economy.rewardPerkWeight = 0` (las recompensas dejan
de dar perks) y la política contextual esquivando los mercados, `runWinRate_noMarket`. 300 runs × cuatro
semillas (1, 1001, 2001, 3001) = **1.200 runs** en cada lado.

| | antes | después |
|---|---|---|
| **Suelo sin build** | **12,67%** | **12,08%** |
| … por semilla | 12,00 / 13,67 / 12,33 / 12,67 | 9,33 / 12,00 / 12,67 / 14,33 |
| Perks en el once | 1,48 | 1,44 |

La diferencia es de **−0,59 puntos con un error típico de 1,34**: no se distingue de cero. El suelo **no
ha bajado**. (El 14,5% de la ADR 0057 se midió con 200 runs y una semilla; con 1.200 el mismo instrumento
da 12,67% antes del paquete, así que la comparación honesta es 12,67 → 12,08 y no 14,5 → 12,1.)

**El diagnóstico, y es estructural.** Un equipo sin build no se enfrenta a builds: los rivales ordinarios
de una run se generan con `RunSystems.OpponentFor` → `TeamGenerator.Generate`, **sin ningún perk**
(`PerkAssignment.AssignInitial` solo se llama para la plantilla inicial del jugador). En los ~20 partidos
ordinarios de una run, la capa de perks solo existe en un lado del campo. Los únicos rivales con perks son
los **tres jefes**, que llevan catorce cada uno, y son también el único sitio donde se pierde una run
(`defeatShare_bossMatchLost` = 100%).

De ahí se sigue todo:

1. La P1 hace la capa de perks **más débil** (§26.2). En un partido ordinario eso baja al que tiene build
   y no toca al que no la tiene.
2. Contra el jefe, que lleva catorce perks y el equipo sin build lleva uno y medio, unos perks más débiles
   **favorecen al que no los tiene**. El suelo tendería a **subir**.
3. Lo único que lo compensa es endurecer a los jefes (§26.4), que es una palanca de curva de dificultad y
   afecta por igual al que construye bien y al que no.

Las dos fuerzas se cancelan y el suelo se queda donde estaba. **La P1 no es una palanca sobre el suelo**,
y la hipótesis de la ADR 0057 queda falsificada con la misma clase de medición con la que se formuló.

La palanca que sí lo sería está señalada por la propia 0057 ("eso es diseño de rivales, no de economía") y
ahora tiene nombre concreto: **que los rivales ordinarios lleven perks**. Mientras el 95% de los partidos
de una run se juegue contra equipos sin build, ni el suelo ni la separación entre perfiles pueden depender
de la build.

### 26.7. Los cuatro objetivos de la ADR 0056

1.200 runs por doctrina, semillas 1/1001/2001/3001. "Buena" es la doctrina contextual y "mediocre" la
gastadora, que es como se midió la tabla de la ADR 0056 (la fila de 74,4 / 50,6 / 47,1 es la gastadora).

| Objetivo | Hoy (antes) | Después | Meta | |
|---|---|---|---|---|
| Build buena, partidos de los actos 2 y 3 | 57,1 / 53,3 | **56,8 / 53,0** | 60% | **no alcanzado** |
| Build mediocre, actos 2 y 3 | 47,3 / 44,2 | **50,0 / 45,1** | 42-45% | **no alcanzado, y empeora** |
| Build mala, completar la run | 12,58% | **14,34%** | < 2% | **no alcanzado, y empeora** |
| Build buena, completar la run | 19,25% | **18,00%** | 20-30%, sin subir | no sube; sigue por debajo de la banda |

**Ninguno de los cuatro se cumple, y dos empeoran.** La causa es la misma de §26.6 y se ve en la
separación: el hueco entre buena y mediocre en el acto 2 pasa de **9,8 a 6,8 puntos**. Un partido
ordinario enfrenta una build contra un equipo **sin build**, así que la separación entre perfiles depende
solo del valor absoluto de los perks del jugador, y la P1 lo ha bajado. Subirlo dentro de la escala no es
posible: 48 de los 68 efectos ya están en el máximo.

No se ha forzado ningún número para acercarlos. Mover cualquiera de los cuatro exige la conversación de
curva de dificultad que la ADR 0057 dejaba prevista.

### 26.8. El resto del bucle de run, antes y después

1.200 runs, doctrina contextual salvo donde se indique.

| | antes | después | banda |
|---|---|---|---|
| Tasa de victoria de la run | 19,25 | 18,00 | 20-30 |
| … gastadora / ahorradora | 12,58 / 20,00 | 14,34 / 18,00 | INFO |
| Ganar sin pisar mercado | 20,08 | **16,92** | < 5 (ADR 0055) |
| Partidos por run | 13,92 | 13,20 | INFO |
| Partidos por run completa | 19,46 | 19,44 | 18-22 |
| Llegan al acto 2 / al acto 3 | 82,09 / 37,58 | 75,91 / 33,08 | INFO |
| Derrotas por acto (1/2/3) | 22,2 / 55,1 / 22,7 | 29,3 / 52,3 / 18,4 | mayoría en el 2 |
| Muertes por run | 1,54 | **1,44** | 1,5-3 |
| Lesiones por partido (ambos) | 0,71 | 0,69 | INFO |
| Arcos cerrados | 27,17 | 23,66 | 20-30 |
| Perks en el once | 10,44 | 9,94 | INFO |
| Compras por mercado | 0,94 | 0,92 | 1-2 |
| Oro sin gastar | 11,02 | 12,10 | < 15 |

`deathsPerRun` cae de 1,54 a **1,44** y sale por debajo de su banda de diseño (1,5-3). La causa está
localizada y es de longitud de run, no de letalidad: por **partido** las muertes apenas se mueven (0,111 →
0,109, un 1,4%) y el resto lo pone que la run es más corta (13,92 → 13,20 partidos) por el jefe del acto 1
recalibrado. No se ha tocado `tuning.injury.lethality`: hacerlo sería tapar con una constante de muerte un
efecto de la curva de dificultad.

### 26.9. Estado de las seis puertas

| Puerta | Estado |
|---|---|
| Sensación de fútbol (RT-056 + `betterTeamWinRate` 70-88) | **verde** |
| Rareza y jefe final (RF-024, ADR 0027) | **verde** |
| Equilibrio entre razas (D-29) | **verde** |
| Curva de puertas de la ADR 0033 | **verde**, con los tres jefes recalibrados (§26.4) |
| Run completa | **roja en una afirmación** |
| Criterio de salida de fase 1 (builds) | **roja en dos métricas** |

Tres afirmaciones fallan y **ninguna se ha relajado**: cambiar un rango es una decisión explícita del
revisor (RT-057).

**1. `buildsWinDifferently_passChain` = 1,16, pide ≥ 1,30.** Mide cuánto más alarga la cadena de pases una
build técnica (`elf_tiki_taka`, siete copias de `fine_touch`) que una física (`orc_violence`) respecto de
sus referencias sin perks. Con la fórmula aditiva, un `fine_touch` clavaba el pase en el techo del 98% y la
cadena se disparaba; con cuotas, `×2` sobre una base del 77% da el 87%, y siete copias en siete jugadores
distintos no se apilan. **Es el umbral el que estaba calibrado contra un canal saturado**, y saturar el
canal de pase con un solo perk es justo lo que la P1 impide. Componentes medidos: la técnica alarga 1,145
sobre su referencia y la física 0,927.

**2. `badBuildsLoseToNone_elf_out_of_zone` = 45,21, pide ≤ 45,00.** Fuera por dos décimas. La build es
mala por llevar siete `forward_line` y cuatro `own_third_anchor` en jugadores que no cumplen su condición,
así que vive de los `elseEffects`; el castigo por perk pasa de ×0,15 a ×0,5. Misma causa que la anterior y
del mismo signo.

**3. `TheThreeDoctrinesBuyDifferently`: "la ahorradora debería terminar con más oro sin gastar que la
contextual".** Es **ruido de muestra pequeña**, no una regresión: con las 60 runs de la puerta sale 13,29
frente a 14,18 (orden invertido) y con 1.200 runs sale **14,86 frente a 12,10** (orden correcto y con
margen). La afirmación compara dos medias sin ningún margen sobre una muestra en la que el error típico es
de varios puntos; cualquier perturbación la voltea. Es un test que puede fallar por mala suerte, que es lo
que las convenciones del proyecto prohíben, y la corrección honesta es darle muestra o margen, no
cambiar el juego.

### 26.10. Lo que queda abierto

- **AJ-A · La descripción proporcional exagera en los canales de base alta** (§26.3). Decisión de
  convención del revisor: aceptar la cota, o pasar a la formulación exacta en cuota.
- **AJ-B · Los rivales ordinarios no llevan perks** (§26.6). Es la causa estructural de que ni el suelo ni
  la separación entre perfiles respondan a la capa de build, y por tanto de que la P1 no pudiera ser la
  palanca que la ADR 0057 esperaba. Es el siguiente paquete natural si se quiere mover cualquiera de los
  cuatro objetivos de la ADR 0056.
- **AJ-C · Dos umbrales de la puerta de fase 1 están calibrados contra la fórmula aditiva** (§26.9,
  puntos 1 y 2). Necesitan un ADR que los revise contra el motor nuevo, o una build de medida que no
  dependa de saturar un canal.
- **AJ-D · `TheThreeDoctrinesBuyDifferently` es frágil por tamaño de muestra** (§26.9, punto 3).
- **La P3 sigue suspendida** (ADR 0057) y este paquete refuerza el motivo: si la separación entre perfiles
  no la sostiene la build, subir el crecimiento por nivel solo subiría el peso de los atributos.
- **Z-A queda resuelta**: `data/items` y `data/consumables` pasan ya por la misma escala que los perks
  (`EffectJson` valida contra `ProbabilityScale`), así que se acabaron las dos unidades para el mismo
  campo.
- **Z-B sigue abierta pero cambia de naturaleza**: reponer la mitad de intercepción del Toque élfico ya no
  choca con ninguna escala —con cuotas `interceptEvasion` admite cualquiera de los ocho valores—, solo con
  la necesidad de revalidar la puerta de razas.

## 27. Decisiones de implementación del paquete AK: la exigencia la pone el rival (ADR 0058)

Implementa los **tres puntos de la ADR 0058** en un solo paquete, porque los dos primeros se compensan
entre sí: el techo de la escala de perks pasa a depender de la **rareza**, la capa de build del **rival
ordinario crece con el acto**, y la descripción pasa a hablar de **cuota** en vez de proporción de
probabilidad.

**El resultado es una falsificación, y de las limpias.** Los dos primeros puntos hacen justo lo que la ADR
decía que harían mecánicamente, y **aun así el objetivo central no se mueve**: el hueco entre una build
buena y una mediocre **no se abre**, se estrecha un poco. La causa está medida y es geométrica, no de
calibración: §27.6. El único de los cinco objetivos que se mueve de verdad es el **suelo sin build**, que
por primera vez responde a algo.

### 27.1. El techo de la escala depende de la rareza

La escala gana tres magnitudes por arriba —`±200, ±300, ±500`, es decir ×3, ×4 y ×6— y **deja de ser
plana**: cada rareza alcanza un escalón más.

| Rareza | Techo | k | Techo con contador |
|---|---|---|---|
| Común | 100 | ×2 | 50 (×1,5) |
| Poco común | 200 | ×3 | 100 (×2) |
| Raro | 300 | ×4 | 200 (×3) |
| Legendario | 500 | ×6 | 300 (×4) |

Tres decisiones de implementación:

**1. Ningún común baja.** El techo del común se queda donde estaba el techo único (×2), así que la tabla es
**monótona hacia arriba**: ningún perk del catálogo se debilita por el cambio de escala. Bajar el común
habría sido la otra forma de abrir el abanico y es peor por un motivo medido: el 43% de los perks de una
build buena son comunes (y el 56% de los de una mediocre), así que un común más débil castiga más a quien
construye bien.

**2. El techo de un efecto con contador es un escalón más bajo.** Ahí el multiplicador se aplica hasta `n`
veces y el total es `k^n`, así que el techo de la rareza acota lo que vale **una copia**, no la línea. Sin
ese escalón, `steady_hands` —cuyo efecto viejo valía ×2 987— habría vuelto a clavar el pase en su techo del
98% con cinco copias, que es exactamente la patología que la P1 vino a quitar.

**3. La validación es del cargador, no del esquema.** El esquema JSON no ve la rareza del perk desde el
efecto, así que su rango pasa a ser una cota de cordura (−500..500) y la comprobación real vive en
`PerkLoader.ToMultiplier`, con un mensaje que dice **la rareza, el valor y el techo**. `EffectJson` hace lo
mismo para consumibles, con la rareza del consumible: un efecto no vale distinto por venir de una tienda.

### 27.2. Cómo se reasignaron los 68 efectos: el mismo método de la P1 con el techo nuevo

Se reutiliza el método de §26.2 —calcular el cociente de cuotas que el efecto **aditivo viejo** producía de
verdad en su canal (por la acumulada triangular donde toca) y elegir el valor legal más cercano **en
logaritmo**— cambiando solo la cota. En los efectos con contador el cociente se toma **a tope de contador**
y se le saca la raíz `n`-ésima, que es el cociente por copia.

| Canal y valor viejo | Cociente real | P1 (techo ×2) | ADR 0058 |
|---|---|---|---|
| `pass` +25 (común) | ×2 987 | +100 | +100 |
| `shotOnTarget` +25 (poco común) | ×22,3 | +100 | **+200** |
| `shotOnTarget` +15 (raro) | ×4,05 | +100 | **+300** |
| `interceptEvasion` +5 (raro) | ×256 | +100 | **+300** |
| `intercept` +5 (poco común) | ×3,16 | +100 | **+200** |
| `tackle` +15 (poco común) | ×3,11 | +100 | **+200** |
| `tackle` +15 (común) | ×3,11 | +100 | +100 |
| `tackle` −30 (poco común) | ×0,029 | −100 | **−200** |
| `injury` +3 (legendario) | ×2,58 | +100 | **+200** |
| `tackle` vpc3/max15 (raro) | ×3,11 en 5 copias → ×1,25 | +50 | **+30** |
| `pass` vpc5/max25 (común) | ×2 987 en 5 copias → ×4,95 | +50 | +50 (techo) |

De los 68 efectos de probabilidad, **19 pasan del ×2** (12 a +200, 2 a +300, 5 a −200) y **8 contadores
bajan**, porque el método medido dice que su cociente por copia era de ×1,25 a ×1,37 y el `+50` uniforme de
la P1 les venía grande. Los que más suben son los de las líneas de build —`forward_line`, `last_ditch`,
`spearpoint`, `covering_shadow`, `crowd_control`— y **los tres maestros con recorrido**: `killing_range`
(+300), `first_touch_school` (+300 en `interceptEvasion`) y `granite_line` (+200). `blood_tithe` **no se
mueve** y hay que decirlo: su `injure +2` viejo valía ×2,04, así que el método lo deja en +100 aunque sea
un maestro. La rareza abre el techo; no obliga a usarlo.

### 27.3. La capa de build del rival ordinario, y la etiqueta de estilo que le faltaba

Los quince rivales de `data/rivals/` pasan de **1-4 perks por plantilla sin pendiente** a **2 / 7 / 9** por
acto. El acto 1 se queda **exactamente como estaba** (§27.7 explica por qué no se pudo subir); los actos 2
y 3 triplican y cuadruplican su capa, hasta quedar por debajo del jugador (5,0 / 10,8 / 12,7 perks en el
once al llegar a cada jefe) y muy por debajo de los 14 de un jefe.

**El hallazgo del punto 2 es de datos, no de balance: un rival estático no tenía etiqueta de estilo.**
`RivalTeamBuilder` escribía `StyleTag.Neutral` para todos y no la metía en `Tags`, así que media docena de
perks del catálogo —los que consultan `hasTag(owner,'Bulwark')`, `teammatesWithTag(owner,'Fine')`,
`hasTag(actor,'Cold')`— **no se activaban nunca** en un rival y cobraban sus `elseEffects`. El caso peor
estaba en producción: `act1_orc_ironclad` llevaba `bulwark_stance` en su portero *Neutral*, así que el perk
que debía darle ×2 de entrada **le dividía por 2 la de todo el equipo**. Un rival con perks de su línea se
castigaba a sí mismo. `RivalPlayer` gana `StyleTag` (opcional, `Neutral` por omisión para no mover a nadie
que no la declare) y `RivalTeamBuilder` la compone en `Tags` en el mismo orden que
`PlayerGenerator`: `[SpeciesTag, StyleTag, Position, ...Traits]`.

Con eso, cada rival lleva una build **legible y en su línea**, que es la mitad del encargo: el Bastión de
Granito y los Reyes de Hierro son enanos `Bulwark` con La Muralla (`own_third_anchor`, `pit_veteran`,
`bulwark_stance`, `last_ditch`, y el maestro `granite_line` en el acto 3); los Virtuosos y las Hojas de
Tormenta son elfos `Fine` con El Toque (`fine_touch` repetido, `fine_orchestra`, `silky_veteran`,
`spearpoint` y `killing_range`); la Horda y los Señores de la Guerra son orcos `Brute` con La Carnicería
(`bruised_knuckles`, `shadow_marker`, `pack_mentality`, `brute_boots` y el maestro `blood_tithe`); los
no-muertos son `Cold` y los humanos siguen siendo `Neutral` de manual, con perks puramente posicionales.
Las condiciones se comprobaron una a una contra la colocación 2-3-1 real —`spearpoint` y `gentle_giant`
exigen vínculo `ahead`, que el delantero no tiene, y `center_conductor` exige carril central, que el
interior no ocupa—, así que ningún perk nuevo cobra su `elseEffects` por estar mal puesto.

**El número de perks letales no sube**: siguen siendo cuatro rivales con uno y uno con dos, ninguno en el
acto 1, y `deathsPerRun` no se mueve (1,44 antes y después). Lo que sí sube son las lesiones por partido
(0,69 → 0,74) y las graves por run (1,56 → 1,88), que es lo que se espera de veinte partidos contra
equipos con build.

### 27.4. La descripción habla de cuota

De *"tiene un 30% más de probabilidad de pase"* a *"multiplica por 1,3 sus opciones de pasar"* /
*"multiplies their passing odds by 1.3"*. La convención anterior **mentía** en los canales de base alta: en
`pass` (base 77%) el aumento real de la probabilidad con `k = 1,3` es del 5,6%, no del 30%.

Tres consecuencias de implementación:

- El marcador `{value:odds}` deja de rendir un porcentaje entero y rinde el **factor**: "1,15", "1,3",
  "1,5", "2", "3", "4" o "6". Sin coma flotante y sin depender de la cultura del proceso (RT-023, RT-024):
  la parte entera y la decimal salen de la magnitud entera, y el separador lo elige el idioma de las
  plantillas.
- La **dirección sigue en la clave** de plantilla, pero ahora con la **misma cifra**: `×1,3` es "multiplica
  por 1,3" y `1/1,3` es "divide por 1,3". Se acabaron los dos números por magnitud ("un 30% más" frente a
  "un 23% menos"), que era lo más difícil de explicar de la convención vieja.
- La sección `probabilities` de `data/l10n/` cambia de sustantivo: "probabilidad de pase" pasa a "sus
  opciones de pasar", "pass chance" a "their passing odds". `ProbabilityScale.ToPercent` se retira porque
  ya no la usa nadie.

Esto **cierra AJ-A** con la alternativa exacta que esa entrada dejaba sobre la mesa.

### 27.5. Lo que mide cada punto por separado

El punto 1 se pudo aislar barato (600 runs por lado, semillas 1 y 1001, solo la escala nueva aplicada):

| | antes | solo punto 1 | error típico |
|---|---|---|---|
| Build buena, acto 2 | 55,97 | 56,43 | 0,90 |
| Build mediocre, acto 2 | 50,15 | 50,64 | 0,91 |
| **Hueco, acto 2** | **5,82** | **5,79** | **1,28** |

**El techo por rareza no mueve la separación entre perfiles, y no es una cuestión de tamaño de muestra: es
que no puede.** La razón está medida sobre la composición real de las builds (`finalPerks` de 1.200 runs
por doctrina): una build buena lleva 42,8% de comunes, 45,5% de poco comunes, 11,4% de raros y 0,4% de
legendarios; una mediocre, 56,2 / 35,5 / 8,2 / 0,1. Ponderando `ln(k)` por esa composición, la build buena
sale con un multiplicador medio **un 6,9% mayor** que la mediocre. Y la sensibilidad medida es de **~1,3
puntos de tasa de victoria por cada doblado del peso logarítmico de la capa de perks**, así que 6,9% de
diferencia son **una décima de punto**. La rareza es demasiado parecida entre las dos builds para separarlas.

El punto 2 no se puede aislar barato sin volver a medir la curva entera, y por eso la ADR los mandaba
juntos; su efecto se lee en §27.6 como la diferencia entre "solo punto 1" y el paquete completo.

### 27.6. Los cinco objetivos, y por qué el hueco se estrecha en vez de abrirse

1.200 runs por doctrina (300 × semillas 1/1001/2001/3001). "Buena" es la doctrina contextual y "mediocre"
la gastadora, como en §26.7.

| Objetivo | Antes (P1) | Después | Error típico | Meta | |
|---|---|---|---|---|---|
| Build buena, actos 2 / 3 | 56,83 / 52,98 | **52,42 / 42,05** | 0,64 / 0,97 | 60% | **no alcanzado, y baja** |
| Build mediocre, actos 2 / 3 | 50,02 / 45,10 | **46,14 / 34,43** | 0,67 / 1,07 | 42-45% | acto 2 en el borde; acto 3 se pasa por abajo |
| Build mala, completar la run | 14,33% | **9,92%** | 0,86 | < 2% | no alcanzado, pero mejora un tercio |
| Suelo sin build | 12,08% | **§27.8** | 1,3 | < 10% | |
| **Hueco buena/mediocre, acto 2** | **6,81** | **6,28** | **1,31** | **> 9,8** | **no alcanzado, y se estrecha** |

**La explicación del hueco es geométrica y conviene enunciarla bien, porque invalida la palanca, no la
calibración.** La tasa de victoria de un partido es una sigmoide de la diferencia de fuerza entre los dos
equipos, y su **pendiente es máxima en el 50%**. Antes del paquete, la build buena estaba en el 56,8% y la
mediocre en el 50,0%: las dos a caballo del punto de máxima pendiente, que es donde una diferencia de
fuerza se traduce en el mayor número de puntos posible. Subir la capa de build del rival **baja a las dos**
y las mete en la parte plana de la curva: la misma diferencia de fuerza vale ahora menos puntos. Se ve
sobre todo en el acto 3, donde el rival crece más: el hueco pasa de 7,88 a 7,62 con 9 perks por rival, y
con 11 —la primera versión, medida— se hundía a **3,37**.

De ahí se sigue algo que la ADR no anticipaba: **los objetivos 1 y 2 de la ADR 0056 son incompatibles entre
sí con esta palanca.** "Build buena al 60%" exige que el rival ordinario sea **más débil** frente a una
build buena; "build mediocre al 42-45%" exige que sea **más fuerte** frente a una mediocre. La capa de
build del rival es un solo número y mueve a las dos en el mismo sentido: no hay ningún valor de esa palanca
que ponga a una en el 60 y a la otra en el 43. Lo que haría falta es que la fuerza de una build **buena**
creciera frente a la misma capa de rival —es decir, más recorrido entre "build buena" y "build mediocre" en
el propio catálogo— y eso es lo que el punto 1 intentaba y §27.5 mide que no consigue.

### 27.7. El acto 1, y el guardarraíl que se rompe igual

La primera versión medida subía el acto 1 de 1-2 perks a 3. Costó 3,0 puntos de tasa de victoria en los
partidos del acto 1 (75,17 → 72,16) y llevó las derrotas del acto 1 al **31,25%**. Con el acto 1 revertido
a exactamente lo que era, la tasa de victoria vuelve (74,83) pero la cuota de derrotas se queda en
**30,90%**, por encima del **29,3%** que el encargo fija como techo.

**Y no es del acto 1: es del punto 1.** `defeatShareActN` es la cuota de runs que terminan en el jefe de
ese acto, y el jefe del acto 1 lleva **catorce perks** frente a los **cinco** con los que el jugador llega.
Subir el techo de la escala hace más fuerte a quien más perks lleva, así que el jefe del acto 1 gana peso
relativo aunque no se le toque una cifra. Se ve aislado: con **solo el punto 1** aplicado, la cuota de
derrotas del acto 1 ya sube (35,77% en la semilla 1, frente a 31,25% de la misma semilla antes).

**No se ha recalibrado el jefe.** La ADR 0056 solo permite tocar un jefe cuando una celda de la curva de la
ADR 0033 se sale de banda, y **las doce celdas siguen dentro** (§27.9). Recalibrar `grimhold_guns` para
cuadrar `defeatShareAct1` sería forzar un número contra una puerta que está verde, que es justo lo que
RT-057 prohíbe. Queda anotado como **AK-A**.

### 27.8. El suelo sin build: por primera vez se mueve

Mismo instrumento que §26.6: `economy.rewardPerkWeight = 0` y la política contextual esquivando los
mercados (`runWinRate_noMarket`), 300 runs × cuatro semillas.

| | P1 | ADR 0058 |
|---|---|---|
| **Suelo sin build** | **12,08%** | **10,08%** |
| … por semilla | 9,33 / 12,00 / 12,67 / 14,33 | 8,67 / 9,00 / 11,67 / 11,00 |

La diferencia es de **−2,00 puntos con un error típico de 1,28**: es la primera vez que el suelo responde a
algo —la P1 lo movió −0,59 ± 1,34, indistinguible de cero— y la dirección es la que la ADR 0058 predecía.
Sigue sin cumplir el objetivo, que era **bajar del 10%**, y se queda **en el 10,08%**: justo en la raya, y
por encima de ella.

Que se mueva confirma el diagnóstico de la ADR 0057 y de la 0058 —el suelo lo sostenían los veinte partidos
ordinarios contra equipos sin build— y a la vez enseña su tamaño: **dar build al rival vale dos puntos de
suelo, no siete**. Los otros diez puntos siguen siendo nivel y atributos, que es exactamente la conversación
de la P3 que la ADR 0057 dejó suspendida, ahora con un número al lado.

### 27.9. Las seis puertas

| Puerta | Estado |
|---|---|
| Sensación de fútbol (RT-056 + `betterTeamWinRate` 70-88) | **verde** |
| Rareza y jefe final (RF-024, ADR 0027) | **verde** |
| Equilibrio entre razas (D-29) | **verde** |
| Curva de puertas de la ADR 0033 | **verde**, las doce celdas y **sin recalibrar ningún jefe** |
| Run completa | **verde en los tests**; `runWinRate` sigue OUT como métrica (15,92, banda 20-30) |
| Criterio de salida de fase 1 (builds) | **roja en una métrica**, que antes eran dos |

589 de 591 tests. La única afirmación roja es `buildsWinDifferently_passChain` = **1,19** contra un umbral
de 1,30 (AJ-C, punto 1): mide una build técnica de siete `fine_touch`, que es **común**, así que su techo
sigue siendo ×2 y el umbral sigue calibrado contra un canal saturado que la P1 hizo imposible.

**`badBuildsLoseToNone_elf_out_of_zone` queda arreglada por el paquete**, y era la otra mitad de AJ-C: la
build mala vive de sus `elseEffects` y `forward_line` es **poco común**, así que su castigo pasa de ×0,5 a
×0,333 y la métrica vuelve a banda sin tocar el umbral. Es el efecto que la ADR 0058 esperaba del techo por
rareza, y es el único sitio donde se nota.

`TheThreeDoctrinesBuyDifferently` (AJ-D) pasa en esta ejecución, pero sigue siendo frágil por tamaño de
muestra y no se da por resuelta.

### 27.10. El resto del bucle de run

1.200 runs, doctrina contextual salvo donde se indique.

| | P1 | ADR 0058 | banda |
|---|---|---|---|
| Tasa de victoria de la run | 18,00 | **15,92** | 20-30 |
| … gastadora / ahorradora | 14,33 / 18,00 | 9,92 / 13,92 | INFO |
| Ganar sin pisar mercado | 16,92 | 15,59 | < 5 (ADR 0055) |
| Partidos por run | 13,20 | 12,97 | INFO |
| Partidos por run completa | 19,44 | 19,43 | 18-22 |
| Llegan al acto 2 / al acto 3 | 75,91 / 33,08 | 74,00 / 31,75 | INFO |
| Derrotas por acto (1/2/3) | 29,3 / 52,3 / 18,4 | **30,9** / 50,3 / 18,9 | mayoría en el 2 |
| Muertes por run | 1,44 | 1,44 | 1,5-3 |
| Lesiones por partido (ambos) | 0,69 | 0,74 | INFO |
| Lesiones graves por run | 1,56 | 1,88 | INFO |
| Perks en el once | 9,94 | 9,29 | INFO |
| Compras por mercado | 0,92 | 0,86 | 1-2 |
| Oro sin gastar | 10,68 | 10,89 | < 15 |

La tasa de victoria de la run **baja** de 18,00 a 15,92 y se aleja de su banda por abajo. El guardarraíl del
encargo era el contrario —que no subiera de 30— y se cumple con holgura, pero conviene decir que el paquete
empuja el juego hacia **más difícil para todos** en vez de **más exigente con la build**, que es la
distinción que la ADR 0058 quería hacer.

### 27.11. Lo que queda abierto

- **AK-A · `defeatShareAct1` sube al 30,9% con el acto 1 sin tocar** (§27.7). La causa es el techo por
  rareza sobre los catorce perks del jefe del acto 1, no la capa del rival. Recalibrar el jefe cuadraría el
  número pero rompería la regla de la ADR 0056 (solo se recalibra un jefe cuando su celda se sale), así que
  es decisión del revisor: o se acepta que el acto 1 muerde más, o se revisa la fila del acto 1 de la
  ADR 0033 (que es AJ-E con otro nombre).
- **AK-B · La capa de build del rival y el techo por rareza no abren el hueco entre perfiles** (§27.5,
  §27.6), y la ADR 0058 dice que eso la falsifica. Los objetivos 1 y 2 de la ADR 0056 son incompatibles con
  cualquier palanca que mueva a las dos builds en el mismo sentido; hace falta una que aumente el
  **recorrido del catálogo** entre una build buena y una mediocre, y la que se intentó (que la rareza
  compre cuota) no lo consigue porque las dos builds llevan casi la misma mezcla de rarezas.
- **AJ-A queda cerrada** por §27.4: la descripción habla de cuota y es exacta en todos los canales.
- **AJ-B queda cerrada** por §27.3: los rivales ordinarios llevan build, con pendiente por acto y con la
  etiqueta de estilo que sus perks necesitaban.
- **AJ-C queda a medias**: `badBuildsLoseToNone` se arregla sola con el techo por rareza;
  `buildsWinDifferently_passChain` sigue calibrada contra un canal saturado y necesita un ADR o una build
  de medida que no dependa de saturar el pase.
- **AJ-D y AJ-E siguen abiertas**; AJ-E se agrava y se relee como AK-A.
- **`data/economy/perk-values.json` no se ha regenerado**, igual que en la P1: los valores medidos de la
  ADR 0038 son de antes de la P1 y ahora están dos escalas atrasados. La doctrina contextual elige sus
  perks con ellos, así que su build es peor de lo que podría ser. Regenerarlos cambia a la vez el orden de
  compra y los pesos del pool, o sea el instrumento con el que se ha medido todo este paquete, y por eso no
  se ha hecho aquí. **Es el primer candidato del paquete siguiente.**

## 28. Decisiones de implementación del paquete AL: el instrumento, el diagnóstico y el castigo (ADR 0059 → ADR 0060)

El encargo de la **ADR 0059** fija el orden —primero el instrumento, luego el diagnóstico, y sólo entonces la
palanca— y este paquete lo sigue literalmente. El resultado es que **los dos primeros pasos cambian la
palanca**: la que la 0059 proponía (pagar la coherencia fuera del canal saturado) queda falsificada por su
propia premisa antes de escribirse una línea de código, y en su sitio entra otra, medida, en la **ADR 0060**.
Por primera vez en cuatro paquetes, **el hueco entre una build buena y una mediocre supera los 9,8 puntos**.

### 28.1. El instrumento afinado: el desafine era ruido, no sesgo

`data/economy/perk-values.json` llevaba dos escalas de retraso (§27.11). Se regenera, y de paso se le
multiplica la muestra por ocho: **dos lotes independientes** de `--perk-values --rosters 48 --runs 32` con
semillas 5 y 11, que se **suman** (3.072 partidos por perk, frente a los 384 del protocolo viejo).

El tamaño no es capricho. Con 384 partidos la desviación por fila es de 2,55 puntos de tasa de victoria, o
**51 unidades** de la escala de la tabla; la dispersión **real** entre perks, medida sobre el lote grande, es
de **50 unidades**. Es decir: *la mitad de la varianza de la tabla vieja era ruido de medición*. Y se ve
directamente: la diferencia entre la tabla vieja y la nueva tiene desviación **63,9**, y la diferencia entre
dos lotes nuevos e independientes del mismo tamaño, **46,5**. La tabla vieja no estaba sesgada; estaba
borrosa. Con 3.072 partidos la desviación por fila baja a unas **23 unidades**.

Lo que sí cambia de verdad son **seis filas que no existían**: `killing_range` (+182), `granite_line` (+119),
`first_touch_school` (+82), `blood_tithe` (+56), `second_wound` (+27) e `iron_studs` (−44). Los cuatro
primeros son los **maestros**, y hasta ahora la doctrina contextual los puntuaba con un `?? 0`, es decir a
mitad de tabla; ahora son lo mejor del catálogo y los persigue.

### 28.2. Volver a medir con el instrumento afinado: las tablas no se mueven

1.200 runs por doctrina (300 × semillas 1/1001/2001/3001), mismo protocolo que §27.6. La medición **antes**
reproduce la ADR 0058 hasta la segunda cifra —52,43/41,99, 46,15/34,43, hueco 6,27, suelo 10,09, run
15,92—, lo que valida el banco de pruebas antes de tocar nada.

| | ADR 0058 | instrumento afinado | ET |
|---|---|---|---|
| Build buena, actos 2/3 | 52,43 / 41,99 | **52,28 / 42,03** | 0,29 / 1,40 |
| Build mediocre, actos 2/3 | 46,15 / 34,43 | **45,47 / 35,52** | 0,45 / 1,42 |
| Build mala completa la run | 9,92 | **12,17** | 0,48 |
| Suelo sin build | 10,09 | **10,50** | 0,84 |
| Hueco acto 2 | 6,27 | **6,81** | 0,67 |
| Tasa de victoria de la run | 15,92 | **16,67** | 0,47 |

**Respuesta a la pregunta que la ADR 0059 hacía antes de tocar nada: no, las tablas no se mueven solas.**
Ninguna diferencia pasa de dos errores típicos salvo la de la build mala, que **empeora** 2,25 puntos: con
los pesos del pool recalculados salen otros perks y la gastadora completa más runs. Lo que estos tres
paquetes perseguían no era el desafine.

### 28.3. ¿Concentrar satura? Sí, y no por donde se creía

Medido sobre el juego real y no sobre la fórmula: perks sintéticos de `modifyProbability` puros, todos sobre
**el mismo portador** (un interior, rareza legendaria para tener cinco slots), contra la referencia
`human_none` con **la misma plantilla generada** y 4.800 partidos por celda (±0,72).

| ×2 acumulados sobre el mismo portador | 1 perk | 2 | 3 | 4 |
|---|---|---|---|---|
| `pass` (base 77%) | +0,54 | +0,23 | +1,56 | **+0,19** |
| `shotOnTarget` (base 78,5% realizada) | +1,23 | +0,27 | +0,27 | +0,73 |
| `dribble` (base 72% realizada) | −0,67 | +1,60 | +0,58 | +0,08 |
| `tackle` (base 28% realizada) | +1,81 | +2,42 | +4,04 | +3,79 |
| `intercept` (base 2,5%) | +2,06 | +3,58 | +10,02 | **+15,54** |
| **mezcla** `pass`+`tackle`+`dribble`+`intercept` | +0,54 | +1,85 | +1,48 | **+3,58** |

**Cuatro ×2 concentrados en el pase compran 0,19 puntos de tasa de victoria. Los mismos cuatro ×2 repartidos
entre cuatro canales compran 3,58.** La sospecha de la ADR 0059 se confirma, y con margen.

Pero el diagnóstico que la acompañaba no es el correcto. **No es el techo del 2%-98%: es la base del canal.**
En `pass` el primer perk ya compra medio punto —el canal está saturado desde el primero, no desde el
tercero—, y el techo sólo interviene en el cuarto. Y en `intercept`, con base 2,5%, concentrar **no satura:
acelera**, porque cada ×2 casi dobla una probabilidad diminuta. La "mezcla" queda en medio precisamente
porque promedia un canal muerto con uno vivo.

De ahí la lectura que ordena el resto: **el recorrido de un perk lo fija la base de su canal, no su
magnitud**, y la mitad del catálogo vive en canales de base alta donde multiplicar la cuota no puede comprar
tasa de victoria por mucho que se suba la magnitud. Es una consecuencia de la P1 que la ADR 0050 no
anticipaba y que la ADR 0056 pedía con otras palabras al hablar del "recorrido del catálogo" (AK-B).

### 28.4. La premisa del punto 3 de la ADR 0059 es falsa: la build mediocre es la que concentra

El punto 3 de la ADR 0059 —pagar por completar una línea— se justificaba así: *"discrimina por construcción,
una build mediocre nunca completa una línea, así que nunca cobra"*. Medido sobre los `finalPerks` de 1.200
runs por doctrina, con las líneas de `data/build/arcs.json`:

| doctrina | perks distintos | perks de su mejor línea | ≥3 de una línea | maestros cerrados |
|---|---|---|---|---|
| Contextual (**buena**) | 9,21 | **3,17** | 68,8% | 0,21 |
| Ahorradora | 9,85 | 3,78 | 78,2% | 0,34 |
| Gastadora (**mediocre**) | 10,90 | **4,32** | 87,2% | 0,44 |

**Es al revés de lo que la ADR suponía.** La build que más concentra es la mediocre, y no por casualidad: la
contextual **rechaza** perks (`WorthASlot`, ADR 0038) y termina con un perk y medio menos, mientras la
gastadora compra todo lo barato y acumula de todo, incluida más línea. Además `PursuesMasters` está activo en
las tres doctrinas, así que las tres persiguen arcos. Un pago por coherencia le pagaría **más a la mediocre**
y cerraría el hueco.

Se dice aquí y no en el informe porque es lo que invalida la palanca: **el punto 3 de la ADR 0059 no se
implementa**, y la ADR 0060 recoge por qué.

### 28.5. Lo que sí separa a los dos perfiles, y la asimetría que da la palanca

La diferencia entre las dos doctrinas es casi exactamente **el valor medido de los perks que llevan**: 287,9
milésimas por run la contextual frente a 69,9 la gastadora (tabla nueva, medición final). Y el mecanismo está
en el código y es el del juego: `BestCarrier` comprueba `PerkPlacement.Fits` antes de dar un perk a un
portador —no se gasta un slot en algo que sólo va a aplicar su castigo— y **la gastadora se salta esa
comprobación**. "Construir bien" ya está definido operativamente en el proyecto como *poner cada perk donde
su condición se cumple*.

La palanca sale de la asimetría de la aritmética de cuotas. Mismo instrumento, un solo perk sintético, efecto
sobre **todo el equipo**:

| canal | ×1,15 | ×1,3 | ×1,5 | ×2 | ÷2 | ÷3 | ÷4 |
|---|---|---|---|---|---|---|---|
| `pass` | +0,52 | +2,33 | +0,85 | **+2,02** | −1,02 | −2,70 | **−4,35** |
| `tackle` | +1,23 | −0,12 | +2,95 | +4,73 | −3,50 | −6,38 | −6,50 |
| `dribble` | — | — | — | — | −2,33 | −5,17 | −7,65 |
| `shotOnTarget` | +0,85 | +2,88 | +4,92 | +8,12 | −8,88 | −14,12 | −20,60 |
| `intercept` | +2,12 | +3,23 | +3,77 | +6,98 | — | — | — |
| `save` | +4,35 | +5,33 | +7,50 | +13,60 | — | — | — |

**En un canal de base alta el premio satura y el castigo no.** Doblar la cuota de pase del equipo vale dos
puntos y ahí se queda; dividirla vale 1,0, 2,7 y 4,4 y sigue creciendo. Y una segunda asimetría, del mismo
lote: el mismo castigo sobre el **portador** no vale nada —de −0,15 a −0,98 puntos, dentro del ruido— y sobre
el **equipo** vale de −1 a −8,9.

De las dos sale la palanca de la ADR 0060: **el perk mal puesto lo paga el equipo, y el techo de la rareza no
lo acota.**

### 28.6. La implementación: ocho perks, un techo nuevo y una regla de carga

**El castigo pasa al equipo.** Los `elseEffects` de duración `match` que apuntaban a `owner` pasan a `team`:
`fine_touch`, `fine_orchestra`, `center_conductor`, `flank_specialist`, `forward_line`, `pivot_duo`,
`brute_boots` y `covering_shadow`. Los de duración `play` (`diagonal_press`, `last_ditch`, `safety_net`,
`wing_overlap`) **no se tocan**: son momentáneos y su ámbito es la acción, no el partido.

**El techo de la rareza deja de acotar el castigo.** `ProbabilityScale.DrawbackCeilingFor` da un escalón más
que `CeilingFor` (común 200, poco común 300, raro y legendario 500) y `CounterDrawbackCeilingFor` hace lo
mismo para los efectos con contador. `PerkLoader.ParseEffects` recibe ahora un `drawback` y elige el techo;
el mensaje de error sigue diciendo la rareza, el valor y el techo. `EffectJson` (objetos y consumibles) no
cambia: ahí no hay condición que fallar.

Con el techo nuevo suben tres castigos de canal plano: `fine_touch` y `fine_orchestra` a `team pass -200`
(÷3) y `center_conductor` a `team pass -300` (÷4). `forward_line` **baja** de `-200` a `-100` al pasar al
equipo, porque `shotOnTarget` sobre el equipo es cuatro veces más sensible que los demás canales (−8,88 ya a
÷2) y a ÷3 costaría catorce puntos por un solo perk mal puesto.

**Un escalón se devolvió, y esa es la única corrección forzada del paquete.** `flank_specialist` llegó a
`team dribble -200`; con eso `randomBuildNearNone_human_random` cayó a **38,54** con banda 40-60. Se bajó la
palanca a `-100` y la métrica vuelve a **banda sin tocar el umbral** (RT-057). `human_random` lleva
`forward_line` en un interior y `own_third_anchor` en el delantero: dos perks mal puestos de ocho, y con el
castigo al máximo el "azar" dejaba de estar cerca de su referencia. Ese es el **techo real** de esta palanca,
junto con la celda `incoherente` del jefe del acto 1 (21,6 sobre un mínimo de 20).

**Las descripciones se generan solas** (RT-035): *"Al empezar el partido, si el portador es Fino, el portador
multiplica por 2 sus opciones de pasar; si no, **el equipo** divide por 3 sus opciones de pasar"*. No hay
texto escrito a mano y las plantillas de ámbito de equipo ya existían.

### 28.7. La capa del rival baja a 2/1/2, y los letales no son capa de build

Punto 4 de la ADR 0059. Los quince rivales pasan de **2/7/9** perks por plantilla a **2/1/2**. El acto 1 se
queda exactamente como estaba (AK-A). Lo que queda en los actos 2 y 3 es, en cada rival, **su perk letal si
lo tiene** más una pieza de su línea que le da identidad; los maestros (`killing_range`, `granite_line`,
`blood_tithe`) salen, porque sin su línea detrás no cumplen su `requiresPerks` y no serían legibles.

**El hallazgo del punto: los perks letales no son capa de build.** El primer recorte los quitaba como a
cualquier otro y `deathsPerRun` se hundió de **1,44 a 0,55**, un tercio de su valor y muy por debajo de la
banda 1,5-3. Cinco de los quince rivales llevan uno (`marrow_thirst`, `second_wound`, `skullsplitter`,
`iron_studs`) y **son casi toda la letalidad del juego fuera de los jefes**: la ADR 0048 vive ahí, no en la
capa de build. Con los letales conservados, `deathsPerRun` se queda en 1,46. El coste de conservarlos son
**3,4 puntos** de tasa de victoria de la build buena en el acto 2 (60,95 → 57,53 en la sonda de dos
semillas): el rival que puede matarte es mucho más caro que el rival que sólo juega mejor.

### 28.8. Los seis objetivos

1.200 runs por doctrina (300 × semillas 1/1001/2001/3001), doctrina contextual = "buena", gastadora =
"mediocre" y "mala", como en §26.7 y §27.6.

| Objetivo | ADR 0058 | instrumento afinado | **final** | ET | meta | |
|---|---|---|---|---|---|---|
| Build buena, actos 2/3 | 52,43 / 41,99 | 52,28 / 42,03 | **57,97 / 44,43** | 0,71 / 0,53 | 60% | no alcanzado, faltan 2,0 |
| Build mediocre, actos 2/3 | 46,15 / 34,43 | 45,47 / 35,52 | **47,94 / 40,67** | 0,70 / 0,25 | 42-45% | se pasa por arriba 2,9 |
| Build mala completa la run | 9,92% | 12,17% | **12,00%** | 0,87 | < 2% | no alcanzado |
| Suelo sin build | 10,09% | 10,50% | **10,66%** | 0,56 | < 10% | no alcanzado; sube, como estaba previsto |
| **Hueco buena/mediocre, acto 2** | **6,27** | **6,81** | **10,03** | **0,50** | **> 9,8** | **alcanzado** |
| Tasa de victoria de la run | 15,92% | 16,67% | **17,00%** | 1,28 | 20-30% | no alcanzado, faltan 3,0 |

**El objetivo central de la ADR 0056 se alcanza por primera vez en cuatro paquetes**, y por el camino que la
0056 pedía: no apretando a todo el mundo, sino separando. La prueba de que **el castigo sólo lo paga quien
pone los perks donde no funcionan** es la sonda aislada: con la primera versión de la palanca —solo el cambio
de ámbito de `owner` a `team`, sin subir ninguna magnitud— y el rival **sin tocar** (2/7/9), sobre dos
semillas, la build buena se queda exactamente donde estaba (**52,27** frente a 52,28) y la mediocre baja de
45,47 a **43,69**. El hueco pasa de 6,81 a **8,58** sin que la build buena se mueva un punto.

Está en el borde y hay que decirlo: 10,03 con error típico 0,50 sobre un umbral de 9,8. No es un margen
cómodo.

### 28.9. Por qué el 60% no llega, medido

La capa de build del rival ordinario está **agotada**. Medido en sondas de dos semillas con la palanca ya
aplicada:

| rival ordinario, perks por plantilla | build buena, acto 2 | build mediocre, acto 2 | hueco |
|---|---|---|---|
| 2 / 7 / 9 (ADR 0058) · palanca v1 | 52,27 | 43,69 | 8,58 |
| 2 / 2 / 3 · palanca completa | 56,26 | 45,66 | 10,60 |
| 2 / 1 / 2 · palanca completa | 57,53 | 46,58 | 10,96 |
| 2 / 1 / 2 · **final**, 4 semillas | **57,97** | **47,94** | **10,03** |

(Las tres primeras filas son sondas de **dos** semillas, así que se comparan entre sí y no con la última, que
es la medición completa de 1.200 runs; "palanca v1" es solo el cambio de ámbito, sin subir magnitudes.)

De 7/9 a 1/2 la build buena sube **5,7 puntos** y ahí se acaba el combustible: lo que queda en los actos 2 y 3
es el perk letal, que no se puede quitar sin romper `deathsPerRun` (§28.7). **El 60% no es alcanzable
retocando esta capa**, y el resto de la distancia hay que buscarlo en otra palanca o revisar el objetivo.

Y aparece la incompatibilidad de la ADR 0058 con signo cambiado: bajar el rival sube **más** a la mediocre
que a la buena (de 2/2/3 a 2/1/2, +1,27 la buena y +0,92 la mediocre en el acto 2, pero de 2/7/9 a 2/2/3
+3,99 y +1,97), porque la mediocre está más cerca del 50% y por tanto de la zona de máxima pendiente. El
objetivo 2 (42-45%) se aleja mientras el objetivo 1 se acerca, exactamente como §27.6 predecía.

### 28.10. Las seis puertas

| Puerta | Estado |
|---|---|
| Sensación de fútbol (RT-056) | **verde**; `betterTeamWinRate` 81,68, dentro de la banda 70-88 de la ADR 0054 |
| Rareza y jefe final (RF-024, ADR 0027) | **verde** |
| Equilibrio entre razas (D-29) | **verde** |
| Curva de puertas de la ADR 0033 | **verde**, las doce celdas y **sin recalibrar ningún jefe** |
| Run completa | **verde en los tests**; `runWinRate` sigue OUT como métrica (17,00, banda 20-30) |
| Criterio de salida de fase 1 (builds) | **roja en una métrica**, la misma que en la ADR 0058 |

595 de 597 tests. La única afirmación roja sigue siendo `buildsWinDifferently_passChain`, que **mejora** de
1,19 a **1,23** contra un umbral de 1,30 y sigue midiendo una build de siete `fine_touch` —común, techo ×2—
contra un canal que la P1 hizo imposible de saturar (AJ-C). **No se ha tocado el umbral** (RT-057); necesita
su propio ADR o una build de medida que no dependa de saturar el pase.

Sólo la columna **incoherente** de la ADR 0033 se mueve, que es justo lo que el paquete pretende: 23,8 → 21,6
en el acto 1, 12,3 → 10,9 en el 2 y 1,9 → 2,3 en el final. Las otras nueve celdas quedan idénticas, lo que
confirma que **los tres jefes tienen sus perks bien puestos**: ninguno paga el castigo nuevo.

### 28.11. El resto del bucle de run

1.200 runs, doctrina contextual salvo donde se indique.

| | ADR 0058 | final | banda |
|---|---|---|---|
| Tasa de victoria de la run | 15,92 | **17,00** | 20-30 |
| … gastadora / ahorradora | 9,92 / 13,92 | 12,00 / 14,92 | INFO |
| Ganar sin pisar mercado | 15,59 | 15,00 | < 5 (ADR 0055) |
| Partidos por run | 12,97 | 13,18 | INFO |
| Partidos por run completa | 19,44 | 19,46 | 18-22 |
| Llegan al acto 2 / al acto 3 | 74,00 / 31,75 | 75,33 / 33,34 | INFO |
| Derrotas por acto (1/2/3) | **30,9** / 50,3 / 18,9 | **29,7** / 50,6 / 19,6 | mayoría en el 2 |
| Muertes por run | 1,44 | 1,46 | 1,5-3 |
| Lesiones por partido (ambos) | 0,74 | 0,78 | INFO |
| Lesiones graves por run | 1,88 | 1,78 | INFO |
| Arcos cerrados | — | 21,09 | 20-30 |
| Perks en el once | 9,29 | 9,97 | INFO |
| Compras por mercado | 0,86 | 0,91 | 1-2 |
| Oro sin gastar | 10,89 | 12,50 | < 15 |

**`defeatShareAct1` baja de 30,90 a 29,74** y vuelve por debajo del techo del encargo por primera vez en tres
paquetes: el acto 1 deja de morder más. `deathsPerRun` no se mueve (1,44 → 1,46).

### 28.12. Lo que queda abierto

- **AL-A · El recorrido de un perk lo fija la base de su canal** (§28.3). `pass` (77%), `dribble` (72%) y
  `shotOnTarget` (78,5%) no pueden comprar tasa de victoria multiplicando cuotas por mucho que se suba la
  magnitud —cuatro ×2 sobre el pase valen 0,19 puntos—, mientras `intercept` (2,5%) compra quince puntos con
  los mismos cuatro. Es una consecuencia de la P1 que la ADR 0050 no anticipaba y es, con nombre concreto, el
  "recorrido del catálogo" que la ADR 0056 pedía (AK-B). Arreglarlo es una decisión de fundamentos —mover
  perks a canales con recorrido, o mover la base de los canales altos— y no cabía en este paquete.
- **AL-B · El 60% de la build buena no es alcanzable con la capa del rival** (§28.9). Quedan 2,0 puntos y el
  combustible se ha agotado. O se busca otra palanca (la P3 al revés: **bajar** el peso de los atributos, no
  subirlo) o se revisa el objetivo de la ADR 0056.
- **AL-C · Los objetivos 1 y 2 siguen siendo incompatibles**, ahora medido en las dos direcciones (§28.9):
  bajar al rival sube más a la mediocre que a la buena. El hueco lo abre la palanca de castigo, no el rival.
- **AL-D · `randomBuildNearNone` es el techo de la palanca de castigo** (§28.6), junto con la celda
  `incoherente` del acto 1. Si en el futuro se quiere apretar más la incoherencia, hay que hacerlo por una vía
  que no toque una build de perks al azar.
- **AL-E · `/Balance` marca `betterTeamWinRate` como OUT contra un `65..80` escrito a mano** en
  `Sim/Analysis/MatchMetrics.cs`, mientras las constantes de la misma clase y la **ADR 0054** dicen 70-88. El
  valor medido (81,68) cumple la ADR y no cumple el literal. **No se ha tocado** (RT-057: cambiar ese número
  hace pasar una puerta que hoy falla, y eso es decisión del revisor). Es anterior a este paquete: la misma
  cifra sale con los datos de la ADR 0058.
- **AK-A queda cerrada por la vía buena**: `defeatShareAct1` vuelve a 29,74 sin recalibrar el jefe.
- **AK-B queda diagnosticada**: el recorrido del catálogo no lo limita la rareza, lo limita la base del canal
  (AL-A).
- **AJ-C sigue abierta a medias** y **AJ-D/AJ-E** sin cambios.
- **La P3 sigue suspendida**, y este paquete refuerza el motivo con un número nuevo: el suelo sin build
  responde a la capa del rival (2 puntos, ADR 0058) y **sube** cuando esa capa baja (10,09 → 10,66). Los otros
  ocho puntos siguen siendo nivel y atributos, y la dirección correcta es **bajar** su peso, no subirlo.

## 29. Decisiones de implementación del paquete AM: el peso de los atributos, medido y descartado (ADR 0061, ADR 0062)

El encargo era **AL-B**: bajar el peso de los atributos frente a la build, la P3 de la ADR 0050 al revés,
con el argumento de que sirve a tres objetivos a la vez —sube la build buena, hunde la que no tiene build y
aleja a la mala de completar la run—. **Los tres efectos existen y no ocurren a la vez**, y este paquete lo
mide antes de tocar nada, que era la otra mitad del encargo. El resultado es que **no se mueve ningún número
de balance**: los seis objetivos quedan donde los dejó la ADR 0060 y lo que entra es medición (ADR 0061) más
la recalibración de la única puerta roja (ADR 0062).

### 29.1. El banco de pruebas, y que reproduce la ADR 0060 al decimal

Mismo protocolo que §28.8: 1.200 runs por doctrina (300 × semillas 1/1001/2001/3001), doctrina contextual =
"buena", gastadora = "mediocre" y "mala"; el suelo con `economy.rewardPerkWeight = 0` y la contextual
esquivando mercados. Las sondas son de 600 runs (150 × las mismas cuatro semillas), que es el tamaño con el
que se puede mirar una condición por minuto y sigue dando error típico por debajo de 1,5 puntos en el hueco.

| | ADR 0060 | esta medición | ET |
|---|---|---|---|
| Build buena, actos 2/3 | 57,97 / 44,43 | **57,97 / 44,43** | 0,71 / 0,53 |
| Build mediocre, actos 2/3 | 47,94 / 40,67 | **47,94 / 40,67** | 0,70 / 0,25 |
| Build mala completa la run | 12,00 | **12,00** | 0,87 |
| Suelo sin build | 10,66 | **10,66** | 0,56 |
| Hueco acto 2 | 10,03 | **10,03** | 0,50 |
| Tasa de victoria de la run | 17,00 | **17,00** | 1,28 |

Es la misma semilla sobre los mismos datos, así que la coincidencia es exacta y no una validación
estadística; lo que valida es que el banco de este paquete y el del anterior son el mismo.

### 29.2. El tipo de cambio: qué compra un punto de atributo y qué compra un perk

Es lo primero que pedía el encargo y lo primero que se midió. Tres unidades, todas sobre la tasa de victoria
en partidos **ordinarios** del acto 2:

| unidad | efecto | instrumento |
|---|---|---|
| +1 punto en **cada** atributo de toda la plantilla | **+1,33** | `generation.budgetByRarity` +25 (600 runs) |
| +1 perk **bien puesto** en el once | **+0,93** | `rewardPerkWeight` 0: el once cae de 9,97 a 2,82 perks y la buena de 57,97 a 51,33 (1.200 runs) |
| un nivel de toda la plantilla (+2 en cuatro atributos) | **+2,1** | `attributesPerLevel` 2 → 3 (600 runs) |

**Un punto de atributo sobre la plantilla vale 1,4 perks.** Y sumando cada capa entera, con el once
terminando la run con 9,97 perks y nivel medio 5,11: **la build completa vale +6,6 puntos y la capa de nivel
+13,6**. La segunda es el doble de la primera. Ese es el número que AL-B pedía, y confirma con una cifra el
"los otros ocho puntos son nivel y atributos" de la ADR 0058.

**Una trampa del instrumento que costó una tanda**: `generation.budgetByRarity` y `generation.budgetPerLevel`
**no son mandos del jugador**. Los usa `PlayerGenerator` para todo el que se genera, y los tres jefes se
generan (`quality 31, level 8, rare`), así que subir el presupuesto sube también al jefe. Los rivales
ordinarios **no**: sus atributos están escritos a mano en `data/rivals/`. Por eso la primera tanda medía
+6,7 puntos de partido ordinario y a la vez **menos** runs completadas, que no tenía sentido hasta ver de
dónde venía. El único mando que toca sólo al jugador es `progression.attributesPerLevel`, que es progresión
dentro de la run y no generación.

### 29.3. Ninguna forma de mover ese peso separa a los dos perfiles

Siete condiciones, 600 runs cada una. Se probaron las tres formas que el encargo enumeraba —curva de nivel,
peso de cada atributo en las fórmulas del motor, presupuesto de generación— con sus dos signos:

| palanca | buena 2 | mediocre 2 | **hueco** | suelo | run buena | run mediocre |
|---|---|---|---|---|---|---|
| `attributesPerLevel` 0 | 42,21 | 36,02 | 6,19 | 3,17 | 5,17 | 3,67 |
| `attributesPerLevel` 1 | 50,64 | 40,91 | **9,74** | 6,50 | 8,67 | 6,83 |
| **base (2)** | **57,76** | **47,71** | **10,05** | 14,00 | 16,83 | 11,50 |
| `attributesPerLevel` 3 | 62,12 | 52,39 | **9,72** | 18,00 | 20,67 | 15,00 |
| presupuesto de generación +5 | 64,43 | 54,02 | **10,41** | 14,50 | 14,50 | 7,17 |
| factores de atributo del motor ×0,6 | 55,47 | 47,60 | 7,87 | 10,83 | 10,17 | 7,67 |
| factores de atributo del motor ×1,4 | 57,44 | 47,52 | **9,92** | 16,83 | 17,50 | 12,50 |

(La columna "suelo" de esta tabla es `runWinRate_noMarket` **sin** anular las recompensas de perk, así que
no es el suelo sin build de §29.1 sino su indicador barato; se compara consigo mismo, no con el 10,66.)

**El hueco se queda entre 9,7 y 10,4 en todo el recorrido útil mientras la tasa de victoria se mueve veinte
puntos.** La única celda que rompe el patrón es `attributesPerLevel = 0`, y lo rompe hacia abajo: con la
build buena por debajo del 50% las dos entran juntas en la parte plana de la sigmoide, que es la geometría
que la ADR 0059 ya había enunciado.

Y la incompatibilidad de la ADR 0056 reaparece con signo nuevo. `attributesPerLevel = 3` **alcanza dos
objetivos**: build buena 62,12 (meta 60) y tasa de victoria de la run 20,67, **en banda por primera vez en
cinco paquetes**. Y rompe tres a la vez: mediocre 52,39 (meta 42-45), suelo 18,00 (meta <10) y la mala
completando la run el 15,00% (meta <2). `attributesPerLevel = 1` hace lo contrario, y **cumple el objetivo
del suelo por primera vez** (6,50) a cambio de dejar la build buena en 50,64.

El factor global del motor (`×0,6`) merece una nota porque va en la dirección contraria a la que el encargo
esperaba: bajarlo **estrecha** el hueco (7,87). No es una sorpresa de geometría sino una cadena de causas
medible: el rival del acto 1 es más flojo que el jugador (46,2 de media frente a 50), así que quitar peso a
los atributos borra la ventaja del acto 1 —la buena cae de 74,24 a 67,18—, la run llega al acto 2 con 8,67
perks en vez de 9,86 y un nivel menos, y esa pérdida de build se paga otra vez en el acto 2. **El acto 1 es
el taller y todo lo que lo endurece se cobra dos veces.**

### 29.4. El experimento que lo cierra: compensar el rival no devuelve nada

Si "el peso de los atributos" fuera un grado de libertad, bajar la curva de nivel y compensar la dificultad
con el rival dejaría el mismo resultado medio decidido más por la build. Se hizo:
`attributesPerLevel = 1` con los atributos de `data/rivals/` bajados **1 / 5 / 4** puntos por acto, la
compensación calculada con el tipo de cambio de §29.2.

| | base | `apl` 1 + rival compensado |
|---|---|---|
| buena, actos 1/2/3 | 74,24 / 57,76 / 45,90 | 73,79 / **58,12** / 44,49 |
| mediocre, acto 2 | 47,71 | 47,30 |
| **hueco acto 2** | **10,05** | **10,82** (ET 1,2) |
| perks en el once | 9,86 | 9,49 |
| tasa de victoria de la run, buena | 16,83 | **10,33** |
| build mala completa la run | 11,50 | 5,17 |
| **suelo sin build** | 10,66 | **5,17** |
| muertes por run | 1,43 | **1,19** |
| derrotas del acto 1 | 32,62 | **37,39** |

**La compensación funciona exactamente donde se aplicó y el hueco no se entera** (10,82 contra 10,05, dentro
del ruido). Lo que cambia es la run, y no por el peso de los atributos: el **jefe** es el único rival que no
sale de `data/rivals/` y no se compensó, así que el once llega a las tres puertas cuatro puntos de atributo
más flojo y ahí se va todo.

De ahí el enunciado que la ADR 0061 recoge: **la curva de nivel no es "cuánto pesan los atributos", es la
moneda con la que se llega al jefe.** Y el precio de usarla contra el suelo está medido: para bajarlo de
10,66 a 5,17 hay que romper `deathsPerRun` (1,19, banda 1,5-3), las derrotas del acto 1 (37,39 sobre un
techo de 29,74) y la tasa de victoria de la run (10,33 sobre 20-30). Los tres se rompen **antes** de que el
suelo llegue al 10%.

El experimento espejo (`attributesPerLevel` 3 con el rival subido 2/5/4) confirma la simetría: hueco 9,21,
suelo **20,67**.

### 29.5. Dos hallazgos laterales, y los dos importan

**1. Los perks de la build mediocre valen negativo, y ahí está el 85% del hueco.** Con
`rewardPerkWeight = 0` la doctrina **gastadora mejora**: 47,94 → **49,79** en el acto 2 (1.200 runs, ET
0,74), y el hueco se hunde de 10,03 a **1,54**. Descompuesto: los perks de la build buena valen **+6,6** y
los de la mediocre **−1,8**. Es la confirmación directa de la ADR 0060 —lo que separa es el castigo del
perk mal puesto— y a la vez un aviso incómodo: **hoy no tener build es mejor que tener una mala**, que es lo
contrario de lo que piden los objetivos 3 y 4 de la ADR 0056.

**2. La velocidad no está muerta; la cifra de la ADR 0020 lleva dos motores de retraso.** El encargo citaba
+0,4 puntos (y −1,2 en orcos). Eso es de antes de los cuerpos con volumen, y **D-25 ya lo había corregido**
(+6,6 con `FindSpace`). Esta medición lo confirma por otra vía: partiendo por la mitad el peso de los
atributos **canal a canal**, el más caro de los seis es el de la velocidad.

| canal cuyo peso de atributo se parte por la mitad | buena 2 | coste | hueco |
|---|---|---|---|
| ninguno (base) | 57,76 | — | 10,05 |
| `movement` (velocidad) | 54,76 | **−3,00** | 7,33 |
| `shot` (técnica y fuerza del rematador) | 55,61 | −2,15 | 8,09 |
| `pass` (técnica del pasador y del interceptor) | 56,58 | −1,18 | 10,73 |
| `save` (portero contra técnica del rematador) | 57,01 | −0,75 | 9,70 |
| `dribble` (técnica contra cobertura) | 57,17 | −0,59 | 8,26 |
| `tackle` (presión contra técnica) | 57,26 | −0,50 | 9,32 |

Ninguna mitad mueve el hueco fuera del ruido, así que **el reparto tampoco separa**; pero la tabla desmiente
que quede un atributo sin valor al que quitarle peso saliera gratis. No hay ninguno.

### 29.6. La cadena de pases, recalibrada (ADR 0062)

`buildsWinDifferently_passChain` era la única afirmación roja de las seis puertas: 1,16 → 1,19 → **1,23**
contra un umbral de **1,30**. El umbral estaba calibrado contra la fórmula **aditiva**: `fine_touch` era un
`pass +25` que sumaba 2.500 sobre una base de 7.700 y el canal se recortaba en **9.800, el 98%**; con los
siete titulares llevándolo, la cadena salía en torno a un 30% más larga.

Se midió el canal **aislado** —una build sintética de sólo siete `fine_touch` sobre el once
(`elf_pass_only`) contra `elf_none`, 40 plantillas y 1.440 partidos por celda— recorriendo los cuatro techos
de rareza de la ADR 0058:

| multiplicador de cuota del perk de pase | cadena propia / su referencia |
|---|---|
| ×2 — techo **común**, el que lleva la build de medida | **1,108** |
| ×3 — techo poco común | 1,143 |
| ×4 — techo raro | 1,155 |
| ×6 — techo **legendario**, el de toda la escala | 1,191 |

**Ni con el techo legendario siete perks de pase alargan su propia cadena un 20%**; el 98% de la fórmula
vieja exigiría un ×14,6, que no existe en la escala. Un umbral de 1,30 pedía algo que **ninguna build del
catálogo puede producir**. Es AL-A otra vez, visto en longitud de cadena en vez de en tasa de victoria.

`MinPassChainRatio` pasa a **1,11**: lo que el canal da con los perks que la build de medida puede llevar
legalmente, redondeado a la baja, con la derivación escrita en `Sim.Analysis.BuildMetrics`. No se eligió el
techo de la escala (1,19, o 1,24 normalizado) porque `elf_tiki_taka` lleva **comunes** y pedirle el techo
legendario sería cambiar la afirmación, no calibrar el umbral. Con el número nuevo la puerta mide **1,233**
y pasa, y el margen es atribuible: ×1,108 el canal de pase, ×1,065 los demás perks de posesión de la build,
÷0,958 porque `orc_violence` **acorta** su propia cadena, que es la otra mitad de "ganan de formas
distintas".

### 29.7. Las seis puertas

| Puerta | Estado |
|---|---|
| Sensación de fútbol (RT-056 + `betterTeamWinRate` 70-88, ADR 0054) | **verde**; lote de referencia de 1.000 partidos: `betterTeamWinRate_human_60_vs_human_40` **79,52** IN, y las seis métricas de RT-056 en banda (`possessionChanges` 24,13 · `passChainAvgLength` 2,26 · `shotsPerMatch` 11,99 · `tacklesPerMatch` 9,75 · `injuriesPerMatch` 0,74) |
| Rareza y jefe final (RF-024, ADR 0027) | **verde** |
| Equilibrio entre razas (D-29) | **verde** |
| Curva de puertas de la ADR 0033 | **verde**, las doce celdas, **sin recalibrar ningún jefe** |
| Run completa | **verde en los tests**; `runWinRate` sigue OUT como métrica (17,00, banda 20-30) |
| Criterio de salida de fase 1 (builds) | **verde**, por primera vez desde la P1 (ADR 0062) |

**598 de 598 tests en Release.** No queda ninguna afirmación roja; lo único fuera de banda es `runWinRate`
como métrica, que es lo que la ADR 0061 deja abierto.

### 29.8. Los seis objetivos: sin cambios, y a propósito

| Objetivo (ADR 0056) | ADR 0060 | **este paquete** | ET | meta |
|---|---|---|---|---|
| Build buena, actos 2/3 | 57,97 / 44,43 | **57,97 / 44,43** | 0,71 / 0,53 | 60% |
| Build mediocre, actos 2/3 | 47,94 / 40,67 | **47,94 / 40,67** | 0,70 / 0,25 | 42-45% |
| Build mala completa la run | 12,00% | **12,00%** | 0,87 | < 2% |
| Suelo sin build | 10,66% | **10,66%** | 0,56 | < 10% |
| Hueco buena/mediocre, acto 2 | 10,03 | **10,03** | 0,50 | > 9,8 |
| Tasa de victoria de la run | 17,00% | **17,00%** | 1,28 | 20-30% |

Y los guardarraíles, todos donde estaban: `deathsPerRun` **1,46** (banda 1,5-3, igual que en la ADR 0060),
derrotas del acto 1 **29,74%** (techo 29,74), las doce celdas de la ADR 0033 en banda sin recalibrar ningún
jefe, y `betterTeamWinRate` **79,52** dentro de 70-88.

### 29.9. Lo que queda abierto

- **AL-B queda cerrada como falsificada** (ADR 0061). El peso de los atributos es el mismo número que la
  fuerza del rival: entra en el motor como una **diferencia** y mueve a las dos builds a la vez. La P3 de la
  ADR 0050 se retira en los dos sentidos.
- **AL-A es la única palanca que queda** para los objetivos 1 y 2 de la ADR 0056, que hoy piden un hueco de
  16,5 puntos frente a los 10,03 que hay. Cinco palancas de fuerza probadas y falsificadas en cinco
  paquetes; la única que abrió el hueco fue la asimetría premio/castigo de la ADR 0060 y su techo está
  medido (AL-D).
- **AM-A (nueva)**: *no tener build es mejor que tener una mala* (§29.5). Los perks de la doctrina gastadora
  valen −1,8 puntos en el acto 2, así que quitárselos la mejora. Mientras eso sea cierto, los objetivos
  "suelo < 10%" y "mala < 2%" tiran en direcciones opuestas: lo que hunde a la build mala **sube** el suelo.
- **AM-B (nueva)**: `generation.budgetByRarity` y `generation.budgetPerLevel` **también generan a los tres
  jefes**, así que no sirven como mando del jugador; el único que lo es es
  `progression.attributesPerLevel`. Está anotado porque costó una tanda de medición entenderlo.
- **AJ-C queda cerrada** por la ADR 0062.
- **AL-E queda cerrada**: el literal `65..80` de `MatchMetrics` ya no está y la clase decide con sus propias
  constantes `BetterTeamWinRateMin`/`Max` (70-88, ADR 0054).
- **AL-C y AL-D** sin cambios.

## 30. Decisiones de implementación del paquete AN: de dónde sale el −1,8, las cuatro acciones muertas y por qué el premio no separa (ADR 0063)

El encargo era **AM-A** —*hoy no tener build es mejor que tener una mala*— con dos mediciones obligatorias
antes de elegir palanca y una hipótesis a falsificar: que los perks **desbloqueen comportamiento** en vez
de mover números. Las dos mediciones están hechas; la hipótesis del encargo y la que salió de la medición
quedan **las dos falsificadas**, y por la misma razón de fondo. Como en el paquete AM, **no se mueve
ningún número de balance**: lo que entra es medición (ADR 0063) y un instrumento (`--utility-census`).

### 30.1. El banco, y que vuelve a reproducir la ADR 0060 al decimal

Mismo protocolo que §28.8 y §29.1: 1.200 runs por doctrina (300 × semillas 1/1001/2001/3001), contextual =
"buena", gastadora = "mediocre" y "mala", el suelo con `economy.rewardPerkWeight = 0`. Las sondas son de
600 runs (300 × semillas 1 y 1001).

| | ADR 0060/0061 | esta medición | ET |
|---|---|---|---|
| Build buena, actos 2/3 | 57,97 / 44,43 | **57,97 / 44,43** | 0,71 / 0,53 |
| Build mediocre, actos 2/3 | 47,94 / 40,67 | **47,94 / 40,67** | 0,70 / 0,25 |
| Build mala completa la run | 12,00 | **12,00** | 0,87 |
| Suelo sin build | 10,66 | **10,66** | 0,56 |
| Hueco acto 2 | 10,03 | **10,03** | 0,50 |
| Tasa de victoria de la run | 17,00 | **17,00** | 1,28 |
| Muertes por run · derrotas del acto 1 | 1,46 · 29,74 | **1,46 · 29,74** | 0,02 · 1,87 |

### 30.2. El −1,8 es castigo entero (fase 1.1)

La condición de control es el **catálogo sin `elseEffects`**: se borran los 17 bloques de castigo de
`data/perks/`. Es un instrumento limpio **para la doctrina gastadora**, porque su `Rank` de mercado es
`-precio` y su `BestCarrier` devuelve el elegible de menor id: quitar los castigos no cambia qué compra ni
dónde lo coloca, solo lo que el perk hace en el campo. (Para la contextual sí es un confundido: su `Rank`
premia con 1.000.000 al perk que no castiga, y sin castigos ese término se aplica a todos.)

| valor de los perks de recompensa, acto 2 | con castigo | sin castigo | castigo |
|---|---|---|---|
| Build buena | **+6,64** (ET 1,06) | +6,14 (ET 0,63) | −0,49 |
| Build mediocre | **−1,85** (ET 1,38) | **+3,55** (ET 0,58) | **−5,40** |
| Hueco del acto 2 | **10,03** (ET 0,50) | **4,01** (ET 0,94) | 6,02 |

Tres lecturas, y las tres importan:

1. **El −1,8 es castigo entero.** Los perks de la build mediocre, cuando no pueden castigar, valen +3,55.
2. **El castigo no toca a quien construye bien**: +6,64 con castigos y +6,14 sin ellos es la misma cifra
   dentro del error. Es la confirmación independiente del punto 2 de la ADR 0060.
3. **El 60% del hueco es castigo.** Y sin él no hay hueco alcanzable: con `hueco = 1,54 + 2,59k` (donde
   1,54 es la diferencia entre las dos doctrinas **sin** build y *k* el factor por el que se multiplicara
   el premio), llegar a 9,8 sin castigo pide **k ≥ 3,19**, que deja a la build buena en el **71%** del
   acto 2.

### 30.3. Perk a perk: el castigo se concentra en cuatro, y no hay perks que resten bien puestos

Se remide `--perk-values` (48 × 32, semillas 5 y 11 sumadas: 3.072 partidos por fila) sobre el catálogo sin
castigos. **El control sale perfecto**: los 35 perks sin `elseEffects` reproducen la tabla vigente
exactamente —diferencia media 0,0, desviación 0,3 unidades contra una desviación de fila de 23—, porque
con los mismos datos y las mismas semillas los partidos son bit a bit los mismos. Eso convierte la
diferencia de los 16 perks que castigan en una **medida exacta** del castigo, no en una estimación.

| perk | base | sin poder castigar | castigo |
|---|---|---|---|
| `spearpoint` | −122 | +57 | **179** |
| `bulwark_stance` | −46 | +16 | 62 |
| `own_third_anchor` | −79 | −29 | 50 |
| `gentle_giant` | +40 | +70 | 30 |
| `last_ditch` · `fine_touch` · `pack_mentality` | 7 / 18 / 9 | 17 / 27 / 15 | 10 / 9 / 6 |
| los otros nueve | | | ≤ 2 |

Cuatro perks son el **83%** del castigo del catálogo (349 unidades) y `spearpoint` él solo el 51%. La
unidad de la tabla son **20 por punto** de tasa de victoria, así que `spearpoint` mal puesto cuesta 8,9
puntos.

**Y la segunda mitad de la pregunta del encargo: no, no hay perks que valgan negativo estando bien
puestos.** Dieciséis miden negativo sin poder castigar, pero entre −2,2 y −0,3 puntos contra una
desviación de fila de 1,15; solo cuatro llegan a dos desviaciones y ninguno de los cuatro es "un perk que
resta":

- `cold_focus`, `box_predator`, `long_range_menace`, `poacher_instinct` son `actor shotOnTarget ×2` en una
  jugada: **AL-A** puro, canal de base alta sin recorrido, y el signo es ruido alrededor de cero.
- `clean_sheet_legacy` es un perk de **acumulación** medido con el contador a cero. El instrumento mide un
  solo partido, así que **infravalora por construcción a los 15 perks que acumulan entre partidos**
  (RF-070). Es un sesgo del instrumento con nombre, no del perk.
- `iron_studs` es **letal**: matar no es una palanca de tasa de victoria y la tabla no la mide.

La frase que queda es la contraria de la que el encargo temía: **el problema del catálogo no es que haya
perks que resten, es que hay perks que no suman.** Medidos sin su castigo, **22 de los 51 valen menos de
un punto** de tasa de victoria. Es AL-A cuantificado perk a perk.

### 30.4. El censo de utilidad, y por qué cuatro acciones no ganan nunca (fase 1.2)

`/Balance` gana el modo **`--utility-census N`**: repite el mismo partido de referencia una vez por
(jugador, tick) muestreado con `SimConfig.DumpUtility` y acumula las tablas. **No toca `/Sim`**: es el
volcado de RT-098 que ya existía, agregado. Cuesta un partido por muestra —20 jugadores × 40 ticks × N— y
es barato en código a cambio de ser exactamente lo que la tabla de utilidad decide.

Sobre 6.433 decisiones de `human_50` contra `human_50` (12 partidos, semilla 1):

| acción | descartada % | elegida % | score medio | mejor score | margen medio al ganador |
|---|---|---|---|---|---|
| `FindSpace` | 59,4 | 39,50 | 975 | 1.497 | 3 |
| `CoverSpace` | 9,9 | 36,59 | 395 | 955 | 335 |
| `Retreat` | 0,0 | 18,79 | 290 | 843 | 399 |
| `ChaseBall` | 14,5 | 2,58 | −76 | 1.255 | 807 |
| `ShortPass` | 0,0 | 0,95 | 603 | 1.192 | 181 |
| `MarkOpponent` | 14,5 | 0,75 | 98 | 600 | 633 |
| `Tackle` | 7,2 | 0,28 | −684 | 887 | 1.371 |
| `Block` | **74,9** | 0,06 | 8 | 682 | 714 |
| `PressCarrier` | **81,5** | 0,05 | 51 | 485 | 473 |
| `OfferSupport` | 59,4 | **0,00** | 133 | 396 | 845 |

(El censo muestrea todos los estados de decisión, y los de balón —`ShortPass`, `Dribble`, `Shoot`,
`LongPass`— solo son legales para el poseedor, así que salen infrarrepresentados frente al reparto que ve
el campo animado. Las cuatro acciones del encargo son todas de **sin balón** y no les afecta.)

Las tres respuestas que había que separar:

1. **No es un peso mal puesto.** Ninguna pierde por poco: pierden por 473 a 845 puntos, y **el mejor score
   que han sacado nunca está por debajo del score medio del ganador**.
2. **`Block` y `PressCarrier` están descartadas el 75% y el 82% de las veces**, y no por peso: o no hay
   rival al alcance de la carga dentro de la jugada activa (RF-057, `blockReachMaxCells` 1,2 casillas), o
   no hay poseedor rival. Con peso infinito seguirían sin poder elegirse en tres de cada cuatro decisiones.
3. **`OfferSupport` está dominada por construcción.** Es legal exactamente cuando lo es `FindSpace` y
   compite con 80-160 de peso base contra 200-460, y 150 de multiplicador táctico contra 210. Es lo que
   `fase1b-diseno.md` §16 ya decía; ahora está en la tabla.

Un volcado suelto para el registro (jugador 3, tick 401, defensa sin balón): `CoverSpace` 676 gana;
`PressCarrier` 277, `MarkOpponent` 244, `OfferSupport` y `Block` **descartadas**.

### 30.5. Despertarlas cuesta la puerta de la sensación de fútbol

Se sube el peso base de cada acción lo que el censo dice que hace falta para que gane —×3, ×5, ×3 y ×9— y
se mide el lote de referencia de 600 partidos, semilla 1:

| variante | entradas/partido | lesiones/partido | cadena de pases | cambios de posesión | `betterTeamWinRate` |
|---|---|---|---|---|---|
| **base** | 9,78 | 0,71 | 2,25 | 24,19 | 79,00 |
| `MarkOpponent` ×3 | **0,55** OUT | 0,25 OUT | 1,63 OUT | 21,17 | 67,00 OUT |
| `OfferSupport` ×5 | 11,94 | 0,91 OUT | 1,79 OUT | 29,15 OUT | 63,00 OUT |
| `PressCarrier` ×3 | **2,95** OUT | 0,26 OUT | 2,25 | 22,76 | 80,00 |
| `Block` ×9 | 9,45 | **1,31** OUT | 2,28 | 22,13 | 84,00 |

Las cuatro rompen RT-056 y tres lo rompen por el mismo sitio: **la entrada y la lesión**, que es donde vive
la ADR 0048. Reproduce lo que `fase1b-diseno.md` §21 midió en su día (presionar hundía las entradas de
13,0 a 1,0 y las lesiones de 0,82 a 0,05) y §19 (el bloqueo a peso 300 daba 22 faltas, 2,5 rojas y 37
incomparecencias de 40 partidos).

La medición sube el peso **para los dos equipos**, así que un perk que desbloquee la conducta en un
portador movería el mismo canal a un séptimo de escala; el signo, que es lo que importa, es el mismo. **La
hipótesis del encargo no se sostiene y no se fuerza**: las cuatro acciones no están dormidas por descuido,
están amortiguadas a propósito y el precio de despertarlas está medido.

### 30.6. La palanca que la medición sí sostenía: el ámbito del premio

De §30.2 sale una palanca con la forma correcta. Si el −1,8 es castigo entero y el premio de la mediocre ya
es positivo, basta con que **el premio valga más**, porque el premio lo cobra sobre todo quien coloca bien
(+6,14 contra +3,55, 1,7 a 1). Y la aritmética dice cuánto hace falta:

```
hueco = 1,54 + V_buena − V_mediocre
comprar > no comprar  →  V_mediocre > 0
hueco ≥ 9,8           →  V_buena ≥ 8,26      (hoy 6,64: un +24%)
```

El recorrido estaba en la propia tabla de la ADR 0060 §28.5: **el mismo efecto vale de dos a cuatro veces
más sobre el equipo que sobre el portador** (`pass` ×2: +0,54 en el portador y +2,02 en el equipo; `tackle`
×2: +1,81 y +4,73). El punto 2 de la ADR 0060 movió los **castigos** de `owner` a `team`; su espejo es
mover los **premios**, y de paso arregla una asimetría que hoy se lee en la descripción generada: ocho
perks tienen el castigo sobre el equipo y el premio sobre un solo jugador.

| perk | premio antes | premio en la prueba | castigo (sin tocar) |
|---|---|---|---|
| `fine_touch` | `owner pass ×2` | `team pass ×2` | `team pass ÷3` |
| `flank_specialist` | `owner dribble ×2` | `team dribble ×2` | `team dribble ÷2` |
| `own_third_anchor` | `owner tackle ×2` | `team tackle ×2` | `team tackle ÷2` |
| `bulwark_stance` | `owner tackle ×2` | `team tackle ×2` | `team tackle ÷2` |
| `pivot_duo` | `linked tackle ×2` | `team tackle ×2` | `team tackle ÷3` |
| `forward_line` | `owner shotOnTarget ×3` | `team shotOnTarget ×1,5` | `team shotOnTarget ÷2` |
| `spearpoint` | `linked shotOnTarget ×3` | `team shotOnTarget ×1,5` | `team shotOnTarget ÷3` |
| `covering_shadow` | `linked intercept ×3` | `team intercept ×1,5` | `team intercept ÷1,5` |

Las descripciones se generan solas y quedan simétricas (RT-035): *"si el portador es Fino, **el equipo**
multiplica por 2 sus opciones de pasar; si no, **el equipo** divide por 3 sus opciones de pasar"*.

### 30.7. Y por qué se cae: el catálogo es compartido y la oposición lo lleva bien puesto

Sonda de 600 runs (2 semillas), con `data/economy/perk-values.json` **regenerado** para el catálogo nuevo
—sin regenerarlo la doctrina contextual sigue rechazando por valor medido justo los perks que se acaban de
mejorar, y la sonda mide el instrumento en vez de la palanca—:

| | base | premio al equipo |
|---|---|---|
| buena, acto 2 | 57,53 | **56,28** |
| mediocre, acto 2 | 47,01 | 46,50 |
| hueco | 10,53 | 9,78 |
| acto 1 | 75,84 | **72,39** |
| **derrotas del acto 1** | **28,38** | **43,98** |
| **muertes por run** | **1,46** | **1,04** |
| tasa de victoria de la run | 14,83 | 9,17 |
| perks en el once | 9,92 | 8,88 |

**La build buena empeora**, y dos guardarraíles se rompen a la vez (`deathsPerRun` 1,04 sobre una banda de
1,5-3 y las derrotas del acto 1 en 43,98 sobre un techo de 29,74). La causa no es de calibración:

> **Cinco de los ocho perks están en `data/rivals/` y los tres jefes los llevan.** `grimhold_guns` (acto 1)
> tiene **14 slots de perk** —dos `own_third_anchor`, `bulwark_stance`, dos `pivot_duo`, `forward_line`—,
> `the_hunt` otros 14 y `eternal_crown` **27**. El jugador llega a esas tres puertas con **3,2 / 6,3 /
> 8,6** perks (`groups.json`, `actDensity`). Subir lo que da un perk **bien puesto** le da al jefe del acto
> 1 catorce perks mejores y al jugador tres.

Aislado en dos pasos:

| | buena 2 | acto 1 | derrotas acto 1 | hueco | muertes | run |
|---|---|---|---|---|---|---|
| base | 57,53 | 75,84 | 28,38 | 10,53 | 1,46 | 14,83 |
| premio al equipo | 56,28 | 72,39 | 43,98 | 9,78 | 1,04 | 9,17 |
| … quitando los 8 a los **rivales** | 56,96 | 73,32 | 40,55 | 8,10 | 1,10 | 10,00 |
| … quitándoselos también a los **jefes** | 58,48 | 76,64 | 22,62 | 9,31 | **1,68** | **23,33** |

Quitárselos a los quince rivales recupera el acto 2 pero **no** el acto 1: lo que queda ahí es el jefe.
Quitárselos también a los tres jefes son **19 slots menos**, un recorte bruto y no una recalibración, así
que la última fila es una **cota superior** y no un resultado. Lo que demuestra es que el margen existe
**si se recalibra a la oposición a la vez**, que es un paquete propio.

### 30.8. La afirmación que cierra las seis palancas

La ADR 0060 §28.10 midió la mitad: *"los tres jefes tienen sus perks bien puestos: ninguno paga el castigo
nuevo"*. La otra mitad es la de este paquete:

> **El castigo es el único canal del catálogo que la oposición no comparte.** Ni los quince rivales ni los
> tres jefes colocan mal un perk, así que ninguno paga nunca el castigo; y todos cobran el premio, con dos
> y tres veces más perks que el jugador en cada puerta. Por eso el castigo es la única palanca que ha
> abierto el hueco, y por eso su espejo **no es una palanca sino un mando de dificultad**, igual que el
> peso de los atributos de la ADR 0061.

Seis palancas en cinco paquetes con una sola explicación: oro y precios (ADR 0055), lo que vale un perk
(P1), techo por rareza y capa de build del rival (ADR 0058), pago por coherencia (ADR 0059), peso de los
atributos (ADR 0061) y el ámbito del premio (esta) **mueven al jugador y al rival a la vez**. La del
castigo (ADR 0060) no.

### 30.9. Las seis puertas y los seis objetivos

Sin cambios: no se ha tocado `/Sim` ni `/data`. **598 de 598 tests en Release** y las seis puertas verdes,
las mismas de §29.7. Los seis objetivos, exactamente los de §29.8.

Lo único que entra en el repositorio es `--utility-census` en `/Balance` (`UtilityCensusRunner.cs`,
`Options.cs`, `Program.cs`, `README.md`), que no toca ninguna métrica ni ninguna puerta.

### 30.10. Lo que queda abierto

- **AM-A queda diagnosticada del todo, y devuelve una elección al revisor** (ADR 0063). *Comprar siempre
  mejor que no comprar* es `V_mediocre > 0`, y eso **fija a la build mediocre en 49,8 o por encima**,
  porque 49,79 es lo que gana esa misma doctrina **sin** perks. El objetivo 2 de la ADR 0056 la quiere en
  42-45. Los dos no pueden ser verdad a la vez: "mediocre al 42-45%" *es* la frase "construir mal es peor
  que no construir". Hay tres salidas y ninguna es una calibración.
- **AN-A (nueva)**: el premio del catálogo no separa mientras la oposición lo comparta. Subirlo exige
  recalibrar a la vez `data/rivals/` y los 55 slots de perk de los tres jefes.
- **AN-B (nueva)**: `--perk-values` mide **un partido**, así que infravalora por construcción a los 15
  perks de acumulación (RF-070), y esa tabla decide el peso del pool y el orden de compra de la doctrina
  contextual.
- **AL-A sigue abierta y sigue siendo la decisión de fondo**, ahora con una cifra por perk: 22 de los 51
  perks del catálogo, medidos sin su castigo, valen menos de un punto de tasa de victoria.
- **AL-C, AL-D y AM-B** sin cambios.

## 31. Decisiones de implementación del paquete AO: qué cuesta perder un partido ordinario (ADR 0064)

El encargo era la **tercera salida de la ADR 0063** —bajar el suelo sin build por la vía de **qué cuesta
perder un partido ordinario** (RF-002c)—, la única de las tres que nadie había medido y el último parámetro
grande del recorrido sin caracterizar. Fase 1 medir, fase 2 sólo si la medición lo sostiene. **No lo
sostiene**, así que, como en los paquetes AM y AN, **no se mueve ningún número de balance**: entra medición
(ADR 0064) y un instrumento en cero.

### 31.1. El banco, y que vuelve a reproducir la ADR 0060 al decimal

Mismo protocolo que §28.8, §29.1 y §30.1: 1.200 runs por perfil (300 × semillas 1/1001/2001/3001),
contextual = "buena", gastadora = "mediocre" y "mala", el suelo con `economy.rewardPerkWeight = 0` y la
política que esquiva mercados. Las sondas son de 600 runs (300 × semillas 1 y 1001).

| | ADR 0060/0061/0063 | esta medición | ET |
|---|---|---|---|
| Build buena, actos 2/3 | 57,97 / 44,43 | **57,97 / 44,43** | 0,71 / 0,53 |
| Build mediocre, actos 2/3 | 47,94 / 40,67 | **47,94 / 40,67** | 0,70 / 0,25 |
| Build mala completa la run | 12,00 | **12,00** | 0,87 |
| Suelo sin build | 10,66 | **10,67** | 0,56 |
| Hueco acto 2 | 10,03 | **10,03** | 0,50 |
| Tasa de victoria de la run | 17,00 | **17,00** | 1,28 |
| Muertes por run · derrotas del acto 1 | 1,46 · 29,74 | **1,46 · 29,74** | 0,02 · 1,87 |

Para desglosar el perfil **sin build** por acto hacía falta un volcado que no existía: la política de
control de la ADR 0055 es contextual, así que en `runs.csv` era indistinguible de la build buena. `/Balance`
escribe ahora también **`runs-nomarket.csv`** con sus runs. Es el único cambio de instrumentación que el
paquete necesitó para medir, y no toca ninguna métrica ni ninguna puerta.

### 31.2. Separar el partido ordinario del de jefe es exacto, no una estimación

La derrota contra el jefe **termina la run** (RF-002), así que la única derrota de jefe de una run es la
terminal y se identifica con `cause = BossMatchLost` y el acto alcanzado. Los jefes ganados salen de
`bossesBeaten`. Todo lo demás de `matchesAct{n}` —liga y élite— es ordinario, y perderlo no termina la run
(RF-002c). No hace falta instrumentar el motor.

### 31.3. Las tres builds pierden el mismo número de partidos ordinarios

| perfil | ordinarios jugados | **ordinarios perdidos** | tasa de derrota ordinaria | a1 | a2 | a3 |
|---|---|---|---|---|---|---|
| Build **buena** | 11,09 | **4,03** | 36,3% | 1,19 | 1,74 | 1,10 |
| Build **mediocre/mala** | 10,24 | **4,16** | 40,7% | 1,35 | 2,00 | 0,82 |
| **Sin build** (suelo) | 10,37 | **4,03** | 38,8% | 1,24 | 1,92 | 0,88 |

**4,03 contra 4,16 contra 4,03.** El equipo sin build pierde a mayor *ritmo* (38,8% frente a 36,3%) pero
juega menos partidos, porque su run se corta antes en la puerta del jefe: **la truncadura compensa el ritmo
casi exactamente**. Un castigo de D por derrota ordinaria es, a primer orden, un impuesto uniforme de 4·D
por run para los tres perfiles, que es la palanca de oro de la ADR 0055 con otro nombre.

### 31.3b. Un efecto colateral de haber separado el jefe: la métrica publicada incluye la puerta

Separar los partidos ordinarios de los de jefe deja ver que `winRateAct{n}` —la métrica con la que se
publican los objetivos 1 y 2 desde la ADR 0056— **cuenta también el partido de jefe**, mientras que la
tabla de la propia ADR 0056 dice "partidos **ordinarios** (perder uno no termina la run, RF-002c)". Las dos
cifras, sobre las mismas 1.200 runs:

| perfil | acto 2 con jefe | acto 2 **sólo ordinarios** | acto 3 con jefe | acto 3 **sólo ordinarios** |
|---|---|---|---|---|
| Build buena | 57,97 | **60,37** | 44,43 | 43,26 |
| Build mediocre | 47,94 | 50,42 | 40,67 | 38,64 |
| Sin build | 50,78 | 53,45 | 39,83 | 39,07 |
| **Hueco del acto 2** | **10,03** | **9,95** | | |

No cambia ninguna conclusión —el hueco es el mismo dentro del error y los siete paquetes se comparan entre
sí con la misma métrica— pero sí cambia **cómo se lee el objetivo 1**: medido como su ADR lo describe, la
build buena ya está en **60,37** en el acto 2 y sigue lejos (43,26) en el 3. Queda anotado en **AO-D**; no
se toca la métrica (RT-057).

### 31.4. Y perder ya cuesta un tercio de la economía de la run

| perfil | oro no cobrado | oro ganado | derrotas como % de la economía | recompensas perdidas | perks que no se compran |
|---|---|---|---|---|---|
| Build buena | 44,1 | 86,22 | **33,8%** | 4,03 | 1,8 |
| Build mediocre/mala | 44,7 | 71,13 | **38,6%** | 4,16 | 1,9 |
| Sin build | 43,6 | 77,84 | **35,9%** | 4,03 | 1,8 |

Oro no cobrado = derrotas ordinarias del acto × el oro base de ese acto (9/11/13); "perks que no se
compran" reparte ese oro a 24, el precio de un perk raro. Es el **segundo sumidero de la run** por detrás
del mercado (63,4) y por delante de clínica + matrícula + rerolls juntos (22,0) — y las tres builds pagan
la misma factura.

### 31.5. El instrumento, y las cinco magnitudes medidas

`economy.defeatGoldPenalty` (oro fijo) y `economy.defeatGoldPenaltyPercent` (porcentaje del oro **en
mano**) se cobran en `StandardRunSystems.AfterMatch`, en la rama `!summary.Won`, que es exactamente la
derrota ordinaria: la derrota contra el jefe no llega ahí porque `RunEngine.ResolveMatch` corta antes
cuando el desenlace termina la run. El oro no baja de cero (`RunState.WithGold`), así que no hay deuda.
Los dos entran en `/data` y en el esquema con valor **0**: el juego de hoy es idéntico.

Las magnitudes no son tímidas: **+12 de oro por derrota es más de lo que paga ganar** (9/11/13) y **−50%
del oro en mano** es media bolsa en cada tropiezo.

| condición | buena a2 | mediocre a2 | hueco | run buena | run mala | **SUELO** | derrotas a1 | muertes |
|---|---|---|---|---|---|---|---|---|
| **hoy** (1.200) | 57,97 | 47,94 | 10,03 | 17,00 | 12,00 | **10,67** | 29,74 | 1,46 |
| −50% del oro (1.200) | 57,78 | 47,34 | 10,44 | 16,33 | 8,58 | **10,58** | 31,30 | 1,48 |
| −75% del oro (1.200) | 57,37 | 47,67 | **9,71** | 16,25 | 10,58 | **11,50** | 30,16 | 1,50 |
| +3 de oro (600) | 57,94 | 46,87 | 11,07 | 14,67 | 9,67 | **9,50** | 29,89 | 1,39 |
| +6 de oro (600) | 58,82 | 47,92 | 10,90 | 15,33 | 11,50 | **8,67** | 31,30 | 1,48 |
| +12 de oro (600) | 57,39 | 48,37 | 9,01 | 14,00 | 11,50 | **11,17** | 30,60 | 1,45 |
| −25% del oro (600) | 57,45 | 48,58 | 8,87 | 13,83 | 11,00 | **8,83** | 30,35 | 1,40 |

(Las filas de 600 runs se comparan entre sí y con la base de 600 runs —buena 57,53 · mediocre 47,01 · hueco
10,53 · run 14,83 · mala 12,00 · suelo 10,00 · derrotas a1 28,38 · muertes 1,46—, nunca con la de 1.200.
La columna SUELO de las filas de 600 sale de la condición `rewardPerkWeight = 0` medida en paralelo.)

Cuatro lecturas:

1. **El suelo no se mueve y no tiene orden**: 8,67 con +6 y 11,17 con +12; 10,58 con −50% y 11,50 con −75%.
   Es ruido alrededor de 10,7 con error típico 0,6-1,1, y el castigo más duro da el suelo **más alto**.
2. **La build buena tampoco se mueve donde tiene que moverse** (57,97 → 57,37 en el acto 2) y **sí donde no
   debe**: su tasa de victoria de la run baja de 17,00 a 16,25, alejándose de la meta de 20-30.
3. **La build mala no tiene tendencia**: 12,00 → 8,58 con −50% y 10,58 con −75%. El único valor que parecía
   una señal no sobrevive a subir el castigo.
4. **Dos guardarraíles se mueven en contra**: las derrotas del acto 1 pasan de 29,74 a 30,2-31,3 sobre un
   techo de 29,74, y con −75% el hueco cae a **9,71**, por debajo del suelo de 9,8.

### 31.6. Por qué falla: el castigo se cobra en la moneda que sólo tiene quien construye

Poder de compra destruido, sonda de 600 runs con +6 de oro por derrota:

| perfil | oro gastado en mercado, hoy → con castigo | perks al final |
|---|---|---|
| Build buena | 63,05 → **49,09** (−22%) | 12,07 → 11,49 |
| Build mediocre | 71,95 → 61,88 (−14%) | 14,23 → 13,70 |
| **Sin build** | 8,94 → 5,29 | 1,56 → **1,35** |

El perfil sin build termina la run con **22 de oro sin gastar y 33 quemados rerolleando recompensas que no
quiere**: un colchón de ~55 de oro inútil que absorbe cualquier peaje antes de llegarle a la build, y la
build que tiene son 1,6 perks. La build buena termina con **11 de oro sobre 86 ganados**: su restricción
está apretada y el peaje le llega entero. La palanca no es neutra, es **regresiva**. Y eso vale para
cualquier denominación pagable del castigo —oro, clínica, matrícula, perder un perk, perder un objeto—:
todas son cosas de las que el perfil sin build tiene **menos**.

### 31.7. La identidad que cierra la pregunta

| perfil | puerta 1 | puerta 2 | puerta 3 | **producto** | tasa de victoria de la run |
|---|---|---|---|---|---|
| Build buena | 75,33 | 44,25 | 51,00 | **17,00** | **17,00** |
| Build mediocre/mala | 68,58 | 33,29 | 52,75 | **12,04** | **12,00** |
| Sin build | 70,75 | 35,18 | 44,29 | **11,02** | 10,67 |

**La tasa de victoria de la run es, al decimal, el producto de las tres tasas de victoria contra los jefes.
Los veinte partidos ordinarios no aparecen en el producto.** La diferencia del perfil sin build es la única
otra vía de derrota, quedarse sin plantilla, que ocurre 0,009 veces por run. Es la frase de la ADR 0057
—*"el jefe filtra; el resto del recorrido, no"*— convertida en aritmética, y explica por qué los partidos
ordinarios sólo pueden actuar por el canal indirecto (menos oro → peor build → peor jefe), que es el que la
ADR 0055 ya midió y falsificó.

Y el jefe además **separa más por partido** que el partido ordinario: en el acto 2 el jefe separa 9,1
puntos entre build buena y sin build (44,25 contra 35,18) y el ordinario 6,9 (60,37 contra 53,45). Mover
presión del jefe al partido ordinario **reduce** la discriminación por unidad de presión.

### 31.8. Las seis puertas

| Puerta | Estado |
|---|---|
| Sensación de fútbol (RT-056 + `betterTeamWinRate` 70-88, ADR 0054) | **verde**; lote de referencia de 1.000 partidos idéntico a §29.7: `betterTeamWinRate_human_60_vs_human_40` **79,52** IN, `possessionChanges` 24,13 · `passChainAvgLength` 2,26 · `shotsPerMatch` 11,99 · `tacklesPerMatch` 9,75 · `injuriesPerMatch` 0,74 |
| Rareza y jefe final (RF-024, ADR 0027) | **verde** |
| Equilibrio entre razas (D-29) | **verde** |
| Curva de puertas de la ADR 0033 | **verde**, las doce celdas, **sin recalibrar ningún jefe**; `--boss-gate` con **0 celdas OUT** |
| Run completa | **verde en los tests**; `runWinRate` sigue OUT como métrica (17,00, banda 20-30) |
| Criterio de salida de fase 1 (builds) | **verde** |

**598 de 598 tests en Release**, y los 184 ficheros de `/data` validados contra esquema.

### 31.9. Los seis objetivos: sin cambios, y a propósito

| Objetivo (ADR 0056) | ADR 0063 | **este paquete** | ET | meta |
|---|---|---|---|---|
| Build buena, actos 2/3 | 57,97 / 44,43 | **57,97 / 44,43** | 0,71 / 0,53 | 60% |
| Build mediocre, actos 2/3 | 47,94 / 40,67 | **47,94 / 40,67** | 0,70 / 0,25 | 42-45% |
| Build mala completa la run | 12,00% | **12,00%** | 0,87 | < 2% |
| Suelo sin build | 10,66% | **10,67%** | 0,56 | < 10% |
| Hueco buena/mediocre, acto 2 | 10,03 | **10,03** | 0,50 | > 9,8 |
| Tasa de victoria de la run | 17,00% | **17,00%** | 1,28 | 20-30% |

Guardarraíles donde estaban: `deathsPerRun` **1,46**, derrotas del acto 1 **29,74%**, doce celdas de la ADR
0033 en banda sin recalibrar ningún jefe, `betterTeamWinRate` **79,52**.

### 31.10. Lo que queda abierto

- **AO-A (nueva)**: la tasa de victoria de la run **es el producto de las tres puertas de jefe** y los
  partidos ordinarios no entran en él. La única palanca que la medición deja viva para los objetivos 4 y 5
  es **de puerta, no de recorrido**: hoy cada puerta discrimina 1,15 entre build buena y sin build y hace
  falta 1,26 (producto 2,0 en vez de 1,54). Las dos formas permitidas por el guardarraíl son empinar la
  pendiente `correct`→`good` de los tres jefes —el acto 3 es la puerta más plana, 25,0 → 38,9— o añadir un
  cuarto evento filtro por run.
- **AO-B (nueva)**: el criterio de dos condiciones que explica los siete paquetes. Una palanca separa
  perfiles sólo si **(a)** la oposición no tiene ese número y **(b)** la build buena tampoco. Las seis de
  las ADR 0055-0063 fallan por (a); ésta cumple (a) y falla por (b). El castigo del perk mal puesto (ADR
  0060) sigue siendo lo único medido que cumple las dos.
- **AM-A sigue siendo una elección del revisor**, sin cambios: la ADR 0064 sólo cierra la tercera de sus
  tres salidas, que era la única que no era una decisión de diseño.
- **AO-D (nueva)**: `winRateAct{n}` incluye el partido de jefe y la tabla que la publica dice "partidos
  ordinarios". Build buena, acto 2: 57,97 con jefe y **60,37** sin él. No cambia ninguna conclusión, pero
  antes de dar el objetivo 1 por alcanzado o no hay que decir cuál de las dos métricas se quiere.
- **AL-A, AN-A, AN-B, AL-C, AL-D y AM-B** sin cambios.

## 32. Decisiones de implementación del paquete AP: cuánta discriminación se puede comprar en las puertas (ADR 0065, ADR 0066)

El encargo era **AO-A**: que las tres puertas de jefe discriminen más sin mover la tabla de la ADR 0033,
midiendo primero cuánto se puede comprar y diciendo con un número si llega. **No llega**, y la medición
falsifica además la segunda salida que la ADR 0064 dejaba abierta (un cuarto evento filtro). Como en los
paquetes AM, AN y AO, **no se mueve ningún número de balance**: entra medición (ADR 0065) y un arreglo de
métrica que el revisor decidió (ADR 0066, AO-D).

### 32.1. El banco, y que vuelve a reproducir la ADR 0060 al decimal

Mismo protocolo que §28.8, §29.1, §30.1 y §31.1: 1.200 runs por perfil (300 × semillas 1/1001/2001/3001).
Las sondas de la curva de puertas son de `--boss-gate` con 25 plantillas × 8 partidos por celda (12.000
partidos, 52 s en Release).

| | ADR 0064 | esta medición | ET |
|---|---|---|---|
| Build buena, actos 2/3 (con jefe) | 57,97 / 44,43 | **57,97 / 44,43** | 0,71 / 0,53 |
| Build mediocre, actos 2/3 (con jefe) | 47,94 / 40,67 | **47,94 / 40,67** | 0,70 / 0,25 |
| Build mala completa la run | 12,00 | **12,00** | 0,87 |
| Suelo sin build | 10,66 | **10,66** | 0,56 |
| Hueco acto 2 (con jefe) | 10,03 | **10,03** | 0,50 |
| Tasa de victoria de la run | 17,00 | **17,00** | 1,29 |
| Muertes por run · derrotas del acto 1 | 1,46 · 29,74 | **1,46 · 29,74** | 0,02 · 1,87 |
| Puertas, buena · suelo | 75,33·44,25·51,00 · 70,75·35,18·44,29 | **idénticas** | |

### 32.2. Las doce celdas, y las dos que sólo entran por el margen de medida

Con las dos muestras del proyecto: la de la puerta de `Sim.Tests` (32 plantillas × 4 partidos por celda,
con contadores) y la sonda de `--boss-gate` de los experimentos (25 × 8, sin contadores).

| jefe | incoherente | correcta | buena | muy buena |
|---|---|---|---|---|
| `grimhold_guns` (puerta / sonda) | 21,6 / 18,4 [20-35] | 70,5 / 67,6 [65-80] | 80,6 / 84,6 [75-88] | 90,9 / 91,1 [85-95] |
| `the_hunt` | 10,9 / 8,9 [<15] | 38,9 / 39,6 [35-50] | 62,7 / 66,5 [60-72] | 80,8 / 78,6 [72-85] |
| `eternal_crown` | 2,3 / 4,3 [<10] | 26,4 / 25,0 [15-28] | **40,2 / 38,9** [40-55] | 58,6 / 62,3 [55-70] |

Con la muestra de la puerta las doce celdas están en banda **sin usar** el margen de ±2,5 puntos de
`BossGateTests.TolerancePercent`, pero la celda `buena` del jefe final está clavada en su suelo (40,2 sobre
40; la sonda de 25 × 8 la mide en 38,9). Es la que el encargo señalaba: **la puerta más plana es también la
que peor cumple la tabla**. Los experimentos de §32.3 se comparan siempre contra la sonda de 25 × 8.

### 32.3. Los cuatro tipos de modificador de jefe borran build, y por eso aplanan la puerta

Cada modificador se aísla sustituyéndolo por uno del mismo tipo incapaz de tocar nada (`banChannel` sobre
`Card`, canal que ningún perk modifica), conservando el número que RF-001b/RF-001c exigen:

| condición | incoherente | correcta | **buena** | muy buena |
|---|---|---|---|---|
| `grimhold_guns` sin `singleCopy` | 18,4 → 18,4 | 67,6 → 67,6 | **84,6 → 84,6** | 91,1 → **93,8** |
| `the_hunt` sin `markStar` | 8,9 → 8,5 | 39,6 → 39,7 | **66,5 → 66,3** | 78,6 → **82,7** |
| `eternal_crown` sin `pushBack` | 4,3 → 4,7 | 25,0 → 25,0 | **38,9 → 38,9** | 62,3 → 62,3 |
| `eternal_crown` sin `banChannel` | 4,3 → **1,6** | 25,0 → 25,0 | **38,9 → 47,2** | 62,3 → **66,2** |

- `singleCopy` y `markStar` **sólo tocan el escalón superior**: a la densidad de build que la ADR 0040 da a
  cada acto, una build correcta ni repite perks ni concentra en un portador. Sobre la run no compran nada
  (razón de la puerta 1: 1,065 → 1,068; de la puerta 2: 1,258 → **1,251**).
- `iron_curtain` (el `pushBack` de columna 6) está **inerte**: no mueve ninguna celda y, sobre la run, el
  experimento con los dos modificadores fuera devuelve *exactamente* los mismos números que el
  experimento con sólo el `banChannel` fuera (75,33·44,25·**56,00** y 70,75·35,18·**45,33**). No hay
  titular por delante de la columna 6 al que retrasar.
- `sealed_goal` es el único que muerde, y muerde **al revés**: cuesta **8,3 puntos** a la celda `buena` y
  **cero** a la `correcta`. Es un impuesto que sólo paga quien tiene build.

Cambiarle el canal tampoco empina la pendiente: `save` y `intercept` cuestan más y sacan de banda al
escalón superior (52,5 y 51,9 sobre un mínimo de 55); `dribble` y `pass` dan la misma tabla que **no tener
modificador** (47,2 / 66,2). Empinar la puerta 3 por esa vía es quitarle el modificador al jefe final con
otro nombre.

### 32.4. Lo único que queda es la dificultad, y la tabla dice cuánta

| jefe | multiplicador de cuota que la tabla permite | qué lo limita |
|---|---|---|
| `grimhold_guns` | **1,109 – 1,335** (sólo puede ablandarse) | por abajo la celda incoherente; por arriba la `buena` |
| `the_hunt` | **0,821 – 1,295** | `correcta` por abajo, `buena` por arriba |
| `eternal_crown` | **1,047 – 1,167** | `buena` por abajo (hoy fuera), `correcta` por arriba |

(Los tres rangos se derivan de la sonda de 25 × 8; con la muestra de la puerta el límite inferior de
`eternal_crown` es 1,00 en vez de 1,047, porque su celda `buena` mide 40,2 y no 38,9. La conclusión no
cambia: el jefe final sólo admite ablandarse, y muy poco.)

Comprobado en el campo: `the_hunt` de calidad 46 a **42**, el máximo que la tabla admite (celdas
14,5 / 46,1 / 71,3 / 83,6, las cuatro dentro):

| | hoy | calidad 42 | previsión del modelo |
|---|---|---|---|
| Puertas, buena · suelo (acto 2) | 44,25 · 35,18 | **51,77 · 43,67** | 50,4 · 41,0 |
| Razón de la puerta 2 | 1,258 | **1,186** | 1,229 |
| **Tasa de la run, buena** | 17,00 (ET 1,29) | **19,83** (ET 0,78) | 19,4 |
| **SUELO** | 10,66 (ET 0,56) | **12,58** (ET 0,58) | 12,85 |
| Muertes por run · derrotas del acto 1 | 1,46 · 29,74 | **1,55 · 30,77** | |

El objetivo 4 se roza y el objetivo 5 se aleja dos puntos. El modelo acierta las dos direcciones y las dos
magnitudes.

### 32.5. La frontera, que es la respuesta con número

| | razones de cuota de hoy (1,262 · 1,462 · 1,309) | con la puerta 3 empinada al máximo medido (R₃ = 1,535) |
|---|---|---|
| Si la buena gana el **20%** de las runs… | suelo mínimo **13,29%** | **12,17%** |
| Si el suelo se queda en el **10%**… | buena máxima **15,63%** | **16,96%** |

El punto de hoy —17,00 con un suelo de 10,66— ya está **sobre** esa frontera, y la frontera es
prácticamente la misma con o sin la restricción de la tabla: **revisar la tabla de la ADR 0033 no
desbloquea nada**. Lo que haría falta es elevar las tres razones de cuota a la potencia **1,622**
(1,459 · 1,852 · 1,548), es decir que el hueco de la puerta 2 pase de **9,1 a 14,3 puntos**.

Y el cuarto evento filtro tampoco: con las razones de hoy, la mejor tasa de la buena compatible con un
suelo del 10% es 15,63% con tres puertas y 15,62 / 15,63 / 15,87 / 16,82% añadiendo una cuarta con razón
1,065 / 1,151 / 1,258 / 1,462. **Añadir una puerta mueve el punto sobre la misma frontera, no la
desplaza.**

### 32.6. Un guardarraíl que es incompatible con el objetivo 4 por aritmética

`defeatShareAct1` es la **cuota** de runs perdidas que se pierden en el acto 1, `(1 − P₁)/(1 − producto)`,
así que sube sola cuando sube la tasa de victoria de la run aunque el acto 1 no se toque:

| tasa de la run | 17,00 (hoy) | 18,67 | 19,83 | 20,00 |
|---|---|---|---|---|
| `defeatShareAct1` con `P₁` = 75,33 | **29,74** | 30,33 | 30,77 | **30,84** |

Para que la run llegue al 20% con la cuota por debajo de 29,74 hace falta `P₁ ≥ 76,21`. **Se para aquí y
se devuelve al revisor** en vez de compensarlo ablandando el jefe del acto 1.

### 32.7. AO-D: la métrica del acto pasa a medir partidos ordinarios (ADR 0066)

Decisión del revisor. `winRateAct{n}` y `matchesLostAct{n}` cuentan sólo partidos ordinarios —que es lo
que la tabla de la ADR 0056 dice— y las cifras viejas se publican al lado como
`winRateAct{n}_withBoss` y `matchesLostAct{n}_withBoss`. La separación es exacta: `RunPlayResult` gana
`BossWinsByAct` (los jefes **superados** por acto) junto al `BossSamplesByAct` que ya tenía, y
`MatchesByAct − BossSamplesByAct` / `WinsByAct − BossWinsByAct` son el partido ordinario sin inferencia.
`BossesBeaten` no vale: sólo cuenta las puertas que dejan la run viva.

| perfil | acto 2 con jefe | acto 2 **ordinarios** | acto 3 con jefe | acto 3 **ordinarios** |
|---|---|---|---|---|
| Build buena | 57,97 (0,71) | **60,33** (0,85) | 44,43 (0,53) | **43,30** (0,73) |
| Build mediocre | 47,94 (0,70) | **50,42** (0,98) | 40,67 (0,25) | **38,65** (0,16) |
| **Hueco del acto 2** | **10,03** (0,50) | **9,91** (0,87) | | |

**El objetivo 1 pasa a estar alcanzado en el acto 2** (60,33 sobre una meta de 60) y sigue lejos en el 3.
Y el hueco baja de 10,03 a 9,91 sobre un suelo de 9,8: sigue por encima, con menos margen y más error, y
**no se compensa con ningún otro número** (RT-057).

### 32.8. Las seis puertas

| Puerta | Estado |
|---|---|
| Sensación de fútbol (RT-056 + `betterTeamWinRate` 70-88, ADR 0054) | **verde**, sin tocar `/Sim/Engine` ni `/data` |
| Rareza y jefe final (RF-024, ADR 0027) | **verde** |
| Equilibrio entre razas (D-29) | **verde** |
| Curva de puertas de la ADR 0033 | **verde**, las doce celdas, **sin recalibrar ningún jefe** |
| Run completa | **verde en los tests**; `runWinRate` sigue OUT como métrica (17,00, banda 20-30) |
| Criterio de salida de fase 1 (builds) | **verde** |

**598 de 598 tests en Release** y los 184 ficheros de `/data` validados contra esquema.

### 32.9. Los seis objetivos

| Objetivo (ADR 0056) | ADR 0064 | **este paquete** | ET | meta |
|---|---|---|---|---|
| Build buena, actos 2/3 | 57,97 / 44,43 (con jefe) | **60,33 / 43,30** (ordinarios) | 0,85 / 0,73 | 60% — **alcanzado en el acto 2** |
| Build mediocre, actos 2/3 | 47,94 / 40,67 (con jefe) | **50,42 / 38,65** (ordinarios) | 0,98 / 0,16 | 42-45% |
| Build mala completa la run | 12,00% | **12,00%** | 0,87 | < 2% |
| Suelo sin build | 10,66% | **10,66%** | 0,56 | < 10% |
| Hueco buena/mediocre, acto 2 | 10,03 (con jefe) | **9,91** (ordinarios) | 0,87 | > 9,8 |
| Tasa de victoria de la run | 17,00% | **17,00%** | 1,29 | 20-30% |

Guardarraíles donde estaban: `deathsPerRun` **1,46**, `defeatShareAct1` **29,74%**, `betterTeamWinRate`
**79,52**, doce celdas de la ADR 0033 en banda sin recalibrar ningún jefe.

### 32.10. Lo que el instrumento no puede ver

`RunPolicy` **nunca lee `BossRuleModifiers`** al componer la alineación ni al repartir perks. La build
automática **no puede prepararse contra el modificador**, que es justo lo que RF-012b y RF-014 le dan a un
jugador humano. Todo lo medido en §32.3 sobre los modificadores es por tanto una **cota inferior de su
valor de diseño**: se miden como impuesto puro porque quien los sufre no puede hacer nada. Antes de tocar
un modificador de jefe por lo que aquí se mide hay que enseñarle el modificador a la política, y eso es un
paquete propio.

### 32.11. Lo que queda abierto

- **AO-A queda cerrada como palanca**: ni empinar la pendiente dentro de la tabla, ni revisar la tabla, ni
  un cuarto evento filtro hacen alcanzables los objetivos 4 y 5 a la vez. Lo que falta no es permiso para
  mover celdas, es **razón de cuotas**.
- **AP-A (nueva)**: los cuatro tipos de modificador de jefe borran build, así que la puerta no puede
  endurecerse sin cobrarle a quien construye. Un modificador que cambie las reglas del campo sin tocar el
  once del jugador sería el primero con signo neutro.
- **AP-B (nueva)**: `defeatShareAct1 ≤ 29,74%` y `runWinRate ∈ [20,30]` son incompatibles con `P₁` donde
  está. Decisión del revisor.
- **AP-C (nueva)**: la política no lee el informe de ojeo del jefe (RF-012b), así que mide los cuatro
  modificadores como impuesto puro.
- **AO-D cerrada** por la ADR 0066.
- **AL-A sigue siendo la decisión de fondo**, y ahora con el número que le pide la frontera: la razón de
  cuotas de cada puerta tiene que subir un 62% en log-cuotas, y eso es fuerza del catálogo, no dificultad
  del jefe.
- **AM-A, AN-A, AN-B, AL-C, AL-D, AM-B y AO-B/AO-C** sin cambios; AO-B gana un tercer modo de fallo (el
  número tiene que ser de **uno** de los dos perfiles del jugador).

## 33. Decisiones de implementación del paquete AQ: el recorrido del catálogo, medido (ADR 0067, ADR 0068)

### 33.1. El banco, y que vuelve a reproducir la ADR 0060 al decimal

Mismo protocolo que §28.8, §29.1, §30.1, §31.1 y §32.1: 1.200 runs por perfil (300 × semillas
1/1001/2001/3001), contextual = "buena", gastadora = "mediocre"/"mala", el suelo con
`economy.rewardPerkWeight = 0` y la política que esquiva mercados. Novedad del paquete: las tres puertas
salen ahora **exactas** de `BossWinsByAct / BossSamplesByAct` (ADR 0067) y no estimadas desde
`BossesBeaten`, que se come la run que gana la puerta y se queda sin plantilla en ese mismo nodo.

| | ADR 0065/0066 | esta medición | ET |
|---|---|---|---|
| Build buena, actos 2/3 (ordinarios) | 60,33 / 43,30 | **60,33 / 43,30** | 0,85 / 0,73 |
| Build mediocre, actos 2/3 (ordinarios) | 50,42 / 38,65 | **50,42 / 38,67** | 0,98 / 0,14 |
| Hueco del acto 2 (ordinarios) | 9,91 | **9,91** | 0,87 |
| Build mala completa la run | 12,00 | **12,00** | 0,87 |
| Suelo sin build | 10,66 | **10,67** | 0,56 |
| Tasa de victoria de la run | 17,00 | **17,00** | 1,28 |
| Muertes por run · derrotas del acto 1 | 1,46 · 29,74 | **1,46 · 29,74** | 0,02 · 1,87 |
| Puertas, buena | 75,33 · 44,25 · 51,00 | **75,33 · 44,26 · 51,06** | |
| Puertas, suelo | 70,75 · 35,18 · 44,29 | **70,75 · 35,19 · 44,32** | |

### 33.2. La unidad de medida del paquete: la separación en log-cuotas

La ADR 0065 dejó el encargo con un número: para cumplir a la vez los objetivos 4 (run de la build buena
en 20-30%) y 5 (suelo < 10%) hay que elevar las tres razones de cuota a la potencia **1,622**. Escrito
como una sola cifra sumable, que es como se mide aquí:

```
S = Σ ln R_n = ln 1,2624 + ln 1,4624 + ln 1,3107 = 0,8837      (hoy)
S necesaria = 1,622 × 0,8837 = 1,4333
```

La frontera se reproduce con el mismo modelo logístico de la ADR 0065 y da lo mismo al decimal: con la
`S` de hoy la build buena no pasa del **15,68%** con el suelo en el 10% (la ADR midió 15,63%) y con la
buena en el 20% el suelo no baja de **13,27%** (la ADR, 13,29%). **`S` es lo único que mueve la
frontera**; la dificultad mueve el punto sobre ella.

### 33.3. La pregunta del encargo, respondida por construcción antes de medir nada

> ¿Existe una forma de premio que la oposición no pueda cobrar **estructuralmente**?

Sí, y es una sola: **el contador** (RF-070). Los quince perks con `accumulatesAcrossMatches` valen
`k^n`, donde `n` es el contador que su portador ha ido cargando **a lo largo de la run**, a razón de una
unidad por partido como mucho —`limit: {per: match, times: 1}` en los diez que se disparan en juego, y un
solo `MATCH_START` en los otros cinco—. Y ese `n` la oposición no lo tiene:

- `RivalTeamBuilder.Build` monta un `PlayerDefinition` por partido a partir de `data/rivals/`, que **no
  tiene dónde declarar un contador**: el campo no existe en el esquema del rival.
- `BossDefinition.ToTeamSetup` genera el equipo del jefe desde cero con `TeamGenerator.Generate` y le
  pega los perks de su plantilla. Tampoco hay contador que pegar.
- `ProgressionRules.ApplyCounterDeltas` sólo escribe de vuelta en la plantilla **del jugador**
  (`MatchResolution`), que es la única que persiste entre partidos.

Seis de los quince perks de acumulación están escritos en `data/rivals/` y `data/bosses/`
—`eternal_crown` lleva `clean_sheet_legacy`—, y **los seis valen `k⁰ = 1`**: el jefe los enseña en el
informe de ojeo y no hacen nada. Subir `valuePerCounter` es, por tanto, subir un premio que los **55
slots de perk de los tres jefes** no pueden cobrar. Es el primer canal en nueve paquetes que cumple la
condición (a) de la ADR 0064 **por construcción y no por calibración**, y por eso es el que este paquete
mide.

Las otras dos condiciones, medidas sobre las mismas 1.200 runs por perfil:

| | contadores acumulados por run | perks de acumulación en el once |
|---|---|---|
| Build **buena** (contextual) | **15,82** (ET 0,50) | 3,11 |
| Build **mediocre** (gastadora) | **9,41** (ET 0,48) | 2,72 |
| **Sin build** (suelo) | **2,22** (ET 0,07) | 0,52 |

**(b)** la build buena no lo tiene al máximo y **(c)** es un número que le pertenece más que a la
mediocre —1,7 a 1 con sólo un 14% más de perks de acumulación— y siete veces más que al suelo, porque
cargar un contador exige haber comprado el perk **pronto**, habérselo puesto a un titular que **sigue
vivo** y seguir **jugando partidos**: es, literalmente, construir a lo largo de la run.

### 33.4. Cuánta separación hay hoy en el catálogo, y de dónde: cuatro condiciones

Cada condición se mide con el banco completo —1.200 runs de las tres doctrinas **y** 1.200 del suelo— y
se resume en `S`. Ninguna toca `data/economy/perk-values.json`, así que la doctrina contextual **compra
lo mismo** en todas y la diferencia es lo que el perk hace en el campo, no lo que el jugador elige (el
mismo aislamiento que la ADR 0063 §1; se comprueba en `runs.csv`: la mezcla de perks finales cambia menos
del 2% entre condiciones).

| condición | R₁ · R₂ · R₃ | **S** | run buena | suelo |
|---|---|---|---|---|
| **hoy** | 1,262 · 1,462 · 1,311 | **0,8837** | 17,00 (ET 1,28) | 10,67 (ET 0,56) |
| **sin el eje de acumulación** (`accumulatesAcrossMatches: false` en los 15) | 1,153 · 1,470 · 1,330 | **0,8119** | 15,58 (1,34) | 10,00 (0,68) |
| **sin castigo** (`elseEffects` vacíos en los 17 perks que lo tienen) | 1,260 · 1,342 · 1,609 | **1,0013** | 15,92 (0,53) | 9,25 (0,64) |
| **la oposición sin catálogo** (71 slots de perk fuera de `data/rivals/` y `data/bosses/`, salvo los letales) | 1,160 · 1,187 · 1,456 | **0,6952** | 37,67 (1,30) | 29,50 (1,55) |
| **necesaria para los objetivos 4 y 5** | | **1,4333** | | |

Tres lecturas, y dos de ellas falsifican algo:

1. **El eje de acumulación vale hoy 0,072 de los 0,884**, el **8,1%** de toda la separación que el
   catálogo consigue contra un equipo sin build. Y no la reparte por igual: en la puerta del acto 2 vale
   **cero** —R₂ = 1,462 con el eje y 1,470 sin él, la misma cifra dentro del error— y la pone entera en
   las puertas 1 y 3.
2. **El castigo no separa a la build buena del suelo: la separa de la mediocre.** Quitarlo *sube* `S` de
   0,884 a 1,001. No contradice la ADR 0063 —el hueco de partido ordinario entre buena y mediocre se
   hunde de 9,91 a **4,22** al quitarlo, que es su medición reproducida— sino que dice otra cosa: **el
   suelo no tiene build que castigar**, así que el castigo es un impuesto que la build buena paga un poco
   y el suelo nada. Es la palanca del objetivo 2, no la de los objetivos 4 y 5.
3. **Y la lectura ingenua de la pregunta del encargo queda falsificada.** "Que la oposición no pueda
   cobrar el premio", entendido como *quitarle el premio a la oposición*, **baja** la separación: `S`
   pasa de 0,884 a **0,695** mientras la run de la build buena se dispara al 37,67% y el suelo al 29,50%.
   Es el mando de dificultad más potente medido en nueve paquetes, y es un **mando**, no una palanca:
   entra en la misma lista que el oro, la rareza, la fuerza del rival, el peso de los atributos, el
   ámbito del premio, el precio de perder y la calibración del jefe.

> **No se trata de que la oposición deje de cobrar un premio que existe. Se trata de que exista un premio
> que la oposición no puede encender**, y eso es el contador.

### 33.5. El techo del eje: qué pasa si el contador vale lo que su rareza permite

`ProbabilityScale.CounterCeilingFor` ya acota lo que puede valer **una copia** de un efecto con contador
—común 50, poco común 100, raro 200— y el catálogo está por debajo de ese techo en los canales que la
ADR 0060 §28.3 midió **con recorrido** (`intercept` base 2,5%, `tackle` 28%, `save`) y **en el techo** en
los que no lo tienen (`pass` 77%, `dribble` 72%, `shotOnTarget` 78,5%). Las dos sondas suben al techo los
efectos con contador de los canales con recorrido y dejan el resto quieto:

| | R₁ · R₂ · R₃ | **S** | potencia | run buena | suelo | hueco acto 2 | muertes |
|---|---|---|---|---|---|---|---|
| hoy | 1,262 · 1,462 · 1,311 | 0,8837 | 1,000 | 17,00 (1,28) | 10,67 (0,56) | 9,91 | 1,46 |
| **eje al techo, 4 canales** (`+dribble`) | 1,255 · 1,612 · 1,534 | **1,1323** | **1,281** | **19,50** (0,80) | **10,83** (0,91) | **10,79** | 1,48 |
| **eje al techo, 3 canales** (sin `dribble`) | 1,288 · 1,579 · 1,482 | **1,1034** | **1,249** | **19,08** (0,90) | 10,83 (0,87) | 9,81 | 1,47 |

**Es la primera condición en nueve paquetes en la que `S` sube.** Y sube donde tiene que subir: la run de
la build buena gana 2,5 puntos y **el suelo no se entera** (10,67 → 10,83, dentro del error), porque un
equipo sin build acumula 2,2 contadores por run frente a los 15,8 del que construye. La build mediocre
tampoco: 12,00 → 11,58.

La diferencia entre las dos sondas es **AL-A confirmado por tercera vez**: la de tres canales no toca
`silky_veteran` (canal `dribble`, base 72%) y da prácticamente el mismo resultado. Subir el contador en
un canal saturado no compra nada; el eje de acumulación vale lo que valga **el canal donde se acumula**.

En la potencia que pide la frontera, el techo del eje lleva de 1,000 a **1,281**: cubre el **45%** del
camino a 1,622. **No es suficiente, y hay que decirlo con el número: el eje de acumulación, exprimido
hasta el techo que la rareza permite, no hace alcanzables los objetivos 4 y 5 a la vez.** Con la potencia
1,281 la mejor combinación posible es buena 17,58% con suelo 10%, o suelo 11,69% con buena 20%.

Lo que sí hace es mover el punto a **(19,50 · 10,83)**, que es mejor **en los dos ejes** que el plan de
repliegue que el revisor fijó en la cabecera de la ADR 0065 —subir la run aceptando un suelo del 13%—.

### 33.6. Y lo que impide gastarlo: el escalón `muy buena` de la ADR 0033 es el mismo punto del eje que la run

La sonda de `--boss-gate` (25 plantillas × 8 partidos = 1.000 por celda) sobre la condición del eje al
techo, contra la misma sonda sobre el catálogo de hoy:

| jefe | incoherente | correcta | **buena** | **muy buena** |
|---|---|---|---|---|
| `grimhold_guns` hoy → eje al techo | 18,4 → **18,4** | 67,6 → **67,6** | 84,6 → **88,8** | 91,1 → **95,4** OUT |
| `the_hunt` | 8,9 → **8,9** | 39,6 → **39,6** | 66,5 → **74,6** OUT | 78,6 → **89,2** OUT |
| `eternal_crown` | 4,3 → **4,3** | 25,0 → **25,0** | 38,9 → **47,7** | 62,3 → **77,9** OUT |

**Las columnas `incoherente` y `correcta` no se mueven ni una décima**, y las dos de arriba suben. Es
**exactamente "empinar la pendiente `correct`→`good`"**, que es lo que la ADR 0064 pedía y la ADR 0065
demostró que **no se puede comprar desde el lado del jefe**. Desde el lado del catálogo sí se puede: los
escalones de abajo no llevan contadores —`*_correct` y `*_incoherent` no declaran ninguno— y los de
arriba sí (`*_good` con 2, `*_excellent` con 5). Y de paso arregla la única celda que estaba clavada en
su suelo: la `buena` del jefe final pasa de 38,9 (40,2 con la muestra de la puerta) a **47,7**.

Pero al techo se sale de la tabla por arriba en cuatro celdas, así que hay que dosificar. Y ahí está el
hallazgo que cierra el paquete. Se probó la dosis "**el contador paga antes y llega al mismo sitio**": la
magnitud por copia sube al techo de rareza y `maxValue` se acorta para que el producto `k^max` no cambie
(`battle_reader` 1,5⁵ = 7,59 → 2,0³ = 8,00, y así los seis). Medido:

| | R₁ · R₂ · R₃ | S | run buena | suelo | hueco acto 2 | celdas de la ADR 0033 |
|---|---|---|---|---|---|---|
| hoy | 1,262 · 1,462 · 1,311 | 0,8837 | 17,00 (ET 1,28) | 10,67 (0,56) | **9,91** | 12 en banda |
| **paga antes, mismo total** | 1,280 · 1,529 · 1,389 | **0,9993** | 17,67 (1,35) | 10,50 (0,52) | **9,43** OUT | **12 en banda** (92,9 · 79,4 · 63,9 arriba) |
| eje al techo, 3 canales | 1,288 · 1,579 · 1,482 | 1,1034 | **19,08** (0,90) | 10,83 (0,87) | 9,81 | 4 fuera por arriba |

> **El escalón `muy buena` de la tabla y la build con la que la doctrina contextual llega al jefe final
> son el mismo punto del eje: los dos con el contador a 5.** Por eso todo lo que el eje le da a la run se
> lo da también a la celda `muy buena`, y acortar la línea para salvar la celda se lo quita a la run en la
> misma medida: **17,67 sobre 17,00 con ET 1,35 es la misma cifra**. La dosis que salva las doce celdas
> sube `S` un 13% —lo cual dice que el eje *funciona*— y no lo convierte en tasa de victoria; y además
> deja el **hueco del acto 2 en 9,43**, por debajo de su suelo de 9,8. **No se aplica**: sería mover un
> número de balance para conseguir un efecto que cabe dentro del error típico y a cambio de un
> guardarraíl.

De donde sale lo que hay que hacer, y por qué no cabe aquí: **gastar el eje exige recalibrar los tres
jefes a la vez**, que es lo que el guardarraíl de la ADR 0056 autoriza ("si una celda se sale, recalibras
el jefe, nunca la tabla"). Con el eje al techo, `eternal_crown` tiene por primera vez margen para
endurecerse —su celda `buena` pasa de 38,9 a 47,7, con 7,7 puntos por encima de su suelo de 40, que hoy
no tiene—, `the_hunt` necesita ~4 puntos y `grimhold_guns` **no puede endurecerse** porque su celda
`incoherente` (18,4 en la sonda, 21,6 en la puerta) ya está pegada a su mínimo de 20. Es un paquete de
jefe con su propio banco, no un ajuste.

### 33.7. La segunda palanca del mismo eje, y por qué también se cae

La ADR 0063 dejó anotado como **AN-B** que `--perk-values` mide **un solo partido**, así que los quince
perks de acumulación se miden con el contador a **cero**, es decir **inertes**. En la tabla vigente eso
se lee perk a perk:

| perk | valor medido | por qué |
|---|---|---|
| `clean_sheet_legacy` | **−42** | ×1,5⁵ sobre `save`, medido a ×1,5⁰ |
| `scar_veteran` | −18 | +3 de fuerza por partido, medido a +0 |
| `pit_veteran` | −13 | ×1,3⁵ sobre `tackle`, medido a ×1,3⁰ |
| `lane_reader` | −8 | ×1,3⁵ sobre `intercept`, medido a ×1,3⁰ |
| `poacher_instinct` | −6 | ×2⁵ sobre `shotOnTarget`, medido a ×2⁰ |

Y `RunPolicy.WorthASlot` compara ese valor con `MinPerkValue` = 0 **sólo en la doctrina contextual**. O
sea: **el instrumento le dice a la build buena que los perks cuyo valor sólo existe a lo largo de una run
no valen nada, y la build buena los rechaza; la gastadora, que no pregunta, los compra.** Medido en las
1.200 runs: la contextual termina con `clean_sheet_legacy` en 0 de 1.200 runs y `poacher_instinct` en 4;
la gastadora, con 99 y 1.

La sonda mide qué pasaría si el instrumento dejara de decirlo —los cinco negativos puestos a 0, que es lo
mínimo para que `WorthASlot` los deje pasar; no es la corrección, es su cota:

| | R₁ · R₂ · R₃ | S | run buena | suelo | perks de acumulación | contadores |
|---|---|---|---|---|---|---|
| hoy | 1,262 · 1,462 · 1,311 | **0,8837** | 17,00 (1,28) | 10,67 (0,56) | 3,11 | 15,82 |
| el instrumento deja de rechazarlos | 1,123 · 1,397 · 1,275 | **0,6928** | **15,75** (1,05) | 10,58 (0,55) | **4,18** | **19,16** |

**La build buena acumula un 21% más de contadores y gana un 7% menos de runs.** El sesgo de AN-B es real
—el instrumento mide inertes unos perks que no lo son— pero **corregirlo bajando el listón sale caro**:
a las magnitudes de hoy esos cinco perks siguen valiendo poco incluso cargados, y el slot que ocupan se
lo quitan a uno que vale más. **El orden importa: primero la magnitud, después volver a medir el valor.**
Queda como la segunda mitad del paquete de AQ-A, no como una palanca por su cuenta.

### 33.8. Las seis puertas y las doce celdas

El paquete **no toca ningún dato**, así que las doce celdas de la ADR 0033 y las bandas de RT-056 quedan
donde estaban. Lo que sí cambia es el código de métricas (ADR 0067), y por eso se revalida entero:

- **La suite completa en Release: 599/599 verdes**, una afirmación **más** que las 598 de la ADR 0065
  (`FullRunGateTests.Act1IsTheWorkshop`); `Category=Gate` son 42 de ellas, en 51 s.
- **Las doce celdas**, sonda de `--boss-gate` 25 × 8 sobre el catálogo sin tocar: 18,4 / 67,6 / 84,6 /
  91,1 · 8,9 / 39,6 / 66,5 / 78,6 · 4,3 / 25,0 / 38,9 / 62,3, **idénticas al decimal** a las de §32.2. Con
  la muestra de la puerta (32 × 4) siguen siendo las de siempre y las doce están en banda.
- `data/` validado: 184 ficheros.

### 33.9. Los seis objetivos: sin cambios, y a propósito

| Objetivo (ADR 0056) | ADR 0065/0066 | este paquete | ET | meta | |
|---|---|---|---|---|---|
| Build buena, actos 2/3 (ordinarios) | 60,33 / 43,30 | **60,33 / 43,30** | 0,85 / 0,73 | 60% | acto 2 alcanzado |
| Build mediocre, actos 2/3 (ordinarios) | 50,42 / 38,65 | **50,42 / 38,67** | 0,98 / 0,14 | 42-45% | se pasa 5,4 |
| Build mala completa la run | 12,00 | **12,00** | 0,87 | < 2% | no |
| Suelo sin build | 10,66 | **10,67** | 0,56 | < 10% | no |
| **Hueco buena/mediocre, acto 2** | **9,91** | **9,91** | **0,87** | **> 9,8** | **sí** |
| Tasa de victoria de la run | 17,00 | **17,00** | 1,28 | 20-30% | falta 3,0 |

Guardarraíles: `deathsPerRun` **1,46**, `betterTeamWinRate` **79,52**, `randomBuildNearNone` en banda, y
el acto 1 medido con la métrica nueva: **`ordinaryDefeatRateAct1` = 24,90** sobre un techo de 30 (la
cuota vieja, informativa, sigue en 29,74).

### 33.10. Lo que queda abierto

- **AQ-A es la vía viva**: el eje de acumulación al techo de rareza en los canales con recorrido **más**
  la recalibración de los tres jefes. Con el eje al techo hay por primera vez margen para endurecer a
  `eternal_crown`, y `grimhold_guns` sigue sin poder endurecerse.
- **AQ-B, AQ-C y AQ-D** quedan anotadas: la tabla es lo que acota el eje; quitarle el catálogo a la
  oposición baja la separación; el castigo sirve al objetivo 2 y no al 4 ni al 5.
- **AN-B se precisa**: el sesgo del instrumento es real y corregirlo *antes* de subir la magnitud empeora
  la run. Es la segunda mitad de AQ-A, no un paquete propio.
- **AP-B cerrada** por la ADR 0067; **AP-A y AP-C** sin cambios.
- **AL-A deja de ser una sospecha y pasa a tener presupuesto**: el catálogo tiene 45% del camino, y el
  55% restante no está en ninguna palanca medida. Los objetivos 4 y 5 siguen sin ser alcanzables a la vez.
