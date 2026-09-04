# Rediseño espacial: cuerpos, adyacencia y roles

Plan de trabajo que agrupa las decisiones ADR 0020 (cuerpos con volumen), ADR 0021 (adyacencia estática por pares y proximidad dinámica) y ADR 0022 (roles derivados de la colocación). Se implementan **juntas y en este orden**, con un único reajuste de balance al final.

## Por qué juntas

Cada una de las tres invalida el ajuste de las otras: los cuerpos cambian el contacto, los roles cambian dónde está cada jugador, y la adyacencia cambia qué perks se activan. Reajustar `data/ai/weights.json` y `data/sim/tuning.json` tres veces cuesta tres veces lo mismo y no aporta información intermedia útil. La línea base contra la que se compara es `docs/balance/fase1-perks.md`, medida sobre el motor previo.

## Orden de implementación

| Paso | Contenido | Criterio de terminado |
|---|---|---|
| 1 | `bodyRadius` por raza; separación blanda en dos fases (buffer de desplazamientos aplicado al final del tick); empuje repartido por fuerza y tamaño; empuje también al resolver `TACKLE`; radios de interacción escalados por el radio de cuerpo | Determinismo intacto (huella idéntica en dos ejecuciones y entre SO); rendimiento medido y por encima de 167 partidos/s |
| 2 | Zona de acción con forma en lugar de radio duro (ADR 0028); contraste fuerte de pesos por estado táctico; acciones `FindSpace` y `PressCarrier`; marcaje con asignación estable | El volcado de utilidad explica las decisiones; los jugadores se reparten de forma reconocible en el log de posiciones |
| 3a | Generación por presupuesto con baremos (ADR 0025), etiquetas de estilo individuales (ADR 0024), habilidades raciales (ADR 0026) y la nueva relación rareza/nivel (ADR 0027); valores de perk en puntos porcentuales redondos | Distribución de atributos inspeccionable; común de nivel 8 entre 45% y 55% contra legendario de nivel 2; un equipo sin legendarios puede ganar al jefe final |
| 3b | Catálogo rehecho: 90% universal sin condiciones de raza y 10% exclusivos por raza (ADR 0023), con los ocho ejes de `perks-ejes.md` | Distribución por eje dentro de lo previsto; ninguna build de prueba depende de perks que el juego no ofrecería |
| 3 | Adyacencia resuelta al construir el partido en vínculos direccionales (`beside`, `ahead`, `behind`, `left`, `right`); efectos dirigidos a pares en el motor de efectos; condición de proximidad dinámica; revisión del catálogo de perks | Los perks de colocación se activan sin exigir formaciones absurdas; descripciones generadas correctas para ambas familias |
| 3c | Acciones de ataque diferenciadas (`ShortPass`, `LongPass`, escalado de regate y tiro), bloqueo sin balón dentro de la jugada activa y árbitro con criterio activo (ADR 0030) | Estilos de juego distinguibles en el log; la violencia es viable pero castigada por el árbitro |
| 4 | Reajuste único: RT-056 en rango y criterio de salida de fase 1 (coherentes ≥ 58%, malas ≤ 45%, progresión que premia) | Ambas puertas en verde y `docs/balance/fase1-perks.md` actualizado con el antes y el después |

## Hallazgos de la línea base que condicionan el rediseño

De `docs/balance/fase1-perks.md`, medido con plantillas emparejadas. Son restricciones de partida, no opiniones.

1. **Los canales de probabilidad saturados no responden hacia arriba.** `pass +1500` puntos base repartido entre los siete titulares vale **+0,4 puntos** de tasa de victoria (el mismo valor en negativo, −2,0), porque `pass.baseSuccess` está en 9.200 sobre 10.000 y el techo absorbe la mejora. Canales con recorrido real: `intercept` (+24 con +1500, fuera de escala), `injure` (+9), correa ±1 casilla (±8), `save` ±800 (±6), `shotOnTarget` (+6), `dribble` (±7). **Consecuencia**: un perk que promete "mejor pase" es una mentira medible; o se baja `pass.baseSuccess` para dejar recorrido, o esa familia de perks actúa sobre `intercept` en vez de sobre `pass`.
2. **Los perks de acumulación no producen progresión perceptible**: su efecto máximo vale 0,2-0,4 puntos porque acumulan sobre **atributos**, el canal más barato. Para que la progresión premie a quien construye bien (objetivo explícito de la fase 1), los perks de acumulación deben escalar sobre canales con recorrido, o desbloquear efectos estructurales al alcanzar umbrales, no sumar +1 de fuerza por partido.
3. **La velocidad es un atributo casi muerto** (+0,3 en humanos, −1,2 en orcos). Es la métrica que debe moverse con los cuerpos y la búsqueda de espacio; si tras el rediseño sigue plana, el rediseño no ha funcionado.
4. **La varianza de generación domina cualquier medición ingenua**: la misma build contra la misma referencia da entre 16,5% y 59,5% según qué plantilla salga (sd 14,9 puntos). Toda comparación de builds usa plantillas emparejadas y varias plantillas; ya está implementado en `/Balance --rosters`.

## Riesgos

- **Determinismo en la separación**: aplicar los empujes sobre la marcha haría que el orden del bucle cambiara el resultado. Es el mismo error que produjo el sesgo por id en la fase 0. Se resuelve con el buffer de dos fases y se comprueba con la huella.
- **Atascos y bailes**: la separación blanda puede producir oscilaciones cuando varios cuerpos convergen. Mitigación: empuje proporcional al solapamiento con un tope por tick, y sin resolución iterativa.
- **Rendimiento**: 91 pares por tick es asumible, pero `FindSpace` con 8 candidatos por jugador que decide sí es medible. Si el lote baja de 200 partidos/s hay que reducir candidatos antes que abandonar la mecánica.
- **Alcance**: esto es trabajo de fase 1 tardía que toca reglas del núcleo. Se hace **antes** de encargar arte (regla de fase) y antes de invertir más en ajuste fino.
