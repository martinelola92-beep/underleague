# 0096. La liga paga oro, el mercado es donde se construye, y el slot es el recurso escaso

**Fecha:** 2026-09-11
**Estado:** Aceptada e implementada (`data/economy/economy.json`, `Sim/Analysis/RunPolicy.cs`, `Sim/Run/Systems/StandardRunSystems.cs`)
**Aplica la tercera palanca de la ADR 0055** (directriz del revisor: «el mercado tiene que ser gran parte del núcleo de la build»). **Reinterpreta el escalón de la ADR 0043** —sigue estando, pero en valor total y no en moneda— y **recalibra la doctrina contextual de la ADR 0037**.
**Requisitos:** RF-071, RF-071b, RF-094, RF-114, RF-114e, RF-114k, RT-055, RT-057
**Relacionada:** ADR 0037 (la economía es la dificultad), ADR 0043 (el trampolín), ADR 0046, ADR 0051 (los maestros solo se compran), ADR 0055, ADR 0072/0086 (el listón del slot), ADR 0095 (la run entra en banda)

## El problema (AZ-H)

Cerrada la ADR 0095, la run se ganaba el 22,2 % de las veces **y el 18,3 % esquivando todos los mercados**.
El mercado no era el núcleo de la build: era un extra. Desglosado con 4.800 runs:

| | contextual | sin mercado |
|---|---|---|
| perks del once al final | 9,65 | 8,03 |
| objetos | 2,08 | 0,58 |
| maestros comprados (`mastersReached`) | 31 % | 0 % por definición |
| tasa de victoria | 22,4 | 18,3 |

La causa es la que la ADR 0055 ya había escrito: **las recompensas gratis cubren casi toda la build**. Sus
dos primeras palancas ya estaban aplicadas (los maestros solo se venden, ADR 0051; los objetos no salen en
recompensa, `rewardItemWeight` 0) y aun así el que no compra se lleva el 83 % de los perks del que compra.
Quedaba la tercera: **«que algunos partidos paguen solo oro»**.

## Decisión

1. **La liga paga oro en vez de elección** (`nodeRewards.league`): `picks` 1 → **0**, `goldBonusPercent`
   0 → **90**. El élite y el jefe conservan sus elecciones (1 y 2) y su curación: el trampolín de la ADR
   0043 no se toca, y la liga —el nodo más común— pasa a financiar el mercado en vez de sustituirlo.
   Un nodo que no da ninguna elección **ya no se queda abierto** (`StandardRunSystems.AfterMatch`), para
   que la run no pase por una pantalla de recompensa vacía.
2. **La doctrina contextual no gasta un slot en un común mientras le queden mercados en el acto**
   (`RunPolicy.ClearsTheBar`). Con la liga pagando oro, el recurso escaso dejó de ser el oro y pasó a ser el
   **slot**: catorce en el once, y uno ocupado por un común ya no se puede dar a un maestro. La ahorradora
   tenía media regla desde siempre —no compra un común **nunca**— y por eso le ganaba a la contextual (24,0
   contra 23,0 en la primera medición). La contextual aprende la otra media: **en el último mercado del acto
   sí lo compra**, porque guardar el slot para un surtido que ya no va a ver no vale nada. Esa asimetría es
   lo que sigue separando a las dos doctrinas; darle la regla entera las igualaba y tumbaba la puerta
   `TheThreeDoctrinesBuyDifferently` (contextual 1,01 por mercado contra 1,03 de la ahorradora). La
   gastadora sigue comprando cualquier cosa, que es lo que la define.
3. **Los sumideros suben para que el oro nuevo no lo pague todo** (RF-114k): clínica 10 → **14**,
   inscripción [14, 28] → **[20, 38]**, reroll base 1 → **3** y paso 2 → **3**. Y **lo que la build no
   necesita se encarece**, para que el surtido deje de estar todo al alcance: jugadores
   [18, 27, 38, 64] → **[40, 60, 85, 140]**, consumible 20 → **45**, objetos [8, 14, 28, 52] →
   **[12, 22, 45, 85]**. El **precio de los perks no se toca**: es lo que la build compra.

## Lo que se mide

Cada variante con 600 runs y semilla 1; el paquete elegido, con **1.200 runs en las semillas 1 y 7**. La
columna «antes» es la medición de cierre de la ADR 0095 (4.800 runs).

