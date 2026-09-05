# 0044. El oro se cuenta en decenas, no en millares

**Fecha:** 2026-09-05
**Estado:** Aceptada (decisión del revisor)
**Requisitos:** RF-114g..k, UI-004
**Complementa:** ADR 0037 (la economía es la dificultad) y ADR 0038 (el precio deriva del valor)

## Decisión

Toda la economía pasa a una escala de **1 a 100**: un partido ganado paga entre 3 y 8, un objeto común cuesta entre 4 y 8, uno raro entre 20 y 30, la clínica entre 10 y 15. El total ganado en una run completa queda del orden de 100.

La escala absoluta del oro **es arbitraria**: lo único que importa es la relación entre lo que ganas y lo que cuestan las cosas. Dividir todo por veinte produce exactamente el mismo juego.

## Por qué no complica el balanceo, sino que lo mejora

**1. El jugador puede hacer la cuenta.** Es el argumento decisivo, y no es cosmético. La ADR 0037 pide que existan momentos de *"tenía que haber ahorrado"*, y eso **exige que el jugador pueda contar**: si los precios son 285 y 460 y llevas 812 de oro, nadie calcula nada y se compra por impulso. Si son 6 y 11 y llevas 17, todo el mundo hace la aritmética mental y la decisión se toma de verdad. **La tensión económica que buscamos depende de que las cifras quepan en la cabeza.**

**2. Una escala corta obliga a que cada precio signifique algo**, igual que la escala corta de valores de perk de la ADR 0035. Con rango 1-100 no caben cien precios distintos, así que dos artículos que cuestan lo mismo **son** equivalentes para el jugador, y si uno debe ser mejor hay que separarlos de verdad.

**3. Menos granularidad, ninguna pérdida real.** Nadie distingue 47 de 49. Redondear un precio derivado del valor a entero pequeño colapsa diferencias que el jugador no percibía de todos modos.

## Lo que hay que hacer además de escalar

**Escalar no es acotar, y el problema medido es el rango, no la magnitud.** El volcado de un mercado real da precios de 99 a 1810: **dieciocho a uno**. Dividir por veinte deja 5 y 90, igual de roto: la mitad del surtido sigue siendo inalcanzable siempre y la otra mitad trivial.

Hay que **acotar el rango dentro de una misma categoría** a algo del orden de 4:1, y dejar que la diferencia grande esté **entre** categorías (un jugador cuesta más que un objeto) y **entre** rarezas, no entre dos objetos comunes.

El mismo volcado enseña un segundo desajuste que no se arregla escalando: un objeto de dos atributos cuesta 104 y un perk poco común 460. **Cuatro veces más caro un perk que un objeto**, lo que hace que nunca compense comprar perks. Esa relación entre categorías es un ajuste aparte.

## Valores de partida

| Concepto | Escala nueva |
|---|---|
| Victoria de liga, por acto | 3 · 5 · 7 |
| Victoria de élite | +50% |
| Victoria de jefe | +100% |
| Objeto común / poco común / raro | 4-8 · 10-16 · 20-30 |
| Perk común / poco común / raro | 6-10 · 12-20 · 25-35 |
| Jugador | 15-40 |
| Clínica | 10-15 |
| Reroll | 2, +1 por uso |
| Oro ganado en una run completa | ~100 |

## El oro inicial es la primera palanca de dificultad por división

El club empieza con **poco oro: el justo para comprar un artículo común en la primera tienda**. Y esa cifra **baja conforme se sube de división** (RF-128), como palanca de dificultad junto al encarecimiento del mercado.

| División | Oro inicial | Qué permite en la primera tienda |
|---|---|---|
| Tercera | 10 | un común con holgura |
| Segunda | 8 | un común justo |
| Primera | 5 | solo lo más barato |
| Mundial | 2 | nada: la primera tienda es un escaparate |

**Es una palanca pequeña en números y grande en ritmo.** Diez de oro sobre los ~100 que se ganan en una run es un 10% del total: por sí sola no decide una partida. Lo que decide es **el tono del arranque** — si tu primera visita al mercado es una compra o una lista de cosas que no puedes pagar. En Mundial, empezar mirando sin tocar prepara al jugador para el resto de la división.

Por eso se combina con las otras palancas de RF-128 (precios más altos, rivales mejores, sin canteranos gratis) en vez de sustituirlas.

**Riesgo a vigilar**: en las divisiones altas, empezar sin oro **y** con precios más caros puede sacar al acto 1 de su función de taller (ADR 0043). La curva del acto 1 debe seguir cumpliéndose en todas las divisiones; si no, el encarecimiento tiene que empezar en el acto 2.

## Consecuencias

- `data/economy/` se reescribe entero con la escala nueva, y la fórmula de precio de la ADR 0038 redondea a entero con mínimo 1.
- Los rangos de escasez de la ADR 0037 no cambian: son porcentajes del surtido, no cifras absolutas.
- La interfaz gana legibilidad: un contador de oro de dos dígitos cabe donde uno de cuatro no (UI-004).
