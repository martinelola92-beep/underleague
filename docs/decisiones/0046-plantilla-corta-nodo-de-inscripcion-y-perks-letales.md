# 0046. Plantilla de diez, nodo de inscripción y perks letales

**Fecha:** 2026-09-05
**Estado:** Aceptada (decisión del revisor)
**Modifica:** RF-020, RF-011
**Requisitos:** RF-002b, RF-005, RF-013, RF-093, RF-114k, RF-122

## Contexto

El desgaste no mata (ADR 0045): 0,06 muertes por run frente a la banda 0,5-2, y subir el daño no lo arregla porque con doce o trece jugadores siempre hay recambio. Tres decisiones para que el recurso central del juego lo sea de verdad.

## 1. La plantilla base es de diez

**RF-020 pasa de "mínimo 7, máximo 12" a "base 10, ampliable a 12".** Diez es exactamente lo que RF-005 ya da al empezar (7 titulares y 3 suplentes), así que el club inicial no cambia: lo que cambia es que **ya no se puede crecer gratis**.

Con diez jugadores y un mínimo de cinco para no perder por incomparecencia (RF-002b), el margen es de cinco bajas en toda la run. Cada lesión grave sin tratar y cada muerte se nota, que es justo lo que faltaba.

## 2. Nodo de inscripción: el despacho del presidente

Tipo de nodo nuevo en el mapa (amplía RF-011). Paga oro y te **amplía la plantilla en un hueco**, hasta el techo de doce.

Es una decisión de **ruta**, no solo de bolsillo: aparece en el mapa como cualquier otro nodo, así que ir a por un hueco significa **no ir** al mercado o a la clínica. Eso es lo que lo convierte en una elección y no en un botón.

- **Coste creciente**: el primer hueco es caro y el segundo bastante más (orden de partida: 12 y 25 sobre un total de ~100 de oro por run, ADR 0044). Ampliar de diez a doce cuesta casi la mitad del oro de una run entera.
- **Compite con la clínica**: *¿curo al que tengo o me traigo a otro?* Con la plantilla corta, esa pregunta se hace de verdad.
- Encaja con la ficción del deporte —los clubes pagan por poder inscribir jugadores— y con el tono cínico del juego (RA-025).

Su nombre y su presentación son cosa del arte; el concepto es el límite de inscripción.

## 3. Perks letales

RF-093 los contempla, el mecanismo está implementado y probado, y **ningún perk del catálogo es letal todavía**. Se escriben ahora, con estas reglas:

- **Solo matan a un jugador que ya no está sano** (RF-093). Un jugador sano nunca muere, y eso sigue siendo una propiedad del sistema.
- **Destacados en el informe de ojeo** (RF-013) y anunciados antes de entrar al partido (RF-012d). Una muerte no telegrafiada rompe el principio rector del juego.
- **Escasos y tardíos**: aparecen sobre todo en rivales de los actos 2 y 3. En el acto 1, que es el taller (ADR 0043), no deberían existir.
- **También disponibles para el jugador**, no solo para los rivales: es la culminación natural de las builds de violencia y da sentido a la fantasía de "carnicería administrada".

**Riesgo a vigilar**: matar rivales reduce su plantilla, y por debajo de cinco pierden por incomparecencia (RF-002b). Ganar por aniquilación debe ser posible pero **difícil y lento**, nunca la vía más eficiente de ganar un partido; si `/Balance` detecta que una build de violencia gana sistemáticamente por incomparecencia, hay que encarecer la letalidad.

## Consecuencias

- Con plantilla de diez, **fichar y tratar suben de valor** y los sumideros de oro se reequilibran solos: la economía hay que remedirla.
- Las muertes deben entrar en la banda 0,5-2 por run; si no lo hacen ni con plantilla corta ni con perks letales, el problema es otro y hay que volver a diagnosticar.
- El nodo nuevo entra en el reparto de tipos de nodo del mapa (D-2/D-10), quitando sitio a otro: hay que recalibrar la distribución.
- Cada muerte necesita su ceremonia (RF-122): ahora que van a ocurrir, el obituario y el memorial dejan de ser adorno.
