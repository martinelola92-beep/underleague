# ADR 0108 · Un jugador vale lo que has invertido, y lo que queda de él

**Fecha:** 13 de septiembre de 2026 · **Estado:** aceptada · **Decisión del revisor**
**Toca:** RF-114b/c/f, RF-035, RF-104 · **Cierra:** el arbitraje de canteranos

## El agujero, que lo encontró el revisor

El canterano es **gratis** (RF-114b: `BuyPlayer` con `requirePayment: false`) y se podía **vender en el
mismo nodo de mercado** en el que se fichaba. `Sell` paga base por rareza + nivel + perks + vínculos, así
que un canterano común se compraba por 0 y se vendía por 4.

Cada mercado ofrece **1-2 canteranos**. Con tres mercados por acto y tres actos, eso son **hasta 72 de oro
de la nada**. El ingreso de un acto completo es **9, 11 y 13**. El grifo valía **más que toda la economía
de la run**.

## Decisión

**1. Un fichaje que todavía no ha pasado por un partido no se vende.** La experiencia es la señal exacta:
un recién llegado tiene 0 hasta que el equipo juega, incluso si lo hace desde el banquillo. Cierra el
arbitraje sin tocar la economía y sin quitar el canterano de ningún mercado — que era la otra salida y
habría eliminado una de las tres vías de recuperación que la ADR 0037 declara obligatorias para que
arruinarse no sea irreversible.

**2. Lo que valga se cobra según el estado en que lo vendas** (`playerSaleStatePercent`):

| estado | se cobra |
|---|---|
| sano | 100 % |
| tocado | **50 %** |
| lesión grave | **25 %** |
| **muerto** | **0 %** |

Un muerto no vale nada porque ya no sirve para nada. Y con esto **vender la plantilla rota deja de ser una
salida de emergencia gratuita**, que es lo que convierte el desgaste en el recurso central que RF-035 y
RF-104 dicen que es, en vez de en algo que se liquida en el mostrador.

## Lo que la medición dice, y es lo interesante

**Nada. Literalmente nada:** 1.200 runs, y `runWinRate`, `runWinRate_noMarket`, `contextualAdvantage`,
`mastersReached`, `deathsPerRun`, `leftoverGoldShare`, `brokeMarketRunShare` y `purchasesPerMarket` salen
**idénticas hasta el último decimal**.

Porque **la política automática nunca usaba el exploit**. Su regla de venta lo dice desde que se escribió:
vende «solo cuando hay a quién fichar y la plantilla está llena, y **nunca un canterano —es la inversión de
RF-114c, no mercancía**—».

Eso deja dos conclusiones que conviene no perder:

- **Es un arreglo para el jugador humano, no un cambio de balance.** El agujero era real y grande, pero
  vivía en un hueco que ninguna métrica mira.
- **Y por eso lo encontró una persona y no el banco de pruebas.** Las 43 puertas llevan meses en verde
  sobre una economía con un grifo que valía más que un acto entero. Es el argumento más claro que hay a
  favor de que el revisor juegue: hay una clase de defecto que solo se ve desde el mostrador.

43 puertas y 688 tests rápidos en verde. Instrumento: `Sim.Tests/Run/Systems/PlayerSaleTests.cs`.

## Lo que queda anotado

La política automática **sigue sin vender casi nunca**, así que los multiplicadores de estado tampoco se
ejercitan. Es el mismo hueco que CAT-B tenía con los consumibles: una regla calibrada que ninguna medición
toca. Si se quiere balancear la venta con datos, hay que darle a la política una razón para vender.
