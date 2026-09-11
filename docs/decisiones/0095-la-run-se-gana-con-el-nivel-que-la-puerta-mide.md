# 0095. La run se gana con el nivel que la puerta mide, y la muerte vuelve a su tasa

**Fecha:** 2026-09-11
**Estado:** Aceptada e implementada (`data/perks/skullsplitter.json`, `data/perks/marrow_thirst.json`, `data/sim/tuning.json`)
**Cierra AY-B** (la palanca que el paquete AY dejó anotada para después de AZ) y pone `runWinRate` **en banda por primera vez con muestra suficiente** (AV-A).
**Requisitos:** RF-025, RF-027, RF-093, RT-055, RT-056, RT-057
**Relacionada:** ADR 0033 (la curva de puertas de jefe), ADR 0048 (un jugador sano también puede morir), ADR 0091/0092/0094 (la física del pase, las razas y la sustitución, que movieron las dos cifras de abajo sin que nadie lo decidiera)

## El problema

`runWinRate` **14,50** (banda 20-30) con el motor de AZ cerrado. **El 92,4 % de las derrotas son un partido de
jefe**, así que la run se explica casi entera por el producto de las tres tasas de jefe: 0,765 × 0,530 × 0,414
= 0,168, menos las runs que se quedan sin plantilla.

La banda **no es arbitraria ni inalcanzable**: las celdas de la ADR 0033 para una build **buena** son 75-88
(acto 1), 60-72 (acto 2) y 40-55 (acto 3), y el producto de sus centros vale **25,6 %**, justo en medio de
20-30. La banda dice «quien llega a cada jefe con una build buena gana una run de cada cuatro». El problema
no era la banda: era que la run no entregaba lo que la puerta mide.

## Lo que se midió (dos causas, las dos con número)

**1. La run llega a cada jefe un nivel por debajo del que la puerta usa.** La puerta de la ADR 0033 mide con
el jugador en **nivel 5, 6 y 7** (`gate.playerLevel` de cada jefe). Fila nueva `levelAtBossActN` en
`--full-runs` (media de nivel del once al entrar en el jefe), 600 runs, semilla 1:

| Acto | Nivel que la puerta mide | Nivel que la run entregaba |
|---|---|---|
| 1 | 5 | **3,79** |
| 2 | 6 | **5,80** |
| 3 | 7 | **6,37** |

Con `matchExperience` 100 y ~13,5 partidos por run, un titular a tiempo completo llega al jefe del acto 1 con
400 puntos y el nivel 5 empieza en 700. La curva de experiencia y la curva de puertas nunca se habían
comparado entre sí; esta fila es la que lo hace visible y se queda en el arnés.

**2. Las muertes subieron un 50 % sin que nadie lo decidiera.** `deathsPerRun` **2,62** contra **1,70 / 1,81**
al cerrar AY. Entre medias: la física del pase (más entradas por partido), las razas nuevas y la sustitución
forzada (más cuerpos en el campo durante más partidos contra los cuatro rivales que llevan `skullsplitter` o
`marrow_thirst`). Cada muerte cuesta doble: el titular muerto se reemplaza por una firma de **nivel 1**, así
que la tasa de muerte y el nivel al jefe son la misma palanca vista dos veces. AY-B ya había dejado escrito
que la palanca era `lethalChance` y que se tocaba después de AZ.

## Las palancas, medidas una a una (600 runs, semilla 1)

| Variante | `runWinRate` | `deathsPerRun` | nivel al jefe 1/2/3 |
|---|---|---|---|
| Estado de partida | 14,50 | 2,62 | 3,79 / 5,80 / 6,37 |
| `lethalChance` 1.200 / 900 | **20,50** | 1,82 | 3,79 / 5,90 / 6,64 |
| `matchExperience` 140 | 19,83 | 2,92 | 4,78 / 6,79 / 6,89 |
| `interceptContactPercent` 350 → 200 | 15,83 | 2,59 | 3,79 / 5,77 / 6,33 |
| letal + `matchExperience` 120 | 18,67 | 1,81 | 4,15 / 6,38 / 7,15 |
| **letal + `matchExperience` 140** | **22,50** | 2,01 | 4,78 / 6,88 / 7,11 |

