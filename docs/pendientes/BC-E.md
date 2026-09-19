# BC-E — Re-simular con la sustitución elegida falla en el 9,4 % de los casos

**Estado:** Resuelta (19 sep 2026) · la elección del jugador es la respuesta a su punto

## Observación

Encontrado al investigar si «importa qué sustituto pongo» (anotación del revisor, 19 sep 2026; ver
[BB-F](./BB-F.md)).

## Medición

400 runs reales, 3.759 partidos, 688 puntos de sustitución del jugador (test temporal en una copia de
`a7a9e9c`): en **65/688 (9,4 %)** rejugar con la elección del jugador lanza `ArgumentException` («la
sustitución … no es legal», `Sim/Engine/MatchEngine.cs:506-512`).

## Causa

**CONFIRMED:** `MatchPlaybacks.Of` (`Sim/Run/View/MatchPlayback.cs:60-73`) parte de `built` **sin las
sustituciones previas del rival**. Si el rival tuvo una baja antes de T, la primera vuelta de
`SubstitutionPoints.ResolveAutomatically` diverge antes de T y el que sale ya no se lesiona en T. Los 41
casos con ≥2 candidatos tenían una sustitución del rival antes de T, y los 41 dejan de fallar pasando al
rival sus sustituciones anteriores a T.

En `/Game`, la excepción salta en `RunController.Substitute` (`Game/Autoload/RunControllerMatch.cs:47-59`)
después de modificar `Decisions` y antes de cerrar la ventana: **LIKELY** que la ventana quede abierta y cada
pulsación vuelva a fallar.

## Arreglo candidato (sin implementar)

Conservar las sustituciones del rival con tick < T al rejugar (sembrarlas en `ResolveAutomatically` o
usar las del `Playback.Setup` anterior), con este caso como test de regresión.

## Resolución (19 sep 2026)

`SubstitutionPoints.ResolveAutomatically` (`Sim/Run/Substitutions.cs`) retira del estado inicial las
sustituciones que trae y las trata como **respuestas**: se aplican cuando la resolución, en orden cronológico,
llega a su punto (mismo equipo, tick y jugador que sale, candidato legal). Sin sustituciones de entrada —todo
`/Balance` y la política automática— el comportamiento es idéntico. Una respuesta que no corresponde a ningún
punto es `ArgumentException` al terminar (contrato de la ADR 0094, RT-032), no un descarte silencioso.

- Con el código anterior existía una **segunda forma** del fallo: sin excepción, la elección del jugador se
  **perdía en silencio** cuando la sustitución del rival se resolvía antes (`WithSubstitution` la descartaba).
- Revisión independiente (4 suplentes por equipo, 150 semillas, flujo de `/Game` simulado): 415 puntos de
  decisión, 0 excepciones (119 con el código anterior), 0 decisiones no aplicadas, 0 diferencias ≤T entre la
  reproducción y la resolución de la run.
- Pruebas en `SubstitutionTests`: `ThePlayerChoiceIsAppliedAfterEarlierRivalSubstitutions` (también por el
  camino de la run) y `AChoiceThatAnswersNoDecisionPointIsAnExplicitError`.
- **Sin verificar en Godot** (la ventana de `/Game` ya no debería quedarse colgada; pendiente de `visual-review`).
  El 9,4 % → 0 % no se ha remedido sobre runs reales.

## Hermanos

[BC-F](./BC-F.md) (mismo camino de re-simulación), [BA-B](./BA-B.md), [BB-F](./BB-F.md).
