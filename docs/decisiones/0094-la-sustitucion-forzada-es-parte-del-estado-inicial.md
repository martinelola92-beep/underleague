# 0094. La sustitución forzada es parte del estado inicial del partido

**Fecha:** 2026-09-10
**Estado:** Aceptada e implementada en `/Sim` (`Sim/Model/Substitution.cs`, `Sim/Run/Substitutions.cs`, `Sim/Run/MatchDecisions.cs`, `MatchEngine.ApplySubstitutions`); la ventana de `/Game` en el commit siguiente
**Decisión del revisor** (segunda partida, `pendientes.md` AZ-F: «cuando un jugador se lesiona/muere debe salir una ventana para elegir un sustituto durante el partido; las sustituciones voluntarias no existen en este juego»). **Modifica RF-054** (tercera pausa dramática: la ventana de sustitución) y matiza el §1 de requisitos («toda la decisión ocurre entre partidos»: salvo esta, que es forzada y solo existe si hay banquillo). **No toca RT-013**: `Simulator.Run` sigue siendo una sola entrada pura.
**Requisitos:** RF-005, RF-025, RF-054, RF-059, RF-082, RT-013, RT-024, RT-040, RT-061
**Relacionada:** `docs/arquitectura.md` §«Consumibles manuales durante el partido» (el precedente exacto), ADR 0048 (la muerte en el partido), `docs/plan-segunda-partida.md` tanda 4

## La regla

1. Cuando un jugador **del equipo del jugador** sale del campo por **lesión o muerte** y hay al menos un
   suplente disponible (en la plantilla, no alineado, sano o con lesión leve, y no usado ya en este
   partido), el partido **se detiene en la reproducción** y aparece la ventana de sustitución. Elegir es
   obligatorio: sin banquillo el equipo sigue con uno menos (hoy); con banquillo, entra alguien.
2. **No hay sustituciones voluntarias** ni por expulsión (la roja deja al equipo con uno menos, como en el
   fútbol).
3. El sustituto entra en la **casilla-hogar del que sale**, al principio del tick siguiente al de la salida
   (el evento `SUBSTITUTION` lleva tick T+1), en estado `Positioning`; sus perks se suscriben al entrar (los de `MATCH_START` no disparan: llegó
   tarde al saque; los de `PLAY_START` disparan en la siguiente jugada).
4. El rival también sustituye si tiene banquillo, con la política por defecto (misma posición; si no hay,
   el de mayor calidad). Es lo que haría un entrenador y evita que la regla sea una ventaja unilateral.
5. Experiencia (RF-025): quien entra **jugó** (100 %); quien sale, también.

## La arquitectura: el precedente de RF-082, sin excepciones a RT-013

`docs/arquitectura.md` ya resolvió cómo entra una decisión humana en mitad de un partido determinista: es
**parte del estado inicial**, la reproducción corre por tramos, y al decidir en el tick T se vuelve a
llamar a `Run` con la decisión incluida y se descartan los eventos posteriores a T de la ejecución
anterior. La sustitución usa exactamente ese mecanismo:

- `Substitution(int Tick, int OutPlayerId, int InPlayerId)` en `Sim/Model`; `TeamSetup.Substitutions`
  (propiedad `init`, vacía por defecto, como `Consumables`). `TeamSetup.Players` ya incluye a los
  suplentes.
- El motor valida cada sustitución al construirse (el que entra está en la plantilla y no alineado, no se
  repite, el que sale ha dejado el campo por lesión o muerte en un tick ≤ T; lo demás es
  `ArgumentException`) y crea de antemano al `MatchPlayer` del sustituto con `PlayerState.Benched` y
  `OnPitch = false`. Al **final del tick T** entra (`EnterPitch`), se suscriben sus perks y se emite
  `EventType.Substitution` (`Actor` = el que entra, `Target` = el que sale, detalle `injury` / `death`).
- **Propiedad clave, comprobada por test**: los eventos hasta T incluido son idénticos con y sin la
  sustitución. Es lo que hace legal descartar la cola y seguir desde T.
