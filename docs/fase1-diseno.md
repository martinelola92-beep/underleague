# Fase 1: diseño de implementación (motor de efectos, perks, progresión)

Especificación cerrada. Los subagentes implementan contra este documento; lo que no está aquí se pregunta al orquestador o se decide con el criterio más simple y determinista y se anota en §9. Convenciones de `fase0-diseno.md` (enteros, orden determinista, sin E/S, identificadores en inglés) siguen vigentes.

Objetivo de la fase (plan de fases): "dos builds distintas ganan de formas distintas y se nota". Criterio ampliado por el revisor: **las builds coherentes ganan más que un equipo sin perks y las incoherentes ganan menos**; las sinergias por etiqueta y adyacencia se prueban desde el principio; el juego es progresivo (experiencia, niveles, perks que acumulan) y las buenas builds deben escalar mejor a lo largo de una secuencia de partidos.

Requisitos: RF-023, RF-025, RF-027, RF-044, RF-065..072, RT-033..035, RT-040..043.

## 1. Estructura nueva en `/Sim`

```
Sim/
  Perks/PerkDefinition.cs      sealed record PerkDefinition, EffectDefinition, LimitDefinition; enums PerkKind, EffectType, EffectTarget, EffectDuration, LimitScope, PerkScope, ProbabilityKind
  Perks/PerkCatalog.cs         sealed class PerkCatalog (lista ordenada por id, búsqueda por id)
  Perks/ConditionCompiler.cs   compila la condición NCalc una vez; expone Evaluate(ConditionContext)
  Perks/ConditionContext.cs    struct con actor/target/opponent/owner y acceso al motor
  Perks/EffectEngine.cs        internal: suscripciones, orden RT-041, recursión RT-042, aplicación de efectos, límites, registro RT-043
  Perks/Modifiers.cs           internal: modificadores de atributo y de probabilidad con expiración
  Perks/DescriptionGenerator.cs   público: Describe(PerkDefinition, lang, Catalog) desde plantillas l10n (RT-035)
  Progression/Progression.cs   público: experiencia y niveles (RF-025, RF-027), slots por rareza (RF-023)
Sim.csproj: PackageReference NCalcSync 7.1.0
```

`Catalog` (Data/Catalog.cs) gana `Perks` (PerkCatalog), `Localization` (plantillas por idioma) y `Progression` (tabla de niveles). `DataLoader.FromJson` carga `perks/*.json`, `l10n/<lang>/templates.json` y la sección `progression` de `tuning.json`.

## 2. Formato de perk (`data/perks/<id>.json`, RT-033)

```json
{
  "id": "bloodlust",
  "name": { "es": "Sed de sangre", "en": "Bloodlust" },
  "rarity": "rare",
  "kind": "conditional",
  "trigger": "TACKLE",
  "scope": "actor",
  "condition": "hasTag(actor, 'Brute') && bias() < 0",
  "effects": [
    { "type": "modifyAttribute", "target": "actor", "attribute": "strength", "value": 3, "duration": "play" }
  ],
  "limit": { "per": "match", "times": 2 },
  "accumulatesAcrossMatches": false,
  "lethal": false,
  "positionOnly": null,
  "tagsRequired": [],
  "tagsForbidden": []
}
```

- `rarity`: `common | rare | legendary`. `kind`: `filler | conditional | ruleBreaker` (RF-069, 60/30/10; `/Balance --describe` informa de la distribución real).
- `trigger`: nombre `UPPER_SNAKE` de `EventType` (RF-066). Eventos sin actor (`MATCH_START`, `MATCH_END`, `MOB_START`, `REFEREE_LEAVES`, `PLAY_START`, `PLAY_END`) se evalúan **una vez por perk** con `actor = owner`.
- `scope` (RF-065 "alcance"): a quién debe corresponder el evento para que el perk del dueño `owner` se evalúe: `actor` (por defecto: `event.Actor == owner`), `target` (`event.Target == owner`), `team` (actor del mismo equipo que owner), `opposingTeam` (actor del equipo rival), `any`.
- `condition`: NCalc, vacío = siempre. Funciones (RT-034), todas enteras o booleanas:

| Función | Devuelve |
|---|---|
| `hasTag(who, 'Tag')` | bool; `who` ∈ `actor`, `target`, `opponent`, `owner` (identificadores sin comillas; `target`/`opponent` ausentes → false) |
| `attr(who, 'strength')` | int, atributo **efectivo** (con modificadores) |
| `level(who)` | int |
| `position(who)` | string `'Goalkeeper'`… |
| `isMob()` | bool |
| `bias()` | int, desde el punto de vista del equipo de owner (positivo = favorable) |
| `zone(who)` | `'Own'`, `'Middle'`, `'Opposing'` respecto al equipo de `who` |
| `adjacent(who, 'Tag')` | bool: algún compañero con esa etiqueta cuya casilla-hogar es adyacente (8 vecinos, RF-044). **Con radio 1 la alineación por defecto no tiene ni una sola pareja adyacente** y apiñar el bloque para conseguirla cuesta 16 puntos de tasa de victoria (medido, paquete I); lo sustituye la sinergia estática de la ADR 0021 |
| `adjacentCount(who, 'Tag')` | int |
| `teammatesWithTag(who, 'Tag')` | int, compañeros en campo con esa etiqueta (excluye a `who`) |
| `distanceToGoal()` | int, casillas (redondeo hacia abajo) del actor a la portería rival |
| `scoreDiff()` | int, goles propios − rivales del equipo de owner |
| `tick()` | int |
| `counter('name')` | int, contador del owner (RF-070) |
| `detail()` | string, `MatchEvent.Detail` |

