# BO-A — Portador y rival atascados en la misma jugada

Estado: **Implementada** ([ADR 0153](../decisiones/0153-a-distancia-de-entrada-no-se-persigue-y-proteger-tiene-tope.md), 27 sep 2026).

## Observación

«Dos rivales se quedan atascados compitiendo por el balón. Uno regateando/protegiendo y el otro intentando
quitarle el balón. Estuvieron casi todo el partido así, en la misma jugada. Hay que evitar esto: o
provocando una entrada, o creando un balón suelto o algo. Supongo que en cada tick la mejor opción de ambos
era la misma.» (Revisor, 27 sep 2026.)

## Medición (200 partidos `TestMatches.Reference`, `5236461`)

Tramos de más de 3 s con el mismo portador y el mismo rival a menos de una casilla: **20**, mediana 76
ticks, el peor **632 ticks (42 s)** — semilla 40, tick 614: el portador en `Shielding` 627 ticks, movido
0,34 casillas; el rival alterna `ChaseBall` y `CoverSpace`; **cero entradas**.

## Hipótesis

- **CONFIRMED** — el rival pegado no entra porque «ir a por el balón» le puntúa más que entrar
  (`ChaseBall` 530 contra `Tackle` 510, volcado RT-098), y perseguir un balón que lleva un rival que ya
  tienes encima no hace nada. El portador protege porque le aprietan (`Shield` 688).
- **CONFIRMED** — hay más variantes con el mismo final: un defensa cerca de su portería cuyo punto de
  cobertura cae encima del portador (`CoverSpace` 879 > `Tackle` 648), o un rival que se repliega en la
  misma dirección en la que el portador se aparta. En todas, **el portador protege sin fin**. Es el
  hermano que BI-D ya había medido y nadie tenía: «proteger dura 50,51 ticks con `ShieldingTicks` 12».
- **REJECTED** — que fuera un duelo que se repite (entrada fallida, enfriamiento, otra vez): en el peor
  caso no hubo ni una entrada.
- **REJECTED** — que los cuerpos se bloqueen: el rival se mueve lo que manda su acción.

## Arreglo

1. **A distancia de entrada no se persigue**: `EvaluateChaseBall` se descarta si el poseedor es rival y
   está a `tackleDistanceMaxCells` o menos. No obliga a entrar (entra en el 16 % de esas decisiones, un
   30 % más que antes); quita una elección que no hacía nada.
2. **Proteger tiene tope**: `MatchPlayer.ShieldedTicks` cuenta la protección de la posesión actual;
   `ai.context.shieldMaxTicks` = 36 (tres compromisos de `ShieldingTicks` 12, provisional) y a partir de
   ahí `Shield` se descarta y el portador tiene que jugar el balón. **Es el que corta el atasco**; también
   corta el 34 % de las protecciones que no lo eran.

| | tramos > 3 s (200 partidos) | peor |
|---|---|---|
| antes | 20 | 632 ticks |
| sólo (1) | 23 | 203 |
| (1) + (2) | **5** | **53** |

Balance, 4.000 × 2 contra `5236461` (`out/area/{final,cap,both}`): nada fuera de banda, goles iguales,
**entradas +0,9 / +1,2** con las dos reglas (sólo el tope las baja 0,3 / 0,5: el portador se la quita de
encima con un pase), lesiones +2-4 %. Test permanente `StalledDuelTests`: el atasco ≤ 60 ticks (falla sin
el tope, 90, y sin las dos reglas, 632) y nadie persigue el balón encima del portador.

Revisión independiente: el primer umbral del test (90) no cazaba que se quitara el tope; el título de la
ADR decía «se entra» y no es lo que pasa; el esquema decía «seguidos» y el tope es por posesión.
Corregido. Queda abierta la causa de fondo (por qué se vuelve a elegir proteger con la misma presión).

## Hermanos

[BJ-A](./BJ-A.md) y [BI-D](./BI-D.md) (proteger dura cuatro veces su contador).

## Barrido de detectores (3 oct 2026)

