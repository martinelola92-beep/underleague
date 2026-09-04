# 0041. La probabilidad de lesión se mide contra el rival, no contra un valor fijo

**Fecha:** 2026-09-05
**Estado:** Aceptada
**Requisitos:** RF-090..094, RF-114k, RT-056

## El defecto

En el bucle de run se producen **0,04 lesiones propias por partido**, frente a las 0,62 que mide RT-056 en partidos sueltos. Quince veces menos.

La causa es que la fórmula de lesión es **absoluta**: compara los atributos contra el valor 50, que es el de un jugador de nivel 1. Como subir de nivel añade 2 puntos por atributo, un titular de nivel 6 resta 60 sobre una base de 40, la probabilidad se acota a cero y **una entrada limpia deja de poder lesionar**. En un partido suelto los equipos son de nivel bajo y la fórmula funciona; en una run, donde la plantilla sube de nivel, el sistema de lesiones se apaga solo.

Las consecuencias van más allá de las lesiones:

- **Cero tratamientos de clínica en 500 runs.** La clínica es contenido muerto.
- **Los mercenarios no tienen función**: se fichan para cubrir bajas que no ocurren.
- **Dos de los cuatro sumideros de oro no se usan**, lo que falsea RF-114k y toda la calibración económica.
- Y sobre todo: **el desgaste de plantilla, que el documento declara el recurso central del juego, no existe** en la práctica.

## Decisión

La probabilidad de lesión pasa a medirse **de forma relativa**: la fuerza del que entra contra la resistencia del que la recibe, no contra una constante. Un jugador de nivel 8 que entra a otro de nivel 8 debe lesionar aproximadamente lo mismo que uno de nivel 1 contra otro de nivel 1; lo que cambia el riesgo es la **diferencia** entre los dos, no su nivel absoluto.

El mismo defecto afecta a cualquier otra fórmula del motor que compare un atributo contra un valor fijo en vez de contra su oponente. Hay que revisarlas todas: la fórmula de entrada, la de regate y la de parada usan el mismo patrón.

## Consecuencias

- Hay que recalibrar `data/sim/tuning.json` y revalidar RT-056 y las puertas de fase 1 y 2: es un cambio global, del alcance del reajuste que cerró la fase 1.
- Se espera que **reaparezcan** la clínica y los mercenarios como sumideros, lo que cambia el equilibrio económico y obliga a remedir las tres doctrinas de compra de la ADR 0037.
- El desgaste vuelve a ser un recurso: las muertes por run deberían entrar en el rango 0,5-2 de `fase2-diseno.md` §10, hoy en 0,00.