Identificador desconocido, función desconocida o tipo incorrecto → `DataException` al cargar (nunca en partido).

- `effects[]`: lista ordenada; se aplican en orden. Tipos:

| `type` | Campos | Semántica |
|---|---|---|
| `modifyAttribute` | `target`, `attribute`, `value` **o** (`valuePerCounter`, `counter`, `maxValue`), `duration` | suma `value` (o `valuePerCounter × counter('counter')` acotado a `maxValue`) al atributo efectivo del/los objetivo(s) durante `duration`; el efectivo se acota a 1..99 |
| `modifyLeash` | `target`, `value` (casillas), `duration` | igual sobre la correa en casillas (mínimo 1) |
| `modifyBias` | `value` | desplaza el criterio del árbitro (positivo = a favor del equipo de owner); sin duración, persiste el resto del partido |
| `modifyProbability` | `target`, `probability`, `value` (puntos base 10000), `duration` | suma `value` a la probabilidad indicada cuando el objetivo es el sujeto de la resolución: `probability` ∈ `foul`, `card`, `injury` (ser lesionado), `injure` (lesionar), `severeInjury`, `pass`, `intercept`, `dribble`, `tackle`, `shotOnTarget`, `save` |
| `cancelEvent` | — | anula la consecuencia del evento disparador; solo válido con `trigger` ∈ `CARD`, `INJURY`, `FOUL` (validado al cargar). El evento se registra igual con `Detail` sufijado `":cancelled"` |
| `addCounter` | `counter`, `value` | suma al contador del owner |
| `setState` | `target`, `state` ∈ `KnockedDown`, `ticks` | derriba al objetivo (solo objetivos rivales; validado) |

- `target` ∈ `actor`, `target`, `opponent`, `owner`, `adjacent` (compañeros adyacentes al owner), `team`, `opposingTeam`, `withTag:<Tag>` (compañeros del owner con esa etiqueta, incluido él), `adjacentWithTag:<Tag>`.
- `duration` ∈ `instant` (solo válido para `addCounter`/`modifyBias`/`setState`/`cancelEvent`), `play`, `match`, `run`. En fase 1 `run` se comporta como `match` dentro del partido y además persiste vía contadores (ver §6).
- `limit`: `per` ∈ `play | match | mob | run`, `times` ≥ 1. Sin `limit` = ilimitado.
- `positionOnly`: `null` o posición; el perk **no puede asignarse** a otra posición (validación de build). `tagsRequired`/`tagsForbidden`: etiquetas del portador exigidas/prohibidas al asignar. Son la base de las **antisinergias**: un perk mal colocado es un slot perdido y, si el diseñador lo quiere, un castigo activo (ver §7).
- `lethal`: si `true`, uno de los efectos debe poder producir `DEATH`; en fase 1 no hay muertes, así que se valida que sea `false`.

## 3. Motor de efectos (RT-040..043)

- `EffectEngine` se construye en `MatchEngine` con los perks de los jugadores en campo (suplentes no). Suscripción: por `trigger`, lista **ordenada** de `(rarity desc, ownerId asc, perkId ordinal asc)` (RT-041); orden calculado una vez.
- Publicación: `MatchEngine` llama a `_effects.Publish(evt)` en el momento de emitir cada evento. **Semántica pre-resolución** para los eventos que gobiernan una resolución: `SHOT` se publica antes de calcular calidad y parada; `TACKLE` antes de los rolls de falta/victoria/lesión; `PASS_ATTEMPTED` antes del roll de éxito; `DRIBBLE_ATTEMPTED` antes del duelo. Así `modifyAttribute` con `duration: play` y `modifyProbability` afectan a esa misma resolución. `FOUL`, `CARD`, `INJURY` se publican **antes** de aplicar su consecuencia para permitir `cancelEvent`; el resto (GOAL, SAVE, RECOVERY…) después.
- Para cada suscriptor: comprobar `scope`, límite, condición (excepción de NCalc en partido es imposible por construcción; si ocurriera se convierte en `InvalidOperationException` con id de perk), aplicar efectos, registrar la activación (RT-043): `PerkActivation(PerkId, OwnerId, Tick, EventType, Detail)`, incrementar contadores de límite.
- Recursión (RT-042): `Publish` lleva profundidad; los efectos que publiquen eventos (`setState` no publica; en fase 1 ninguno lo hace) pasan `depth+1`; si `depth > config.MaxDepth` (nuevo campo de `SimConfig`, por defecto 4) se descarta y se registra `RecursionCut` en el informe.
- Modificadores: `Modifiers` guarda por jugador una lista de `(AttributeKind, delta, expiresAtPlayEnd | never)` y `(ProbabilityKind, delta, expiry)`; `MatchPlayer.Effective(AttributeKind)` = base de nivel + suma de deltas, acotado 1..99, **recalculado y cacheado al cambiar** (no en cada lectura). Todos los sitios del motor que hoy leen atributos pasan a leer los efectivos. La correa en casillas también.
- `MatchReport` gana: `IReadOnlyList<PerkActivation> PerkActivations`, `IReadOnlyList<PerkActivationSummary> PerksSummary` (perkId, ownerId, activations), `int RecursionCuts`.
- Rendimiento: con 0 perks el coste añadido debe ser despreciable; con 30 perks entre los dos equipos, < 20% sobre la fase 0. La condición compilada se evalúa con un `ConditionContext` reutilizado (struct/campos), sin asignar por evaluación.

