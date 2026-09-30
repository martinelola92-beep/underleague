# ADR 0180 — Una acción extra va después de la original, y el rechace tiene su propio disparador

Fecha: 30 sep 2026 · Estado: **aceptada** (autónoma, dentro de la autorización del revisor para mover rangos)
Cierra [BM-B](../pendientes/BM-B.md) y [BC-C](../pendientes/BC-C.md). Requisitos: RF-065, RF-066, RF-069, RT-014, RT-041, RT-042.

## Por qué

`TACKLE` y `SHOT` se publican **antes** de tirarse los dados (§3), y `extraAction` repetía la acción **dentro** de esa
publicación. La repetición pasaba, por tanto, *antes* que la original:

- **Entrada (`charge`, `bull_rush`).** La entrada «segunda» era la primera que se tiraba. Si fallaba, tumbaba al que
  entra, y la original se seguía tirando con él en el suelo. *Medido (CONFIRMED, traza de 300 partidos):* 7 de 144
  activaciones de `bull_rush` acababan el tick con el jugador **derribado y con el balón**.
- **Tiro (`double_shot`, `point_blank`).** `LaunchShot` exterior sobrescribía el vuelo del interior y decidía su propia
  tirada: el segundo tiro no cambiaba ningún resultado (BC-C: 1,191 goles por partido con el perk frente a 1,156 sin
  él, dentro del ruido). Y su texto («si el primero sale mal») describía algo que no existía.

## Decisión

1. **Regla general.** Un perk que reacciona al **resultado** de una resolución se cuelga del evento del resultado,
   publicado cuando el mundo ya lo refleja; no actúa dentro de la publicación previa. La publicación previa queda para
   lo que **cambia los datos de la propia tirada** (probabilidades, atributos) o la situación de los participantes
   antes de tirar (`setState` sobre el portador: Muro, Duelista, Ancla del tercio).
2. **`extraAction` sobre `TACKLE` se arma y se ejecuta al terminar la entrada** (`MatchPlayer.RepeatTacklePending`,
   `MatchEngine.FinishRepeatedTackle`). No hay repetición tras una falta (el juego se paró) ni si quien entra ya no
   está en pie. La entrada original **fallada sin falta no tumba todavía** a quien lleva una repetición armada («no
   frena»): la caída la decide la repetición. La repetición va contra el **mismo portador** si sigue en pie, con el
   balón y a su alcance («vuelve a por ella»), y si no contra el rival **en pie** más cercano (el comentario ya decía
   «en pie» y el código no lo comprobaba). No encadena otra.
3. **Un derribado no lleva el balón.** La entrada fallada de quien acaba de ganarlo (repetición de `steamroller`,
   `charge`) y la falta no vista sueltan el balón al caer, como ya hacía `KnockDown`.
4. **Un participante que sale del campo en la publicación previa anula la resolución** (entrada, bloqueo, regate):
   `ankle_bite` lesionaba a la víctima y la entrada se seguía tirando contra ella (segunda lesión sobre el mismo
   cuerpo, «robo» de un balón ya suelto y falta que reanudaba en (-1,-1): 5 de 61 lesiones provocadas).
5. **Casos que se dejan como están, documentados y con test** (`PreResolutionParticipantsTests`): `setState` sobre el
   portador en `TACKLE` (`duelist`, `own_third_anchor`) —el derribo le suelta el balón y la entrada se juega contra el
   balón suelto; el final de tick es coherente— y `setState` sobre el defensor en `DRIBBLE_ATTEMPTED` (`nutmeg`) —si el
   regate se pierde, el defensor recupera el balón y `Decide` lo levanta—.
6. **Nuevo disparador `SHOT_REBOUND`** (`EventType.ShotRebound`, al final del enum): el tiro terminó y el balón queda
   **suelto** (`detail` = `blocked` | `parried` | `post`; `actor` = el tirador, `opponent` = quien lo paró, si lo hay).
   Se **publica sin registrarse** en el flujo de eventos (como la publicación previa de `TACKLE`): el log ya tiene el
   `SHOT_BLOCKED`, el `SAVE parried` o el `SHOT_POST`. No se publica en penaltis. `extraAction` deja de admitir `SHOT` y
   admite `SHOT_REBOUND`: quien tiró recupera el balón y remata otra vez, sólo si sigue libre. Se exige `scope: actor`.

## Alternativas descartadas

- Armar el segundo tiro en `SHOT` y ejecutarlo en el rebote: gasta el uso en el primer tiro (casi siempre gol, fuera o
  atrapada) y el cartel del perk sale cuando aún no ha pasado nada.
- Diferir también `injure`: cambia la fantasía de `ankle_bite` («va a por la pierna, no por el balón»); se resuelve con la
  regla 4.
- Un disparador por cada final (`SHOT_BLOCKED`, `SAVE`, `SHOT_POST`): tres perks o un campo `triggers[]` para lo mismo.

## Medido

Frecuencia de rechaces (300 partidos, `TestMatches.Reference`): 1,39 por partido entre los dos equipos (bloqueos 0,11,
paradas rechazadas 1,14, palos 0,13). El tirador está a ≤ 3 casillas del balón en el 77 % de los rechaces y a ≤ 4 en el
93 %, así que el salto del balón al recuperarlo es del tamaño de lo que un disparo recorre en un tick. Valor de los
perks antes/después: ver las fichas.
