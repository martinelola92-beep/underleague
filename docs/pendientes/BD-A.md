# BD-A — El reparto de los penaltis por fila (el sesgo arriba/abajo resultó ser ruido)

Estado: **abierta y reducida** (la hipótesis que la abrió está REJECTED desde la segunda medición). Encontrada el 20 sep 2026 por el `independent-reviewer` dentro de la medición de la
**ADR 0121** (área del portero centrada), no jugando: es un hallazgo de dato, el sexto del patrón que ya
señala `docs/project-state.md` («el texto promete lo que el dato o el código no hacen»).

## Síntoma

En el *behavioral audit* de la ADR 0121 (300 partidos, semilla 777, los dos equipos sumados), las faltas
señaladas como **penalti**, por fila del campo (0 a 6):

| | fila 0 | 1 | 2 | 3 | 4 | 5 | 6 |
|---|---|---|---|---|---|---|---|
| antes de la ADR 0121 | 0 | 10 | **5** | 6 | **14** | 1 | 0 |
| después | 0 | 6 | **5** | 6 | **15** | 7 | 0 |

Las filas **1 y 5** eran la asimetría que la ADR 0121 arregla (el área no estaba centrada). Las filas **2 y 4
no**: las dos están enteras dentro del área antes y después, y el portero pasa en ellas prácticamente los
mismos fotogramas (5.660 contra 5.765 antes; 5.714 contra 5.863 después). Aun así, **se pitan tres veces más
penaltis en la fila 4 que en la 2**, y el cambio del área no lo mueve.

Es **mayor** que la asimetría que la ADR 0121 corrigió, y contradice la premisa que esa ADR da por supuesta:
«nada del diseño dice que atacar por abajo sea distinto de atacar por arriba».

## Segunda medición (20 sep 2026, 3.000 partidos): **el desequilibrio se invierte**

El test `PenaltyAreaSymmetryTests` (3.000 partidos, `RngStreams.MatchSeed(777, i)`, mismo emparejamiento de
referencia) recoge 126 penaltis y los reparte así: fila 1 **12** · fila 2 **67** · fila 4 **21** · fila 5 **26**.

Con diez veces más muestra, **la fila 2 triplica a la 4** — exactamente lo contrario de lo que medían los 300
partidos (5 contra 14). Un sesgo estructural del campo no cambia de signo al ampliar la muestra; el ruido sí.
La hipótesis 4 pasa a ser la principal y el síntoma, tal como se describió, queda **LIKELY ruido**.

Lo que sigue abierto, y ya no es la asimetría arriba/abajo: **por qué las filas 2 y 4 concentran los penaltis
frente a las 1 y 5** (88 de 126 en las dos centrales). Eso puede ser geometría razonable —se entra más cerca
del centro de la portería— o no; nadie lo ha mirado.

## Estado epistemológico

- **REJECTED como sesgo arriba/abajo**: con 3.000 partidos el desequilibrio cambia de signo (ver arriba). Lo
  que se midió con 300 partidos era ruido de muestra, no una regla oculta.
- **Abierto**: la concentración en las filas centrales (88 de 126) frente a las de borde.
- **REJECTED**: que lo cause el área descentrada (la ADR 0121 la centró y el desequilibrio sigue igual).
- **REJECTED**: que lo cause el dominio del portero (los fotogramas por fila son casi idénticos entre las
  dos filas).

## Hipótesis a ordenar por coste × poder discriminativo

1. **El espejado de equipos solo invierte la columna**, no la fila (`MatchEngine.cs:418`): un sesgo en filas
   **se suma** entre los dos equipos en vez de cancelarse. Es el candidato más barato de comprobar: mirar la
   distribución **por equipo** en vez de sumada.
2. Asimetría en la colocación por defecto o en las zonas de acción (la formación por defecto usa filas 2 y 4
   simétricas respecto a 3, pero las zonas y los márgenes pueden no serlo).
3. Asimetría en la IA de persecución o de entrada según el signo de la coordenada Y.
4. Ruido: con 36-39 penaltis en total, 5 contra 14 podría ser una muestra corta. Lo discrimina repetir con
   cuatro semillas más, que es lo primero que hay que hacer.

## Por qué importa

Un penalti es la consecuencia más cara de una entrada, y la previsibilidad (RF-012d, regla 11) se apoya en
que el riesgo sea el mismo en sitios equivalentes del campo. Si atacar por una banda es sistemáticamente más
caro, es una regla oculta que nadie ha decidido.
