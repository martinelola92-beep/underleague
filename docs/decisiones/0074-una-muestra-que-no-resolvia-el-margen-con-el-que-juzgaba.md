# 0074. Una muestra que no resolvía el margen con el que juzgaba

**Fecha:** 2026-09-06
**Estado:** Aceptada e **implementada**. Toca la **muestra** de `BossGateTests` (32 × 4 → 64 × 16, con las
doce celdas jugadas en paralelo) y **un número de un jefe** (`the_hunt.template.quality` 46 → 44).
**No toca la tabla de la ADR 0033, ni ninguna banda, ni el margen de ±2,5, ni ningún otro jefe, ni el
catálogo, ni la economía, ni la política de run**
**Cierra:** AT-B · **abre** AU-A, AU-C y AU-D (**AU-B** la abre la ADR 0075)
**Requisitos:** RF-001b, RF-002b, RF-032, RT-054, RT-055, RT-056, **RT-057**
**Relacionada con:** **ADR 0033** (la tabla, **intocada**), ADR 0040 (la densidad de cada celda),
ADR 0049 y ADR 0050 (las dos calibraciones anteriores de `the_hunt`), **ADR 0056** (que autoriza
recalibrar el jefe y prohíbe tocar la tabla), ADR 0065 (la frontera y los multiplicadores de cuota que
cada jefe admite), **ADR 0072** (que corrigió el mismo defecto en `FullRunGateTests`), **ADR 0073** (que
dejó AT-B abierta), **ADR 0075** (la frontera remedida, del mismo paquete)

## El defecto, que es el de la ADR 0072 un nivel más arriba

La afirmación más repetida del proyecto —«las doce celdas de la ADR 0033 están en banda»— se medía con
**640 partidos por celda** (32 plantillas × 4 partidos, semilla 1) y se juzgaba con un margen de **±2,5**
puntos. Medido sobre **doce semillas**, el error típico de una celda con esa muestra es **1,85 puntos**:

> El margen valía **1,4 desviaciones**. Una cota a 1,4 desviaciones no es una cota, es una moneda — que es
> literalmente lo que la ADR 0072 escribió sobre `deathsPerRun` y la muestra de 60 runs.

Y se cobró, igual que allí: con 640 partidos `the_hunt/buena` medía **62,66**, dentro de su banda 60-72.
Su valor real es **58,30**.

## 1. El tamaño de muestra, derivado

La varianza de una celda se descompone midiéndola a **dos** números de partidos por plantilla con las
mismas doce semillas (32 × 4 y 32 × 8, 24 estimaciones):

```
Var(celda) = A + B/m        A = varianza entre plantillas ya promediada por 5 razas × 32 plantillas
                            B/m = binomial, con m partidos por plantilla
medido:     A ≈ 0 (−0,60 ± 1,5)      B ≈ 16,1
```

Es decir: **la varianza entre plantillas no domina**. Con 5 razas × 32 plantillas ya está promediada, y lo
que queda es binomial con un factor de diseño de **1,15** (media de las 24 estimaciones de
`Var_observada / Var_binomial`). Eso tiene una consecuencia útil: la varianza escala como **1/N** sin
importar cómo se reparta N entre plantillas y partidos.

De ahí el tamaño, con el criterio escrito antes de medir: **el margen de ±2,5 tiene que valer tres
desviaciones**, para que una celda que se declare fuera lo esté con más del 99% de confianza (y para que
las 24 comparaciones de borde de la tabla no produzcan un falso positivo por azar: a 2σ la probabilidad de
que alguna salte es del 25%, a 3σ del 1,6%).

```
ET ≤ 2,5/3 = 0,83   →   N ≥ 1,15 · p(1−p) · 10⁴ / 0,83²  =  4.172 partidos/celda  (peor caso p = 0,5)
```

