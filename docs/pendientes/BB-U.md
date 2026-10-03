# BB-U — Un solo perk común rompe el invariante de cero de la puerta de economía

**Estado:** **CERRADA (3 oct 2026).** Descartada bajo la ADR 0182 (la puerta de cero exacto era frágil; la economía no tiene problema real) y **el tercer perk de `Bulwark` ya está en `/data`** (`immovable`, ver «Cierre del 3 oct» al final). Historia abajo.

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

## Cierre (ADR 0182, 2 oct 2026)

La cola de actos que pagan los cinco sumideros mide 0,07 % (4 de ~5.700 actos, 12 semillas) y la media 2,34 ± 0,03: el
cero exacto se ponía rojo en ~30 % de las muestras sin que pasara nada. La puerta ahora tiene techo del 1,0 %
([ADR 0182](../decisiones/0182-la-cola-de-los-sumideros-tiene-techo.md)). **Esto desbloquea el tercer perk de `Bulwark`**
(ya no puede romper esta puerta por una mariposa), **sin implementarlo**: sigue pendiente de `perk-authoring` y de que
la cola con ese perk se mida bajo el techo. Límite: el 0,196 de entonces (~20 % de los actos) era mucho mayor que la
cola actual; no se ha re-medido aquel perk concreto (LIKELY que era otro efecto de catálogo antiguo).

## Cierre del 3 oct 2026: `immovable` entra

El perk previsto era `immovable` (common, `filler`, `tackleEvasion`, `hasTag(owner,'Bulwark')`; commit `66e979c`). Entra con
la misma forma que `elf_touch` bajo la ADR 0149 (`TACKLE`, `scope: opponent`, el dueño es a quien entran, `limit` 1 por
jugada para que `Modifiers.AddProbability` no lo componga, Regla I) y con **+50 en vez de +100**: 50 es el techo de la escala
5/10/15/20/25/50 de `data/perks/README.md`. El valor como perk es **provisional, sin medir** (protocolo de la ADR 0087 pendiente).
Descripción generada (RT-035): «Cuando le entran, el portador multiplica por 1,5 su resistencia a las entradas. Condición: si el
portador es Muro (enanos). Límite: 1 por jugada.»

Medido con `Balance --full-runs 240` (la muestra de la puerta), con el perk en el catálogo:

| semilla | `actsWithAllSinksAffordable` (techo 1,0) | `sinksAffordablePerAct` (2,0-3,0) |
|---|---|---|
| 1 | 0,21 IN | 2,39 IN |
| 2 | 0,00 IN | 2,39 IN |

La puerta de cola **no se rompe** (CONFIRMED para este perk y estas dos semillas; el 0,196 de 2026-09-19 no se reproduce y es LIKELY que
fuera otro efecto del catálogo de entonces, como ya decía el cierre de la ADR 0182). `brokeMarketRunShare` midió 7,9 y 7,5 con el perk
frente a 13,75 sin él en la semilla 1: sigue dentro de la banda de la puerta (5-25) y es ruido de muestra (7,83-8,50 en BA-H, antes del perk).

Efecto en el juego, `dwarf_good` con `immovable` en el hueco de `crowd_control` contra `human_good`, 4 × 4.000 partidos
(semillas 1-4, pareadas): 65,90 / 63,85 / 69,70 / 74,40 sin él, 66,00 / 64,20 / 69,62 / 74,62 con él, o sea +0,15 ± 0,1 puntos:
inerte para la victoria, como corresponde a un `filler` común. No se cambió ninguna build (son la línea base de las puertas, ADR 0118).

### Revisión independiente (3 oct 2026): activaciones reales y decisiones de forma

**Regla J, el instrumento.** `Sim.Tests/Perks/ImmovableCensusTests` (Diagnostic) juega enanos con estilo Bulwark forzado contra
humanos, 3.000 partidos, mismas semillas con y sin el perk. Control (0 portadores): 0 activaciones, el instrumento no inventa.
Con 1 portador: **1,32 activaciones por partido** (sufre 1,18 entradas) y victoria 64,73 → 64,87 (+0,14, ruido). Con 6 portadores: 12,7 activaciones
y 67,60 (+2,87). Así que el «inerte» de antes era imprecisa: el perk **se dispara de sobra**; con un solo portador (el caso de
`dwarf_good`) no mueve el resultado y con la línea entera sí (LIKELY +2-3 puntos, una sola muestra de 3.000, sin otras semillas).

**Qué ve el jugador.** El pergamino con el nombre del perk sobre el portador cada vez que le entran (PERK_TRIGGERED, pantallas 2D y 3D) y una
entrada que no se resuelve a su favor. No hay cartel nuevo. **Por qué +50 y no +100**: 50 es el techo de la escala 5/10/15/20/25/50;
el +100 era una inmunidad sobre la base. **Si se queda corto** (no se implementa): que la activación proteja al compañero Bulwark más
cercano, o que la entrada evitada devuelva el balón al portador.

**Forma.** `family: "wall"` (es una pieza de La Muralla como `back_to_back` y `bulwark_stance`, así que suma para un maestro de esa línea) y
frecuencia 150 como sus hermanos: con `tagsRequired: ["Bulwark"]` no sale a quien no puede llevarla, y bajarla a 100 la haría rara sin medida
que lo pida. `tagsRequired` hace que el motor rechace asignarla a un no Bulwark (`Simulator` lanza) y que el mercado no la ofrezca sin portador.

**`bulwark_stance` NO recibe `tagsRequired`.** Es hermano y tendría el mismo sentido, pero `elf_brawler` (sin `styles`: elfos sin etiqueta Bulwark; `dwarf_fortress` no se ha comprobado) y varios tests de
medición la asignan a jugadores sin la etiqueta Bulwark; con `tagsRequired` el motor lanzaría y habría que cambiar las líneas base de las
puertas (ADR 0118). Queda como pendiente de decidir con el revisor: o se corrigen esas builds, o se acepta la carta muerta (hoy su `condition`
ya la deja inerte fuera de Bulwark).
