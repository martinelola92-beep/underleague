# 0048. Un jugador sano puede morir

**Fecha:** 2026-09-05
**Estado:** Aceptada (decisión del revisor)
**Modifica:** RF-093 de forma sustancial, y retira una mitigación del §8
**Requisitos:** RF-012d, RF-013, RF-093, RF-114, RF-122
**Sustituye:** la ADR 0047, que buscaba rodear esta limitación

## Decisión

**Los perks letales pueden matar a un jugador sano.** RF-093 decía: *"Un jugador en estado sano **nunca** puede morir"*, y esa regla desaparece. Las muertes suben: el objetivo pasa de la banda 0,5-2 por run a **más**, y el desgaste se convierte en un riesgo permanente en vez de en el castigo por una mala decisión.

De paso resuelve la asimetría de la ADR 0047 —que un perk letal propio no pudiera matar a nadie— sin necesidad de la solución indirecta que allí se proponía.

## Lo que esto asume

RF-093 no era una regla cualquiera: era la **mitigación** de un riesgo que el propio documento clasifica como alto en su §8, *"muerte percibida como injusta → reseñas negativas"*. Al retirarla, el riesgo vuelve a estar vivo y hay que sostenerlo con otra cosa.

Lo que separa "azar duro pero justo" de "azar injusto" no es la probabilidad, es **si el jugador podía haber hecho algo**. La muerte de un sano es azar de salida (`docs/curva-de-dificultad.md` §2.4), el tipo que peor sienta, así que **toda la carga recae en poder anticiparlo y poder reducirlo**.

## Las cinco condiciones que lo hacen sostenible

Ninguna es nueva: todas existen ya en el diseño, y a partir de ahora son obligatorias.

1. **Se sabe antes de entrar.** El perk letal aparece destacado en el informe de ojeo (RF-013), que es gratuito y completo, y la letalidad ya sale en la descripción generada. Entrar en ese partido es una decisión informada.
2. **Se puede evitar el partido.** El mapa es ramificado: ese rival concreto está en un nodo concreto y hay otra ruta.
3. **Se puede reducir el riesgo.** El indicador de riesgo por jugador (RF-012c) se recalcula al mover la alineación: no alinear a tu mejor jugador contra ese rival, o alejarlo de la banda peligrosa, tiene que **notarse en el número**. Esto es lo que convierte el azar en decisión, y es la condición más importante de las cinco.
4. **Se puede rehacer.** Es el argumento del revisor: comprar en el mercado, heredar el equipamiento del muerto, ascender a un canterano. **El objeto del jugador muerto vuelve al inventario**, no se pierde con él.
5. **La muerte pesa, pero no es ruido.** Corrección del 5 de septiembre: la redacción original de esta condición pedía *"una muerte cada dos o tres runs"* (0,3-0,5) y a la vez la sección de consecuencias fijaba una banda de **1,5-3 por run**. Son cifras que se diferencian en un factor de cinco y no pueden cumplirse a la vez; **vale la banda**, que es la que mide la puerta. Con una plantilla de diez y una run de veinte partidos, 1,5 muertes significan perder a uno o dos jugadores por partida: se nota en cada alineación sin que la plantilla sea desechable.

## Consecuencias

- **RF-093 se reescribe**: la muerte deja de exigir un estado previo. Las dos vías siguen siendo alinear a un lesionado grave y el perk rival letal, pero la segunda ya no necesita víctima herida.
- El §8 del documento pierde su mitigación para el riesgo de muerte injusta y hay que **sustituirla por las cinco condiciones de arriba**, que pasan de deseables a requisito.
- Las bandas de `fase2-diseno.md` §10 suben: muertes por run de 0,5-2 a **1,5-3**, a confirmar midiendo.
- **La ceremonia de muerte y el memorial (RF-122) dejan de ser adorno**: si va a morir gente sana, el juego tiene que darle peso a cada una. Un obituario con las estadísticas de ese jugador es lo que convierte una tirada en una historia.
- Hay que medir que **reducir el riesgo funciona**: dos políticas idénticas salvo que una atiende al indicador de riesgo y la otra no deben separarse claramente en muertes.

  **Medido, y no se separan**: 1,51 muertes leyendo el ojeo y el indicador frente a 1,57 ignorándolos, un −3,8% dentro del ruido. El rango completo del indicador sí existe pero es **asimétrico**: buscar el peligro a propósito cuesta 1,90 y obedecer el indicador a rajatabla solo baja a 1,54. La causa medida es que **una alineación elegida por valor deportivo ya está cerca de la más segura**, porque el valor de un jugador incluye su aguante.

  Lo que sí se separa es la **tasa de victoria** (21,0 frente a 17,6): atender al riesgo paga en partidos ganados, no en cuerpos salvados. Es una agencia real, pero **no la que esta ADR necesitaba**. Ver ADR 0052.
