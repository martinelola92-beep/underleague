# BB-Q — «Arrollador» no se activa nunca (0/480 partidos)

**Estado: RESUELTA** (19 sep 2026). Diagnóstico + revisión de arquitectura
(`docs/analisis/bb-q-post-tackle-arquitectura.md`, Alt 0 aprobada) + arreglo + medición.
**Exposición 0,0 % → 12,5 %** con el mismo instrumento que produjo el cero.

## Síntoma

`steamroller` («Arrollador») da **0,0 % de exposición en 480 partidos** en el cribado del catálogo
(§34 de `docs/analisis/protocolo-balanceo-automatizado.md`). Es el único perk del lote de 24 con
exposición exactamente cero.

## El control que da el propio catálogo

`charge` («Embestida») tiene el **mismo disparador** (`TACKLE`), el **mismo efecto** (`extraAction`), la
**misma ausencia de `positionOnly`** —así que el mismo portador, el Defensa del slot 1— y **ninguna
condición**. Medido sobre las mismas 20 plantillas y semillas:

| perk | condición | activaciones en 20 partidos |
|---|---|---|
| `charge` | (ninguna) | **65** |
| `steamroller` | `stat(target,'down') == 1` | **0** |

El `_doc` de `steamroller` lo dice él mismo: *"la condición es lo que lo separa de Embestida"*. El
portador entra de sobra; lo que no ocurre nunca es la condición.

## Hipótesis

- **H1 — el portador no hace entradas** (como `cannon`/`double_shot`, §34.4). **REJECTED**: `charge`
  encadena 65 veces con exactamente el mismo portador.
- **H2 — `extraAction` no se cuenta como activación, y el 0 % es artefacto de medición.** **REJECTED**:
  mismo motivo, `charge` usa el mismo efecto y se cuenta.
- **H3 — `target` no está ligado en un evento TACKLE.** **CONFIRMED**.
- **H4 — `'down'` no significa "derribado".** **CONFIRMED**.
- **H5 — TACKLE se publica antes de resolverse, así que el derribo aún no ha ocurrido.** **CONFIRMED**.

## Las tres causas, encadenadas

Cada una basta por sí sola para que la condición sea falsa. **Arreglar solo una no arregla el perk.**

**1 — `target` no está ligado (causa proximal).** El motor pasa al jugador entrado como `opponent`, nunca
como `target`: `PublishBeforeResolving(EventType.Tackle, "attempted", tackler, opponent: carrier)`
(`MatchEngine.cs:2172`) y el `Emit` posterior (`:2202`) hacen lo mismo. `ConditionContext.Who(WhoRef.Target)`
devuelve `null`, y el manejador de `stat` devuelve **0** para un identificador sin ligar
(`ConditionCompiler.cs:779`, `who is null ? 0 : ...`). Así que `stat(target,'down') == 1` es
**idénticamente falsa**, no "rara". Medido: **307 eventos TACKLE en 20 partidos, 0 con `target` ligado,
307 con `opponent`.**

**2 — `'down'` significa otra cosa.** `MatchStat.Down` está documentado como *"1 si el jugador ha
terminado el partido **de baja** —lesionado o muerto—"* (`PerkDefinition.cs:269-274`) y se resuelve como
`player.Injured || player.Dead` (`EffectEngine.Stat`). El `_doc` del perk pide *"el rival de la entrada
QUEDÓ EN EL SUELO"*, que es `PlayerState.KnockedDown`. Son cosas distintas.

