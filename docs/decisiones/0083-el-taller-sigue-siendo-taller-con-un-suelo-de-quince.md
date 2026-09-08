# 0083. El taller sigue siendo taller con un suelo de quince: la celda incoherente del primer jefe pasa de 20-35 a 15-35

**Fecha:** 2026-09-08
**Estado:** Aceptada e implementada (decisión del revisor)
**Modifica:** la fila del jefe del acto 1 de la tabla de la ADR 0033, solo en su celda **incoherente** (`data/bosses/grimhold_guns.json`, `gate.targets.incoherent.min`)
**Requisitos:** RT-056, RT-057
**Relacionada:** ADR 0033 (la tabla), ADR 0043 (trampolín), ADR 0074 (la palanca de calidad del jefe), AU-C (la escalera que no cabe)

## El aviso

Cerrado el paquete de motor de la primera partida jugada (AW-A a AW-T: intercepción del disparo tick a tick, bloqueo de tiros, techo de la línea defensiva, persecución como precondición, reposicionamiento durante el balón muerto), la curva de puertas de la ADR 0033 tiene **una** celda fuera de doce, medida a la muestra que la ADR 0074 derivó (64 plantillas × 16 partidos por celda, 61.440 partidos, semilla 1):

| Celda | Antes del paquete (¼ de muestra) | Ahora (muestra completa) | Banda |
|---|---|---|---|
| `grimhold_guns` · incoherente | 28,0 | **17,5** | 20-35 |
| `grimhold_guns` · muy buena | 95,0 | 94,0 | 85-95 |
| las otras diez | dentro | dentro | |

El error típico de la celda a muestra completa es de medio punto: 17,5 está a cinco desviaciones del suelo. Y el desplazamiento es del motor, no de la tabla: con el mismo lote y la misma semilla en el commit anterior al paquete, la celda daba 28,0. La señal es coherente en los tres jefes —la build incoherente pierde más en todos (28 → 16, 16 → 7, 4 → 2 a un cuarto de muestra)— y es exactamente lo que la ADR 0033 pide de verdad: que construir mejor se note. El motor de ahora separa más a las builds buenas de las malas; lo que no cabe es la anchura de esa separación en la fila del primer jefe.

## Por qué la palanca de la ADR 0074 no sirve aquí

La ADR 0074 recalibró `the_hunt` bajando su calidad de 46 a 44 cuando una celda se cayó. Aquí esa palanca está bloqueada: la celda **muy buena** del mismo jefe está en 94,0 con techo 95. Cualquier rebaja de calidad que suba la incoherente cuatro o cinco puntos revienta la otra. La escalera de `grimhold_guns` mide ahora 76,5 puntos de la incoherente a la muy buena y la fila admite como mucho 75. Es el caso de **AU-C** —"el catálogo produce saltos más grandes de lo que la tabla admite"— en la fila de al lado.

## Decisión

El suelo de la celda incoherente del primer jefe baja de **20 a 15**. El techo (35) y las otras tres celdas de la fila no se tocan.

Quince sigue siendo el taller de la ADR 0033: *"el acto 1 es el taller, no el examen; hasta una incoherente se cuela a veces"*. Una de cada seis builds sin sentido pasa el primer jefe igual, y el trampolín de la ADR 0043 sigue ahí para quien pasa. Lo que cambia es que la incoherente ya no pasa una de cada cuatro, que es lo que 20-35 permitía sobre un motor que no bloqueaba tiros ni recomponía la marca en cada balón muerto.

## Alternativas descartadas

- **Debilitar el motor para que castigue menos a las builds malas.** No hay ninguna palanca que castigue *solo* a la incoherente: los cambios del paquete son realismo genérico del partido, y revertirlos en parte devolvería los fallos que el revisor vio jugando.
- **Bajar la calidad de `grimhold_guns`.** Bloqueado por la celda muy buena, como se explica arriba.
- **Bajar el suelo a 10 o quitarlo.** Descartado: 15 es el mínimo que sigue cumpliendo la frase de la ADR 0033 con un margen medido de 2,5 puntos (cinco desviaciones), y la vigilancia de la celda sigue teniendo sentido por arriba y por abajo.

## Consecuencias

- `Sim.Tests.Analysis.BossGateTests.TheGateCurveMatchesTheAdr0033Table` vuelve a verde sin tocar ningún número del motor ni del jefe.
- Si un cambio futuro empuja la celda por debajo de 15, o la muy buena por encima de 95, la fila del primer jefe hay que replantearla entera (anchura, no un número), como AU-C ya anticipa para el acto 2.