## 4. Descripciones generadas (RT-035)

`DescriptionGenerator.Describe(perk, "es"|"en", catalog)` compone: `[condición] efecto(s) [límite]`. Plantillas en `data/l10n/<lang>/templates.json`:

```json
{
  "effects": {
    "modifyAttribute": "{target} {value:+} de {attribute} durante {duration}",
    "modifyAttributePerCounter": "{target} {valuePerCounter:+} de {attribute} por cada {counter} (máximo {maxValue}) durante {duration}",
    "modifyLeash": "{target} {value:+} de correa durante {duration}",
    "modifyBias": "criterio del árbitro {value:+}",
    "modifyProbability": "{target}: {probability} {value:+%}",
    "cancelEvent": "anula {event}",
    "addCounter": "+{value} a {counter}",
    "setState": "{target} derribado {ticks} ticks"
  },
  "triggers": { "TACKLE": "al entrar", "SHOT": "al tirar", "MATCH_START": "al empezar el partido", "...": "..." },
  "conditions": { "hasTag": "si {who} es {tag}", "isMob": "en la turba", "biasLt": "si el criterio es menor que {n}", "scoreDiffLt": "si va perdiendo", "scoreDiffGt": "si va ganando", "adjacent": "si tiene un {tag} adyacente", "adjacentCount": "por cada {tag} adyacente", "teammatesWithTag": "si hay al menos {n} {tag} en el equipo", "and": "{a} y {b}", "or": "{a} o {b}", "not": "no {a}" },
  "targets": { "actor": "el jugador", "target": "el receptor", "opponent": "el rival", "owner": "el portador", "adjacent": "los compañeros adyacentes", "team": "el equipo", "opposingTeam": "el equipo rival", "withTag": "los {tag} del equipo", "adjacentWithTag": "los {tag} adyacentes" },
  "durations": { "instant": "", "play": "la jugada", "match": "el partido", "run": "la run" },
  "limits": { "play": "máximo {times} por jugada", "match": "máximo {times} por partido", "mob": "máximo {times} en la turba", "run": "máximo {times} por run" },
  "attributes": { "strength": "fuerza", "speed": "velocidad", "technique": "técnica", "stamina": "resistencia", "leash": "correa" },
  "probabilities": { "foul": "probabilidad de falta", "injury": "probabilidad de lesionarse", "injure": "probabilidad de lesionar", "...": "..." },
  "tags": { "Brute": "Bruto", "Fine": "Fino", "Neutral": "Neutral", "Aggressive": "agresivo", "...": "..." }
}
```

La condición se traduce con un *pretty-printer* sobre el AST de NCalc (visitante): funciones conocidas → plantilla de `conditions`; comparaciones simples `f() < n` → plantillas `*Lt/*Gt/*Eq` cuando existen; cualquier otra expresión → error de carga ("condición no describible"), de modo que **toda condición del catálogo es describible por construcción**. `/Balance --describe [lang]` imprime id, rareza, tipo y descripción de cada perk en ambos idiomas. Sin campo `description` en JSON (el validador lo rechaza).

## 5. Rareza, slots y asignación (RF-023, RF-071..072)

`PlayerDefinition` gana `IReadOnlyList<string> Perks`. `Progression.PerkSlots(rarity)`: Common 2, Rare 3, Legendary 4; `InitialPerks(rarity)`: 0/1/2. Validación al construir `MatchSetup` (en `Simulator.Run`): perks existentes en el catálogo, sin repetir en un jugador, ≤ slots, `positionOnly`/`tagsRequired`/`tagsForbidden` respetados → `ArgumentException` con id de jugador y perk. Una **build inválida no se simula**: el castigo por perks mal colocados que sí se simulan viene de perks con condición falsa (slot desperdiciado) o con efecto negativo declarado (§7).

## 6. Progresión (RF-025, RF-027) y campaña

`Progression` (público, puro):
- `AwardExperience(team, playedIds, benchIds, matchXp)`: 100% a los que jugaron, 45% a suplentes (`tuning.progression.matchExperience`, `benchShare`).
- `LevelFor(experience)`: tabla `tuning.progression.experiencePerLevel[8]` (acumulada). Subir de nivel: `+attributesPerLevel` (tuning, p. ej. 2) a **cada** atributo salvo correa (RF-027: solo atributos base, nunca perks). Nivel máximo 8 (RF-023). Canteranos (+33%) quedan para fase 2.
- Contadores `run`: `PlayerDefinition` gana `IReadOnlyDictionary<string,int> Counters` (inmutable, ordenado por clave ordinal al iterar). Al terminar el partido, `MatchResult` expone `IReadOnlyList<PlayerCounterDelta>` con los contadores que los perks `accumulatesAcrossMatches: true` han sumado; la campaña los aplica al `PlayerDefinition` siguiente. Los perks con `accumulatesAcrossMatches: false` empiezan cada partido con sus contadores a 0.

```json
"progression": { "matchExperience": 100, "benchSharePercent": 45, "experiencePerLevel": [0, 100, 250, 450, 700, 1000, 1400, 1900], "attributesPerLevel": 2 }
```