**3 — el evento se publica antes de la resolución.** `PublishBeforeResolving` existe a propósito (§3,
semántica pre-resolución: *"así un perk disparado por SHOT o TACKLE puede modificar atributos y
probabilidades de la propia resolución que ese evento gobierna"*). El derribo (`:2221`) y
`ResolveInjury` (`:2233`) ocurren **después**. Cuando la condición se evalúa, el rival no está ni
derribado ni lesionado por esa entrada.

## Por qué no es una errata

**El vocabulario de condiciones no tiene ninguna función que exponga el estado del jugador.** Las 22
son: `hasTag, attr, level, position, isMob, bias, zone, adjacent, adjacentCount, teammatesWithTag,
teammatesWithSameStyle, distanceToGoal, scoreDiff, tick, counter, detail, startsIn, startsOn, linked,
nearAlly, nearOpponent, stat`. Ninguna lee `PlayerState`. El perk **no se podía escribir bien**: su autor
usó `stat(target,'down')` porque era lo más parecido que existía.

El motor **sí** representa "derribado", y otro perk **sí** lo usa — pero por C#, no por condición:
`blood_scent` («Olfato de sangre») escribe `MatchPlayer.PreferKnockedDownTackleTarget`
(`EffectEngine.cs:1052`), que `Utility.cs:1408` consume para preferir como objetivo a quien está en
`PlayerState.KnockedDown` (`Utility.cs:1415`). Existe la semántica; falta la puerta desde `/data`.

## Evidencia

`Sim.Tests/Perks/SteamrollerConditionTests.cs` — cuatro tests que pasan y fijan las tres causas, más
`SteamrollerChainsAtLeastOnceWhenItsCarrierWinsTackles`, la prueba de regresión del arreglo: **hoy falla
a propósito** y está `Skip` para no dejar el árbol en rojo. Quitar el `Skip` al cerrar la ficha.

## Lo que hace falta decidir antes de tocar código

El arreglo no es un cambio de `/data`. Hacen falta las tres, y al menos las dos primeras son decisiones
de diseño/arquitectura, no de balance:

1. **Una forma de preguntar por el estado de un jugador desde una condición** — primitiva nueva del
   vocabulario (`game-design-review` por ser mecánica nueva, `architecture-review` por ser superficie de
   `/data`). Afecta a todo el catálogo, no solo a este perk.
2. **Cómo se le da a un perk de TACKLE acceso al resultado de la entrada**, dado que el disparador es
   pre-resolución por diseño: ¿un disparador posterior, un `detail()` que ya distingue `won`/`missed`, o
   un evento nuevo? `detail()` existe ya y el `Emit` posterior sí trae `won` — pero ese `Emit` va con
   `publish: false`, así que hoy no llega a ningún perk.
3. **Ligar el entrado a `target` además de a `opponent`**, o aceptar que en TACKLE se escribe `opponent`
   y arreglar el dato. Esto sí es barato, pero por sí solo no resuelve 2 ni 3.

## Hermanos

- `docs/analisis/protocolo-balanceo-automatizado.md` §34 — de dónde sale el 0,0 %.
- `docs/pendientes/BB-I.md` — «Depredador de área» pareció activarse cuando no era un tiro: el otro
  síntoma conocido de desajuste entre disparador y condición.


---

## Resolución (19 sep 2026)

Se aplicó **Alt 0** de la revisión de arquitectura: usar el evento post-resolución que ya existía, en vez
de una primitiva nueva. `Emit(Recovery, "tackle", tackler)` (`MatchEngine.cs:2223`) se publica justo
después del derribo de `:2221`, así que **una entrada ganada ES un rival en el suelo**.

`steamroller` pasa de `TACKLE` + `stat(target,'down') == 1` a **`RECOVERY` + `detail() == 'tackle'`**. Las
tres causas se resuelven de golpe: el evento es post-resolución (causa 3), el `actor` está ligado (causa
1) y no hace falta preguntar por ningún estado (causa 2). Es además el **primer uso de `detail()`** en el
catálogo, una primitiva que llevaba implementada y sin estrenar.

Infraestructura mínima (`extraAction` solo admitía `SHOT`/`TACKLE`):

- `Sim/Perks/EffectEngine.cs` — `ExecuteExtraAction` traduce `RECOVERY` a `RepeatTackle`.
- `Sim/Perks/PerkLoader.cs` — la regla RT-032 admite `RECOVERY`.
- `Sim/Analysis/PerkBalanceClassifier.cs` — `extraAction`+`RECOVERY` → `tacklesPerMatch`. **Necesario**:
  el clasificador mapeaba la métrica por el nombre del disparador, y sin esto `steamroller` salía
  `NotReady`/`NotReadyNoMetric` y desaparecía del conjunto `ReadyForScreening` (24 → 23), es decir el
  arreglo lo habría vuelto inmedible. La métrica correcta es `tacklesPerMatch` porque lo que el efecto
  ejecuta es `RepeatTackle`, no una "recuperación".

**No** se creó ningún `EventType`, ni primitiva de `PlayerState` (Alt 4), ni post-evento de TACKLE
(Alt 2), ni se ligó `target` (Alt 3).

### Medición

| | antes | después |
|---|---|---|
| exposición (mismo instrumento, 480 partidos) | **0,0 %** | **12,5 %** |
| activaciones (20 partidos, portador Defensa) | **0** | **3** |
| estado del cribado | `INSUFFICIENT_EVIDENCE` (imposible) | `INSUFFICIENT_EVIDENCE` (raro) |
| `charge` (control) | 65 | **65** |

Sigue por debajo del suelo del 50 %, pero **por otro motivo**: antes la condición era imposible, ahora el
perk es raro. Esto **no** dice si el perk es bueno o malo — solo que el mecanismo ya se manifiesta y por
fin se puede evaluar su diseño.

### Auditoría preventiva (Alt 5)

`Sim.Tests/Perks/TriggerBindingAuditTests.cs`, **solo informativa**: inventaría, para los 42 perks con
condición, qué identificadores pide cada uno frente a los que su disparador liga de verdad. Resultado:
**ninguna condición restante pide un identificador sin ligar.** `steamroller` era el único caso del
catálogo, y ningún perk pide `target` ni `opponent` en ninguna condición. No se ha convertido en error de
carga todavía (RT-032/RT-083 lo pedirían, pero primero había que medir).

### Efecto colateral registrado, no arreglado

`FaseAMeasurementTests` tenía a `steamroller` con rol predicho `Defender`, inferido cuando su disparador
era `TACKLE`. `PopulationFitness` está **congelado** (`e152253`) y solo mapea sitios de emisión
verificados, así que con `RECOVERY` ya no infiere rol. Se **retiró el caso** del test en vez de tocar el
analizador o la predicción congelada: la predicción no se ha falsado, el perk sobre el que se hizo dejó de
existir con esa forma.
