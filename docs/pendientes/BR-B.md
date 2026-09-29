# BR-B — Jugadores muertos que vuelven a la vida: INJURY tras DEATH

**Estado: CERRADA (29 sep 2026), causa CONFIRMED.** Encontrada por la revisión independiente de la ADR 0163
(la Gaceta enseñaba muertes que el estado no reflejaba). Anterior a la rama de la memoria visible.

## Observación

Semilla de balance 7, run 0, partido 15 (`700000 + i`, doctrina contextual): `DEATH` de `perk:skullsplitter`
sobre el jugador 9 (tick 1322) y, **en el mismo tick**, `INJURY minor` al mismo jugador. `MatchResolution`
aplicaba la lesión sin mirar si ya estaba `Dead` y lo dejaba en `MinorInjury`; mientras, `DeathDetails`
contaba la muerte (oro, reliquia, Herencia) y `ApplyRivalCredits` apuntaba `sufferedDeath`. Contradicción
visible: muerto en los créditos, vivo y lesionado en la plantilla.

Instrumento (Regla J): 60 runs con la política contextual, observador de partidos, contando `INJURY` de un
actor con `DEATH` previo en el mismo partido. **28 casos** antes del arreglo; los tres tipos de víctima
(propios y rivales), siempre en el mismo tick que la muerte. Reproduce exactamente el caso de la revisión.

## Hipótesis

| # | Hipótesis | Estado |
|---|---|---|
| H1 | El motor emite `INJURY` sobre un jugador que ya ha muerto en la misma disputa | **CONFIRMED** (caso 0/15: `Tackle block` publicado → `skullsplitter` mata al objetivo → `ResolveBlock` sigue y llama a `ResolveInjury` contra el muerto) |
| H2 | `MatchResolution` no ignora las bajas posteriores a la muerte | **CONFIRMED**: el `case Injury` no miraba `PhysicalState.Dead` |
| H3 | `ApplyRivalCredits` cuenta hechos que el bucle de bajas no cuenta | **CONFIRMED** (hermano): recorría el partido entero, el bucle de bajas para en `defeatTick`, y no distinguía bajas tras la muerte |
| H4 | Todos los casos son la misma vía (perk letal de contacto, trigger `TACKLE`) | **REJECTED** para los 28: quedan casos de `second_wound` (trigger `INJURY`), ver abajo |

## Causa

La disputa se publica al motor de efectos **antes** de resolverla (`EffectEngine`, §3 de la ADR 0046). Un perk
letal de contacto (`skullsplitter`, `marrow_thirst`, `iron_studs`) mata al objetivo ahí dentro, pero
`ResolveTackle`/`ResolveBlock` siguen su curso y tiran una lesión contra alguien que ya no está vivo. Nada en
la resolución, además, protegía el invariante «la muerte es terminal».

**`second_wound` es otro orden, no otro bug**: su trigger es `INJURY`, se publica antes de registrar la
lesión y su `DEATH` queda **delante** de la `INJURY` que lo causa en la secuencia. Es orden de registro (la
lesión ocurrió, luego la muerte), no una lesión sobre un muerto; la resolución lo cubre igual.

## Arreglo (en la causa y en la red)

- `MatchEngine.ResolveInjury`: `if (victim.Dead) return;` antes de cualquier tirada. No consume dados que un
  jugador vivo consumiría, así que el flujo de RNG solo cambia en los partidos donde había un muerto en
  disputa.
- `MatchResolution.Apply`: un `INJURY`/`DEATH` sobre quien ya es `Dead` no cuenta ni cambia el estado.
- `ApplyRivalCredits`: llega hasta el mismo evento que el bucle de bajas (`defeatTick`; hermano BE-C) y no
  acredita bajas de quien ya había muerto.
- Test permanente: `Sim.Tests/Run/MatchResolutionDeathIsTerminalTests.cs` (tres sintéticos y la reproducción
  real en las runs 0, 2, 4 y 14, con la comprobación «todo `DeathDetails` está `Dead` al final»).
