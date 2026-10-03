# ADR 0153 — A distancia de entrada no se persigue el balón, y proteger tiene tope

Fecha: 27 sep 2026 · Estado: **aceptada** (el revisor pidió evitar el atasco «provocando una entrada, o
creando un balón suelto o algo» y dejó el cómo a criterio). Ficha: [BO-A](../pendientes/BO-A.md).

## Contexto

Un portador apretado y el rival pegado a él podían quedarse así toda una jugada: medido, hasta **632 ticks
(42 s)** sin una entrada. El portador protegía (`Shield`) porque le apretaban; el rival elegía «ir a por
el balón» (`ChaseBall` 530) por encima de entrar (`Tackle` 510), o cubría un punto que caía encima del
portador. Cada uno repetía su mejor opción y nada cambiaba.

## Decisión

1. **`ChaseBall` no existe a distancia de entrada del poseedor rival** (`Utility.EvaluateChaseBall`).
   Perseguir sirve para llegar; encima del portador el balón no se recoge, se quita. Precondición que
   describe la situación, la misma forma que proteger y despejar (ADR 0138), no un peso retocado.
2. **Proteger tiene tope por posesión**: `ai.context.shieldMaxTicks` = **36** — tres compromisos de
   `states.ShieldingTicks` (12). **Provisional, sin barrido** (regla H): el valor sólo tiene que estar por
   encima de un compromiso y muy por debajo del atasco; con él, el peor tramo medido es 53 ticks. Llegado
   el tope, `Shield` se descarta y el portador tiene que pasar, conducir o despejar. El tope es **por
   posesión**, no por ticks seguidos: proteger, conducir y volver a proteger suma; se reinicia al recibir
   un balón nuevo.

## Medición

Tramos de más de 3 s con el mismo portador y el mismo rival pegado, 200 partidos: 20 → **5**; el peor
632 → **53** ticks. Balance 4.000 × 2: nada fuera de banda, goles iguales, entradas **+0,9 / +1,2** por
partido, lesiones +2-4 %, tarjetas un poco más. Por separado, sólo el tope baja las entradas (−0,3 / −0,5)
y deja el peor tramo en 53 igual; las dos reglas juntas dan la salida que pidió el revisor —la entrada—.

**Lo que cada regla hace de verdad** (revisión independiente, sonda de 200 partidos):

- **Quien corta el atasco es el tope.** La regla 1 sola deja el peor tramo en 203 (90 en las 100 semillas
  del test). La regla 1 **no obliga a entrar**: con un rival a distancia de entrada y sin enfriamiento, entra
  en el 16 % de las decisiones (62 % cubre espacio). Sube la entrada un 30 % frente a sólo el tope, y de ahí
  salen las +0,9 / +1,2 entradas por partido. Se conserva porque quita una elección que no hace nada y
  porque el revisor pidió la entrada como primera salida; su coste en lesiones (+2-4 %) queda anotado.
- **El tope corta también protecciones que no eran atasco**: el 34 % de las posesiones con protección
  termina en él, y 36 está por debajo de la duración media de proteger que midió BI-D (50,5). Es
  provisional; si se sube, `StalledDuelTests` dice hasta dónde (el atasco caza a partir de 60).
- Efectos laterales sin aislar: `passInterceptRate` baja (7,01 → 6,76; 7,93 → 7,39), `possessionChanges`
  sube a 27,0 en s2 (techo 28).

## Consecuencias

- Más entradas, y con ellas más faltas y lesiones: coherente con la carnicería, dentro de banda.
- `StalledDuelTests` fija el atasco en ≤ 60 ticks —falla si se retira el tope (90) o las dos reglas (632)—
  y comprueba por separado que nadie persigue el balón encima del portador.
- **Puertas: 5 rojas de 43, como antes, con otra composición.** Vuelve a verde `badBuildsLoseToNone`
  (roja desde la ADR 0152); pasa a rojo `TheThreeDoctrinesBuyDifferently` —la doctrina ahorradora acaba
  con 9,36 de oro y la contextual con 9,63, −0,28 donde se pide que la primera tenga más—. Sin aislar,
  puerta de una sola semilla; anotada, no descartada. Las otras tres, de antes (curva de jefes,
  `orc_violence` 57,45, `elf_none` 36,15 % y `undead_none` 60,60 %).
- **La causa de fondo sigue abierta** (BI-D, BJ-A): por qué la utilidad vuelve a elegir proteger con la
  misma presión. El tope trata el síntoma. Y es el segundo arreglo puntual del patrón «dos jugadores que
  repiten su mejor opción sin progreso» (el primero, ADR 0117, con un peso): sin regla común todavía.

## Enmienda (3 oct 2026): el tope es exacto (BO-A)

El tope se miraba al **elegir** proteger y el compromiso (`ShieldingTicks` 12) se cumplía entero: un compromiso que
empezaba con 35 ticks gastados llegaba a 47-48. Tras la ADR 0184 más posesiones llegan al tope y los tramos de más de 3 s
se doblaron (0,024 → 0,047 por partido); **todos** tenían 40-49 ticks de protección. Ahora el último compromiso es lo que
queda del tope (`MatchEngine.ShieldCommitTicks`), que es lo que esta ADR escribió: «tres compromisos de 12». Margen
restante: la cadencia de decisión (2 ticks). Medido, 1.000 partidos por traza: **0,047 → 0,005** (`ref`), 0,048 → 0,002
(`run`); partido de referencia sin cambio medible (10.000 × 2). Tests: `ShieldCapTests`. Ficha: [BO-A](../pendientes/BO-A.md).
