# BB-U — Un solo perk común rompe el invariante de cero de la puerta de economía

**Estado:** Abierta, sin causa. El perk que lo destapó se ha retirado para no dejar la puerta roja; el
hallazgo es lo que importa.

## Observación

`FullRunGateTests.TheGoldOfAnActPaysTwoOrThreeSinksAndNeverAllOfThem` exige
`actsWithAllSinksAffordable == 0` **exactamente** (RF-114k: el oro de un acto paga dos o tres sumideros,
nunca los cuatro).

Al añadir **un** perk `common` al catálogo (`immovable`, `hasTag(owner,'Bulwark')` →
`modifyProbability tackleEvasion +100`, más su hueco en `dwarf_good` sustituyendo a `crowd_control`), la
métrica pasa de **0 a 0,196** — es decir, en ~20 % de los actos el jugador puede pagarlo todo.

**Verificado que es esa la causa**: retirando el perk y su cambio de build, la puerta vuelve a pasar; con
ellos, falla. Las tres métricas de `BuildGateTests` (CAT-J) se quedan **idénticas** en los dos casos
(48,96 / 45,83 / 1,26), así que no es un movimiento general del balance.

## Por qué sorprende

1. El efecto del perk (`tackleEvasion`) **no toca la economía** por ningún camino evidente.
2. La puerta **no es de una sola semilla**: agrega 180 runs (`Seed + i`, tres doctrinas), así que no es el
   tipo de ruido que la ADR 0118 arregló en la puerta de equipamiento.
3. Hoy mismo se añadieron **doce** perks al catálogo (94 → 102) y esta puerta **siguió en verde**. Lo
   rompió el número trece. Eso huele a umbral, no a proporcionalidad: la economía estaría justo al borde
   de que el oro de un acto alcance para los cuatro sumideros, y un perk más en el mercado cambia lo que
   la política compra lo suficiente para cruzarlo.

## Lo que hay que averiguar antes de tocar nada

- ¿Es un borde real o el invariante de **cero absoluto** es demasiado frágil? `Assert.Equal(0.0, ...)`
  sobre una fracción medida en 180 runs se rompe con que UN acto cruce. Conviene saber cuál era el margen
  antes: si la métrica venía siendo 0 con holgura o si ya rozaba.
- ¿Depende del perk concreto o de que el catálogo tenga un perk más? Probar con otro perk `common` distinto
  lo separa en una ejecución.
- Si el catálogo va a seguir creciendo —y la tabla de raza/rasgo lo pide—, este invariante va a volver a
  saltar. Mejor entenderlo ahora que cada vez.

## Consecuencia inmediata

**`Bulwark` se queda en 2 perks**, no en los 3 que pide el objetivo de raza/rasgo. Es el único hueco que
queda de ese cuadre (razas 2/2, y `Brute`, `Fine`, `Cold` y `Neutral` a 3). Cerrarlo exige entender esto
primero.

## Hermanos

- `docs/pendientes/CAT-J.md` — las otras tres puertas rojas, que NO se movieron con este cambio.
- ADR 0118 / `BB-T` — el caso en que una puerta roja SÍ era ruido; aquí está descartado.