## 7. Catálogo de prueba (24-30 perks) y sinergias

Se diseña en `data/perks/` según estas familias. Cada perk tiene que ser **describible** (§4) y **medible** en `/Balance`. Distribución RF-069: ~16 filler, ~8 conditional, ~3 ruleBreaker.

| Familia | Idea | Ejemplos (id → efecto) |
|---|---|---|
| Violencia (Brute) | premia contacto, castiga a los finos | `heavy_boots` (MATCH_START, hasTag(owner,'Brute'): +6 fuerza partido); `bloodlust` (TACKLE, actor Brute: +4 fuerza jugada, +injure 300, límite 3/partido); `bone_breaker` (TACKLE, target hasTag(target,'Fine'): +injure 600); `enforcer` (INJURY scope team: +3 fuerza a withTag:Brute el partido, acumula) |
| Técnica (Fine) | pases y regates | `silk_touch` (+6 técnica partido si Fine); `one_touch` (PASS_ATTEMPTED: +pass 800 si adjacent(actor,'Fine')); `matador` (DRIBBLE_ATTEMPTED: +dribble 1200 si opponent Brute); `glass_cannon` (MATCH_START: +10 técnica, −10 resistencia; **antisinergia declarada** con `Resilient`) |
| Bloque / muro | defensa adyacente | `shield_wall` (MATCH_START, adjacentCount(owner,'Defender') ≥ 1: +5 fuerza y +tackle 500 a adjacentWithTag:Defender); `sweeper` (RECOVERY zone Own: +1 correa jugada); `bouncer` (positionOnly Defender: TACKLE +tackle 700) |
| Contragolpe | velocidad tras recuperar | `counter_punch` (RECOVERY scope team, zone(actor)=='Own': +8 velocidad a withTag:Forward jugada); `long_legs` (+1 correa partido); `sprinter` (PLAY_START: +6 velocidad jugada si hasTag(owner,'Fast')) |
| Turba | solo en gol de oro | `mob_lawyer` (ruleBreaker: CARD en turba → cancelEvent… en fase 1 sin árbitro fuera, así que: FOUL isMob() → cancelEvent, 2/partido); `second_wind` (MOB_START: +10 resistencia); `street_fighter` (isMob(): +8 fuerza, −4 técnica partido) |
| Escalado (acumulan, RF-070, ≥ 6 en el catálogo) | contadores entre partidos | `veteran` (MATCH_END: addCounter matches +1; MATCH_START: +1 fuerza por matches, máx 8); `bloodline` (INJURY actor: addCounter injuries; MATCH_START: +1 fuerza por injuries, máx 10); `poacher` (GOAL actor: addCounter goals; SHOT: +2 técnica por goals, máx 12, jugada); `bookworm` (PASS_COMPLETED: addCounter passes; MATCH_START: +1 técnica por cada 25 passes → se modela con `valuePerCounter: 1`, `counter: passes25` que el perk suma cada 25… **simplificación**: `addCounter` con `value: 1` en PASS_COMPLETED y `valuePerCounter` 1 con `maxValue` 6 y un divisor `counterDivisor: 25`, campo opcional de `modifyAttribute`); `survivor`, `iron_lungs_plus` |
| Rompe-reglas | anular consecuencias | `innocent_face` (CARD target owner: cancelEvent 1/partido); `lucky_charm` (INJURY target owner: cancelEvent 1/partido); `iron_skull` (MATCH_START: +severeInjury −10000 al owner) |
| Sinergia de posición | perks que exigen posición | `goalkeeper_wall` (positionOnly Goalkeeper: +save 800); `target_man` (positionOnly Forward: SHOT +4 fuerza jugada si hasTag(owner,'Brute')) |
| **Antisinergias y castigos** | build incoherente = pérdida real | `glass_cannon` (arriba); `lone_wolf` (MATCH_START: +8 fuerza si adjacentCount(owner,'Brute') == 0, **−6** si no: dos efectos con condiciones opuestas → dos perks internos o `effects` con `when`: se resuelve con **`elseEffects[]`** opcional, aplicado cuando la condición es falsa); `berserker` (TACKLE: +6 fuerza jugada, y +foul 1500: sin `innocent_face`/árbitro a favor sale caro); `showboat` (DRIBBLE_ATTEMPTED: +dribble 1500 si hasTag(owner,'Fine'), `elseEffects`: −1500); `tagsForbidden`/`tagsRequired` en al menos 6 perks para que el validador rechace asignaciones imposibles |

Reglas del catálogo: nombres es/en en `name`; ids `snake_case`; nada de texto descriptivo; los valores enteros pequeños (atributos ±3..±10, probabilidades ±300..±1500 puntos base) para que el efecto sea medible sin dominar; al menos 5 perks con `tagsRequired`, al menos 4 con `elseEffects`, al menos 6 con `accumulatesAcrossMatches: true`.

## 8. Builds y simulaciones (`/Balance`)

`data/balance/builds/<id>.json`:

```json
{ "id": "orc_violence", "name": "Orcos: carnicería", "race": "Orc", "quality": 50,
  "perks": [ { "slot": 6, "perk": "target_man" }, { "slot": 1, "perk": "bouncer" }, { "slot": 3, "perk": "bloodlust" } ],
  "rarities": { "6": "rare" },
  "lineup": null }
```
`slot` = índice de titular en `TeamGenerator` (0 GK, 1-2 DEF, 3-5 MID, 6 FWD). `rarities` sube la rareza de un titular (y sus slots). `lineup` opcional: casillas-hogar relativas por slot para probar adyacencias (`[[c,r], ...]`), si `null` se usa `Lineup.Default`. Validación de build al cargar (§5): una build inválida detiene el lote con mensaje.

