# BB-Q — «Arrollador» no se activa nunca (0/480 partidos)

**Estado: abierta.** Diagnóstico cerrado, arreglo NO implementado: necesita una primitiva nueva del
vocabulario de condiciones, y eso pasa por `game-design-review` + `architecture-review` antes de código.

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
