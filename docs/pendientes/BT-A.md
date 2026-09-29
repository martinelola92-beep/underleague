# BT-A — La política de `RunPolicy` intenta vender a un fichaje sin experiencia y lanza

**Estado:** Abierta, **CONFIRMED** (29 sep 2026). Encontrada por el censo de [BA-G](./BA-G.md); no es de nombres.

## Síntoma

`RunPolicy.Play` termina con `ArgumentException: 'Ada Nieto' todavía no ha jugado un partido con el club y no se puede
vender (RF-114f, ADR 0108)` (`MarketSystem.Sell`, `:85`, llamado desde `RunPolicy.VisitMarket`, `:2159`).

## Medido

En el árbol de `main` a 29 sep (`24fefc3`), sin tocar nada: **9 de 60 runs** (3 razas × 20 semillas, `SystemsTestSupport.Setup()`
y las opciones de fábrica de la política) lanzan al intentar vender. Con `SellKeepingAvailable = 99` (la política no vende) las 36
runs completan.

## Causa

**CONFIRMED por lectura y por la medida de arriba.** `RunPolicy.NextMarketAction`, rama (e), con la plantilla llena y un fichaje
mejor que ofrecer, vende a `WorstSellable`: el disponible de menor valor que no sea mercenario, canterano ni titular. **No excluye
a quien tiene `Experience <= 0`**, y `MarketSystem.Sell` lo prohíbe desde la ADR 0108 (un fichaje que no ha pasado por un partido no se
vende). Un fichaje de pago del acto 1 entra al nivel 1 con experiencia 0, y si el mercado ofrece otro mejor, la política intenta
venderlo.

## Por qué importa y por qué no se ha visto

Es el **instrumento**, no el juego (Regla J): la política es lo que mide `/Balance`. Si esta rama se ejerciera en los lotes de
`/Balance`, la run lanzaría y el lote se abortaría; que no se haya visto sugiere que con la configuración de `/Balance`
(`Balance --full-runs`) no se da —plantilla más corta, más oro, otras semillas—, **no** que no exista. Sin medir.

## Qué falta

- Excluir a `Experience <= 0` en `WorstSellable`, con un test. **Mueve el balance**: cambia qué vende la política y por tanto
  el oro y la plantilla de las runs que hoy no llegan ahí, así que exige `balance-measure` y no se ha tocado en el paquete de
  BA-G.
- Decidir si una política que lanza es un fallo (probablemente) o algo que `RunPolicy.Play` debería capturar.
