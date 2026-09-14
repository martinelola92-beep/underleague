# Matriz de efectos de perk/objeto/consumible: qué variable toca cada uno y dónde

Análisis **de lectura pura** del árbol en `/home/martinelola92/underleague` (commit `9caf15b`, rama `main`).
Nada del repositorio se ha modificado. Todas las rutas son absolutas; las líneas son del árbol real
(**no** de `.claude/worktrees/`, que contiene copias rancias con numeración distinta).

Etiquetas: **MEDIDO** (ejecuté algo y lo vi), **DERIVADO** (leído del código/datos), **HIPÓTESIS**.
Prácticamente todo es DERIVADO; no se ejecutó ningún lote de `/Balance` ni ningún test.

---

## 0. El enum real de tipos de efecto

`/home/martinelola92/underleague/Sim/Perks/PerkDefinition.cs:17-35` — `EffectType`, diez valores:

```
ModifyAttribute, ModifyLeash, ModifyBias, ModifyProbability, CancelEvent,
AddCounter, SetState, ModifyKnockdownTicks, Immunity, ModifyExperience
```

Hay **otro** enum de efectos que NO es este y no debe confundirse: `EventEffectKind` de las cartas de
evento (`Sim/Run/Systems/Events/EventCatalog.cs:150-165`, con `gold`, `goldShare`, `heal`, `experience`,
`experienceTarget`, `injure`). Vive en la capa de run, no toca el motor de partido, y queda **fuera** de
este encargo salvo para no contarlo por error al inventariar `/data`. (DERIVADO)

Los dos caminos por los que un efecto llega al motor:

| camino | entrada | qué admite |
|---|---|---|
| **Perk / habilidad racial** (con disparador y condición) | `EffectEngine.ApplyEffects` — `Sim/Perks/EffectEngine.cs:697` | los 10 tipos |
| **Objeto y consumible** (pasivo, sin disparador ni condición) | `EffectEngine.ApplyPassiveEffect` — `Sim/Perks/EffectEngine.cs:426-450` | **solo 5**: `ModifyAttribute`, `ModifyProbability`, `ModifyLeash`, `ModifyKnockdownTicks`, `Immunity`; el resto cae en `default: return false` (línea 448) y **se ignora en silencio** |

---

## 1. Catálogo de tipos de efecto

Columna «¿lo ve la IA?» = si el valor que ese efecto mueve entra en el cálculo de `Utility.Evaluate` /
`Utility.Choose` (`Sim/Engine/Utility.cs`) y por tanto puede cambiar **qué acción elige** el jugador.

