# ADR 0101 · El consumible tiene precio de consumible

**Fecha:** 13 de septiembre de 2026 · **Estado:** aceptada · **Paquete:** CAT-B
**Toca:** RF-080..085, RF-084, RF-114, RT-057 · **Sustituye:** el `consumablePrice` plano de la ADR 0044

## El problema, y por qué llevaba meses invisible

La cadena del consumible estaba **entera** en `/Sim`: el mercado lo vende
(`MarketSystem.BuyConsumable` → contador `consumable_owned:<id>`), el estado lo equipa
(`RunEngine.Apply(SetConsumables)`, con el tope de 3 de RF-080, el manual obligatorio de RF-082 y los dos
condicionales de RF-081), el motor lo resuelve (`EffectEngine.ResolveConsumables`) y `MatchResolution` lo
gasta (RF-085). Todo menos **el llamador**: buscar `SetConsumables` en `Game/` y en `Sim/Analysis/` no
devolvía ni una llamada.

Consecuencia doble, anotada como CAT-B:

- En la **build jugable**, el jugador gastaba oro en algo que entraba en el inventario y no podía llevar a
  ningún partido. La cuarta categoría del mercado cobraba y no devolvía nada.
- En la **medición**, `RunPolicy` tampoco los equipaba, así que los cuatro consumibles de RF-084 nunca se
  jugaban y **cualquier cambio en `data/consumables/` era invisible para las 43 puertas**. Así se coló
  CAT-A: el vendaje de campaña llevaba meses escrito sobre el canal `injure` —el del que **entra**— y por
  tanto protegía las piernas del **rival**, y ninguna puerta podía verlo.

El precio lo dice todo. El `_doc` de `data/economy/economy.json` lo escribió sin rodeos:

> *"`consumablePrice` sube a 20: el consumible es de un solo uso y **no lo compra nadie** —el estado no
> lleva inventario de consumibles (X-9)—, así que su precio solo influye en la mitad aspiracional del
> mostrador (`affordableShareAtMarket`)."*

Es decir: el precio no era un precio, era un **decorado calibrado para mover una métrica**. Y con el
tiempo subió a **45**, más caro que un perk raro (42) y al nivel de un objeto raro (45), para algo de un
solo uso. Ninguna política racional lo compraría, que era exactamente la premisa.

## Decisión

**1. El precio sale de la rareza, como el de un perk o un objeto.** `consumablePrice` (plano) se retira y
entra `consumablePriceByRarity`, con la misma forma que `perkPriceByRarity` e `itemPriceByRarity`:

| | común | poco común | raro | legendario |
|---|---|---|---|---|
| perk (permanente, un jugador) | 10 | 15 | 24 | 42 |
| objeto (permanente, un jugador) | 12 | 22 | 45 | 85 |
| **consumible (un partido, equipo entero)** | **10** | **16** | **26** | **45** |

El consumible queda **justo por encima del perk y por debajo del objeto**. Puede parecer caro para algo de
un solo uso, y esa es la intención: alcanza a **los siete del campo a la vez**, no a un jugador, y el
mercado no puede tener un escalón barato que no cueste renunciar a nada. La primera versión medida fue
6-11-18-32 y **se pasó de barata**: ver abajo.

**2. La política automática compra y equipa** (`RunPolicy`). Compra **la última** del ciclo de mercado, por
detrás de perks, objetos y fichajes: así se lleva el oro que ya no compra nada más —el "oro parado" que
AZ-H señala como media estrategia de no comprar— en vez de competir con la build. El tope de inventario es
**2** (`ConsumableStockTarget`), no los 3 que se pueden equipar.

**3. La descripción generada deja de decir "el portador".** Un consumible **no tiene portador**:
`EffectEngine.ResolveConsumables` ignora el campo `target` del efecto y alcanza a todo el equipo propio
sobre el campo. `DescriptionGenerator.DescribeEffects` reescribe el objetivo a `Team` antes de componer el
texto, así que el mercado ya no lee *"el portador multiplica por 2 su resistencia a las entradas"* en algo
que le pasa al equipo entero. Es RT-035 aplicado: el texto tiene que decir lo que hace el motor.

## Lo que el manual **no** mide, y por qué se acepta

RF-082 exige que, si se equipa algo, uno de los consumibles sea **manual**. El manual solo se dispara si
alguien lo pulsa, y en `/Balance` no hay quien pulse: llega con `ManualTick` a −1 y no se activa nunca. De
los dos que equipa la política, entonces, **solo el condicional produce efecto**.

Se acepta a sabiendas y queda escrito porque **sesga la medición hacia abajo**: el coste en oro se paga
entero y el efecto se cobra a medias, así que las cifras de abajo son una **cota inferior** del valor de la
familia. Corregirlo es dar a la política una doctrina de activación —a qué tick pulsa— y eso es una
decisión de diseño propia, no parte de cerrar CAT-B. Abierto como **CAT-C**.

