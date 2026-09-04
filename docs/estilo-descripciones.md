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

Internamente las probabilidades son enteros en base 10.000 y los efectos **suman**, no multiplican. Un `+1500` sobre una base de 2.200 lleva la probabilidad del 22% al 37%: son **quince puntos porcentuales más**, no "un 20% más".

Regla: las descripciones expresan estos cambios como **puntos de probabilidad**, redondeados a enteros, con la fórmula "más probabilidad de X" y no "un N% más de X". Si un efecto sí es multiplicativo, se dice "el doble" o "la mitad", nunca un porcentaje. Confundir ambas cosas es la vía más rápida a una descripción que miente, y una descripción que miente incumple RF-012d tanto como una muerte no telegrafiada.

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
