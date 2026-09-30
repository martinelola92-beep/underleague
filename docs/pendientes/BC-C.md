# BC-C — «Doble disparo» no tiene sentido y no cambia el resultado

**Estado:** **Cerrada (30 sep 2026), ADR 0180** · diagnóstico CONFIRMED · rediseño hecho

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


## Resolución (30 sep 2026) — ADR 0180

**Hermano hallado por la Regla G**: `point_blank` («A bocajarro») era el mismo perk con otra condición
(`extraAction` sobre `SHOT`, `distanceToGoal(actor) < 3`) y era igual de inerte.

`game-design-review` (resumen). *Qué vive el jugador:* su delantero dispara, el balón vuelve suelto (bloqueo,
rechace del portero, palo) y **él mismo lo remata otra vez en el acto**; el cartel sale cuando empieza el
segundo tiro. *Decisión:* la alternativa 1 de arriba, sin punto de reubicación nuevo: se mide que el tirador
está a ≤ 3 casillas del balón en el 77 % de los rechaces (≤ 4 en el 93 %; 557 rechaces en 400 partidos), así
que el salto del balón al recuperarlo es de lo que un tiro recorre en un tick. *Primitiva:* disparador nuevo
`SHOT_REBOUND` (publicado con el balón ya suelto, sin registrarse en el log) y `extraAction` acepta
`SHOT_REBOUND`, `TACKLE` y `RECOVERY` con `scope: actor`, rechazando `SHOT`. *Alternativas:* armar en `SHOT`
(gasta el uso en el primer tiro y el cartel sale antes del suceso), un disparador por final (tres perks),
la alternativa 3 de la ficha (rechace de un compañero: mismo motor, más exposición y otro nombre).
*Degeneración:* dos perks del mismo equipo cuelgan del mismo rebote → el primero en el orden de RT-041 se
lo queda (el balón debe seguir libre). *Frecuencia medida:* 0,16 activaciones por partido (119 en 750).

| perk | antes | después | (por semilla 5 / 11) |
|---|---:|---:|---|
| `double_shot` | +19,5 | +13,7 | 22,1/16,9 → 15,6/11,7 |
| `point_blank` | +9,1 | +5,9 | −3,9/22,1 → 6,5/5,2 |

Tabla emparejada de la ADR 0087 (`--perk-values --rosters 192 --runs 16`, semillas 5 y 11, horizonte 8, mismo
catálogo salvo el perk): **sigue siendo un perk de relleno pequeño, y ya no es inerte** (antes 0 tiros
extra por diseño, ahora ~0,16 remates por partido). El valor ya no depende del ruido de una semilla
(±20 antes, ±2 ahora), pero no es un perk fuerte: su techo lo pone que sólo hay 1,39 rechaces por partido
entre los dos equipos. `double_shot` pasa a 2 usos con 25 s de enfriamiento (RF-069c); no medido por separado.
El instrumento se validó con perks intactos (`blood_tithe`, `duelist`, `nutmeg`…: idénticos al decimal).

## Remedición sobre main tras el rebase (30 sep 2026, ADR 0175 incluida)

Tabla de la ADR 0087 (`--perk-values --rosters 192 --runs 16`), semillas 5/11/17, **main contra rama** (mismo instrumento,
mismo catálogo salvo los cambios de la rama), error típico de la diferencia entre medias de tres semillas. Instrumento
validado: `blood_tithe`, `duelist`, `nutmeg` y `own_third_anchor`, que la rama no toca, salen **idénticos al dígito**
en los dos lados (Regla J). El lote de referencia (`--runs 3000`, semillas 1 y 2) es igual en main y en rama salvo dos
métricas INFORMATIVAS del orden de 0,03 puntos (la referencia casi no lleva estos perks). Las builds de `/Balance` que
los usan (`elf_glass`, `orc_butchery`) **no se pueden medir**: fallan con «asigna N perks, solo tiene M slots» también en
main (BuildGate, ya rojo); la tabla por perk es el sustituto.

| perk | main | rama | diferencia (± e.t.) | etiqueta |
|---|---:|---:|---:|---|
| `double_shot` | +1,3 | +6,0 | +4,7 ± 9,3 | LIKELY un relleno pequeño (±10 por semilla): no separable de 0 |
| `point_blank` | +3,7 | +5,0 | +1,3 ± 3,2 | idem |

Activaciones por partido (`ReboundShotTests`, 750 partidos): `double_shot` 133, `point_blank` 76: el perk **se activa y
cada activación va detrás de un rechace y seguida de otro tiro** (CONFIRMED por el test, no por el valor). El tope lo
pone que hay pocos rechaces por partido.
