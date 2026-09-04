# 0029. Visualización de la zona de acción en la colocación

**Fecha:** 2026-09-04
**Estado:** Aceptada (decisión del revisor). **Modifica RF-045: exige subir `requisitos.md` de versión.**
**Requisitos afectados:** RF-045, RF-042, RF-044, UI-001, UI-002, UI-005, UI-006, UI-020

## Contexto

RF-045 dice: *"En la pantalla de colocación se muestran **todas** las correas simultáneamente. Durante el partido solo se muestra la del jugador seleccionado o señalado."*

Con la ADR 0028 la correa deja de ser un círculo y pasa a ser una zona con forma, asimétrica y con dos capas (la zona propia y el margen exterior hasta el límite duro). Siete zonas asimétricas superpuestas producen una mancha ilegible que cubre casi todo el campo y no informa de nada.

Además, el diagnóstico de la fase 1 dejó claro que **el coste de apiñar el equipo es invisible para el jugador**: una alineación concentrada cuesta entre 16 y 24 puntos de tasa de victoria, y nada en la interfaz lo insinúa. Si el juego no lo enseña, el jugador cae en la misma trampa en la que cayeron las builds de prueba.

## Decisión

1. **Al arrastrar o seleccionar un jugador** en la pantalla de alineación se pintan en el suelo sus dos capas: la **zona de acción** en un tono sólido y el **margen exterior** —adonde puede llegar, pero tiende a no quedarse— en un tono más claro. Dos tonos porque son dos promesas distintas: "aquí estará" y "aquí puede llegar".
2. **Se muestra la del jugador manipulado, no las de todos.** Sustituye a la lectura literal de RF-045.
3. **Diferencia por forma además de por color** (UI-002): la zona lleva borde sólido y el margen borde punteado o trama, para que la distinción no dependa de percibir dos tonos de azul.
4. **Modo de cobertura del equipo**, accesible con una pulsación desde la misma pantalla: mapa de calor de cuántos jugadores cubren cada casilla, con los huecos destacados. Responde a la pregunta que de verdad importa —*¿qué parte del campo no cubre nadie?*— y hace visible el coste de concentrar el equipo.
5. **Los vínculos de colocación se dibujan en el mismo gesto**: al mover a un jugador se ve qué vínculos direccionales (ADR 0021) se crean y cuáles se rompen. Es la misma pantalla, el mismo gesto y la misma decisión.
6. **Ratón y mando por igual** (UI-006): con mando, la zona se actualiza al desplazar el cursor con un jugador seleccionado, sin gesto propio de arrastre.

## Consecuencias

- Refuerza UI-020 (la pantalla de Equipo concentra las decisiones de plantilla) dándole la información que hace que la colocación sea una decisión informada y no una intuición.
- Cumple UI-005: es información **transitoria** sobre el campo, visible solo mientras se manipula a un jugador, no un adorno permanente.
- Es trabajo de la fase 2 o 3, según cuándo exista la pantalla de alineación definitiva, pero se registra ahora porque condiciona cómo se dibuja el campo.
