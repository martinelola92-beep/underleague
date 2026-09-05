# 0057. Un equipo sin build gana el 14,5% de las runs, y eso es el problema de fondo

**Fecha:** 2026-09-05
**Estado:** Aceptada; **la palanca que elige queda falsificada** al aplicarse la P1 (`fase2-diseno.md` §26.6): el suelo pasa de 12,67% a 12,08% sobre 1.200 runs por lado, una diferencia de −0,59 con error típico 1,34. La causa está medida: los rivales **ordinarios** de una run no llevan ningún perk, así que la capa de build solo existe en un lado del campo salvo contra los tres jefes, y hacer los perks más fuertes o más débiles no mueve al equipo que no los tiene. La alternativa que la propia ADR dejaba prevista —"eso es diseño de rivales"— pasa a ser la vía viva, con nombre concreto: **que los rivales ordinarios lleven perks** (AJ-B en `pendientes.md`)
**Corrige:** la palanca elegida en la ADR 0055 y **la P3 de la ADR 0050**
**Requisitos:** RT-055, RF-032
**Relacionada con:** ADR 0033 (curva de puertas) y ADR 0056 (separación entre perfiles)

## El hallazgo

Al intentar cumplir la ADR 0055 —*ganar sin pasar por el mercado por debajo del 5%*— el paquete de economía la falsificó con dos mediciones:

- **Techo**: con bolsa ilimitada, una política que compra todo llega a 15,9 perks y 7,5 objetos y gana el **30%** de las runs; la misma política esquivando los mercados gana el **20,5%**. El valor máximo que el mercado puede llegar a tener es de unos **10 puntos**, y la ADR necesitaba de 15 a 25.
- **Suelo**: con las recompensas dando **cero perks**, una política que además esquiva los mercados termina la run con **1,58 perks y 0,16 objetos —sin build ninguna— y aun así gana el 14,5%**.

Entre "sin build" y "build completa y equipada" hay **14,5% → 30%**: en torno a **1,1 puntos de tasa de victoria por perk** del once, con el **suelo en 14-15%, no en cero**.

**Ninguna cantidad de oro puede bajar del 5% mientras ese suelo esté en 14%.** El problema nunca fue la economía.

## Lo que esto contradice

La tabla de la **ADR 0033** dice que una build **incoherente** completa la run el ~0,1% de las veces y una **correcta** el ~6%. Medido en runs completas, un equipo **sin ninguna build** gana el 14,5%.

Las dos cosas se miden distinto y ambas son ciertas: la curva de puertas enfrenta build contra jefe en **partidos directos**, mientras que la run entera incluye veinte partidos ordinarios donde **el nivel y los atributos bastan para ganar**. El jefe filtra; el resto del recorrido, no.

Y choca de frente con la directriz del revisor de que **una build mala no debe completar la run** (ADR 0056, objetivo <2%).

## Decisión

**El peso relativo de la build frente a los atributos es la palanca, no el oro.** Y eso reordena las dos correcciones que quedaban de la ADR 0050:

- **La P1 (perks multiplicativos sobre cuotas) pasa a ser la palanca principal**, no solo una mejora de coherencia. Al hacer que cada perk pese más y de forma predecible, **no tenerlos duele más** y el suelo baja. Es exactamente lo que hace falta.
- **La P3 (curva de nivel más agresiva) queda en suspenso.** Subir el crecimiento por nivel del 22% al 39% **sube el peso de los atributos**, que es justo el componente que sostiene ese 14,5%. Iría en contra del objetivo. Se retoma solo si, tras P1, el suelo ha bajado lo suficiente como para permitírselo.

## Consecuencias

- La métrica de la ADR 0055 (ganar sin mercado <5%) se mantiene como objetivo, pero **su palanca cambia**: se alcanza bajando el suelo, no encareciendo la tienda.
- Si tras P1 el suelo sigue por encima del 10%, la conversación es de **curva de dificultad**: los partidos ordinarios de los actos 2 y 3 tendrían que castigar a un equipo sin build, y eso es diseño de rivales, no de economía.
- Hay una alternativa que conviene tener sobre la mesa: **cambiar la métrica** de "ganar sin mercado" a "cuánto peor se juega sin mercado" (hoy 1,4 puntos; con bolsa ilimitada 9,5). Mide lo mismo que se quería medir y no depende de un suelo que tiene otras causas.
