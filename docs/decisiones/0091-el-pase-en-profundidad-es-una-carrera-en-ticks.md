# 0091. El pase en profundidad es una carrera en ticks

**Fecha:** 2026-09-10
**Estado:** Aceptada e implementada, medida; **ajuste de estilo del revisor aplicado** (pase más profundo, §abajo). `shotsPerMatch` queda en 7,7 / 7,1 con los goles recuperados (2,40 / 2,13); las filas que siguen fuera se investigan sin tocar bandas
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

1. **Tiros**: ~~aceptar 7-8 por partido o pedir un pase más profundo~~ → el revisor eligió **pase más
   profundo**; aplicado y medido en la sección siguiente. Tiros 7,7 / 7,1, goles 2,40 / 2,13 (los de antes
   del paso 5 eran 2,37 / 2,04): se tira menos y mejor. La banda 8-16 sigue en rojo y no se toca.
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

## Ajuste del revisor: el pase más profundo (10 sep)

Decisión del revisor ante los tiros: «pase más profundo» (candidatos más lejos del corredor y sin recorte por
la línea defensiva en el último tercio) en vez de aceptar la banda o restringir el pase en zona de tiro. Tres
datos nuevos en `weights.json` → `context`: `throughPassMinCells` **2**, `throughPassMaxCells` **6** (eran la
constante 2-4), `throughPassFreeZoneCells` **5,0** (a menos de 5 casillas de la portería rival —el último
tercio de las 16— la casilla candidata no se recorta por la línea: el pase a la espalda de la defensa es lo
que la acción es). Dos filas INFO nuevas para verlo: `throughPassesPerMatch` y `throughPassCompletionRate`.

Medido primero a 300 partidos (semilla 1) para elegir, después a 2.000 (semillas 1 / 7):

| Variante | Profundidad / zona libre / lateTicks | Pases en prof. / partido | Tiros | Goles | Tercio | Lesiones |
|---|---|---|---|---|---|---|
| Paso 5 (referencia) | 2-4 / 0 / 4 | 7,3 | 7,27 | 2,17 | 52,1 | 1,05 |
| 4-6 tal cual | 4-6 / 5 / 4 | **2,1** | 7,89 | 2,34 | 52,0 | 0,97 |
| 3-5 | 3-5 / 5 / 4 | 5,0 | 7,75 | 2,32 | 50,7 | 0,99 |
| 2-6 + zona libre | 2-6 / 5 / 4 | 8,2 | 7,33 | 2,17 | 48,8 | 0,87 |
| **2-6 + zona libre + el balón se para** | 2-6 / 5 / 4 | 8,3 | **7,83** | **2,46** | 50,6 | 0,89 |
| … + zona libre 8 | 2-6 / 8 / 4 | 8,1 | 7,82 | 2,41 | 50,2 | 0,87 |
| … + lateTicks 8 | 2-6 / 5 / 8 | 8,8 | 7,54 | 2,47 | 50,5 | 0,85 |

Lo que se aprendió por el camino, con el censo de utilidad (RT-098, 100 partidos) y una sonda de eventos:

- **Más lejos no es más profundo**: a 4-6 casillas la carrera casi nunca es legal (el defensa más cercano
  a la casilla suele estar a menos ticks que el corredor) y el pase pasa de 7,3 a 2,1 por partido; los tiros
  «suben» porque el fútbol vuelve a ser el de antes del paso 5. Con 2-6 el candidato hondo gana cuando es
  legal (`findSpaceAdvanceBonusPerCell`) y el corto sigue existiendo.
- **El pase en profundidad sustituye a la conducción**, no al tiro: con el pase apagado se eligen 122
  `Dribble` por cada 32 `Shoot` en la muestra; con el pase encendido, 49 `Dribble`, 29 `Shoot` y 21
  `ThroughPass`. Los tiros que faltan son los que salían de conducir hasta la portería.
- **Un defecto de verdad**: la legalidad supone que el balón «se para donde cae» y el corredor llega hasta
  `lateTicks` después, pero el motor lo soltaba rodando en la dirección del vuelo como cualquier pase
  fallido, así que la mitad de los pases legales (`throughPassCompletionRate` ~49 %: la tirada de técnica y
  la llegada a 1 casilla) acababan en un balón que se alejaba del corredor. Ahora el pase en profundidad
  que cae sin dueño se para en la casilla (`ResolvePassArrival`) y la carrera la termina la regla del balón
  suelto. Es el cambio que devuelve los goles (2,17 → 2,46 en la muestra).