**CONFIRMED, residual raro.** Detector validado con el caso exacto de la ficha: en la build anterior al arreglo da **semilla 40,
tick 614, 632 ticks** (y semilla 92 tick 284, 217). Build actual: 0,024 tramos > 3 s por partido (2,4 %, `ref`) y 0,036 (3,4 %,
`run`), frente al 9 % de antes. El peor: **run semilla 743, tick 1127, 173 ticks (11,5 s)**, portador 6 contra el rival 2000003; la
hoja (`hojas/hoja-BO-A.png`, MP4 `BO-A.mp4`) muestra un grupo de rojos alrededor del portador azul con el balón a los pies, no
un duelo de dos. Los demás `ref` son 53 ticks (justo sobre el umbral de 45). Informe: [barrido-detectores-2026-10-03](../analisis/barrido-detectores-2026-10-03.md).

## Sube tras la ADR 0184 y se arregla el tope (3 oct 2026, tarde)

Barrido de la tarde: **0,024 → 0,047 ± 0,007** tramos de más de 3 s por partido (`ref`), 0,036 → 0,048 (`run`).

- **Instrumento validado (Regla J)**: la sonda por variante (`WorstCaseProbeTests.StuckAndCrowdByVariant`, 1.000 partidos
  por traza) reproduce el barrido al milésimo con el `main` actual (0,047 / 0,048) y el de la mañana con la sostenida a 0
  y BV-B apagada (0,023 / 0,038).
- **De dónde viene la subida — LIKELY la sostenida (ADR 0184)**: sólo sostenida 0,035 / 0,066; sólo BV-B 0,030 / 0,036;
  ninguna 0,023 / 0,038 (±0,006). Las dos suben algo en `ref`; en `run`, la sostenida.
- **Por qué pasa de 3 s — CONFIRMED**: **todos** los tramos tenían entre 40 y 49 ticks de protección (el resto, unos
  pocos de conducción con el rival encima), con `shieldMaxTicks` = 36. El tope se miraba al **elegir** proteger, y el
  compromiso de 12 que empezaba con 35 gastados se cumplía entero (hasta 48). La sostenida no cambia eso: hace que más
  posesiones lleguen al tope.
- **Arreglo** (código, `MatchEngine.ShieldCommitTicks`): el último compromiso es lo que queda del tope; sin tope (0), el de
  siempre. Hace verdad lo que la ADR 0153 escribió («tres compromisos de 12»); no es una regla nueva. Margen que queda: la
  cadencia de decisión (`decisionIntervalTicks` = 2), porque al acabar un compromiso el portador sigue en `Shielding`
  hasta su turno.
- **Medido** (1.000 partidos por traza): **0,047 → 0,005** (`ref`), **0,048 → 0,002** (`run`), por debajo de la mañana.
  Los tres peores del barrido no cambian (`run:810@1175` 62, `ref:29@1081` 60, `run:162@89` 56): son el residuo de
  diseño, 36-39 ticks de protección más conducción con el defensa encima (`ref:29`: `CoverSpace` 693 contra `Tackle`
  549, el defensa cuyo punto de cobertura cae sobre el portador, la variante que ya estaba CONFIRMED arriba).
- **Tests** (`ShieldCapTests`): valor conocido del compromiso (12 / 6 / 1 con 0 / 30 / 35 gastados; control sin tope, 12)
  y el caso real `ref:100@1270` (48 ticks protegiendo sin el arreglo; ahora ≤ 38 y ningún tramo de más de 3 s en esa
  semilla). Comprobado que los dos fallan sin el arreglo.
- Partido de referencia sin cambio medible (10.000 × 2): s1 goles 2,480, entradas 9,27, lesiones 0,744; s2 2,090, 11,20,
  0,415 (ADR 0188: 2,479 / 9,29 / 0,742 y 2,088 / 11,24 / 0,412).

Queda abierta la causa de fondo de siempre (por qué se vuelve a elegir proteger con la misma presión, BI-D/BJ-A) y el
defensa cuyo punto de cobertura cae encima del portador.
