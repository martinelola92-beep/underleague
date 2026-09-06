# 0080. Un portero siempre en el mostrador

**Fecha:** 2026-09-07
**Estado:** Aceptada e implementada. **Decisión del revisor**, tras jugar la primera run en la build
exportada de Windows.
**Modifica:** RF-114 (nuevo RF-114l)
**Requisitos:** RF-114, RF-114b, RT-021, RT-022
**Relacionada con:** ADR 0046 (portero de emergencia cuando muere el titular, paquete Z-9/W-9)

## La anotación del revisor

> "Si un portero muere, te debería dejar poner a cualquier otro jugador como portero, y en el mercado
> debería salir siempre un portero (mínimo y máximo)."

La primera mitad ya estaba resuelta desde el paquete Z: si no hay portero titular disponible, el titular
de campo de menor id ocupa el puesto solo para ese partido (`Sim/Run/RunLineup.cs`, `PickGoalkeeper`). La
segunda mitad no lo estaba: la posición de cada fichaje, canterano y mercenario se sorteaba **uniforme**
entre las cuatro posiciones (`GeneratedPlayers.Generate`, `rng.Pick(AllPositions)`), así que la
probabilidad de que un mercado no ofreciera ningún portero entre sus tres fichajes de pago era
`(3/4)³ ≈ 42%`. Con la plantilla inicial reducida a 9 (ADR 0079) y sin ningún suplente de portero, un
mercado sin portero es más caro de resolver que antes.

## Decisión

**Todo nodo de mercado ofrece exactamente un portero: mínimo y máximo uno.** El portero garantizado sale
siempre de los **fichajes de pago** —nunca de un canterano ni de un mercenario—, así que el máximo también
queda fijado en uno sin necesidad de excluir la posición en ningún otro sitio. El resto de fichajes, todos
los canteranos y el mercenario se sortean solo entre las tres posiciones de campo
(`GeneratedPlayers.PickOutfield`).

Nuevo campo `market.goalkeeperOffers` en `data/economy/economy.json`, fijado a 1, validado
`≤ playerOffers` en la carga de economía. La recompensa de partido (`GeneratedPlayers.Reward`) **no
cambia**: sigue sorteando uniforme entre las cuatro posiciones, porque no es la vía que este problema
señala —el jugador no elige cuándo gana una recompensa— y no hay ninguna anotación sobre porteros de
recompensa.

## Por qué el máximo también se fija en uno, y no solo el mínimo

El revisor pidió explícitamente "mínimo y máximo". Sin ese límite superior, un mercado podría ofrecer dos
o tres porteros a la vez por puro sorteo del resto de fichajes, lo cual no ayuda a nadie: la plantilla
solo tiene una casilla de portero y un segundo portero fichado juega de campo o se queda sin usar. Forzar
el portero garantizado a salir siempre de las primeras `goalkeeperOffers` posiciones de pago, y forzar
todo lo demás a ser de campo, resuelve el mínimo y el máximo con el mismo mecanismo.

## Qué falsificaría esta decisión

- **Que el revisor solo quisiera un mínimo,** es decir que un mercado con más de un portero por sorteo no
  le pareciera un problema. En ese caso bastaría con no forzar la posición de los demás fichajes y dejar
  que un canterano o mercenario porteros aparecieran por sorteo además del garantizado.
- **Que la recompensa de partido también debiera garantizar portero.** Hoy no lo hace; si en la práctica
  se sigue perdiendo el portero titular sin que el mercado esté cerca, ampliar la garantía a la recompensa
  es la siguiente palanca disponible.