Conjunto inicial (`data/balance/builds/`): `human_none` (sin perks, referencia), `orc_none`, `elf_none`, `orc_violence`, `elf_tiki_taka`, `human_wall`, `human_counter`, `orc_mob`, `elf_glass` (coherente pero frágil), **malas a propósito**: `orc_misplaced` (perks técnicos en orcos: condiciones siempre falsas + `showboat` sin Fine), `elf_brawler` (perks de violencia en elfos: `glass_cannon` + `berserker`), `human_random` (perks tomados con `RngStreams.Rewards` sin criterio), `human_scattered` (perks de adyacencia con alineación que separa a los jugadores).

Modos nuevos de `/Balance` (además de los actuales):

- `--builds a,b,c` con `--vs <buildId>` (por defecto `human_none`) y `--rosters N`: matriz build × rival, `--runs` partidos por celda repartidos, salida `builds.csv` (`build, opponent, matches, winRate, goalsFor, goalsAgainst, injuriesFor, injuriesAgainst, activationsPerMatch`) y `perks.csv` (`perkId, build, activations, matchesWithActivation, activationRate`). Sin `--vs`, todos contra todos.
- `--campaign N` (por defecto 8): cada build juega N partidos seguidos contra rivales `human_none` de calidad creciente (`quality = 46, 48, …` hasta `46 + 2(N−1)`; con N=8, 46..60), arrastrando experiencia, niveles y contadores `run` (§6); rivales sin progresión. Salida `campaign.csv` (`build, matchIndex, opponentQuality, winRate, avgLevel, avgStrength…`) y una tabla por consola: tasa de victoria en partidos 1-4 vs 5-8 por build.
- `--describe [es|en]`: catálogo con descripciones y distribución RF-069.
- `--home-away`: cada emparejamiento se juega también invertido (elimina cualquier sesgo local/visitante en la comparación).

Métricas de fase 1 en `Sim/Analysis` y en la puerta estadística (`StatisticalTests`, categoría `Gate`):

| Métrica | Condición de aprobado |
|---|---|
| `coherentBuildsBeatNone` | cada build coherente (`orc_violence`, `elf_tiki_taka`, `human_wall`, `human_counter`, `orc_mob`) gana ≥ 58% contra su `*_none` de la misma raza y calidad (RF-024/fase 1) |
| `badBuildsLoseToNone` | cada build mala (`orc_misplaced`, `elf_brawler`, `human_scattered`) gana ≤ 45% contra su `*_none` |
| `randomBuildNearNone` | `human_random` entre 40% y 60% |
| `buildsWinDifferently` | `orc_violence` produce ≥ 1,5× lesiones que `elf_tiki_taka`; `elf_tiki_taka` ≥ **1,11×** cadena media de pases que `orc_violence` (**ADR 0062**; era 1,3, calibrado contra la fórmula aditiva que clavaba el pase en el techo del 98%, y la escala de cuotas de la ADR 0050 P1 lo hace inalcanzable: medido aislado, siete `fine_touch` comunes alargan su propia cadena un 10,8% y ni con el techo legendario ×6 pasan del 19,1%). Las dos magnitudes **normalizadas contra la referencia sin perks de la propia raza** y medidas en los mismos partidos (ADR 0012): sin normalizar la métrica mide la raza y no la build — `orc_none` ya causa 3,9× las lesiones de `elf_none` sin un solo perk, y la cadena de pases de un bloque orco es estructuralmente más larga que la de uno élfico. "Lesiones que produce" son las **causadas al rival**, no las sufridas |
| `scalingRewardsGoodBuilds` | en campaña, la tasa de victoria de las builds coherentes en partidos 5-8 ≥ la de 1-4 − 10 puntos aunque los rivales suban de calidad. La segunda mitad ("las builds malas caen ≥ 15 puntos") **no es alcanzable con la progresión de §6**: el rival sube 14 puntos de calidad en 8 partidos y la plantilla propia sube 8, así que hasta un equipo sin perks pierde solo 6,6 puntos entre las dos mitades, y ninguna mecánica hace decaer a una build mala más rápido que a la referencia. Ver `docs/balance/fase1-perks.md` |
| `noDeadPerks` | cada perk del catálogo se activa en ≥ 1% de los partidos en los que está asignado en alguna build (perk muerto = fallo de diseño) |
| RT-055 | ninguna build catalogada > 70% ni < 30% contra `human_none` de calidad 50 (las malas pueden bajar del 30%: se excluyen de RT-055 y se documenta, porque son casos de prueba, no builds del juego) |
| Distribución RF-069 | 60/30/10 ± 8 puntos |

**Metodología de medida (paquete I).** Una celda de la matriz solo dice algo del diseño de la build si el
único cambio entre los dos equipos son los perks:

1. **Plantillas emparejadas.** Cuando las dos builds de un emparejamiento comparten raza y calidad, las dos
   se generan con el **mismo índice de generación**: son los mismos diez jugadores, con los mismos
   atributos y rasgos, y solo cambian perks, rarezas y alineación. Con plantillas independientes (lo que
   hacía el paquete H) la tasa de victoria de una misma build contra su referencia iba del 16,5% al 59,5%
   según el dado del generador — desviación típica de 14,9 puntos entre plantillas, con 20 plantillas ×
   200 partidos.
