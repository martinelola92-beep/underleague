# 0100. El nodo de evento es una carta con opciones, no una tirada

**Fecha:** 2026-09-12
**Estado:** Aceptada e implementada (`data/events/`, `Sim/Run/Systems/Events/`, `RunPolicy`, `/Game` pantalla de nodo)
**Decisión del revisor**: «habría que ajustar/crear nodos de evento», con el alcance elegido: la estructura más las dos primeras familias.
**Desarrolla RF-011** (el nodo de evento pasa a tener mecánica propia) y **RF-114j** (los eventos siguen siendo una fuente de oro, ahora con signo). No toca RF-012d: la refuerza.
**Requisitos:** RF-011, RF-012d, RF-114j, RT-021, RT-024, RT-031, RT-035, RT-057
**Relacionada:** ADR 0037 (la economía es la dificultad), ADR 0043 (el trampolín), ADR 0053 (el mapa), ADR 0098 (el atesoramiento paga), ADR 0099 (la clínica, medida con este paquete)

## El problema

El mapa tiene **cinco nodos de evento por acto** (≈15 por run; el jugador pisa uno o dos). Cada uno pagaba
**1-3 de oro** cuando un partido de liga paga veinte: la política solo los elegía yendo pobre. Eran relleno
de capa, y el 15 % de la superficie del mapa no hacía nada.

## La forma

Un evento es una **carta con opciones** definida en `data/events/*.json` (RT-031: datos, no código), con su
esquema y su validación. Cada opción declara sus efectos; **una opción es siempre seguir camino**. Reglas:

1. **Todo coste se ve antes de elegir** (RF-012d). Un evento no quita nada por sorpresa: lo ofrece. La línea
   de efecto **se compone** de las plantillas de `data/l10n/*/templates.json` §`eventEffects` (RT-035: no hay
   texto de efecto escrito a mano).
2. **No hay tiradas dentro de una opción.** La apuesta es la elección, no el dado. Lo único aleatorio es qué
   carta sale, y se **deriva** del nodo (`RngStreams.Rewards`), no se guarda: dos lecturas del mismo estado
   ven la misma carta (W-12), y la interfaz puede enseñarla sin resolver nada.
3. **Quien pone el cuerpo lo elige el jugador**: una opción con `needsTarget` no se puede resolver sin
   señalar a un disponible.

Seis efectos, los justos para las dos familias: `gold`, `goldShare`, `heal`, `experience`,
`experienceTarget`, `injure`.

## Las dos familias, y de qué problema medido sale cada una

**1. El oro parado** (`guild_tithe`, `field_school`, `smugglers_cart`). Cobran un **porcentaje de lo que
llevas encima**, no una cantidad fija, así que quien atesora paga caro y quien va justo casi no paga. Sale
directamente de la ADR 0098: no comprar seguía ganando el 7,8 % de las runs porque el oro parado se convertía
en salud y veteranía, y esta familia es la que le pone precio a eso.

**2. Carne por ventaja** (`blood_pit`, `blood_oath`, `iron_vigil`). Piden un cuerpo —una lesión leve o una
grave, en quien tú señales— a cambio de experiencia para el once o de oro. Es la identidad del juego dicha
en un menú, y el coste es visible y elegido, que es lo que lo separa de un accidente.

## La política automática

`RunPolicy` tasa cada opción **en oro** —lo que da, lo que cuesta y lo que se ahorra en la clínica, esto
último contando solo lo que de verdad habría pagado— y se queda con la mejor; seguir camino vale cero, así
que una carta que no ofrece nada se deja pasar. Para las opciones que piden un cuerpo elige al **disponible
más barato que no sea titular**: la familia de la carne se paga con el banquillo antes que con el once. Dos
constantes nuevas y explícitas: `EventGoldPerHundredExperience` (6) y `EventInjuryPremium` (10).

La política **no mira la carta antes de entrar**, aunque podría (es derivable): el jugador tampoco puede, y
una política de referencia que juegue con información que la interfaz no da mide otro juego.

## Lo que se mide

Filas nuevas: `eventsTakenPerRun` y `eventsDeclinedPerRun`. Si «tomadas» saliera cero, el catálogo no
competiría con el resto del mapa y el nodo seguiría siendo relleno.

**1.200 runs × 2 semillas, con la ADR 0099 dentro** (la columna «antes» es el estado de la ADR 0098):

| Métrica | Antes (1 / 7) | Ahora (1 / 7) | Banda |
|---|---|---|---|
| `runWinRate` | 25,25 / 25,33 | **25,33 / 27,00** | 20-30 |
| `deathsPerRun` | 1,80 / 1,82 | 1,85 / 1,85 | 1,5-3 |
| `brokeMarketRunShare` | 10,75 / 9,50 | **12,92 / 11,33** | 10-25 (**dentro en las dos semillas** por primera vez) |
| `leftoverGoldShare` | 8,87 / 8,75 | 8,67 / 8,78 | ≤ 15 |
| `sinksAffordablePerAct` | 2,74 / 2,76 | 2,78 / 2,79 | 2-3 |
| `purchasesPerMarket` | 1,10 / 1,09 | 1,20 / 1,22 | 1-2 |
| `affordableShareAtMarket` | 63,5 / 64,0 | 63,4 / 63,9 | 20-35 (sigue fuera) |
| `contextualAdvantage` | 5,67 / 5,92 | 4,17 / 8,00 | ≥ 8 (sigue fuera) |
| `runWinRate_noMarket` | 7,83 / 7,17 | 6,58 / 8,08 | ≤ 5 (sigue fuera) |
| `eventsTakenPerRun` | — | **0,38 / 0,40** | INFO |
| `eventsDeclinedPerRun` | — | 0,12 / 0,12 | INFO |
| `squadTreatmentsPerRun` (ADR 0099) | — | 0,38 / 0,39 | INFO |
| `riskyTreatmentsPerRun` (ADR 0099) | — | 0,03 / 0,02 | INFO |

Las cartas **se toman**: de cada tres nodos de evento que la política pisa, resuelve dos y deja pasar uno, que
es exactamente lo que se pedía de un nodo que antes valía 1-3 de oro. Ninguna banda que estaba dentro se
sale, y `brokeMarketRunShare` entra del todo. Las 43 puertas en verde.

## Lo que queda fuera

- Las familias 3 (tienda de camino) y 4 (presión sobre el partido siguiente) del plan que el revisor vio.
- Efectos diferidos (deudas que se cobran en el mercado siguiente): exigen estado nuevo y un gancho en el
  mercado; con efectos inmediatos el catálogo ya dice lo que tenía que decir.
- El viejo `ServiceNodeSystem.Event` y las claves `eventGoldMin`/`eventGoldMax` desaparecen: eran justo lo
  que esta ADR sustituye.