| tipo | qué campo toca | dónde se aplica (fichero:línea, método) | permanencia | ¿lo ve la IA de utilidad? |
|---|---|---|---|---|
| `ModifyAttribute` | `MatchPlayer._attributeDeltas[kind]` → recalcula `_effectiveAttributes`, `_mass` y la `ActionZone` | `Sim/Perks/EffectEngine.cs:772` (`ApplyEffects`) y `:430` (`ApplyPassiveEffect`) → `Sim/Perks/Modifiers.cs:49` `AddAttribute` → `Sim/Engine/MatchPlayer.cs:371` `AddAttributeDelta` → `:403` `Recalculate` | **jugada** si `duration: play` (se deshace en `Modifiers.cs:233-256` `ExpirePlayModifiers`); **partido** en los demás casos. `run` **no** persiste dentro del motor | **SÍ** (para Strength/Speed/Technique/Leash; **no** para Stamina) |
| `ModifyLeash` | `MatchPlayer._leashCellDelta` → tamaño de `Zone` y `OuterZone` | `EffectEngine.cs:775` y `:436` → `Modifiers.cs:64` `AddLeash` → `MatchPlayer.cs:383` `AddLeashCellDelta` → `:403` `Recalculate` (`extraMilli`, línea 419) | jugada / partido, igual que el anterior | **SÍ**, y además cambia una **precondición dura** |
| `ModifyProbability` | `Modifiers._matchOdds[] / _playOdds[]` (multiplicador de **cuota**, base 10.000) o `_pairs` si es por par | `EffectEngine.cs:784` (`AddProbability`) / `:777` (`AddPairProbability`, solo `linked*`+canal `pass`, condición en `:728-729`) y `:433` → `Modifiers.cs:90` / `:117`; se lee en `Modifiers.cs:146` `Probability` | jugada / partido | **NO** — ver §4 |
| `ModifyKnockdownTicks` | `Modifiers._knockdownTicks[playerIndex]` | `EffectEngine.cs:787` y `:439` → `Modifiers.cs:175` `AddKnockdownTicks`; se lee en `Sim/Engine/MatchEngine.cs:2073` `KnockdownTicksCausedBy` (usado en `:2028` entrada ganada y `:2287` bloqueo) | **partido** (nunca expira: `AddKnockdownTicks` no tiene rama `expiresAtPlayEnd`) | NO |
| `Immunity` | `Modifiers._immunities[playerIndex]` (máscara de bits) y, si es `Push`, `MatchPlayer.Immovable` | `EffectEngine.cs:790` y `:442` → `Modifiers.cs:195` `AddImmunity` | **partido** (no expira) | NO directamente; `Immovable` cambia la física de empuje, no la decisión |
| `SetState` | Llama a `MatchEngine.KnockDown(player, effect.Ticks)` | `EffectEngine.cs:793` → `Sim/Engine/MatchEngine.cs:320` `KnockDown` (pone `PlayerState.KnockedDown` y suelta el balón) | **instantáneo** (el loader lo fuerza: `Sim/Perks/PerkLoader.cs:733-738`); sus consecuencias duran `Ticks` | Indirecto: `StateMachine.LegalActions(p.State)` (`Utility.cs:140`) recorta las acciones del derribado |
| `CancelEvent` | Devuelve `cancelled = true`, que aborta la resolución en curso | `EffectEngine.cs:718-722`; lo consume `MatchEngine.EmitCancellable` (`:3045`), llamado en `:2130` (falta), `:2160` (amarilla), `:2319` (roja) y `:2366` (lesión) | instantáneo | NO |
| `AddCounter` | `EffectEngine._counters[playerIndex][nombre]` (+ `_counterDeltas` si `accumulatesAcrossMatches`) | `EffectEngine.cs:732-736` → `:908` `AddCounter` | **instantáneo** al escribir, pero el valor persiste todo el partido y, si `accumulatesAcrossMatches`, se vuelca a `PlayerDefinition.Counters` entre partidos (relectura en `:673-683` `SeedCounter`) | NO: solo alimenta condiciones NCalc (`counter()`) y `EffectValue` (`:868-905`) |
| `ModifyBias` | `MatchEngine._bias` (criterio del árbitro, acotado −100..100) | `EffectEngine.cs:724-730` → `Sim/Engine/MatchEngine.cs:317` `ApplyBiasDelta`; se lee vía `BiasRollShift` en falta (`:1997`), tarjeta (`:2150`), penalti (`:2175`) y bloqueo (`:2264`) | **partido** | NO |
| `ModifyExperience` | **Nada dentro del partido**: `EffectEngine.cs:738-744` hace `continue` explícito. Fuera, suma a `ExperiencePercent` | `Sim/Progression/Progression.cs:122-137` `ExperiencePercent`, que lee `perk.Effects[e].Value` de la **definición**, no del motor | **run** (el loader exige `duration: run` y `target: owner`, `PerkLoader.cs:824-841`) | NO |

**Nota sobre la duración `Instant`** (DERIVADO): en `EffectEngine.cs:766` la única distinción es
`bool expiresAtPlayEnd = effect.Duration == EffectDuration.Play;`. Es decir, `Instant`, `Match` y `Run`
son **indistinguibles** dentro del partido. El loader lo tapa: `PerkLoader.cs:733-743` obliga a que los
cinco tipos «interruptor» (`AddCounter`, `ModifyBias`, `SetState`, `CancelEvent`, `Immunity`) sean
`instant` y a que los otros **no** lo sean, así que la ambigüedad no es alcanzable desde `/data` válido.

---

## 2. Canales de probabilidad (`ProbabilityKind`)