**Se adopta 64 plantillas × 16 partidos = 5.120 partidos por celda**, 61.440 en total. 64 es el máximo de
plantillas que admite `BossGateMetrics.BossRosterBlock` sin que dos razas compartan las plantillas del
jefe. Comprobado sobre **ocho semillas**: el error típico de una celda va de **0,20 a 0,89** y el margen de
±2,5 vale entre **2,8 y 12 desviaciones**. **El margen no se toca**: lo que estaba mal era la muestra.

**Y no cuesta lo que parecía.** Las doce celdas son independientes —el desplazamiento de semilla de partido
de cada una es exacto, `índice · plantillas · partidos`— así que se juegan en paralelo y el resultado es
bit a bit el mismo. La puerta pasa de **33 s a 1 m 38 s** con **ocho veces** la muestra; la suite completa
en Release, de 1 m 20 s a **3 m 10 s**.

## 2. Las doce celdas, y cuál estaba fuera

Ocho semillas × 5.120 partidos por celda = 40.960 por celda. «ET» es el de **una** muestra de 5.120, que es
lo que mide la puerta:

| jefe | incoherente | correcta | buena | muy buena |
|---|---|---|---|---|
| `grimhold_guns` | **29,31** ±0,69 [20-35] | **67,85** ±0,74 [65-80] | **79,39** ±0,45 [75-88] | **94,34** ±0,20 [85-95] |
| `the_hunt` (quality 46) | **12,31** ±0,42 [<15] | **37,67** ±0,63 [35-50] | **58,30** ±0,71 [60-72] **FUERA** | **80,95** ±0,66 [72-85] |
| `eternal_crown` | **5,09** ±0,25 [<10] | **23,08** ±0,68 [15-28] | **44,87** ±0,89 [40-55] | **62,69** ±0,80 [55-70] |

**Once de las doce estaban dentro. Una estaba fuera: `the_hunt/buena`, 1,70 puntos por debajo de su suelo**
(6,8 errores típicos del agregado; nunca llegó a 60 en ninguna de las ocho semillas, que van de 57,36 a
59,34).

**Y la otra celda que la ADR 0073 avisó no estaba fuera.** `grimhold_guns/muy buena` mide **94,34 ± 0,20**,
0,66 por debajo de su techo. El 95,35 / 95,00 de aquel aviso se reproduce **exactamente** —64 × 8, semilla
11, da 95,00 al decimal— pero es una desviación alta de una muestra de 2.560: la misma semilla 11 con
5.120 partidos mide **94,20**. Con 2.560 partidos el error típico de esa celda es 0,28 y el aviso estaba a
2,4 de ellos.

> De las dos celdas que la ADR 0073 dio por fuera, **una lo estaba y la otra era la muestra**. Que es,
> otra vez, el mismo defecto: 2.560 partidos tampoco resolvían ±2,5.

**La ADR 0073 no empeoró nada y arregló la mitad de esto.** Con las densidades **viejas** y la muestra
nueva (dos semillas, ET 0,14), `grimhold_guns/muy buena` mide **95,50** —fuera— y `the_hunt/buena` **57,87**, la misma cifra que
con las nuevas. El remedido de densidad **metió** la celda de `grimhold_guns` en banda y no movió la de
`the_hunt` ni una décima, exactamente como aquella ADR predijo.

## 3. La recalibración: `the_hunt` de quality 46 a 44

La ADR 0056 lo dice sin ambigüedad —*«si se salen, se recalibran los jefes, nunca la tabla»*— y este jefe
**sí puede** llegar: la ADR 0065 midió que admite ablandarse. Medido a tres calidades con la muestra nueva:

| `the_hunt` | incoherente [<15] | correcta [35-50] | buena [60-72] | muy buena [72-85] |
|---|---|---|---|---|
| quality **46** (hoy) | 12,31 | 37,67 | **58,30 FUERA** | 80,95 |
| quality **45** | 13,85 | 40,05 | **60,47** | 81,50 |
| quality **44** | **14,30** | 42,55 | **63,91** | 83,23 |

