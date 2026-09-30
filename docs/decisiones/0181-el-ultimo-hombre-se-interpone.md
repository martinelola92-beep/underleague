# ADR 0181 — El último hombre se interpone: `SHOT_ON_TARGET` y `guardShot`

Fecha: 30 sep 2026 · Estado: **aceptada** (autónoma, dentro de la autorización del revisor para mover rangos)
Cierra [BC-D](../pendientes/BC-D.md). Requisitos: RF-065, RF-066, RF-069, RF-012d, RT-014, RT-021, RT-041. Complementa la [ADR 0180](0180-una-accion-extra-va-despues-de-la-original.md).

## Por qué

«Último hombre» (`last_man`) se activaba —cartel incluido— y no cambiaba la jugada: `relocate` al punto medio entre el
balón y su portería, y después sólo quedaba el bloqueo genérico (techo ≈ 4,4 %). *Medido (BC-D, 1.000 partidos
emparejados):* goles encajados 1,079 con el perk contra 1,065 sin él. Gastaba además su único uso en el primer tiro
rival aunque fuese fuera (~26 % de las veces). Lo que pidió el revisor (19 sep 2026): *«que el jugador se interponga
delante del balón y tenga una alta probabilidad de quedarse con él (75 %). Es el héroe del momento»*.

## Decisión

1. **Disparador `SHOT_ON_TARGET`** (`EventType.ShotOnTarget`, al final del enum): se publica al final de `LaunchShot`,
   cuando la tirada de dentro/fuera ya se hizo y el balón va en vuelo hacia la portería. Sólo de perks (publicado y no
   registrado, `EventTypeNames.IsTriggerOnly`); no se publica en penaltis. El uso del perk ya no se gasta en tiros que
   van fuera.
2. **Efecto `guardShot`** (`EffectType.GuardShot`, `value` = % de quedarse con el balón, 1-100): el portador se coloca
   en el punto de la trayectoria más cercano a él, entre el 50 y el 90 % del recorrido (para no aparecer pegado al
   tirador ni dentro de la portería), y tira `_rng.Chance(value·100)` del flujo del partido. Si sale, `SHOT_BLOCKED`
   con detalle `guard` (el texto de la pantalla, «X bloquea el disparo de Y», ya existe), `SetOwner` y `RECOVERY`
   `guard`: el tiro se cuenta como bloqueado y el balón es suyo. Si no sale, se queda en la trayectoria y el tiro sigue
   (el bloqueo genérico y el portero lo resuelven como siempre). Sólo con disparador `SHOT_ON_TARGET`, `scope:
   opposingTeam` y `target: owner` (lo comprueba el cargador, RT-032).
3. **Probabilidad fija, no cuota.** La [ADR 0050](0050-fundamentos-matematicos.md) P1 fija que los perks multiplican la
   cuota; para llegar de un ≈ 4 % de bloqueo a un 75 % haría falta un ×65, fuera de toda escala legal. Un acto que
   rompe una regla («un tiro a puerta se para») declara su probabilidad, y el 75 % es la petición del revisor: su
   procedencia es esa, no una medición. **Provisional, sin medir**: el valor de `last_man` en la tabla de perks
   (ficha BC-D) decide si baja.
4. **El teletransporte se queda**: el revisor lo pidió («ha podido teletransportarse»), es un corte igual que el resto de
   reubicaciones (BA-K) y ocurre en la trayectoria de un tiro, donde el balón ya se mueve varias casillas por tick.
   Previsibilidad (regla 11): el perk sólo salva; no introduce daño.

## Alternativas descartadas

- Mantener `relocate` y subir `blockChancePercent` sólo para el portador: cambia una constante global de balance por un
  perk y no se lee como un acto.
- Resolver la tirada al llegar el balón a la línea (como el portero): el perk quedaría armado varios ticks y su cartel
  saldría antes que el suceso.
- Exigir alcance máximo a la trayectoria: añade dos funciones de condición nuevas para limitar un corte que el diseño
  acepta; si el balance lo pide, va como condición en el dato.

## Medido

Real, 750 partidos con un defensa distinto llevando el perk: 456 activaciones (0,61 por partido; el tope es 1) y 327
paradas = 72 % con un 75 % de probabilidad (`GuardShotTests`). Antes/después de goles encajados y valor en la tabla:
ficha [BC-D](../pendientes/BC-D.md).

## Enmienda (30 sep 2026): sólo el último hombre de verdad, y una probabilidad dentro de la banda de un raro

**Decisión del revisor** tras ver que el 75 % medía **+124,7 ± 7,7** (tabla de la ADR 0087, semillas 5/11/17), el segundo
valor más alto de un raro. RT-057 (nunca un ajuste silencioso) y Regla H (la cifra dice de dónde sale).

1. **Condición `teammatesGoalside(owner) == 0`.** Nueva función NCalc genérica, `teammatesGoalside(who)` (tabla de
   [modelo-datos](../modelo-datos.md)): compañeros de campo de `who` —sin el portero y sin él— **entre el balón y su
   propia portería**, por proyección sobre X. Se buscó antes por el concepto (`tools/existe-ya.sh`): sólo existía esa
   misma lectura, «entre el balón y la portería» por proyección sobre X, dentro de `CoverSpace`
   (`coverBetweenBallAndGoalBonus`), sin ser una función de condición. Limitación conocida: la proyección sobre X no
   mira la banda, así que un compañero ancho en la misma columna cuenta como «entre». Efecto sobre la frecuencia,
   medido con la condición y el 75 %: 387 activaciones en 750 partidos frente a 420 sin ella (**LIKELY** que casi todos
   los tiros a puerta los recibe un defensa sin nadie más detrás: el filtro es casi un no-op como limitador de
   frecuencia, y vale sobre todo como coherencia del texto).
2. **La probabilidad baja de 75 a 20.** Banda de un raro = **percentil 90 de los otros 27 raros** medidos con el mismo
   instrumento y las mismas semillas (5/11/17): **+43,8** (mediana +5,0; máximo +197,0, `deathless_march`, que es el
   outlier que la propia ficha ya señalaba, y por eso no vale de techo). Barrido del dato:

   | `value` | valor (semillas 5/11/17) |
   |---:|---|
   | 75 (con condición) | +124,7 ± 7,7 |
   | 35 | +64,0 ± 6,9 |
   | 30 | +56,0 ± 6,2 |
   | 25 | +48,3 ± 6,2 |
   | **20** | **+42,7** (con cinco semillas, +45,4 ± 3,7) |

   Elegido el mayor valor redondo cuyo valor medido no pasa de la banda: **20**. Frecuencia real con 20: 387
   activaciones en 750 partidos y 93 paradas (24 %, `GuardShotTests` compara con el `value` del dato, no con un 75
   escrito en el test).
3. **Coste de diseño, dicho.** Un «héroe del momento» al 20 % deja de ser lo que el revisor pidió en su observación
   original; la mecánica (teletransporte, balón suyo, cartel) no cambia. Si quiere un valor más alto, el coste es salir
   de la banda del catálogo (+56 con 30) o pagarlo con otro freno (un uso por partido ya lo es). Decisión suya.

Ficha: [BC-D](../pendientes/BC-D.md).
