# BC-D — «Último hombre» se activa pero no hace nada

**Estado:** Abierta · diagnóstico CONFIRMED · mecánica nueva pendiente de `game-design-review` y ADR

## Observación

«"Último hombre" se activa bien pero no hace nada. Debería ser que el jugador se interponga delante del balón
y tenga una alta probabilidad de quedarse con él (75 %). Es el héroe del momento: ha podido teletransportarse
y parar un balón que iba a gol.» (Partida del revisor, 19 sep 2026.)

## Estado actual

`data/perks/last_man.json`: trigger `SHOT`, `scope: opposingTeam`, sin condición, `limit` 1 por partido, solo
Defensa. Efecto `relocate` a `betweenBallAndOwnGoal` (`Sim/Perks/EffectEngine.cs:987-1008`), en la publicación
previa a resolver el tiro (`MatchEngine.cs:1764`). Después solo queda el bloqueo genérico `TryBlockShot`
(`MatchEngine.cs:1210-1250`), con techo ≈4,4 %. Su propio `_doc` lo dice: «es pura colocación».

## Medición (1.000 partidos emparejados, mismo tiro con y sin perk)

| | con perk | sin perk |
|---|---|---|
| bloqueos del portador | 23 | 5 |
| goles | 223 | 222 |
| paradas | 445 | 453 |

Goles encajados por partido 1,079 frente a 1,065 (ruido). Gasta su único uso en el primer tiro rival aunque
vaya fuera (~26 % de las veces).

**CONFIRMED:** el efecto se ejecuta y se ve (cartel ADR 0112, corte de teletransporte BA-K) pero no cambia la
jugada. La intención del revisor no la implementa ningún dato actual: es una mecánica distinta.

## Mecánica candidata (sin implementar)

Efecto nuevo tipo `guardShot`: arma al portador y actúa **después** de la tirada a puerta, solo si el tiro va
a puerta; lo coloca en la trayectoria y tira `_rng.Chance(p)` del flujo del partido (p en enteros en el
dato; orden RT-041 si hay varios). Eventos existentes: `SHOT_BLOCKED` (detalle nuevo, p. ej. `caught`) y
`RECOVERY`; el render no calcula nada (RT-014).

Conflicto: una probabilidad fija choca con la ADR 0050 P1 (los perks multiplican la cuota, no la fijan) →
ADR propia. Riesgo: ~0,25 goles menos por partido por portador → `balance-measure`.

## Hermanos

[BC-C](./BC-C.md).
