# BC-D — «Último hombre» se activa pero no hace nada

**Estado:** **Cerrada (30 sep 2026), ADR 0181** · diagnóstico CONFIRMED · mecánica implementada; **75 % provisional, sin medir contra un techo**

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


## Resolución (30 sep 2026) — ADR 0181

`game-design-review` (resumen). *Qué vive el jugador:* un tiro rival va a puerta, su defensa aparece en la
trayectoria y **se queda con el balón** (`SHOT_BLOCKED guard` + `RECOVERY guard`, el texto «X bloquea el
disparo de Y» ya existe); si la tirada falla, queda plantado en la trayectoria y el tiro sigue. *Decisión:*
la mecánica candidata de arriba, con dos matices: (1) disparador nuevo `SHOT_ON_TARGET`, publicado tras la
tirada de dentro/fuera, para no gastar el uso en el ~26 % de tiros que van fuera; (2) el efecto `guardShot`
con `value` = 75 (petición del revisor; **probabilidad fija, no cuota**, ADR 0050 P1: para pasar de ≈ 4 % a
75 % haría falta un ×65, fuera de escala). *Teletransporte:* se mantiene, el revisor lo pidió («ha podido
teletransportarse»); el defensa se coloca en el punto de la trayectoria más cercano a él, entre el 50 y el 90 %
del recorrido. *Degeneración:* con 75 % y un uso por partido, ≈ 0,5 activaciones por partido; medido abajo.
*Frecuencia real:* 456 activaciones en 750 partidos, 327 paradas = 72 % (`GuardShotTests`).

| perk | antes | después | (por semilla 5 / 11) |
|---|---:|---:|---|
| `last_man` | +43,6 | **+118,5** | 75,5/11,7 → 136,7/100,3 |

**Aviso, para el revisor**: +118 es el segundo valor más alto del catálogo tras `deathless_march` (+178) y
muy por encima de cualquier otro raro (`duelist` +57, `own_third_anchor` +35). El diseño lo pidió («un
héroe»), pero esta cifra es la de un 75 % puesto por petición y sin contrapeso de coste; si el revisor lo
quiere en la banda de un raro, el dial es `value` (dato) o el uso por partido. **No se toca sin su decisión**:
es un cambio de balance de un perk que él mismo fijó (RT-057). Efecto secundario anotado: `RECOVERY guard`
activa los perks de `RECOVERY` sin filtro de detalle (`lane_reader`, `road_warrior`, `sweeper_keeper`); es una
recuperación de verdad.
