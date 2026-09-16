# BB-N — El saque de córner no ocurre nunca

**Estado:** Abierta

## Observación

Hallazgo del `independent-reviewer` durante la revisión de BB-B (barrera geométrica de reanudación,
16 sep 2026): en 60 partidos de referencia (dos árboles distintos, con y sin la barrera), **cero saques de
córner**. `RestartKind.Corner`, `ScheduleCorner`, `CornerTicks` y el `case RestartKind.Corner` de
`ResolveRestart` existen en el código y nunca se ejercitan en la muestra.

## Análisis / estado actual

**Abierta, sin reproducir ni diagnosticar todavía.** No se sabe si:

- El córner nunca se programa (`ScheduleCorner` nunca se llama desde donde debería —un balón que sale por
  la línea de fondo tras tocar a un defensor, RF típico de fútbol).
- Se programa pero algo lo reconvierte antes de resolverse (una condición que lo redirige a otro tipo de
  reanudación).
- Es correcto que sea raro con la física de pase/tiro actual (ADR 0091) y simplemente no ha salido en las
  60 semillas medidas — hipótesis más débil dado que 0/60 en dos árboles independientes es una muestra
  consistente, no una racha.

No investigado más allá de la medición del revisor. Antes de hipotetizar una causa, Regla A: consultar si
`gameplay-debug` encuentra la vía de programación de `ScheduleCorner` y confirmar si el evento se dispara
en un lote más grande antes de tocar código.

## Hermanos

- [BB-G](./BB-G.md) — "el balón se queda parado en el campo": mismo área del motor (resolución de balón
  fuera de banda/gol), posible causa común con por qué el córner nunca se programa.
- [BB-B](./BB-B.md) — donde se detectó, de camino, sin ser el objeto de esa revisión.
