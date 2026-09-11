# 0097. El hueco de plantilla se compra en el mercado, y la inscripción deja de ser un nodo

**Fecha:** 2026-09-11
**Estado:** Aceptada e implementada (`Sim/Run/Map/NodeKind.cs`, `MapGenerator`, `EnrollmentSystem`, `StandardRunSystems`, `RunPolicy`, `MarketView`, `/Game`)
**Decisión del revisor**: «quitaría el nodo especial del slot y lo metería como algo a comprar en el mercado; así simplificamos».
**Modifica RF-011** (la inscripción sale de la lista de tipos de nodo) **y RF-020** (el hueco se paga en el mercado, no en un nodo propio). **Retira el nodo de la ADR 0046**, cuyo resto —coste creciente, techo de 12, descarte— sigue en pie. **Sube el esquema de guardado a la versión 2**: `NodeKind` se serializa por nombre.
**Requisitos:** RF-011, RF-020, RF-071, RF-114, RF-114k, RT-030, RT-055, RT-057
**Relacionada:** ADR 0037 (la economía es la dificultad), ADR 0046, ADR 0053 (el mapa de cuatro carriles), ADR 0096 (la liga paga oro)

## Qué se quita y qué se pone

1. **`NodeKind.Enrollment` desaparece.** El generador ya no lo coloca (ni en el sorteo de servicios ni
   forzado en la última capa) y el mapa queda con tres servicios: clínica, entrenamiento y evento.
2. **`ExpandRoster` se resuelve en el mercado**: `EnrollmentSystem.Expand` exige ahora un nodo de mercado
   abierto. El precio sigue siendo `economy.enrollmentCosts` (20 y 38 desde la ADR 0096), creciente, y el
   techo sigue siendo el de RF-020. `MarketScreenView` publica `RosterSlotPrice` y la pantalla de mercado
   tiene un botón «Ampliar plantilla»; la de nodo pierde su bloque de inscripción.
3. **Se cae la reserva de oro que existía solo por el nodo.** `SpendableAtMarket` apartaba el precio del
   primer hueco porque el mercado iba antes en el acto y, si no, «el nodo de inscripción es decorado». Con
   el hueco en el mismo mostrador no hay nada que apartar, y la política lo compra **después de la foto de
   llegada** (lo que la ADR 0037 mide es lo que el jugador ve al entrar, no lo que le queda tras gastar).

## Las dos reglas que hubo que escribir, porque el mapa ya no las imponía

El nodo hacía de filtro sin decirlo: para comprar un hueco había que **desviarse**. Al ponerlo en el
mostrador, la política pasó a comprarlo siempre que la plantilla estuviera llena y el oro llegase, y el
efecto se vio en el lote:

| | huecos por run | `brokeMarketRunShare` |
|---|---|---|
| Con el nodo (ADR 0096) | 0,55 | 38,3 |
| Sin nodo, sin reglas | 1,25 | 72,8 |
| **Con las dos reglas** | **0,69** | **10,8** |

1. **El hueco se compra solo si después queda oro para un perk raro.** Es lo que la ADR 0046 ya decía con
   palabras —«a esas alturas un perk raro vale más que el duodécimo cuerpo»— y que antes imponía la ruta.
2. **Ninguna doctrina salvo la gastadora se gasta la última moneda mientras le queden mercados en el acto**:
   se aparta el precio del perk más barato del mostrador. Llegar a un mercado sin poder pagar nada es el
   peor resultado de la ADR 0037, y sin esta línea pasaba del 38 % al 73 % de las runs. La gastadora sigue
   vaciando la cartera, que es lo que la define.

## Lo que se mide (1.200 runs, semillas 1 y 7)

| Métrica | ADR 0096 | Ahora (1 / 7) | Banda |
|---|---|---|---|
| `runWinRate` | 23,33 / 22,00 | **25,25 / 25,33** | 20-30 |
| `brokeMarketRunShare` | 38,3 / 35,7 | **10,75 / 9,50** | 10-25 (en su puerta) |
| `affordableShareAtMarket` | 63,5 / 64,0 | 63,68 / 64,19 | 20-35 (sigue fuera) |
| `leftoverGoldShare` | 12,08 / 11,99 | **8,87 / 8,75** | ≤ 15 |
| `contextualAdvantage` | 5,42 / 4,25 | 5,67 / 5,92 | ≥ 8 (sigue fuera) |
| `runWinRate_noMarket` | 10,67 / 10,75 | 11,75 / 11,08 | ≤ 5 (sigue fuera) |
| `mastersReached` | 46,2 / 44,3 | 45,92 / 45,42 | 2-90 |
| `sinksAffordablePerAct` | 2,74 / 2,76 | 2,80 / 2,81 | 2-3 |
| `purchasesPerMarket` | 1,10 / 1,09 | 1,20 / 1,22 | 1-2 |
| `deathsPerRun` | 1,80 / 1,82 | 1,89 / 1,87 | 1,5-3 |
| `rosterSlotsBoughtPerRun` | 0,55 | 0,69 / 0,71 | INFO |

Las 43 puertas en verde. `brokeMarketRunShare` **cae de 38 a 10** y con ello toca por primera vez el suelo
de su banda de diseño (10-25); su cota de no regresión pasa de 20-88 a **5-25**, redibujada alrededor de lo
nuevo como manda su propio criterio.

## Consecuencias

- **Un tipo de nodo menos** en el mapa, una pantalla menos que mantener y una regla menos que explicar: el
  jugador ya no tiene que aprender qué es «inscripción», solo que la plantilla se amplía donde se compra
  todo lo demás. Es la simplificación que el revisor pidió.
- Los mapas tienen ahora **tres servicios** en vez de cuatro, así que dos capas de servicio pueden repetir
  tipo. No se toca `nodesPerAct` (11/12/12): lo que hace el mercado por dos, lo hacía antes por uno.
- **El guardado sube a la versión 2** (RT-030). Una partida guardada con la versión 1 no carga y lo dice con
  su mensaje: el nodo que contiene ya no existe.
- `goldSpentEnrollmentPerRun` y `sinksAffordablePerAct` siguen contando la inscripción como sumidero
  (RF-114k): sigue siéndolo, solo que ahora se paga en el mostrador.
