# 0054. La banda de `betterTeamWinRate` se revisa antes de aplicar P1

**Fecha:** 2026-09-05
**Estado:** Aceptada e **implementada** al aplicar la P1 (`fase2-diseno.md` §26.5). Medido tras la P1: **79,52**, el mismo valor que antes, porque los equipos de referencia no llevan perks; la banda nueva no se roza
**Modifica:** el rango de `betterTeamWinRate` fijado en la fase 0 (RT-056/RT-057)
**Bloquea:** la P1 de la ADR 0050 hasta que se aplique

## El aviso

Con las dos tiradas promediadas aplicadas, `betterTeamWinRate` con una diferencia de calidad de 20 ha pasado de **72,97 a 79,52**, a medio punto del techo de su banda **65-80**.

Esa banda se fijó en la fase 0, cuando **todas las resoluciones eran lineales y de varianza máxima**. Medía algo razonable entonces: *"los equipos mejores ganan más, con sorpresas creíbles"*. Pero el número que la satisface depende de cuánto pese la habilidad en el sistema, y **todo lo que estamos haciendo sube ese peso**.

**El siguiente cambio que suba el peso de la habilidad rompe la puerta**, y las dos que quedan de la ADR 0050 son exactamente eso: la P1 (perks multiplicativos sobre cuotas) y la P3 (curva de nivel más agresiva). Aplicarlas contra la banda actual haría fallar el build por hacer justo lo que se pretendía.

## Decisión

La banda pasa de **65-80 a 70-88**.

El techo no es arbitrario: el límite superior existe para que **el peor equipo pueda ganar**. Con 88, un equipo veinte puntos peor todavía gana **una de cada ocho veces**, que sigue siendo una sorpresa creíble en un deporte donde el peor gana partidos. Por debajo de eso, la ventaja de construir mejor no se puede expresar; por encima de 90 el resultado se vuelve determinista y el partido deja de tener interés.

El suelo sube de 65 a 70 por el mismo motivo: si veinte puntos de calidad no dan al menos siete de cada diez, el sistema no premia mejorar la plantilla.

## Lo que esto no es

**No es relajar una puerta para que pase.** La puerta seguirá fallando si el mejor equipo gana menos de lo que debería o si el resultado se vuelve determinista. Lo que cambia es dónde está la frontera, porque el sistema que mide ha cambiado de naturaleza: comparar un motor que premia la habilidad contra una banda calibrada para uno que la diluía es comparar contra la referencia equivocada.

Se registra aquí y no en silencio precisamente porque RT-057 lo exige: *un cambio de rango es una decisión explícita, nunca un ajuste silencioso*.

## Consecuencias

- Se aplica **antes** de la P1. Con la banda nueva, P1 y P3 se pueden medir sin que la puerta salte por el motivo equivocado.
- Si tras P1 y P3 el valor supera **88**, entonces sí hay un problema real: la habilidad domina y el azar deja de dar partidos. Esa es la señal que la puerta debe seguir vigilando.
- `docs/balance.md` y la puerta estadística se actualizan con el rango nuevo.
