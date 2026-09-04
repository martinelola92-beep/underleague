# Estilo de las descripciones

Aplica a todo lo que el jugador lee para tomar una decisión: perks, objetos, consumibles, habilidades raciales, razas, clubes y etiquetas de estilo. Cumple RT-035 (las descripciones de efectos se generan, nunca se escriben) y RF-012d (todo lo que puede pasar debe ser previsible).

## La regla

**Una frase. Efecto observable. Nada de implementación.**

El jugador tiene que poder decidir, no auditar el código. Debe entender qué va a pasar en el campo; no necesita saber sobre qué variable se suma ni en qué orden se resuelve.

| Bien | Mal | Por qué |
|---|---|---|
| "Mejora el pase hacia el compañero de su columna" | "+800 a `pass` sobre el objetivo `linked:ahead` durante `match`" | La segunda expone el modelo de datos |
| "20% más de probabilidad de lesionar gravemente a un rival" | "+2000 puntos base a `severeInjury`" | Puntos base sobre 10.000 no significan nada para nadie |
| "Sus entradas dejan al rival derribado más tiempo" | "+12 ticks al estado `KnockedDown` del objetivo" | Los ticks son una unidad interna |
| "El primer pase de cada jugada no puede interceptarse" | "Anula el chequeo de intercepción si `passIndexInPlay == 0`" | La primera se puede ver ocurrir; la segunda hay que creérsela |

## Qué no aparece nunca

Ticks, puntos base, nombres de canales o de campos JSON, identificadores, fórmulas, umbrales internos, orden de resolución, y cualquier número que el jugador no pueda verificar mirando el partido o la ficha.

## Convención de porcentajes

**Los perks se escriben en puntos porcentuales enteros y redondos.** El JSON declara `"value": 20`, no `"value": 2000`; el cargador convierte a la base interna de 10.000 con la que el motor resuelve. El diseñador escribe y lee la misma cifra que ve el jugador. Los valores del catálogo se limitan a una escala corta —para que cada perk tenga un tamaño reconocible y el balanceo sea comprensible— pero esa escala **es propia de cada canal** (ADR 0035): el escalón de cada canal está en `data/sim/tuning.json` → `probabilityChannels.<canal>.step` y un valor legal es ese escalón por 1, 2, 3, 5 o 10. La razón es que un punto porcentual no vale lo mismo en todos los canales: sobre `intercept` (base 250) un `+5` triplica la probabilidad y sobre `pass` (base 7.700) la sube un 6,5%. **Lo que el jugador lee no cambia**: se le sigue diciendo el valor absoluto ("+3% de probabilidad de interceptar"), que es verdad y es verificable; lo que cambia es lo que el diseñador puede escribir.

La base 10.000 se mantiene **solo** dentro del motor, donde hace falta precisión para probabilidades pequeñas (una lesión del 2,4% no se puede expresar en enteros sobre 100).

**Puntos, no proporciones.** Los efectos **suman**: un `+20` sobre una base del 22% la deja en el 42%, que son **veinte puntos porcentuales más**, no "un 20% más" (eso sería 26,4%). Las descripciones dicen "20% más de probabilidad de X" entendido como puntos, que es como lo lee cualquier jugador de un juego de este género, y nunca describen un aumento relativo como si fuera absoluto. Si algún efecto llega a ser realmente multiplicativo, se describe como "el doble" o "la mitad", nunca con un porcentaje. Confundir ambas cosas es la vía más rápida a una descripción que miente, y una descripción que miente incumple RF-012d tanto como una muerte no telegrafiada.

## Qué se genera y qué se escribe

- **Perks, objetos, consumibles y habilidades raciales**: descripción **generada** desde el efecto (RT-035), con las plantillas de `data/l10n/<idioma>/templates.json`. No existe campo `description` y el validador lo rechaza. Es la única forma de garantizar que el texto y el efecto no divergen nunca.
- **Razas, clubes y etiquetas de estilo**: no son efectos, son conceptos, así que su descripción se **escribe a mano** en `data/l10n/`, una frase, con las mismas reglas de estilo. Su habilidad asociada, en cambio, sí se genera, porque es un perk.

## Longitud

Una frase de línea y media como máximo a 1280x800 (RT-070) en el tamaño de texto pequeño (UI-004). Si no cabe, el problema es el efecto: un efecto que necesita dos frases para explicarse combina demasiados ejes (`perks-ejes.md`, regla de legibilidad) y hay que partirlo o simplificarlo.

## Ejemplos por tipo

| Elemento | Descripción |
|---|---|
| Raza (Enanos) | "Bajos, tercos y difíciles de mover. No llegan lejos, pero donde plantan el pie se quedan." |
| Habilidad racial (Enanos) | "No pueden ser desplazados por empujones." |
| Etiqueta de estilo (`Brute`) | "Busca el contacto. Gana duelos y reparte daño." |
| Club (Los Carniceros de Kharg) | "Orcos. Empiezan con poco oro y un delantero legendario con antecedentes." |
| Perk | "Mejora el pase hacia el compañero de su columna." |
| Objeto maldito | "Mucha más fuerza. El portador termina cada partido lesionado." |
