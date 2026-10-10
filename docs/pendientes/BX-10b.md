# BX-10b — la barrera del saque no aparta al rival pegado a la línea de fondo

**Observación** (10 oct 2026, al mover las semillas con BX-10): `RestartClearanceTests`, semilla 121, falta a 1,24
casillas de la línea de fondo; el rival que estaba en x = 0 se queda a 1,24 del balón con la barrera en 2,0.

**Causa CONFIRMED por lectura y por el caso medido**: `MatchEngine.SlideAlongPitchToClear` (ADR 0142) sólo desliza
por el eje X, el de la **banda**: si acotar al campo deja al rival dentro de la barrera contra la línea de fondo, no lo
mueve por el eje Y y se queda donde lo deja el campo.

**Alcance**: el rival queda fuera del alcance real de Tackle/Block (1,2), que es lo que protege la barrera de BB-B, así
que no da entradas ilegales; es la barrera nominal la que no se cumple. `RestartClearanceTests` lo exime con cota (a
más de 1,23 y como mucho cuatro fotogramas en la muestra, para que si crece falle).

**Arreglo candidato**: deslizar por el eje que el campo no recortó, también el Y. Cuidado: el deslizamiento es un salto
de hasta ~1,6 casillas en un fotograma (lo que BA-K quitó del render); medirlo antes.
