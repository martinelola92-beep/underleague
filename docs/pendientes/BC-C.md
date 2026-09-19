# BC-C — «Doble disparo» no tiene sentido y no cambia el resultado

**Estado:** Abierta · diagnóstico CONFIRMED · rediseño pendiente de `game-design-review`

## Observación

«"Doble disparo" no tiene sentido. Si ha disparado, ¿cómo va a volver a disparar automáticamente? Dale una
vuelta a este perk.» (Partida del revisor, 19 sep 2026.)

## Estado actual

`data/perks/double_shot.json`: trigger `SHOT`, `scope: actor`, sin condición, efecto `extraAction`, `limit`
1 por partido. Se ejecuta en la publicación previa a resolver el tiro (`EffectEngine.cs:1083-1107`,
`MatchEngine.cs:1837-1846`).

- **CONFIRMED — efecto nulo:** la llamada exterior de `LaunchShot` sobrescribe el vuelo de las interiores y
  decide su propia tirada. Goles por partido 1,191 con el perk frente a 1,156 sin él (1.000 partidos
  emparejados), dentro del ruido.
- **CONFIRMED:** el «si el primero sale mal» de su texto no existe: se dispara antes de tirar los dados.
- Además se salta su límite: ver [BC-B](./BC-B.md). Arreglar el límite **no basta**: seguiría habiendo dos
  `SHOT` en el mismo tick y el primero se descartaría.

## Opciones de rediseño (sin elegir)

1. **Segundo remate:** se dispara con `SHOT_BLOCKED` (o rechace del portero) de su propio tiro; se reubica
   sobre el balón suelto y remata en un tick posterior. Legible; necesita punto de reubicación «balón
   suelto», acción encolada y `opponent` en `SHOT_BLOCKED`. Hoy la parada es blocaje: solo cubriría bloqueos.
2. **Dos tiradas, la mejor:** `modifyProbability ShotOnTarget` durante la jugada. Solo dato, pero invisible
   (contra «comportamiento observable») y el nombre promete dos tiros.
3. **Remate de segunda línea** (`scope: team`, condición `nearAlly`): remata el rechace del tiro de un
   compañero cercano. Da decisión de alineación; misma primitiva nueva que 1 y menos exposición.

## Hermanos

[BC-B](./BC-B.md), [BC-D](./BC-D.md) (mismo patrón: el perk «se ve» pero no cambia la jugada).
