# Rediseño espacial: cuerpos, adyacencia y roles

Plan de trabajo que agrupa las decisiones ADR 0020 (cuerpos con volumen), ADR 0021 (adyacencia estática por pares y proximidad dinámica) y ADR 0022 (roles derivados de la colocación). Se implementan **juntas y en este orden**, con un único reajuste de balance al final.

## Por qué juntas

Cada una de las tres invalida el ajuste de las otras: los cuerpos cambian el contacto, los roles cambian dónde está cada jugador, y la adyacencia cambia qué perks se activan. Reajustar `data/ai/weights.json` y `data/sim/tuning.json` tres veces cuesta tres veces lo mismo y no aporta información intermedia útil. La línea base contra la que se compara es `docs/balance/fase1-perks.md`, medida sobre el motor previo.

## Orden de implementación

| Paso | Contenido | Criterio de terminado |
|---|---|---|
| 1 | `bodyRadius` por raza; separación blanda en dos fases (buffer de desplazamientos aplicado al final del tick); empuje repartido por fuerza y tamaño; empuje también al resolver `TACKLE`; radios de interacción escalados por el radio de cuerpo | Determinismo intacto (huella idéntica en dos ejecuciones y entre SO); rendimiento medido y por encima de 167 partidos/s |
| 2 | Roles derivados (columna de la casilla-hogar), pesos por rol, acciones `FindSpace` y `PressCarrier`, marcaje con asignación estable | El volcado de utilidad explica las decisiones; los jugadores se reparten de forma reconocible en el log de posiciones |
| 3 | Adyacencia resuelta al construir el partido en relaciones por pares; efectos dirigidos a pares en el motor de efectos; condición de proximidad dinámica; revisión del catálogo de perks | Los perks de colocación se activan sin exigir formaciones absurdas; descripciones generadas correctas para ambas familias |
| 4 | Reajuste único: RT-056 en rango y criterio de salida de fase 1 (coherentes ≥ 58%, malas ≤ 45%, progresión que premia) | Ambas puertas en verde y `docs/balance/fase1-perks.md` actualizado con el antes y el después |

## Riesgos

- **Determinismo en la separación**: aplicar los empujes sobre la marcha haría que el orden del bucle cambiara el resultado. Es el mismo error que produjo el sesgo por id en la fase 0. Se resuelve con el buffer de dos fases y se comprueba con la huella.
- **Atascos y bailes**: la separación blanda puede producir oscilaciones cuando varios cuerpos convergen. Mitigación: empuje proporcional al solapamiento con un tope por tick, y sin resolución iterativa.
- **Rendimiento**: 91 pares por tick es asumible, pero `FindSpace` con 8 candidatos por jugador que decide sí es medible. Si el lote baja de 200 partidos/s hay que reducir candidatos antes que abandonar la mecánica.
- **Alcance**: esto es trabajo de fase 1 tardía que toca reglas del núcleo. Se hace **antes** de encargar arte (regla de fase) y antes de invertir más en ajuste fino.
