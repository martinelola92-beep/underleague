# BC-E — Re-simular con la sustitución elegida falla en el 9,4 % de los casos

**Estado:** Abierta · causa CONFIRMED en `/Sim` · efecto en `/Game` LIKELY (no se ejecutó Godot)

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

## Hermanos

[BC-F](./BC-F.md) (mismo camino de re-simulación), [BA-B](./BA-B.md), [BB-F](./BB-F.md).