Referencia final, 2.000 partidos, semillas 1 / 7: `shotsPerMatch` **7,72 / 7,12** (banda 8-16, sigue fuera),
`goalsPerMatch` 2,40 / 2,13, `possessionChanges` 22,5 / 22,7, `passChainAvgLength` 2,20 / 2,33,
`ballThirdMaxShare` 50,4 / 48,6, `injuriesPerMatch` 0,97 / 0,77, `tacklesPerMatch` 11,2 / 11,7,
`betterTeamWinRate` 60-40 90,1 / 77,8, `passInterceptRate` 8,5 / 8,7, `throughPassesPerMatch` 8,2 / 9,3,
`throughPassCompletionRate` 48,9 / 48,3. Instrucción del revisor para lo que sigue fuera (tiros, tercio en
la semilla 1, lesiones en la semilla 1, `elf_none`, `human_random`, `eternal_crown`): **investigar la
causa con números, sin tocar bandas**.

## Investigación de lo que sigue fuera (10 sep, sin tocar bandas)

Instrucción del revisor: causas con números, ninguna banda se mueve. Medido sobre el estado final de arriba.

**1. Las razas (`RaceBalanceTests`, D-29, 10.000 partidos).** La tabla completa, contra la de cierre de la
ADR 0026 (enanos 47,6 · elfos 54,1 · humanos 49,0 · orcos 51,8 · no-muertos 47,6; abanico 6,6 puntos):

| Estado | enanos | elfos | humanos | orcos | no-muertos | abanico |
|---|---|---|---|---|---|---|
| Final (tanda 3 + ajuste) | **39,9** | **61,7** | 50,2 | 46,1 | 52,1 | 21,8 |
| sin pase en profundidad | 41,4 | 61,0 | 48,4 | 44,5 | 54,8 | 19,6 |
| + sin geometría de intercepción (paso 2, `interceptContactPercent` 100) | 45,2 | 56,3 | 47,4 | 48,0 | 53,1 | 11,1 |
| + sin adelanto del pase (paso 1, `maxLeadCells` 0) | 45,1 | 57,2 | 48,1 | 45,5 | 54,2 | 12,0 |

La causa principal es el **paso 2**: el factor de proximidad (hasta ×3,5 cuando el balón pasa por el cuerpo)
multiplica una cuota de intercepción que ya depende de los atributos, así que el hueco elfo (+14 técnica,
+6 velocidad) / enano (−6, −14) se amplifica; los cuerpos no lo explican (28-38 centicasillas, casi iguales).
El pase en profundidad añade 2 puntos más (la carrera en ticks premia la velocidad). El adelanto del pase
no cuenta. Aun sin la tanda 3 el abanico sería ~11: el resto viene de las tandas 1-2 (pase atrás prohibido,
saque de falta) y no se ha atribuido paso a paso. **Palancas** (decisión del revisor, ADR de balance):
`interceptContactPercent` 350 → ~200 (devuelve parte del 8,5 % de intercepciones que se pidió), o recalibrar
las cinco razas (presupuesto ADR 0026: ±2,5 puntos por raza, insuficiente para 22 de abanico), o que la
cuota base de intercepción dependa menos del atributo antes de multiplicar.

**2. `human_random` 55,4 (banda 45-55, ADR 0088).** Con la muestra ×4 (160 plantillas, 1.920 partidos) da
**55,21**: no es ruido. Los siete perks de la build, emparejados a 200 partidos, valen entre 0 y 30 milésimas
(`own_third_anchor` 30, `comeback_spirit` 30, `box_predator` 20, resto 0): ninguno se ha disparado; la build
gana 5 puntos por acumulación de perks pequeños con el fútbol nuevo, no por uno inflado. Es una décima
sobre el techo; se vuelve a medir después de decidir el punto 1, porque las intercepciones mueven a los
perks de posición.

**3. `eternal_crown` muy buena 51,8 no mejora a buena 54,3** (`--boss-gate`, 1.000 partidos por nivel). Las
dos builds tienen la misma calidad (50) y difieren en dos perks de la buena (`sweeper_keeper`,
`diagonal_press`) contra cinco de la muy buena (`clean_sheet_legacy`, `lane_reader`, `road_warrior`,
`natural_leader`, `poacher_instinct`). Emparejados a 200 partidos: `lane_reader` 130, `clean_sheet_legacy`
70 (tabla: 98), `diagonal_press` 10, resto 0-10. No hay perk hundido: la fila «buena» subió (48,9 → 54,3)
porque el jefe del acto 3 sufre más el fútbol nuevo que las builds, y la muy buena no sube con ella. Es la
escalera estrecha que la ADR 0089 dejó anotada; la palanca es la calidad del jefe (ADR 0083) o el diseño de
las dos builds, y se decide junto con AU-C.

**4. Tiros, tercio y lesiones** están explicados arriba (el pase en profundidad sustituye a la conducción;
el tercio 50,4 y las lesiones 0,97 solo en la semilla 1, con la 7 dentro).