## Medición

1.200 runs × doctrina, semilla 1. La columna «antes» es el estado de la ADR 0100 (nadie compraba ni
equipaba consumibles).

| Métrica | Banda | Antes | 6-11-18-32, tope 3 | **10-16-26-45, tope 2** |
|---|---|---|---|---|
| `runWinRate` | 20-30 | 25,33 | 22,50 | **22,83** |
| `deathsPerRun` | 1,5-3 | 1,85 | 1,89 | **1,83** |
| `sinksAffordablePerAct` | 2-3 | 2,78 | 2,76 | **2,77** |
| `purchasesPerMarket` | 1-2 | 1,20 | 1,52 | **1,32** |
| `leftoverGoldShare` | ≤15 | 8,67 | 7,42 | **8,07** |
| `brokeMarketRunShare` | 10-25 | 12,92 | **5,83 (OUT)** | **11,58** |
| `mastersReached` | ≥2 | 46,2 | 37,25 | **40,83** |
| `affordableShareAtMarket` | 20-35 | 63,4 (OUT) | 70,91 (OUT) | **71,23 (OUT)** |
| `contextualAdvantage` | ≥8 | 4,17 (OUT) | 3,75 (OUT) | **4,00 (OUT)** |

**Por qué se descartó 6-11-18-32.** Barato, el consumible dejaba de ser una renuncia: se comía el oro de la
build (`mastersReached` 46,2 → 37,25) y **borraba la escasez** —`brokeMarketRunShare` caía a 5,83, fuera de
banda por abajo, porque siempre quedaba algo que poder comprar—. Justo la métrica que la ADR 0100 había
metido dentro en las dos semillas por primera vez. Subir el precio y bajar el tope de inventario la
devuelve a 11,58 y recupera cinco puntos de maestros.

**Lo que este paquete cuesta, y se acepta:** `runWinRate` baja **2,5 puntos** (25,33 → 22,83, dentro de
banda) y `mastersReached` **5** (46,2 → 40,8). Es el precio de que la cuarta categoría del mercado exista:
el oro que antes se quedaba quieto o iba a un perk ahora compra un consumible que se gasta. Y recuérdese
que la mitad de ese gasto no rinde nada en la medición por el slot manual, así que el coste real para un
jugador que sí pulsa es menor que el medido.

**Confirmado en la segunda semilla** (1.200 runs × doctrina, semilla 7): `runWinRate` 22,75 ·
`deathsPerRun` 1,84 · `sinksAffordablePerAct` 2,76 · `purchasesPerMarket` 1,32 · `leftoverGoldShare` 8,05 ·
`brokeMarketRunShare` **12,75 (dentro)** · `mastersReached` 39,92 · `affordableShareAtMarket` 71,38 ·
`contextualAdvantage` 3,08. Las dos semillas dicen lo mismo.

**Una puerta se movió, y con qué motivo.** `FullRunGateTests.TheMetricsThatDoNotMeetTheirDesignBandStay...`
—la **valla anti-regresión**, no la banda de diseño— acotaba `affordableShareAtMarket` a 25..70 y ahora mide
71,4. La cota pasa a **25..75**. Queda escrito porque mover una valla es justo lo que RT-057 prohíbe hacer
en silencio: no se mueve porque estorbe, se mueve porque el número que acotaba estaba **falseado** por un
precio que existía para falsearlo.

**Lo que empeora y hay que decir:** `affordableShareAtMarket` sube de 63,4 a 71,2. No es casualidad ni daño
nuevo: **el precio de 45 estaba maquillando esa métrica** haciendo inasequibles 3 de los ~15 artículos del
mostrador, que es literalmente lo que su `_doc` decía que hacía. La métrica llevaba fuera de banda desde
siempre (Z-K, AD-E: se opone a `brokeMarketRunShare`) y ahora está fuera con el mostrador diciendo la
verdad. Su banda (20-35) se fijó cuando el mercado no tenía escalón barato y hay que revisarla; no se toca
aquí, porque cambiar una banda sin medir es lo que RT-057 prohíbe.

## Alternativas descartadas

- **Dejar el precio en 45 y equipar solo lo que caiga.** Cierra la mitad de `/Game` de CAT-B y deja la de
  medición igual de muerta: a 45 la política no compra ninguno y `data/consumables/` sigue siendo invisible
  para las puertas. Es el statu quo con una pantalla nueva.
- **Escalón único barato (precio 5-8 plano).** Un legendario al precio de un común rompe la relación entre
  rareza y precio que sostienen las otras tres categorías, y la medida de 6-11-18-32 ya enseña adónde lleva
  abaratar: se borra la escasez.
- **Que la política pulse el manual.** Corregiría el sesgo de medición, pero es una doctrina nueva (¿a qué
  tick?) y merece su propia decisión. CAT-C.
