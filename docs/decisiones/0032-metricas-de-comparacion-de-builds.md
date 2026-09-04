# 0032. El punto de comparación de las builds: RT-055 y la métrica de progresión

**Fecha:** 2026-09-05
**Estado:** **Propuesta: decisión del revisor** (cambia el criterio de RT-055, que RT-057 protege)
**Requisitos:** RT-055, RT-056, RT-057, RF-023, RF-032

## Contexto

Dos métricas del proyecto miden contra un punto de comparación equivocado, y el reajuste de la fase 1 lo ha dejado en evidencia con datos.

**RT-055** hace fallar el build si una build catalogada supera el 70% o baja del 30% contra el conjunto de referencia. Medido: las nueve builds coherentes ganan entre el **68,3% y el 86,3%** a `human_none`. No están rotas. La referencia es una plantilla con **cero** perks, y una build con los siete titulares equipados lleva **catorce** (RF-023 da dos slots por común). Comparar un equipo completo contra uno desnudo no mide equilibrio entre builds: mide cuánto vale el catálogo entero, que es un número interesante pero distinto.

**`scalingRewardsGoodBuilds`**, en su segunda mitad, pide que las builds malas caigan al menos 15 puntos entre la primera y la segunda mitad de una campaña con rivales de calidad creciente. Medido con un control: las malas caen entre 0 y 2,5 puntos, y **quien más cae es el equipo sin ningún perk (−13,75)**. Una build mala cae menos que la referencia porque hasta ella lleva algún perk que funciona, y porque un perk mal colocado es un **malus estático**: cuesta lo mismo en el partido 1 que en el 8.

## Decisión propuesta

1. **RT-055 compara contra una build de referencia equipada, no contra una plantilla desnuda.** Se añade a `data/balance/builds/` una build neutra por raza —los mismos catorce slots llenos con perks de relleno sin sinergia— y es esa la referencia del criterio 30-70%. La comparación contra `*_none` se conserva como métrica informativa, porque responde a otra pregunta legítima: cuánto aporta el catálogo.
2. **La progresión se mide como distancia a la referencia, no como caída absoluta.** El número que importa no es cuánto cae una build cuando el rival mejora —todas caen—, sino si la **separación** entre una build buena y la referencia **crece** con el tiempo. Medido, esa separación crece de 13,3 a 23,8 puntos para `human_wall`, que es exactamente el comportamiento que se quería demostrar. La métrica pasa a exigir que la distancia de las coherentes crezca y la de las malas no.

## Alternativas descartadas

- **Ensanchar la banda de RT-055** hasta que quepan los valores actuales: esconde el problema en vez de arreglarlo, y deja la puerta sin capacidad de detectar una build realmente rota.
- **Forzar que las builds malas decaigan** con `elseEffects` de malus creciente: se puede hacer, pero fabricaría el número en vez de medirlo. El mecanismo que de verdad hará decaer a una build mala es el **desgaste persistente** de la fase 2 (lesiones que se acumulan, plantilla que se erosiona, RF-090..094): una build mala pierde jugadores más deprisa y ahí sí se hunde sola.

## Consecuencias

- Si se acepta: hay que crear las builds neutras de referencia, cambiar el criterio en `Sim/Analysis/BuildMetrics.cs` y en las puertas, y actualizar `docs/balance.md`. RT-055 se reescribe en `requisitos.md` (v0.9.2).
- Si se rechaza: las puertas de fase 1 seguirán en verde porque miden contra la referencia de la propia raza, pero RT-055 quedará como un criterio que no se cumple y que nadie puede cumplir, que es peor que no tenerlo.