| Métrica | Antes | Después (1 / 7) | Banda |
|---|---|---|---|
| `runWinRate` | 22,4 | **23,33 / 22,00** | 20-30 |
| `runWinRate_noMarket` | **18,3** | **10,67 / 10,75** | ≤ 5 (sigue fuera) |
| `mastersReached` | 31 % | **46,2 / 44,3 %** | 2-90 |
| `brokeMarketRunShare` | 50,0 | **38,3 / 35,7** | 10-25 (sigue fuera) |
| `affordableShareAtMarket` | 67,8 | **63,5 / 64,0** | 20-35 (sigue fuera; cota de no regresión 70) |
| `sinksAffordablePerAct` | 2,56 | 2,74 / 2,76 | 2-3 |
| `purchasesPerMarket` | 1,07 | 1,10 / 1,09 | 1-2 |
| `leftoverGoldShare` | 11,6 | 12,08 / 11,99 | ≤ 15 |
| `deathsPerRun` | 1,94 | 1,80 / 1,82 | 1,5-3 |
| `defeatShare_notEnoughPlayers` | 3,6 | 3,91 / 5,02 | ≤ 35 |
| `contextualAdvantage` | 4,75 | 5,42 / 4,25 | ≥ 8 (sigue fuera) |
| doctrinas: gastadora / ahorradora | 12,9 / 17,7 | 11,4 / 17,9 · 11,3 / 17,8 | INFO |

Las 43 puertas (`Category=Gate`) en verde, incluida `TheThreeDoctrinesBuyDifferently`.

**Ganar sin comprar cae casi a la mitad** y el maestro —el objetivo de una línea, ADR 0051— pasa de ser
cosa de un tercio de las runs a casi la mitad, sin que ninguna banda que estaba dentro se salga.

## Lo que se descartó, con número

| Variante | `runWinRate` | `noMarket` | por qué se descarta |
|---|---|---|---|
| Liga solo con comunes (`commonCeilingPercent` 100) | 15,2 | 10,2 | castiga igual al que compra y al que no: la run se vacía |
| Liga sin elección y **sin** oro | 19,2 | 10,3 | la run se cae de la banda: el pick perdido hay que pagarlo |
| Liga sin elección + oro del acto +55 % | 24,7 | 20,0 | el oro sube a todos: `noMarket` **empeora** |
| Élite también sin elección | 18,2 | 9,5 | −4 puntos de run por 0,5 de `noMarket` |
| Subir todos los precios ×1,7 | 18,2 | 9,0 | el maestro deja de ser comprable (`mastersReached` 28) |
| Listón de compra más alto (`--min-perk-value-market` 80 / 140) | 18,2 / 12,5 | — | el listón no era el problema: a 140 no se compra ningún maestro |

## Lo que queda abierto

> **Corregido por la ADR 0098 (mismo día).** Esta sección afirmaba que la ADR 0033 y la ADR 0055 se
> contradicen —las celdas de la fila «buena» multiplicadas dan 25,6 % y la banda pide ≤ 5 %—. **Era un error
> de categoría**: la fila «buena» mide una build **completa** de catorce perks que no tiene maestros, y la run
> sin mercado llega con 2,81. «Sin maestros» no es «sin mercado». Además el control **compraba** cuando el
> mapa lo metía en un mercado, así que el 10,0 de aquí era una mezcla de dos poblaciones. Con el instrumento
> arreglado la cifra es **7,83** y a la banda le faltan 2,8 puntos, no veinte. El desglose y la palanca que
> queda (no comprar es, en parte, una estrategia: menos muertes y más veteranía) están en la ADR 0098.

Quitando **todas** las elecciones ordinarias (liga **y** élite) el control sigue ganando el 9,5 %: lo que le
queda son los **tres nodos de jefe**, que dan dos elecciones cada uno (ADR 0043). Esa parte se mantiene: el
trampolín por sí solo sostiene un esqueleto de build.

`contextualAdvantage` ≥ 8 tampoco se ha cumplido nunca (histórico −2,1 a +5,6): la ahorradora es una
estrategia fuerte de verdad, y el paquete acerca a las dos doctrinas en vez de separarlas. Con la regla del
slot compartida, lo que queda de ventaja contextual es **qué** se compra, no cuánto se guarda.

## Consecuencias

- El escalón de la ADR 0043 se lee ahora en **valor total**: en moneda la liga paga más que el élite,
  porque ha cambiado su elección por oro. Los tres tests que leían el escalón en moneda lo dicen así.
- La clínica más cara (14) aprieta la otra mitad de la run: el oro se disputa entre curar y construir, que
  es lo que hace que la doctrina importe (ADR 0037). `deathsPerRun` no se mueve (1,82).
- `/Game`: un partido de liga ya no abre la pantalla de recompensa. La de élite y jefe no cambian.
