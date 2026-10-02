# 0185 — Arranque y frenada en `/Sim` (BV-A H4)

Fecha: 3 oct 2026 · Estado: **propuesta, sin implementar** (siguiente paso tras la [ADR 0184](0184-una-colocacion-se-sostiene.md)) ·
Requisitos: RT-020, RT-023, RT-089, RF-050 · Ficha: [BV-A](../pendientes/BV-A.md)

## Problema

BV-A H4 (CONFIRMED en `/Sim`): `MatchEngine.Move` da a cada jugador el paso **constante** de `SpeedPerTick` desde el
primer tick (0,131-0,159 casillas/tick con los atributos de referencia) y lo corta en seco al llegar. La velocidad por
tick de la traza es bimodal: 3.480 ticks a 0 y 5.350 a 2,0-2,5 c/s, casi nada en medio. Además, la sonda de la ADR 0184
cuenta ~478 *paso-0-paso* por partido (carrera, un tick quieto, carrera) con la sostenida puesta, 612 sin ella: un
destino que avanza menos que un paso por decisión, alcanzado en un tick y esperado el siguiente.

## Por qué no entra en la build del 4 oct (`game-design-review`, pregunta 9 y 10)

- **Cambia quién llega antes.** Con arranque, todo el que sale de parado pierde terreno, y con penalización de giro
  el que presiona (que gira mucho) pierde más que el que corre recto. Eso mueve intercepciones, persecuciones,
  entradas y lesiones, que la ADR 0184 ya mueve esta misma noche (entradas +8-10 %, y dos puertas abiertas). Medir las dos a la vez no
  permitiría atribuir nada (`balance-measure`: una hipótesis por tanda).
- **La parte visible ya la cubre la presentación.** `/Game` (BV-A, «Implementación») dibuja la velocidad promediada
  ±2 ticks con un filtro de 0,12 s: el muñeco ya arranca y frena en pantalla aunque la traza no lo haga. Lo que falta
  es la física de verdad (que la trayectoria y el partido lo sepan), no la imagen.
- Coste de una noche: primitiva de motor nueva + baseline propio + puertas completas + huellas. No cabe con H8
  bien medido.

## Diseño propuesto (para la siguiente sesión)

Todo en enteros (RT-023): la velocidad es una magnitud de juego, no una posición.

1. `MatchPlayer.SpeedMilli` (int, milésimas de casilla por tick), la rapidez actual. `SpeedPerTick` sigue siendo el
   **techo** y sigue saliendo del atributo `Speed` cansado: la velocidad de los atributos sigue importando igual.
2. **Arranque**: cada tick `SpeedMilli = min(techo, SpeedMilli + techo / tuning.movement.accelTicks)`.
   `accelTicks` **provisional, sin medir**: 3 (0,2 s hasta la punta). Con 3, salir de parado cuesta un paso
   (0,13-0,16 casillas) a todo el mundo por igual.
3. **Frenada**: el paso nunca supera lo que deja parar a tiempo, `v ≤ isqrt(2 · a · d)` en milésimas (raíz entera),
   y al llegar `SpeedMilli` baja a lo andado. Quita el *paso-0-paso*: con un destino que avanza despacio el jugador
   iguala su velocidad en vez de alcanzar y esperar.
4. **Giro**: al cambiar de rumbo, `SpeedMilli` se multiplica por el porcentaje entero `max(0, cos θ)·100` (el
   coseno se calcula con las posiciones, que ya son `float`, y se convierte a entero una vez). Una media vuelta
   arranca de cero; un giro de 45° conserva el 70 %.
5. Quien lea la velocidad para anticipar (`Utility.PassTarget` usa `SpeedPerTickMilli` del receptor) pasa a leer la
   actual o el techo según lo que quiera predecir: decisión explícita al implementar.
6. `accelTicks = 0` apaga todo, bit a bit (mismo patrón que `coverSpacingCells` y `positioningHoldBonus`).

## Cómo se demostraría

- Caso de valor conocido: desde parado, un jugador alcanza el techo en exactamente `accelTicks` ticks; uno con
  destino fijo a 0,5 casillas no lo rebasa; una media vuelta pasa por velocidad 0.
- Sonda de la ADR 0184 (`OscillationProbeTests`): histograma de velocidad por tick (que deje de ser bimodal) y
  *paso-0-paso* por partido.
- Lote de 10.000 × 2 semillas contra la base del mismo árbol (con la ADR 0184 ya dentro), mirando intercepciones,
  persecuciones ganadas, entradas, lesiones y goles; puertas completas; reparto por puesto de las entradas.

## Riesgos anotados

- El que presiona pierde contra el que conduce recto: puede bajar las entradas (la ADR 0184 las subió), lo que
  en la suma podría ser deseable, pero hay que verlo medido, no supuesto.
- La turba (`tuning.mob.speedPercent`, ADR 0175) sube el techo: el arranque en ticks se mantiene, la aceleración
  absoluta sube con él.