Enum en `Sim/Perks/PerkDefinition.cs:163-182`, 13 valores. Todo consumo pasa por dos envoltorios de
`Sim/Engine/MatchEngine.cs`:

- `Odds(player, kind)` — línea **339** (`ProbabilityScale.Neutral` si no hay `EffectEngine`).
- `OddsAgainst(subject, kind, counterpart, evasion)` — línea **348**: `Combine(Odds(sujeto), Invert(Odds(contraparte)))`.

| canal | resolución concreta (fichero:línea) | qué decide | uso en `/data` |
|---|---|---|---|
| `Foul` | `MatchEngine.cs:1999` (`ResolveTackle`) y `:2266` (`ResolveBlock`) | si la entrada / el bloqueo es falta | 1 |
| `Card` | `MatchEngine.cs:2149` `cardOdds`, aplicado en `:2152` (roja) y `:2158` (amarilla) | si la falta acaba en tarjeta | **0** |
| `Injury` | `MatchEngine.cs:2349`, lado **víctima**, combinado con `Injure` | lo lesionable que es quien recibe | 2 |
| `Injure` | `MatchEngine.cs:2349`, lado **agresor** | cuánto lesiona quien entra | 5 |
| `SevereInjury` | `MatchEngine.cs:2362` | leve vs. grave, una vez ya hay lesión | 3 |
| `Pass` | `MatchEngine.cs:1553` (`ResolvePass`) | si el pase se completa | 3 |
| `Intercept` | `MatchEngine.cs:1241` (`InterceptChance`), vía `OddsAgainst` contra `InterceptEvasion` | si un rival lee el pase | 7 |
| `InterceptEvasion` | `MatchEngine.cs:1241`, lado **pasador** (invertido) | resistencia a que te lean el pase | 1 |
| `Dribble` | `MatchEngine.cs:1940` (`ResolveDribbleDuel`) | si el conductor gana el regate | 4 |
| `Tackle` | `MatchEngine.cs:2062` (`TackleWinChance`, vía `OddsAgainst` contra `TackleEvasion`) y `:2272` (`ResolveBlock`, sin evasión) | si la entrada/bloqueo gana el balón o derriba | 14 |
| `TackleEvasion` | `MatchEngine.cs:2062`, lado **conductor** (invertido). **No** interviene en el bloqueo de `:2272` | resistencia a que te roben | 4 |
| `ShotOnTarget` | `MatchEngine.cs:1697`, **invertido** sobre la probabilidad de fallar | si el disparo va a puerta | 10 |
| `Save` | `MatchEngine.cs:1839` (`ResolveSaveDuel`) | si el portero para | 2 |

**Todos los 13 canales tienen consumidor en el motor.** (DERIVADO)

Dos matices de alcance que sí son reales:

1. **`ProbabilityKind.Card` no lo declara ningún dato.** `grep -rho '"probability": "..."' data/` da 57
   ocurrencias y ninguna es `card`. El consumidor existe (`MatchEngine.cs:2149`); lo que falta es
   contenido. (DERIVADO)
2. **`TackleEvasion` protege de la entrada pero NO del bloqueo.** `ResolveTackle` usa `OddsAgainst`
   (`:2062`), pero `ResolveBlock` usa `Odds(blocker, Tackle)` a secas (`:2272`). Un elfo con su evasión
   está cubierto contra la entrada y desnudo contra la carga sin balón, que también derriba
   (`:2287`). Puede ser deliberado (el bloqueo no disputa balón) pero no está documentado en el código.
   (DERIVADO)

### Los modificadores por par (ADR 0021) y su tabla cerrada

`Modifiers.PairEventFor` (`Sim/Perks/Modifiers.cs:209-215`) mapea canal → evento que forma el par, y
`IsCounterpartOfCurrentEvent` (`:217-226`) exige que el evento en curso sea ese. El contexto lo fija
`Modifiers.SetResolutionContext` (`:134`). **Pero** `EffectEngine.cs:728-729` ya restringe en origen los
modificadores por par al canal `Pass`:

```csharp
bool pairwise = effect.Target is EffectTarget.Linked or EffectTarget.LinkedWithTag
    && effect.Probability == ProbabilityKind.Pass;
```

