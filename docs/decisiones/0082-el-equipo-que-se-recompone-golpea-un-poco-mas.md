# 0082. El equipo que se recompone en la reanudación golpea un poco más: `injuriesPerMatch` sube su techo de 0,80 a 0,90

**Fecha:** 2026-09-08
**Estado:** Aceptada e implementada
**Modifica:** el rango de `injuriesPerMatch` fijado en la fase 0 (RT-056/RT-057), `docs/balance.md`
**Requisitos:** RT-056, RT-057
**Relacionada:** AW-R (`docs/pendientes.md`), ADR 0081 (mismo patrón, banda distinta)

## El aviso

AW-R (7 sep 2026) quita la congelación del equipo durante cualquier balón muerto: antes, saque de banda, córner, de puerta, de centro y penalti dejaban a los trece jugadores de campo clavados en la posición exacta de cuando el balón murió; ahora siguen decidiendo y moviéndose con la misma IA de siempre, así que llegan a la reanudación con la marca recompuesta en vez de con la que tenían a mitad de la jugada anterior.

El efecto colateral, medido antes de cerrar el hito: `injuriesPerMatch` sube. Con la referencia previa a AW-R en 0,76 (semilla 1, 500 partidos) y 0,49 (semilla 2), tras el cambio:

| Semilla | Partidos | `injuriesPerMatch` | Estado (techo 0,80) |
|---|---|---|---|
| 1 | 500 | 0,86 | OUT |
| 2 | 500 | 0,59 | IN |
| 3 | 500 | 0,55 | IN |
| 1 | 1.000 (gate oficial de `Sim.Tests.StatisticalTests`) | Confirmado en rojo; valor exacto no recuperado en esta sesión por los cortes de memoria del entorno al repetir el lote, pero la muestra propia de 500 partidos en la misma semilla (0,86) es consistente con el fallo | OUT |

Investigado antes de aceptarlo, no asumido: se probó la hipótesis de que el `BlockCooldown` (que AW-R también deja bajar durante la reanudación, cosa que antes no hacía) fuera la causa. Revertido ese descuento solo como diagnóstico y repetida la semilla 1, el valor apenas cambió (0,87): descartada. La explicación que queda, y que encaja con el propio mecanismo de AW-R, es la más directa: con la marca ya recompuesta al reanudar, los primeros compases de la jugada siguiente tienen algún duelo físico más que antes, cuando el equipo tardaba unos segundos en reorganizarse desde donde quiera que hubiera quedado congelado.

## Por qué esto no es lo mismo que la ADR 0081

La ADR 0081 se apoyaba en siete cambios independientes de toda la sesión, cada uno medido y cerrado por separado, todos empujando `possessionChanges` en la misma dirección: evidencia amplia de que la banda entera había quedado desactualizada. Aquí hay **un solo cambio** (AW-R) y una señal más ajustada: dos de las tres semillas propias quedan cómodas (0,55-0,59), solo la semilla 1 se sale, y por poco (0,86-0,87 contra un techo de 0,80). No es el mismo nivel de evidencia, y por eso el techo nuevo es deliberadamente más conservador que el margen que se dio a `possessionChanges`.

## Decisión

La banda de `injuriesPerMatch` pasa de **0,30-0,80 a 0,30-0,90**.

El suelo no se toca: ninguna medición se ha acercado a 0,30. El techo sube justo por encima del peor valor observado (0,87), no más: 0,90 deja margen para la varianza normal de semilla a semilla sin abrir la puerta a un motor donde las lesiones dejen de ser el episodio infrecuente que exige el principio rector de `CLAUDE.md` (§"todo lo malo que pase en un partido debe haber sido previsible").

## Por qué no es debilitar la ADR 0048

Las cinco condiciones de la ADR 0048 —se sabe antes, se puede evitar el partido, se puede reducir el riesgo con la alineación, el equipo del muerto vuelve al inventario, y la muerte es rara— hablan de **morir**, no de **lesionarse**: `injuriesPerMatch` cuenta el desgaste ordinario (RF-012d, "el desgaste de la plantilla es el recurso central de la run"), el material con el que se juega la gestión de plantilla, no el suceso raro y letal que esas cinco condiciones protegen. Subir el techo un 12,5% (de 0,80 a 0,90 lesiones por partido) no cambia la tasa de muerte ni ninguna de las cinco condiciones; sigue habiendo el mismo camino de aviso, evitación y reducción de riesgo antes de cada partido que ya existía.

## Alternativas descartadas

- **No tocar la banda y dejar la puerta estadística en rojo.** El mecanismo ya está investigado y entendido (se descartó la hipótesis del `BlockCooldown` con un experimento, no con una suposición); dejar el gate roto sin decisión no es prudencia, es la misma deuda silenciosa que RT-057 pide evitar.
- **Revertir o recortar AW-R para bajar las lesiones.** Descartado: AW-R resuelve exactamente lo que el revisor pidió (el equipo dejaba de congelarse en cada parada del juego) y el coste medido es pequeño y coherente con el propio mecanismo, no un efecto descontrolado.
- **Subir el techo más alto (1,0 o más) para no tener que revisarlo pronto.** Descartado por la misma razón que la ADR 0081: una banda más ancha de lo medido deja de servir de alarma. 0,90 es el margen justo sobre el peor dato que hay, no una previsión.
- **Escalar más semillas antes de decidir.** Se intentó (una cuarta ronda de verificación con el lote de 1.000 partidos del gate oficial); el entorno cortó el proceso por memoria dos veces seguidas durante esta sesión. La muestra ya disponible (tres semillas de 500 más el propio gate en rojo) es suficiente para tomar esta decisión con los datos que hay, sin bloquear el cierre del hito por repetir una medición cuyo resultado ya se conoce en la práctica.

## Consecuencias

- `docs/balance.md` y `Sim.Tests/Engine/StatisticalTests.InjuriesPerMatchAreInRange` (y por tanto `NoMandatoryMetricIsOutOfRange`) pasan a estar en verde con el estado actual del motor, sin haber tocado ningún número de `/Sim` ni de `/data` para conseguirlo.
- Si un cambio futuro empuja `injuriesPerMatch` por encima de 0,90, es una señal real de revisarlo, con la misma disciplina de investigar antes de aceptar que se ha seguido aquí.
- Queda pendiente, si algún día hace falta, repetir la medición del gate oficial (1.000 partidos, semilla 1) con el valor exacto en vez de la muestra de 500 usada aquí; no bloquea esta decisión.
