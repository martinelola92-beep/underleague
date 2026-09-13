# ADR 0104 · La referencia de fase 1 lleva perks

**Fecha:** 13 de septiembre de 2026 · **Estado:** aceptada, **sin cerrar CAT-E** · **Decisión del revisor**
**Toca:** `data/balance/groups.json` (`baselineByRace`), `data/balance/builds/<raza>_neutral.json`, D-34, RT-055

## Qué se decide

La puerta de fase 1 deja de medirse contra `<raza>_none` —una plantilla con **cero** perks— y pasa a
medirse contra `<raza>_neutral`, que lleva **catorce**, los mismos que cualquier build (RF-023, dos slots
por titular), elegidos para no formar ninguna build.

El motivo es **D-34**: comparar catorce perks contra cero no mide equilibrio, mide **cuánto vale el
catálogo entero**. Por eso RT-055 —«ninguna build catalogada supera el 70 % ni baja del 30 %»— no se ha
cumplido nunca: las nueve coherentes ganaban entre el 68,3 % y el 86,3 % a una plantilla desnuda, y eso no
dice nada sobre si están bien construidas.

## La regla de selección, declarada y reproducible

No se eligieron a mano. `tools/build-neutral-reference.py` las recalcula de
`data/economy/perk-values.json` (el valor medido de cada perk contra su **control emparejado**, ADR 0087):
los **14 perks cuyo valor está más cerca de la mediana** del catálogo elegible, con desempate por id
(RT-041, determinista), descartando lo que no sería neutral:

| se descarta | por qué |
|---|---|
| habilidades raciales | no ocupan slot |
| bloqueados por raza, posición o etiqueta | en un portador cualquiera se desperdician |
| letales | la letalidad es una **elección de build**, no el estado neutro |
| maestros | exigen dos perks de su línea, que es coherencia |
| rompe-reglas | no son «lo normal» |
| los que dependen de `linked()` | dependen de la **colocación**, que es justo lo que la puerta mide |

Con tope de **dos por familia**, para que ninguna línea acumule cuota sobre el mismo canal. Alineación por
defecto, sin estilos ni rasgos: la diferencia con una build coherente tiene que ser **la build**.

Resultado: 14 perks que suman **48** milésimas frente a una mediana ideal de 77. Pegados al centro.

## Y no arregló las dos puertas rojas. Lo que eso enseña

| | contra `_none` (0 perks) | contra `_neutral` (14 perks) |
|---|---|---|
| `badBuildsLoseToNone_human_scattered` | 58,12 | **59,58** |
| `randomBuildLosesToNone_human_random` | 56,88 | **55,42** |

Techo: 45. **La referencia no era la variable**: cambiarla de cero a catorce perks mueve el número menos de
dos puntos. Así que la hipótesis con la que se tomó esta decisión —que las puertas estaban rojas por culpa
de la referencia— **queda falsada por la medida**, y se anota como tal.

**Lo que sí encontró la investigación.** `human_scattered` está construida **entera con perks que dependen
de vínculos direccionales** —`covering_shadow`, `pivot_duo`, `wing_overlap`, repetidos— colocados de forma
que **ninguno resuelve** (verificado por geometría sobre la alineación de seis filas: cero de las seis
relaciones). Su diseño era «catorce perks que no se disparan». Y eso funcionaba como build *mala* mientras
un perk que no cumplía su condición aplicaba `elseEffects` **negativos**.

Desde el **paquete AY y la ADR 0088** —«ningún perk es negativo», la rama `else` no hace nada— un perk que
no se dispara **no castiga: no hace nada**. Es decir: **una build mala ya no puede ser mala, como mucho
puede ser neutra**, y neutra contra neutra es una moneda al aire. El criterio de fase 1 «las builds malas
pierden, ≤ 45 %» pasó a ser **insatisfacible por construcción** el día que se aceptó la ADR 0088, y nadie
lo notó porque la colocación mala aún restaba lo suficiente para colar el número por debajo del techo.

Las seis filas de la ADR 0103 no crearon esto: movieron un número que ya estaba al borde.

## Por qué se conserva el cambio aunque no cierre las puertas

Porque **la crítica de D-34 sigue siendo cierta** con independencia de estas dos puertas: contra una
plantilla desnuda, cualquier cosa gana. La referencia nueva hace que las métricas de fase 1 pregunten «¿se
nota construir bien frente a construir del montón?» en vez de «¿vale algo el catálogo?».

**Coste que hay que tener presente: los números de fase 1 anteriores al 13 de septiembre de 2026 no son
comparables con los posteriores.** Cambia la vara, no solo la lectura.

## Lo que queda abierto

**CAT-E no se cierra.** Lo que hay que decidir ahora es **qué significa «build mala» en un juego donde
ningún perk es negativo** (ADR 0088). Tres salidas, y ninguna es un ajuste de rango:

1. **Que la build mala lo sea por colocación y por canal**, no por perks inertes: perks que sí se disparan
   pero sobre los canales que no deciden ese partido. Es la que conserva el criterio tal como está escrito.
2. **Bajar el criterio**: si lo peor que puedes hacer es no hacer nada, el techo de una build mala no es el
   45 % sino el 50 %, y hay que decirlo en RT-055 en vez de fingir que se puede bajar de ahí.
3. **Reabrir la ADR 0088**: que exista de nuevo una forma de construir *en contra*. Es la más cara y toca
   una decisión del revisor que ya se tomó una vez.

Detrás de las tres está **AL-A**, que sigue siendo la raíz: el recorrido de un perk lo fija la base de su
canal, no su magnitud, y por eso el perk *mediano* del catálogo vale casi nada (mediana 5,5 frente a 104
del mejor no letal) y una referencia «mediana» se parece demasiado a una sin perks.