Y la pendiente **no es plana**, que es lo que hace posible la corrección: un punto de calidad mueve la
celda `buena` **2,9 puntos** y la `incoherente` sólo **0,8**, porque la primera está en la parte empinada
de la sigmoide y la segunda en la cola. La escalera `incoherente → buena` **se ensancha** al ablandar: 45,6
puntos a quality 46, 46,6 a 45 y **49,6 a 44**, contra los 45 que la fila del acto 2 exige (60 − 15).

**Se elige 44 por maximin sobre el margen más estrecho**, no por el resultado: con 45 el margen mínimo es
+0,47 en `buena` (0,7 errores típicos) y con 44 es **+0,70 en `incoherente`** (1,1 errores típicos). Con
44, las cuatro celdas de `the_hunt` quedan dentro y **las doce de la tabla** también.

### Lo que cuesta, medido, y por qué hay que decirlo

Banco de 1.200 runs por lado (300 × semillas 1/1001/2001/3001), mismo protocolo que §36:

| | control (quality 46) | **quality 44** | |
|---|---|---|---|
| Tasa de victoria de la run | 20,33 (ET 0,68) | **20,75** (0,57) | banda 20-30 ✔ |
| Suelo sin build | 10,92 (0,69) | **11,33** (1,23) | meta < 10, se aleja 0,41 (no significativo) |
| **`contextualAdvantage`** | **+3,83** (0,67) | **+2,50** (0,83) | **−1,33 emparejado, ET 0,24** |
| Hueco del acto 2 | 13,48 | **13,48** | intacto |
| Buena, actos 2/3 (ordinarios) | 61,10 / 49,40 | **61,10 / 49,22** | intacto |
| Mediocre, actos 2/3 | 47,59 / 35,87 | 47,59 / 35,14 | intacto |
| Mala completa la run | 10,50 | 10,58 | intacto |
| Puertas, buena | 71,58 · 46,57 · 61,15 | 71,58 · **49,59** · **58,73** | |
| Puertas, suelo | 71,42 · 35,24 · 44,71 | 71,42 · **38,86** · **42,77** | |
| `deathsPerRun` · `ordinaryDefeatRateAct1` | 1,42 · 25,23 | **1,45 · 25,23** | 1,5-3 · ≤30 ✔ |
| `masterDivergence` · `mastersReached` | 23,24 · 19,25 | 22,36 · 20,33 | ≥5 · 2-90 ✔ |

**La única cifra que se pierde es `contextualAdvantage`, y se pierde por una razón estructural, no por
azar**: ablandar una puerta sube más al perfil que está más abajo en la sigmoide, así que **comprime la
ventaja de construir**. Es la ADR 0065 leída al revés —*«la puerta amplifica el hueco; no lo crea»*— y la
amplificación se cobra cuando se afloja. La doctrina que sube es la **ahorradora** (16,50 → 18,25), no la
contextual (20,33 → 20,75). Sigue siendo positiva y a tres errores típicos de cero, pero **es el precio de
la celda y hay que verlo escrito**: entre una celda 1,70 fuera de la tabla y 1,33 puntos de ventaja de
doctrina, la ADR 0056 manda arreglar la celda.

## 4. Lo que la pregunta descubrió en la otra puerta

Con la puerta de jefes de fiar, la pregunta del encargo era si los umbrales calibrados contra la muestra
vieja siguen valiendo. **La puerta de fase 1 tiene el mismo defecto en una celda**, y esta vez el
diagnóstico es al revés: la muestra es buena y el número está pegado al borde. Medida a cinco semillas
(la puerta usa la 1):

