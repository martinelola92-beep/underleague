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