- `Substitutions.Pending(setup, result, team)` (`/Sim`) devuelve el primer punto de decisión sin resolver:
  tick, jugador que sale y candidatos. Lo usan `/Game` (para abrir la ventana) y `RunEngine` (para el bucle
  automático).
- `RunEngine.EnterMatch(state, nodeId, catalog, systems, decisions)`: `MatchDecisions` reúne las
  activaciones manuales (RF-082, que `BuildMatch` ya aceptaba) y las sustituciones. Si el llamador no trae
  sustituciones para un punto de decisión, `EnterMatch` aplica la política por defecto y vuelve a ejecutar
  (a lo sumo tantas veces como suplentes hay). Así `/Balance`, los tests y las políticas automáticas
  sustituyen sin saber que existe la ventana; `/Game` trae las decisiones del jugador y el rival se
  resuelve solo.
- `/Game` (`MatchScreen`): al llegar la reproducción a un `Injury`/`Death` del equipo 0 con punto de
  decisión pendiente, pausa, muestra la ventana (candidatos con posición y estado), y al elegir vuelve a
  pedir la reproducción con la sustitución añadida y sigue desde T. `RunControllerMatch` guarda las
  decisiones y las pasa a `EnterMatch`, que reproduce el mismo partido (RT-024) y lo aplica al estado.
  Guardado ironman (RT-061): las decisiones viajan con el partido.

## Lo que queda fuera

- Un motor reanudable con puntos de decisión (la idea de `plan-segunda-partida.md` tanda 4): cuesta una
  arquitectura nueva para ahorrar un recálculo de 60-90 s a 15 ticks/s, que es despreciable (RT-051), y
  abre una segunda entrada a `/Sim`.
- Cambios voluntarios, cambios por expulsión, y un límite de sustituciones distinto del tamaño del
  banquillo.
- Un caso de borde conocido: si dos salidas en el mismo tick dejan al equipo por debajo del mínimo
  (RF-002d) el partido termina en ese tick aunque hubiera banquillo, porque el sustituto entra en el
  siguiente. Con siete titulares y dos suplentes exige tres bajas en dos ticks; se anota, no se trata.

## Lo que se mide

`--full-runs 600`, semilla 1, tres políticas, misma física y mismas razas (ADR 0091-0093) en las cuatro filas:

| Variante | `deathsPerRun` | `matchesPerRun` | `runWinRate` spender / saver / noMarket |
|---|---|---|---|
| Sin sustituciones (HEAD `5999a3d`) | 1,81 | 13,1 | 10,8 / 13,5 / 13,2 |
| Sustituciones, los dos equipos, política sano-primero | **2,62** | 13,5 | **6,8 / 13,0 / 10,2** |
| … solo suplentes sanos | 2,48 | 13,6 | 6,3 / 13,2 / 9,3 |
| … el rival no sustituye | 2,80 | 13,8 | **13,8 / 18,2 / 14,5** |

Lo que se sabe y lo que no:

- El motor no trata distinto al suplente: en 300 partidos de referencia (sin perks letales) se lesiona a
  0,021 por mil ticks en el campo contra 0,026 el titular, y nadie muere. El exceso de muertes por run es
  dinámica de run, no de partido: más cuerpos en el campo durante más partidos contra rivales con perks
  letales (ADR 0048: cualquier entrada de un portador puede matar), y el banquillo deja de ser el refugio
  que `RunPolicy` usa contra el rival letal (RF-012d, «se puede reducir el riesgo con la alineación»). La
  política sano-primero recorta poco (2,74 → 2,62; solo sanos 2,48).
- **Que el rival sustituya cuesta 5-7 puntos de `runWinRate`**: con el rival a siete todo el partido el
  jugador gana menos runs; sin sustituciones del rival la tasa sube a 13,8 / 18,2 (por encima de la línea de
  base, porque el propio banquillo del jugador ya cuenta).

Decisión pendiente del revisor (regla de juego, se pregunta en el informe): si el rival sustituye con la
política por defecto (simétrico, más duro: 6,8) o no sustituye (asimétrico, 13,8). El código deja las dos
a un parámetro (`usesPolicy`) y el ADR se cierra con la elegida.
