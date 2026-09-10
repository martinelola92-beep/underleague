# 0091. El pase en profundidad es una carrera en ticks

**Fecha:** 2026-09-10
**Estado:** Aceptada e implementada, **medida y con un ajuste de estilo pendiente del revisor** (`shotsPerMatch`)
**Decisión del revisor** (segunda partida, `pendientes.md` AZ-B: «los jugadores solo pueden recibir un pase si el balón llega a donde están ellos o si el balón se ha lanzado en profundidad para que el receptor lo reciba en carrera»). **Añade una acción a RT-092** (`PlayerAction.ThroughPass`) y un desenlace del pase (`PassFailed` con detalle `beaten`).
**Requisitos:** RT-020..024, RT-092, RT-093, RT-096, RT-097
**Relacionada:** `docs/plan-pases-trayectoria.md` (pasos 0-5), ADR 0030 (el pase partido en bandas), ADR 0022 (`FindSpace`), `docs/referencia-motores-futbol.md` §1-2 (librcsc: intercepción en ciclos; gfootball: `ballToIntersect` contra `oppToIntersect`)

## Lo que se implementa (tanda 3 completa, pasos 0-5)

| Paso | Qué | Dato nuevo |
|---|---|---|
| 0 | contadores del pase: completado / interceptado / suelto / ganado por el rival | INFO `passCompletionRate`, `passInterceptRate`, `passLooseRate`, `passBeatenRate` |
| 1 | **pase al pie**: el destino se adelanta por lo que el receptor va a poder recorrer hacia donde él ha decidido ir (`TargetPoint`), nunca por la extrapolación ciega de `Velocity` | `pass.maxLeadCells` 1,5 |
| 2 | **intercepción con geometría**: la cuota se multiplica por un factor de proximidad que vale 100 en el borde del radio y sube hasta `interceptContactPercent` cuando el balón pasa por dentro del cuerpo (`bodyRadius` de la raza); `TryBlockShot` lo hereda | `pass.interceptContactPercent` 350 |
| 3 | el **pasillo puntúa** la elección del receptor (nunca descarta) | `passLaneRadiusCells` 0,6 (era constante), `passBlockedLanePenalty` 200, `passBlockedLaneRankPenalty` 150 |
| 4 | el pasillo puntúa la **decisión de tirar**, con el portero fuera del pasillo | `shootBlockedLanePenalty` 150 |
| 5 | **pase en profundidad**: tercera banda del pase, a una casilla vacía 2-4 casillas por delante de un compañero que va hacia delante, recortada por la línea de fuera de juego; legal solo si `ticksReceptor ≤ ticksBalón + lateTicks` y `ticksReceptor + marginTicks ≤ ticksDefensa` (entero, adimensional); se lo lleva quien llega antes (empate por id), y si es un rival, `beaten` | `ThroughPass` en `base`/`tactical`, `throughPassLateTicks` 4, `throughPassMarginTicks` 3, `throughPassBase` 180, `throughPassTechniqueSlope` 12 |

Todo en aritmética entera salvo las posiciones; ninguna tirada nueva de RNG en los pasos 2-5 (la carrera la
resuelve la simulación tick a tick, no una predicción).

## Lo que se mide (2.000 partidos, semillas 1 / 7)

| Métrica | Tanda 2 | Pasos 0-4 | Paso 5 | Banda |
|---|---|---|---|---|
| `passInterceptRate` | 4,8 | 8,5 / 8,7 | 8,5 / 8,6 | INFO (objetivo 8-12) |
| `passLooseRate` | 37,1 | 32,1 / 32,7 | 31,2 / 31,4 | INFO |
| `passBeatenRate` | — | — | 0,8 / 1,0 | INFO |
| `passChainAvgLength` | 2,01 / 2,13 | 2,14 / 2,20 | 2,21 / 2,30 | 2-4 |
| `possessionChanges` | 23,9 / 23,2 | 23,3 / 24,3 | 23,2 / 24,0 | 12-28 |
| `blockRate` | 2,1 / 1,3 | 1,3 / 1,5 | — | INFO |
| `ballThirdMaxShare` | 45,8 / 46,3 | 53,4 / 51,7 | **52,0 / 49,9** | ≤ 50 |
| `injuriesPerMatch` | 0,91 / 0,74 | 1,01 / 0,89 | **0,97 / 0,82** | 0,3-0,9 |
| `betterTeamWinRate` (60 vs 40) | — | 91,6 / 80,8 | **88,3 / 75,4** | 70-88 |
| `shotsPerMatch` | 9,46 / 7,95 | 8,07 / 7,54 | **7,25 / 6,64** | 8-16 |
| `goalsPerMatch` | 2,56 / 2,09 | 2,37 / 2,04 | 2,16 / 1,84 | INFO |

