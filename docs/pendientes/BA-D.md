# BA-D — Los jugadores se teletransportan al reanudar una falta.

**Estado:** CERRADA

## Observación

**Los jugadores se teletransportan al reanudar una falta.** Propuesta del revisor: esperar unos ticks antes de reanudar para que se recoloquen andando

## Análisis / estado actual

**CERRADA (14 sep 2026).** El diagnóstico de la fila era el equivocado: los demás jugadores **ya** caminaban —`Step` les llama a `UpdatePlayer` con normalidad durante el balón muerto—. Quien saltaba era **el sacador**, y lo hacía por el propio arreglo de AZ-A: `BeginRestart` le ponía `Position = point`, así que aparecía sobre la falta y se quedaba inmóvil. Ahora se queda donde está y **camina** hasta el punto a su velocidad normal (`WalkRestartTaker`), sin correr la IA —sigue sin decidir ni perseguir—, con recolocación de seguridad en el tick de la reanudación si no ha llegado: el salto residual es lo que le quedara por andar, no la distancia entera. **El saque de centro queda exceptuado a propósito**: ahí `ResetPositions` devuelve a los catorce a su casilla-hogar, así que el equipo entero se recoloca por diseño y poner al sacador sobre el balón es parte de esa misma reforma. `MatchRulesTests.TheRestartTakerStandsStillDuringTheDeadBall` pasa a afirmar el **requisito** de AZ-A en vez de su implementación: cada paso es un paso y no un salto, y la distancia al punto **nunca crece**. Es más fuerte que el «no se mueve» anterior, que habría dejado pasar a un sacador alejándose despacio. Queda abierta la otra mitad de la queja, la cortinilla: **BA-K**

## Hermanos

Mismo síntoma, causas relacionadas: [BA-K](./BA-K.md).