2. **Varias plantillas por celda** (`--rosters`, 25 por defecto): incluso emparejada, una sola plantilla no
   representa a la build.
3. **Reparto de ids alternado.** Los desempates del motor van por id de jugador ascendente; con el reparto
   fijo, el equipo de ids bajos gana entre 2 y 3 puntos de más con plantillas idénticas (53,1% Human,
   52,2% Orc, 52,0% Elf; alternando, 50,7% / 50,5% / 49,9%). La matriz juega cada emparejamiento en las
   cuatro combinaciones de (local, visitante) × (ids bajos, ids altos).
4. **Cadenas de pases por equipo.** `MatchReport.PassChainsByTeam` / `PassChainTotalLengthByTeam`: la
   estadística de partido completo del paquete H no podía distinguir a las dos builds.

## 9. Paquetes de trabajo

| Paquete | Agente | Depende de | Ficheros |
|---|---|---|---|
| F. Motor de efectos y progresión | deep-reasoner (opus) | — | `Sim/Perks/*`, `Sim/Progression/*`, cambios en `Sim/Engine/*` (atributos efectivos, publicación pre-resolución, cancelación, bias mutable, contadores, informe), `Sim/Data/*` (carga de perks, l10n, progression), `Sim/Model/PlayerDefinition.cs` (Perks, Counters), `Simulator.cs` (validación de builds), `data/schemas/perks.schema.json`, `data/schemas/l10n-templates.schema.json`, `tuning.schema.json` (progression), `data/sim/tuning.json` (progression), **dos perks de ejemplo** (`bloodlust`, `veteran`) y plantillas l10n completas es/en; tests `PerkLoaderTests`, `ConditionTests`, `EffectEngineTests` (orden RT-041, límites, cancelación, recursión), `DescriptionTests`, `ProgressionTests` |
| G. Catálogo y builds | fast-worker (sonnet) tras F (necesita el cargador para validar) | F | `data/perks/*.json` (24-30 según §7), `data/balance/builds/*.json` (§8), `tools/DataValidator` (mapeo de `perks/`, `l10n/`, `balance/builds/`) |
| H. `/Balance` fase 1 | fast-worker (sonnet) | F | `Balance/*` (§8 modos), `Sim/Analysis/BuildMetrics.cs` |
| I. Puerta y análisis | deep-reasoner (opus) | G, H | `StatisticalTests` fase 1, ajuste de valores de perks en `data/perks/` (no de reglas), informe `docs/balance/fase1-perks.md` con tablas de matriz y campaña, conclusiones sobre sinergias y antisinergias, perks muertos, y propuestas |

Revisión del orquestador tras F y tras I.

### 9.1 Decisiones de implementación

Detalles que esta especificación no cerraba y que se resolvieron al implementar. Cada uno está también
comentado en el punto del código donde se aplica.

**Paquete F (motor de efectos y progresión).**

1. **La gramática de condiciones es cerrada, no "NCalc entero"** (`ConditionCompiler`). Se admite
   `Cond := Cond ('&&'|'||') Cond | '!' Cond | BoolFn | IntFn Cmp IntLit | StrFn ('=='|'!=') StrLit`,
   con `Cmp ∈ {<, <=, >, >=, ==, !=}` y la función siempre a la izquierda de la comparación. Esto da tres
   propiedades a la vez: el tipo de cada nodo se conoce **estáticamente** (ninguna condición puede
   devolver algo que no sea booleano en partido), toda la aritmética es entera porque no hay literales
   flotantes ni división real (RT-023), y **toda condición es describible por construcción** (§4), porque
   cada forma sintáctica admitida tiene su clave de plantilla. Aritmética suelta (`bias() + 1 > 0`),
   comparación entre dos funciones, literal a la izquierda, función o identificador desconocidos y tipos
   incorrectos son `DataException` al cargar.
2. **Validación estática del AST + evaluación de prueba.** La validación de tipos recorre el AST completo;
   además se evalúa la condición una vez al cargar con un contexto neutro para ejercitar el cableado real
   de NCalc. Las dos hacen falta: el `&&` de NCalc cortocircuita, así que la evaluación sola no visitaría
   todo el árbol, y la validación sola no detectaría un desajuste entre la tabla de firmas y los
   manejadores.
3. **El contexto de evaluación vive en la instancia de `CompiledCondition`.** Los manejadores de NCalc
   reciben solo `(nombre, argumentos)`, así que la condición guarda el `ConditionContext` en curso en un
   campo propio. Es seguro porque `/Sim` es estrictamente síncrono y de un solo hilo (RT-021 prohíbe
   `Parallel`); si alguna vez se paraleliza el lote, hay que clonar la expresión por partido.
4. **Semántica pre-resolución sin duplicar eventos.** `SHOT` y `TACKLE` se **publican** antes de resolver
   con un evento provisional (`Detail = "attempted"`) que no entra en la secuencia; el evento definitivo,
   con su `Detail` real, se emite después con `publish: false`. `PASS_ATTEMPTED` sí se mueve: su `Emit`
   pasa a estar antes del roll de éxito, en el mismo lugar relativo de la secuencia.
   `DRIBBLE_ATTEMPTED` ya se emitía antes de su duelo. Así la secuencia de eventos tiene exactamente una
   entrada por evento y el orden de consumo del RNG no cambia cuando no hay perks.