Consecuencia: de las cuatro ramas de `PairEventFor`, **solo la primera es alcanzable** (y de ella, solo
`Pass`). Las ramas `Dribble`, `ShotOnTarget`/`Save` y el `_ => Tackle` son código muerto salvo que se
levante esa restricción. El comentario de `EffectEngine.cs:719-727` explica por qué se restringió (un
vinculado es siempre un compañero, y el pase es la única resolución compañero-compañero) — la tabla se
dejó completa a propósito, pero hoy no se ejecuta. (DERIVADO)

---

## 3. Canales de atributo (`AttributeKind`)

Enum en `Sim/Model/Attributes.cs:4-11`: `Strength, Speed, Technique, Stamina, Leash`.
Efectivo = `clamp(base + deltas, 1, 99)` en `Sim/Engine/MatchPlayer.cs:407`.

| atributo | dónde entra en el motor (fichero:línea) |
|---|---|
| **Strength** | masa / empuje `MatchPlayer.cs:410`; calidad de tiro `MatchEngine.cs:1679`; parada a larga distancia `:1815`; falta en entrada `:1995`; entrada dura (umbral) `:2141`; regate (cobertura del defensor) `:1936`; bloqueo `:2270`; lesión (agresor) `:2342`. **Utilidad**: `Utility.cs:1294` (`ShootStrengthSlope`) |
| **Speed** | pasos por tick `MatchEngine.cs:966` (`SpeedPerTick`) → `SpeedPerTickMilli` `:693`; parada a corta distancia `:1814`; presión de la entrada `:2059`; regate (cobertura) `:1936`; bloqueo `:2271`. **Utilidad**: `Utility.cs:1113` (`DribbleSpeedSlope`), y vía `SpeedPerTickMilli` en el pase en profundidad `:1226` y `:1236` y en el pase al pie `:1569` |
| **Technique** | pase `MatchEngine.cs:1547`; intercepción `:1230`; tiro `:1678`; regate `:1939`; entrada (resistencia del conductor) `:2062`; parada (rival del portero) `:1817`. **Utilidad**: `Utility.cs:952` (pase corto/largo), `:1112` (regate), `:1269` (pase en profundidad), `:1293` (tiro) |
| **Stamina** | decaimiento de paradas consecutivas `MatchEngine.cs:1819`; fatiga de movimiento `:987`; resistencia a la lesión `:2342`. **Utilidad: en ninguna parte** — ni un solo uso en `Sim/Engine/Utility.cs` |
| **Leash** | escala de la `ActionZone` `MatchPlayer.cs:417-425`. **Utilidad**: penalización blanda `Utility.cs:525-530` (`OutsidePenalty`, `:548-554`) y **descarte duro** `:532-537` → `:163` |

Bonos de **rasgo** (no de perk) que se agregan una vez en el constructor
(`MatchPlayer.cs:105-117`) y que la utilidad sí lee: `ShootRangeBonusCells` (`Utility.cs:1012`, `:1287`),
`LeaderBonusPercent` y `ActionMultiplier` (`Utility.cs:160`), `HasTrait(Aggressive)` (`Utility.cs:1431`).
Ningún `EffectType` los mueve — no hay canal de efecto que escriba un bono de rasgo. (DERIVADO)

---

## 4. La distinción clave: ESTADÍSTICA vs. DECISIÓN

Criterio aplicado: **MODIFICA DECISIÓN** si el valor entra en `Sim/Engine/Utility.cs` (cambia la acción
elegida en `Utility.Choose`, línea 161, o descarta una en la 163) o cambia una precondición del motor.
**MODIFICA ESTADÍSTICA** si solo altera el resultado de una tirada ya decidida.