El paso 5 hace lo que se le pidió (devuelve el balón hacia delante: `ballThirdMaxShare` baja, `betterTeamWinRate`
vuelve a banda en la semilla 1, las lesiones bajan) y **baja los tiros**, al revés de lo previsto. Dos
experimentos descartados por medición: sin pase en profundidad para los delanteros, 7,6 / 6,8; con
`throughPassBase` a la mitad, 7,7 / 6,9. El descenso no depende de quién da el pase ni de cuánto vale: el
corredor recibe a la altura de la línea defensiva y de ahí salen menos tiros que de la conducción que
sustituye. El plan fija la regla de parada («si `shotsPerMatch` baja de 8, se para: la palanca siguiente es
el peso base, y eso es una conversación de estilo de juego con el revisor») y aquí se aplica.

## Lo que queda para el revisor

1. **Tiros**: aceptar 7-8 por partido con el fútbol nuevo (banda 8-16 → 6-16, ADR de banda) o pedir que el
   pase en profundidad termine más cerca de portería (candidatos a 4-6 casillas en vez de 2-4, o sin
   recorte por la línea en el último tercio), que es cambiar el estilo.
2. **Lesiones** 0,97 (semilla 1) sobre 0,9: hay más juego vivo (entradas 10,8 → 12,1); ADR 0082 ya subió el
   techo de 0,8 a 0,9 por lo mismo en AW-R. Propuesta: 0,3-1,0.
3. **`RaceBalanceTests`** `elf_none` 60,95 sobre 60: la técnica se premia más; recalibrar `elf_touch` dentro
   del presupuesto de la ADR 0026 (+2,5 puntos) en vez de tocar la banda.
4. **`BuildGateTests`** `randomBuildLosesToNone_human_random` 55,42 sobre 55 (ADR 0088: 45-55): la build
   aleatoria humana gana 5 puntos a la base con el fútbol nuevo; hay que ver si es una fila de la muestra o
   un perk que ahora se cobra más (medir con `--perks`).
5. **`BossGateTests`** `eternal_crown` muy buena 51,7 no mejora a buena 52,5: es el hueco estrecho que la
   ADR 0089 dejó anotado (48,9 → 52,3) y que la física del pase acaba de cerrar. Palancas: el techo del
   acumulador `deathless_march` (3 → 4 solo para ese jefe no existe: es global) o la calidad de `eternal_crown`
   (ADR 0083). Decisión de escalera del acto 3, con AU-C.

Las puertas se pasaron enteras tras el paso 5 (`Category=Gate`, una invocación): 5 tests en rojo por las
filas de arriba (`ShotsPerMatchAreInRange`, `NoMandatoryMetricIsOutOfRange` con tiros 7,32 / tercio 51,9 /
lesiones 0,98, `NoLaunchRaceDominates…` 60,48, `RandomBuildLosesToItsBaseline` 55,42, `EveryBossRewardsABetterBuild`).
El test de escenario del letal (`TheSameLethalPerkKillsSomeoneWhoTookTheFieldAlreadyHurt`) elegía al tocado por
la formación y al portador como el jugador 0 (el portero, que no entra a nadie): ahora elige la pareja
(visitante, local) con más entradas medidas en la misma tanda, que es lo que la regla de AY significa.

Mientras tanto, `main` no se publica (`buildsWinDifferently_passChain` volvió a verde con la tanda 3, pero
las cuatro filas de arriba siguen en rojo).