5. **Un evento cancelado se registra igual.** `FOUL`, `CARD` e `INJURY` se publican antes de aplicar su
   consecuencia; si un perk los anula, el evento entra en la secuencia con `Detail` sufijado
   `":cancelled"` y se saltan **las consecuencias**, no la contabilidad: una falta anulada sigue contando
   como falta en el informe (ocurrió), pero no hay derribo, ni tarjeta, ni penalti. Una tarjeta anulada no
   incrementa tarjetas ni expulsa; una lesión anulada no saca al jugador del campo.
6. **Un perk fuera del campo sigue evaluándose.** Los perks se evalúan mientras dura el partido con
   independencia de si su portador sigue en el campo, y los modificadores de duración `match` no se
   retiran al lesionarse o ser expulsado. La alternativa (apagarlos al salir) silenciaría los perks de
   contador disparados por `MATCH_END` justo en los jugadores que peor lo han pasado, que es el caso que
   el diseño quiere premiar. En fase 1 no hay cambios, así que no hay ningún caso en que un perk "entre"
   a mitad de partido.
7. **`veteran` en un solo disparador.** Un perk tiene un `trigger`, no varios; §7 describe `veteran` con
   dos. Se expresa con la lista **ordenada** de efectos sobre `MATCH_START`: primero el
   `modifyAttribute` por contador (que lee el contador **antes** de incrementarlo, así que el primer
   partido da +0) y después el `addCounter`. Cualquier perk de escalado del catálogo se escribe igual.
8. **Los eventos sin actor se identifican por tipo, no por "¿trae Actor?"**: `MATCH_START`, `MATCH_END`,
   `MOB_START`, `REFEREE_LEAVES`, `PLAY_START` y `PLAY_END`, tal y como los lista §2. `PLAY_START` sí
   lleva actor en el motor, pero como disparador es un evento de partido y no de jugador, así que se
   evalúa una vez por perk con `actor = owner` y sin comprobar el alcance.
9. **Los modificadores de jugada caducan después de publicar `PLAY_END`**, no antes: un perk disparado por
   el fin de jugada aplica sobre la siguiente. Los límites `per: play` se reinician en el mismo punto y
   los `per: mob` al publicar `MOB_START`. `per: run` se comporta como `per: match` dentro del partido.
10. **`elseEffects` cuenta como activación**, con la activación registrada con `Detail` sufijado
    `":else"` (RT-043). Un perk cuya condición es falsa y que no tiene `elseEffects` no se activa y no
    consume límite.
11. **Solo se recalcula el atributo efectivo al cambiar.** `MatchPlayer` guarda base, deltas y efectivos
    en tres arrays y recalcula los cinco atributos y el radio de correa cuando entra o expira un
    modificador. Las propiedades `Strength`/`Speed`/`Technique`/`Stamina` pasan a devolver el efectivo, con
    lo que **todo** el motor lee ya el valor con modificadores sin tocar ni una fórmula.
12. **Con 0 perks no existe el motor de efectos.** `MatchEngine` construye `EffectEngine` solo si algún
    titular lleva perks; si no, `_effects` es `null` y cada publicación, cada consulta de modificador de
    probabilidad y cada caducidad son una comprobación de nulo. Medido: 2.000 partidos del conjunto de
    referencia byte a byte idénticos a la salida de fase 0, y 400 → 422-448 partidos/s (ruido de medida).
    Con 30 perks entre los dos equipos, sobre disparadores de alta frecuencia y condiciones de dos
    funciones, el sobrecoste es del **16,5%** (< 20%, §3).
13. **`modifyProbability` se suma dentro de los límites que ya tenía cada resolución**: el `clamp` de
    pase (500..9800) y el de lesión (0..5000) siguen acotando el resultado, y la parada se acota a
    0..10000 puntos base tras convertir el porcentaje. Un perk no puede sacar una probabilidad de su rango
    de diseño.
14. **`setState` suelta el balón.** Si el objetivo derribado lo llevaba, el balón queda muerto en su
    posición y vuelve a estar disponible, en vez de quedarse asignado a un jugador que no puede tocarlo.
15. **`PlayerDefinition.Perks` y `Counters` son propiedades `init`, no parámetros posicionales.** Así las
    construcciones existentes del generador y de los tests siguen valiendo sin tocarlas y se puede
    escribir `definition with { Perks = ["bloodlust"] }`. `Counters` se construye ordenado por clave
    ordinal con `WithCounters`.
16. **`Progression` es una clase estática pura sobre datos sueltos** (ids, enteros, `Attributes`), no
    sobre el estado de la run, que todavía no existe. Expone `PerkSlots`, `InitialPerks`,
    `AwardExperience`, `LevelFor`, `AttributesAtLevel`, `LevelUp` y `ApplyCounterDeltas`. Como
    `PlayerDefinition` no tiene campo de experiencia (§2.2 de fase 0), la experiencia acumulada la lleva
    quien llama (la campaña de `/Balance`).
17. **Claves de condición en las plantillas: `<función><Sufijo>`** con sufijo `Lt/Le/Gt/Ge/Eq/Ne`, más las
    funciones booleanas por su nombre y `and`/`or`/`not`. Son 66 claves por idioma y el conjunto es
    cerrado, así que "condición describible" es comprobable: el cargador genera la descripción de cada
    perk en **todos** los idiomas cargados y una clave que falta es error de carga.
    `scoreDiffLt`/`scoreDiffGt` usan la forma genérica con `{n}` en vez de los "si va perdiendo"/"si va
    ganando" del ejemplo de §4: esos textos solo son correctos para `n = 0` y una plantilla no puede
    inspeccionar su argumento.