| MODIFICA ESTADÍSTICA | MODIFICA DECISIÓN |
|---|---|
| `ModifyProbability` — **los 13 canales**. `Utility.cs` no menciona `Odds`, `Modifiers` ni `_effects` en ninguna línea; los canales solo se leen desde `MatchEngine.Odds` (`:339`), que solo se invoca dentro de las diez resoluciones de §2, todas posteriores a la decisión. | `ModifyAttribute` sobre **Technique** — `Utility.cs:952`, `:1112`, `:1269`, `:1293`: la técnica es una pendiente aditiva sobre el `Context` de pasar, regatear, pase en profundidad y tirar, y ese `Context` se suma al score en `:161`. |
| `ModifyAttribute` sobre **Stamina** — no aparece en `Utility.cs`; solo en `MatchEngine.cs:1819`, `:987`, `:2342`. | `ModifyAttribute` sobre **Strength** — `Utility.cs:1294` (`ShootStrengthSlope`). |
| `ModifyKnockdownTicks` — solo se lee en `MatchEngine.cs:2073`, al **aplicar** el derribo ya resuelto. | `ModifyAttribute` sobre **Speed** — `Utility.cs:1113` (`DribbleSpeedSlope`) y, vía `SpeedPerTickMilli`, `:1226`/`:1236`, que eligen la casilla del pase en profundidad. |
| `ModifyBias` — `MatchEngine.cs:317`; se consume en `BiasRollShift`, un desplazamiento de tirada (`:1997`, `:2150`, `:2175`). Ningún jugador lo consulta al decidir. | `ModifyLeash` — **el caso más fuerte**: `MatchPlayer.cs:419` cambia `Zone` y `OuterZone`; `Utility.cs:532-537` marca `eval.OutsideOuterLimit` y `Utility.cs:163` **descarta la acción entera**. Es precondición dura, no penalización. Además penaliza blando en `:525-530`. |
| `Immunity: Push` — `MatchPlayer.Immovable`, leído en `Sim/Engine/BodySeparation.cs:123-124`: reparte el desplazamiento, no decide nada. | `ModifyAttribute` sobre **Leash** — misma vía que `ModifyLeash` (`MatchPlayer.cs:417`). |
| `Immunity: Mourning` / `MinorInjuryPenalty` — fuera del partido; ver §5. | `SetState` (derribo) — `MatchEngine.cs:320` pone `KnockedDown`, y `StateMachine.LegalActions` (`Utility.cs:140`) recorta el menú de acciones del derribado a cero opciones útiles. Cambia la decisión del **rival**, no la propia. |
| `CancelEvent` — `EffectEngine.cs:718`; anula un resultado, no una elección. | |
| `AddCounter` — solo alimenta condiciones NCalc y `EffectValue` (`EffectEngine.cs:868`); **de segundo orden**: decide si un perk se activa, nunca qué acción elige la IA. | |
| `ModifyExperience` — fuera del partido (`Progression.cs:122`). | |

**Consecuencia de diseño (DERIVADO, la conclusión operativa del encargo):** el canal
`modifyProbability` —que es **56 de los 88 efectos de partido de `/data`**, un 64 %— es incapaz por construcción de
cambiar el comportamiento de un jugador. Un perk de `+tackle` no hace que su portador entre más; hace que
gane más las entradas que ya iba a intentar. Solo `modifyAttribute` y `modifyLeash` (9 efectos,
un 10 %) mueven la IA, y de los cinco atributos solo cuatro llegan a `Utility.cs`. Si se busca una palanca
de «build que juega distinto» y no de «build que tira mejor los dados», hoy no existe ningún tipo de
efecto que escriba directamente en los pesos de utilidad de `data/ai/weights.json`.

---

## 5. Huecos: declarado sin consumidor, o con consumidor inalcanzable

Ordenados por gravedad.

### H1 — `ImmunityKind.Mourning` no tiene consumidor en ninguna capa (RF-104 sin implementar)

- El perk `roots`/`numb` la concede: `/home/martinelola92/underleague/data/perks/numb.json` declara
  `{ "type": "immunity", "target": "owner", "immunity": "mourning" }`.
- Dentro del partido se guarda en `Modifiers._immunities` (`Sim/Perks/Modifiers.cs:197`).
- Fuera del partido la lee `Progression.HasImmunity` (`Sim/Progression/Progression.cs:145`), **pero esa
  función solo se invoca una vez en todo el repositorio**, y con `MinorInjuryPenalty`:
  `Sim/Run/RunState.cs:243`.
