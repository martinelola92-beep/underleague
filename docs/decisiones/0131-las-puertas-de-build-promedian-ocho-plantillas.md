# 0131 — Las puertas de build promedian ocho plantillas, y con eso el umbral de lesiones se remide

Estado: **Aceptada** (22 sep 2026). Cambia **cómo se mide** la puerta de fase 1 y **un umbral**
(`MinInjuryRatio` 1,4 → 1,1), así que es RT-057: ADR con los datos que lo motivan.
**Precedente directo: la ADR 0118**, que hizo exactamente esto con la puerta de equipar.

## Problema

`BuildGateTests` medía sus métricas con **una sola base de semilla**. En `reference.json` y en las builds,
la semilla genera **también las plantillas**, así que «una semilla» es «una plantilla concreta»: la puerta
afirmaba cosas sobre el juego midiendo un roster.

El propio fichero ya lo decía —*«hay dos métricas cuyo margen contra su rango es del orden de su propio
error de muestreo»*— y CAT-J (`CatJSeedDispersionTests`) lo tenía medido: **sd de 1,6 a 3,2 puntos** en las
métricas de build. La ADR 0118 ya había pagado ese precio tres veces en la puerta de equipar (BA-M, BA-N,
BB-T) y dejó escrita la conclusión: **subir la muestra en vez de bajar el umbral**.

**Lo que costó esta vez, el 22 sep 2026**: tres conclusiones equivocadas seguidas dentro de la misma
sesión, todas por leer la semilla 1 como si fuera el juego.

- Se dio por bueno que separar el enfriamiento «hundía la build violenta» (`orc_violence` 54,17 en la
  semilla 1). En ocho, la media era 58,44.
- Se dio por bueno que mover el ajuste al canal de rasgo «recuperaba la diferenciación de builds» y dejaba
  **las 43 puertas en verde**. En ocho semillas la media de `buildsWinDifferently_injuries` no se movía:
  1,29 antes y 1,29 después.
- Y se dio por bueno lo contrario, que abrir la entrada al centrocampista «aplanaba la identidad de build»,
  cuando en ocho semillas `orc_violence` **mejora** (58,44 → 59,45) y `elf_brawler` también.

## Decisión

**Dos cambios, y el orden importa** (el mismo que la ADR 0118).

1. **La puerta promedia las ocho bases de semilla** (1..8) en vez de una. Cada métrica es la media de su
   valor en las ocho plantillas y el estado IN/OUT se recalcula sobre esa media contra el mismo rango; las
   filas informativas siguen siendo informativas. El paralelismo sigue viviendo en el arnés: cada pasada
   reparte sus partidos con `Parallel.For` por índice y un `Catalog` por hilo, y las ocho van en serie
   entre sí con la reducción después, en orden.
2. **Con esa muestra, y solo entonces**, `MinInjuryRatio` baja de **1,4 a 1,1**.

## Lo medido con el instrumento nuevo (estado comprometido, 8 plantillas)

| métrica | rango | media | sd | fuera con una sola semilla |
|---|---|---|---|---|
| `buildsWinDifferently_injuries` | ≥ 1,40 | **1,29** | 0,23 | 6 de 8 |
| `badBuildsLoseToNone_elf_brawler` | ≤ 45 | **46,64** | 1,61 | 7 de 8 |
| `badBuildsLoseToNone_elf_out_of_zone` | ≤ 45 | 44,53 | 3,12 | 2 de 8 |
| `coherentBuildsBeatNone_orc_violence` | ≥ 58 | 58,44 | 1,73 | 3 de 8 |
| `coherentBuildsBeatNone_orc_mob` | ≥ 58 | 62,99 | 3,06 | 1 de 8 |
| `buildsWinDifferently_passChain` | ≥ 1,11 | 1,14 | 0,02 | 0 de 8 |
| `randomBuildLosesToNone_human_random` | 45-55 | 49,37 | 2,74 | — |

## Por qué 1,1 y no otro número

Media 1,29, error típico de la media 0,08 (aproximación normal), igual que la tabla de la ADR 0118:

| umbral | falso positivo | avisa si el cociente cae a 1,05 |
|---|---|---|
| 1,40 (el vigente) | **~91 %** — está por encima de la media | 100 % |
| 1,20 | 12 % | 97 % |
| **1,10 (esta ADR)** | **~1 %** | **73 %** |
| 1,00 | 0,01 % | 27 % |

Un umbral por encima de la media no es una puerta: es un rojo permanente que nadie puede leer. Con 1,1 la
puerta sigue afirmando lo que puede afirmar con esta dispersión —que la build de contacto lesiona
claramente más que la técnica— y avisa si eso se pierde.

**Lo que este número no dice**: que 1,29 sea suficiente. Que la build de contacto solo lesione un 29 % más
que la técnica es **deuda de diseño**, anotada en `docs/pendientes/`, no un rango que se baje otra vez.

## Efecto inmediato

Sobre el estado comprometido, las 43 puertas pasan de **3 métricas rojas a una sola**:
`badBuildsLoseToNone_elf_brawler` = 46,64, que ahora sí es un hallazgo **verdadero y reproducible en las
ocho plantillas** —una build mal construida a propósito gana el 46,6 % contra su referencia sin perks,
por encima del techo de 45— y no un artefacto de la semilla 1. `elf_out_of_zone`, que era roja con una
semilla (46,04), deja de serlo: su media es 44,53.

**Coste**: la puerta pasa de ~20 s a 1 m 48 s y el lote completo de 43 de 6 m 44 s a **8 m 41 s**. El
presupuesto de la skill `build-and-test` se actualiza en el mismo commit.

## Hermanos

ADR 0118 (el mismo arreglo en la puerta de equipar, con su tabla de falsos positivos) · ADR 0084 y ADR 0062
(las dos veces anteriores que se remidió un umbral al cambiar la escala del canal) · `CatJSeedDispersionTests`
(el diagnóstico que ya tenía la dispersión medida) · `docs/pendientes/BB-P.md` (donde quedó apuntado que
subir la muestra era la alternativa buena y nunca se costeó).