18. **Secciones de plantilla añadidas a las de §4**: `layout` (cómo se compone `[disparador]
    [condición]: [efectos] ([límite])`, localizable), `events` (sintagma nominal de cada evento, para
    `anula {event}`), `positions`, `zones`, `details` (para `position()`, `zone()` y `detail()`) y
    `counters` (nombre legible **en singular** de cada contador; si falta, se usa el id tal cual).
    `effects` gana `modifyAttributePerCounterDivided` para el caso `counterDivisor > 1`.
19. **`{value:+%}` convierte puntos base a porcentaje** con punto decimal invariante: 300 → `+3%`,
    1500 → `+15%`, 350 → `+3.5%`. `{value:+}` fuerza el signo.
20. **El validador de datos y los tests enumeran `/data` entero.** `Sim.Tests/TestData.LoadAllFiles`
    pasa de una lista escrita a mano a enumerar el directorio (salvo `schemas/`), como ya hacía
    `/Balance`: el catálogo de perks y las plantillas de otro paquete entran en los tests sin tocar el
    ayudante, y `DataLoaderTests` valida de paso todo lo que escriba el paquete G.
21. **`fingerprint.txt` se escribe en el directorio de salida de la configuración que corre.** Al medir
    la huella antes y después de un cambio hay que comparar la **misma** configuración (`bin/Debug` con
    `bin/Debug`): un `fingerprint.txt` de otra configuración puede ser de una revisión anterior del
    código. La equivalencia con 0 perks se verificó, además de con la huella, con las tres salidas CSV de
    un lote de 2.000 partidos idénticas byte a byte a las de fase 0.

### 9.2 Diagnóstico y correcciones del paquete I (cierre de la fase 1)

El paquete H dejó la fase 1 con las builds coherentes perdiendo y las malas ganando. El diagnóstico
completo, con números, está en `docs/balance/fase1-perks.md`. El paquete I se limitó a **diagnosticar y
corregir defectos**: el rediseño de mecánica que el diagnóstico motiva está en las ADR 0020 (cuerpos con
volumen) y 0021 (adyacencia estática y proximidad dinámica), y el ajuste de valores de perks y builds
espera a que esas ADR estén implementadas.

1. **La medida estaba rota antes que el diseño.** `/Balance --builds` generaba **una** plantilla por build:
   la misma build contra su referencia daba entre el 16,5% y el 59,5% según el dado del generador (sd de
   14,9 puntos, 20 plantillas × 200 partidos). Además el equipo de ids bajos —casi siempre la referencia,
   por orden alfabético— ganaba 2-3 puntos de más por los desempates del motor. Corregido con plantillas
   emparejadas, `--rosters` y reparto de ids alternado (§8, "Metodología de medida"). **Todos los números
   de fase 1 anteriores a esta corrección son ruido de generación, no balance.**
2. **`berserker` con 0 activaciones en 197 partidos no era un defecto del motor.** El perk se publica y se
   evalúa correctamente: con la medida corregida se activa en el 92-98% de los partidos de las builds que
   lo llevan. El 0 venía de que las 197 partidas usaban **una sola plantilla** en la que el portador del
   perk no elegía nunca `Tackle`.
3. **`bloodlust` sí es un perk muerto por construcción**, y sigue estándolo: su condición `bias() < 0` no
   puede ser cierta en fase 1 —el árbitro es fijo y neutro y ningún perk del catálogo usa `modifyBias`—.
   No se ha tocado (es una condición falsa, no un fallo del motor): queda anotado en `pendientes.md`.
4. **`bone_breaker` aplicaba su efecto al jugador equivocado** (corregido). Ponía `injure` —la probabilidad
   de *lesionar*, cuyo sujeto es quien entra— sobre `opponent`, es decir, hacía más peligroso al rival al
   que quería romper. El efecto pasa a `actor`.
5. **`buildsWinDifferently` se propone normalizada** contra la referencia de la propia raza (ADR 0012), y
   "lesiones que produce" son las causadas al rival. Sin normalizar la métrica mide la raza: `orc_none` ya
   causa 3,9× las lesiones de `elf_none` sin un solo perk.
6. **Las cadenas de pases se reparten por equipo** en `MatchReport` (`PassChainsByTeam`,
   `PassChainTotalLengthByTeam`), porque la métrica compara dos builds y la estadística de partido completo
   no las distingue.
7. **`severeInjury` no tiene consecuencia en fase 1** (no hay muertes y "grave" solo cambia el `Detail`),
   así que `guardian_angel` es un perk que se activa y no hace nada. No se ha cambiado; anotado.
8. **La puerta de fase 1 está escrita** (`Sim.Tests/Analysis/BuildGateTests.cs`, `Category=Gate`) y
   **desactivada con `Skip`** hasta que las ADR 0020/0021/0022 estén implementadas: sus umbrales dependen de la
   mecánica que va a cambiar. Lee `data/balance/builds/` y `data/balance/groups.json` con su propio
   cargador mínimo, como la puerta de fase 0 con `reference.json`: `Sim.Tests` no referencia `/Balance`.