- El estado que debería consumirla, `RunState.Mourning` (`Sim/Run/RunState.cs:173`, «Partidos que le
  quedan de duelo, 0 si no aplica (RF-104)»), **solo aparece en la serialización**
  (`Sim/Run/Save/RunSave.cs:374` al escribir, `:583` al leer). Nadie lo escribe con un valor distinto de
  cero ni lo decrementa ni aplica su penalización de atributos.

Es decir: **RF-104 (duelo por vínculo) no está implementado**, y la mitad de la habilidad racial de los
no-muertos —la que el `_doc` de `numb.json` describe como «convierte el desgaste en su ventaja»— vale
literalmente cero. El jugador ve una descripción generada que promete algo que el motor no hace, lo que
choca con RT-035 y con la regla 11 de `CLAUDE.md`. (DERIVADO, alta confianza: el `grep` sobre
`Sim/ Game/ Balance/ tools/` no devuelve ningún otro uso de `Mourning`.)

### H2 — `Modifiers.HasImmunity` es código muerto

`Sim/Perks/Modifiers.cs:186-187` expone `HasImmunity(MatchPlayer, ImmunityKind)`. **Ningún fichero del
repositorio lo llama.** La única inmunidad que surte efecto dentro del partido lo hace por un atajo:
`AddImmunity` escribe además `MatchPlayer.Immovable` (`Modifiers.cs:198-201`), y eso sí lo lee
`BodySeparation.cs:123-124`. Conclusión: el array `_immunities` es hoy **write-only**; si alguien añade
un cuarto `ImmunityKind` y lo concede, no pasará nada y no habrá ningún error. (DERIVADO)

### H3 — `LimitScope.Run` no se reinicia nunca y `LimitScope.Match` tampoco lo necesita

`EffectEngine.ResetLimits` (`Sim/Perks/EffectEngine.cs:685`) solo se llama con `LimitScope.Play`
(`:321`, desde `EndPlay`) y `LimitScope.Mob` (`:325`, desde `StartMob`, invocado en
`MatchEngine.cs:2839`). `Match` funciona por construcción (el `EffectEngine` se crea de nuevo cada
partido, así que `Uses` nace en 0). **`Run` no funciona**: como las suscripciones también se recrean cada
partido, un `"per": "run"` se comportaría exactamente como `"per": "match"`, silenciosamente.

Mitigación actual: los **11** límites que hay en `/data` son todos `"per": "match"`. No hay ninguno
`play`, `mob` ni `run`. Así que `LimitScope.Play` y `LimitScope.Mob` tienen consumidor pero ningún dato,
y `LimitScope.Run` no tiene ni una cosa ni la otra. (DERIVADO)

### H4 — Las ramas no-`Pass` de `Modifiers.PairEventFor` son inalcanzables

Detallado en §2. `EffectEngine.cs:728-729` filtra los modificadores por par al canal `Pass`, así que de
las cuatro ramas de `Modifiers.cs:209-215` solo puede ejecutarse la primera. Fue una decisión consciente
y comentada (`EffectEngine.cs:719-727`, «medido, quitar `covering_shadow` o `pivot_duo` de una build no
cambiaba ni un partido»), pero el resultado es que **`EffectTarget.Linked` combinado con cualquier canal
que no sea `pass` cae por la rama `case EffectType.ModifyProbability:` de `:784` y aplica el bono al
compañero en solitario** — que es lo que el comentario dice querer, pero la tabla de `PairEventFor` sigue
ahí sugiriendo lo contrario. Riesgo de mantenimiento, no bug de hoy. (DERIVADO)

### H5 — `EffectType.SetState` no lo usa ningún dato, y `EffectDefinition.State` no lo lee el motor

- `grep -rho '"type": "..."' data/` no devuelve ni un `setState`. El tipo está implementado
  (`EffectEngine.cs:793` → `MatchEngine.cs:320`) y validado (`PerkLoader.cs:747-763`) y **no se usa**.