La tercera fila es la que se descarta con datos: bajar la amplificación de la intercepción (la palanca que la
ADR 0091 había anotado para las razas) vale solo +1,3 puntos y **empeora** el acto 3 (`ordinaryDefeatRateAct3`
47,1 → 54,0), porque quita habilidad a los dos lados y el jugador es el que construye.

## Decisión

1. **`lethalChance` de los dos perks letales de rival**: `skullsplitter` 1.950 → **1.200**, `marrow_thirst`
   1.500 → **900** (−38 % y −40 %, la misma proporción). No cambia la regla —la muerte sigue siendo
   consecuencia de una entrada del portador, AY paso 1— ni el ojeo que la anuncia (RF-013); cambia cuántas
   veces se cobra. Devuelve `deathsPerRun` a la tasa de AY (1,96 / 1,85 contra 1,70 / 1,81), dentro de la
   banda 1,5-3 de la ADR 0048 y por encima de su suelo.
2. **`progression.matchExperience` 100 → 140**: la run entrega el nivel que la puerta mide. Los porcentajes de
   RF-025 no se tocan (100 % a quien juega, 45 % al banquillo: 140 y 63). Tampoco se toca
   `experiencePerLevel`: la escala se corrige con un número y la **forma** de la curva queda anotada abajo.

## Lo que se mide al cerrar

`--full-runs 1200`, semillas 1 / 7:

| Métrica | Antes | Después | Banda |
|---|---|---|---|
| `runWinRate` | 14,50 | **22,67 / 21,00** | 20-30 |
| `deathsPerRun` | 2,62 | 1,96 / 1,85 | 1,5-3 |
| `defeatShare_notEnoughPlayers` | 7,60 | 3,56 / 4,11 | ≤ 35 |
| `bossWinRateAct1` | 76,50 | 79,42 / 79,50 | INFO |
| `bossWinRateAct2` | 52,99 | 57,43 / 55,63 | INFO |
| `bossWinRateAct3` | 41,43 | 52,61 / 50,91 | INFO |
| `levelAtBossAct1/2/3` | 3,79 / 5,80 / 6,37 | 4,78 / 6,89 / 7,18 | INFO (la puerta mide 5 / 6 / 7) |
| `mastersReached` | 29,83 | 32,25 / 31,08 | 2-90 |
| `contextualAdvantage` | 1,50 | 5,83 | ≥ 8 (sigue fuera) |

El producto de las tres tasas de jefe vale ahora 0,240 y 0,225, que es lo que la curva de la ADR 0033 predice
para una build buena. Las 43 puertas (`Category=Gate`) en verde, incluidas las seis de partido (RT-056), la de
jefes y las de run.

## Consecuencias y lo que queda fuera

- **AY-B cerrada.** La muerte en la entrada sigue costando puntos de run; ahora se cobra al precio que la
  banda de muertes admite.
- **El residuo es la forma de la curva de niveles**, no su escala: con 140 el jefe del acto 1 se juega en 4,78
  (dos décimas corto) y el del 2 en 6,89 (nueve décimas largo). Corregirlo pide mover
  `experiencePerLevel`, y eso cambia el ritmo de las ocho subidas de nivel de la run entera: paquete propio.
- **`runWinRate_noMarket` sube de 10,17 a 19,17** (banda ≤ 5) y `contextualAdvantage` se queda en 5,83
  (banda ≥ 8): las dos dicen lo mismo y ninguna es nueva —las tres filas del mercado
  (`affordableShareAtMarket` 67, `brokeMarketRunShare` 51) ya estaban fuera antes de este paquete—. Subir la
  tasa de la run sube también la de quien no compra: **el mercado no discrimina**, y eso es economía, no
  progresión. Anotado como AZ-H.
- Los cuatro rivales que llevan los perks letales (`act2_orc_warband`, `act2_undead_deadwalkers`,
  `act3_orc_warlords`, `act3_dwarf_ironkings`) siguen siendo los que matan; la build `orc_butchery` de
  `/Balance` pierde un poco de filo y su puerta de fase 1 sigue en verde.