| métrica | s1 | s2 | s3 | s4 | s5 | media | DT | umbral |
|---|---|---|---|---|---|---|---|---|
| `randomBuildNearNone_human_random` | 41,67 | 40,83 | **38,75 OUT** | 41,04 | 40,83 | **40,62** | 1,10 | 40-60 |
| `coherentBuildsBeatNone_orc_mob` (la más ajustada) | 66,88 | 70,83 | 66,67 | 69,79 | 70,62 | 68,96 | 2,04 | ≥ 58 |
| `buildsWinDifferently_passChain` | 1,23 | 1,28 | 1,26 | 1,27 | 1,23 | 1,26 | 0,02 | ≥ 1,11 |
| `buildsWinDifferently_injuries` | 2,09 | 2,34 | 1,63 | 1,91 | 1,87 | 1,97 | 0,27 | ≥ 1,5 |

**La puerta de fase 1 falla con la semilla 3.** Su cell de build aleatoria vale 40,62 con una desviación de
1,10 sobre un suelo de 40: **0,56 desviaciones de margen**, y pasa con la semilla 1 por suerte. Los demás
umbrales de esa puerta sí siguen valiendo, y con **más** holgura que cuando se calibraron (la más ajustada
a 5,4 desviaciones, contra las 2,9 de la medición de cierre de la fase 1b).

**No se toca**, y el motivo es que la salida no es de muestra. Subir la muestra haría que la celda pasara
siempre —en 40,6— pero **lo que dice el número es que una build aleatoria hoy es claramente peor que no
construir**, que es exactamente lo que el revisor eligió en la ADR 0056 (*«construir mal sale peor que no
construir»*) y lo contrario de lo que la banda 40-60 afirma. Bajar el suelo sería relajar un umbral para
que pase una puerta (RT-057) y subirlo sería lo mismo al revés. **Es una conversación de banda y es del
revisor: AU-A.**

## Decisión

1. **La muestra de `BossGateTests` pasa de 32 × 4 (640 por celda) a 64 × 16 (5.120 por celda)**, derivada
   del criterio «el margen de ±2,5 tiene que valer tres desviaciones» y de la varianza medida
   (`A ≈ 0`, factor de diseño 1,15). Las doce celdas se juegan en paralelo, con el desplazamiento de
   semilla fijado antes de empezar: **el resultado es bit a bit el de la versión en serie** (RT-020..024).
2. **`TolerancePercent` sigue siendo 2,5** y ninguna banda de ningún jefe se toca. Lo que estaba mal era la
   muestra.
3. **`the_hunt.template.quality` pasa de 46 a 44**, que es lo único que se toca del juego. Con ello **las
   doce celdas de la ADR 0033 quedan dentro de banda**, ninguna por el margen de medida.
4. **La tabla de la ADR 0033 no se toca.**
5. **`grimhold_guns` no se toca**: su celda `muy buena` no estaba fuera.
6. **La puerta de fase 1 no se toca** y su celda marginal se devuelve al revisor (**AU-A**).

## Qué falsificaría esta decisión

- **Que el factor de diseño 1,15 crezca.** Sale de 24 estimaciones sobre doce semillas y supone que la
  varianza entre plantillas ya está promediada. Si alguien sube las plantillas por encima de **64**, las
  razas empiezan a compartir las plantillas del jefe (`BossRosterBlock = 64`) y esa hipótesis deja de
  valer: el bloque habría que agrandarlo primero.
- **Que la pendiente de `the_hunt` cambie de forma.** El 2,9 contra 0,8 por punto de calidad es lo que
  permite mover `buena` sin sacar `incoherente`, y es una propiedad de dónde cae cada celda en la
  sigmoide. Si la escalera del catálogo se estrecha, el corredor del acto 2 —45 puntos exigidos contra
  49,6 medidos— deja de caber (**AU-C**).
- **Que `contextualAdvantage` sea un objetivo y no un diagnóstico.** Hoy no está en la tabla de objetivos
  de la ADR 0056; si lo fuera, esta recalibración habría que discutirla, no ejecutarla.