- Además, `EffectDefinition.State` (`PerkDefinition.cs:209`) **nunca se lee en el motor**:
  `EffectEngine.cs:793` llama a `KnockDown(player, effect.Ticks)`, que fija `PlayerState.KnockedDown`
  incondicionalmente (`MatchEngine.cs:332`). El campo sobrevive porque `PerkLoader.cs:751-754` obliga a
  que valga `KnockedDown`; es un campo redundante que parece configurable y no lo es. (DERIVADO)

### H6 — `ProbabilityKind.Card` sin ningún dato, y otros objetivos declarados sin contenido

Con consumidor en el motor pero **cero uso en `/data`** (DERIVADO, del inventario de `grep`):

| declarado | consumidor | usos en `/data` |
|---|---|---|
| `ProbabilityKind.Card` | `MatchEngine.cs:2149` | 0 |
| `EffectType.SetState` | `EffectEngine.cs:793` | 0 |
| `EffectTarget.Adjacent` | `EffectEngine.cs:963` | 0 |
| `EffectTarget.AdjacentWithTag` | `EffectEngine.cs:965-968` | 0 |
| `EffectTarget.LinkedWithTag` | `EffectEngine.cs:1000-1003` | 0 |
| `PerkScope.Target` | `EffectEngine.cs:651` | 0 |
| `MatchStat.Goals`, `.PassesCompleted`, `.Shots`, `.Saves` | `EffectEngine.cs:1023-1030` | **0**. `grep -rho "stat([^)]*)" data/perks data/races` devuelve **una sola** invocación en todo el catálogo: `stat(actor,'tacklesWon')`. Los otros cuatro valores del enum no los usa nadie |

### H7 — `ElseEffects` existe, se ejecuta, y todos los datos lo tienen vacío

`EffectEngine.cs:284-288` selecciona `subscription.Perk.ElseEffects` cuando la condición es falsa y hace
`continue` si la lista está vacía. **Comprobado sobre los 61 ficheros de `data/perks/`: todas las listas
`elseEffects` tienen longitud 0** (script Python sobre cada JSON; salida vacía). Coincide con la ADR 0088
(«ningún perk es negativo»), así que es intencional — pero significa que la rama `:284`, el sufijo
`":else"` del informe (`:309`) y el `ElseEffects` de `SeedCounters` (`:664-667`) son código sin ejercicio
en producción. (DERIVADO / MEDIDO — el recuento sí lo ejecuté sobre los ficheros)

### H8 — Objetos y consumibles ignoran en silencio cinco tipos de efecto

`EffectEngine.ApplyPassiveEffect` (`:426-450`) tiene `default: return false`. Un objeto o consumible que
declarase `cancelEvent`, `addCounter`, `setState`, `modifyBias` o `modifyExperience` **no fallaría**: se
contabilizaría como «no aplicado» en `applied` (`:409`) y el informe diría `equipped` igual. **Verificado**: `Sim/Run/Systems/EffectJson.cs:36-41` lanza
`DataException` ante cualquier tipo que no sea `modifyAttribute` o `modifyProbability`, así que el hueco
**no es alcanzable** desde `data/items` ni `data/consumables`. Queda como riesgo latente: si alguna vez se
amplía ese cargador, `ApplyPassiveEffect` aceptará el dato y no hará nada, en silencio, al revés de lo que
pide RT-032.

### H9 — `ImmunityKind.Push` tiene dos caminos y uno es redundante

`MatchPlayer.cs:59` siembra `Immovable` comparando `race.Ability == "roots"` — una comparación de
**cadena literal** contra un id de perk (`ImmunityAbility`, línea 24). El efecto `immunity: push` de
`data/perks/roots.json` hace exactamente lo mismo por la vía de datos (`Modifiers.cs:198-201`). No es un
bug (el resultado es idéntico), pero es la única vez en el motor que se pregunta por un id concreto de
`/data` desde C#, lo que roza la regla 5 de `CLAUDE.md`. (DERIVADO)

---

## Lo que NO se pudo determinar leyendo (UNKNOWN)

1. **El impacto numérico** de cualquiera de estos huecos: quitar `numb` de los no-muertos, o el hueco de
   `TackleEvasion` en el bloqueo, requiere un lote de `/Balance` que el encargo prohíbe expresamente.
