# 0056. Objetivos de separación entre perfiles de jugador

**Fecha:** 2026-09-05

## Decisión del revisor, 2026-09-06: construir mal sale peor que no construir

La **ADR 0063** midió que dos de las directrices del revisor son incompatibles y le devolvió la elección.
La doctrina gastadora **sin ningún perk** gana el **49,79%** en el acto 2, así que pedirle 42-45% es pedir,
literalmente, que **construir mal salga peor que no construir**; y eso es lo contrario de "comprar siempre
compensa".

**El revisor elige que construir mal salga peor.** El objetivo 2 de esta ADR —build mediocre en **42-45%**—
**se mantiene tal cual**, y con él el hecho de que el mercado pueda salirte a deber si compras sin criterio:
es la misma idea que en cualquier roguelike de mazo, donde meter una carta mala es peor que no meter
ninguna. Encaja con la directriz original —*"tiene que premiar al que haga buenas builds pero castigar al
que haga malas"*— y **cierra AM-A y AN-A**.

La consecuencia hay que asumirla y está medida: **el suelo sin build no se hunde por la vía del castigo**,
porque el equipo que no compra no paga ninguno. El objetivo 4 (`suelo < 10%`) necesita una palanca que no
sea el catálogo, y la **ADR 0064** deja identificada cuál es la única viva: la run es el **producto de las
tres puertas de jefe** y los veinte partidos ordinarios no entran en él.

**Fecha original:** 2026-09-05
**Estado:** Aceptada (directriz del revisor). **Tras la ADR 0058 siguen sin alcanzarse, y el paquete mide
por qué los dos primeros no pueden alcanzarse a la vez** (`fase2-diseno.md` §27.6): "buena al 60%" exige un
rival ordinario más débil y "mediocre al 42-45%" lo exige más fuerte, y la capa de build del rival es un
solo número que mueve a las dos en el mismo sentido. Medido: buena 52,4/42,1, mediocre 46,1/34,4, la mala
completa la run el 9,92% y el hueco del acto 2 pasa de 6,81 a 6,28 puntos. Hace falta una palanca que
aumente el **recorrido del catálogo** entre construir bien y construir regular; la que se probó —que la
rareza compre cuota— no lo consigue porque las dos builds llevan casi la misma mezcla de rarezas (AK-B).
*(Estado anterior)* **Los cuatro objetivos seguían sin alcanzarse tras la P1** y dos empeoran (`fase2-diseno.md` §26.7): buena 56,8/53,0 (meta 60), mediocre 50,0/45,1 (meta 42-45), mala completa la run el 14,34% (meta <2%), buena la completa el 18,00%. El hueco entre buena y mediocre en el acto 2 se estrecha de 9,8 a 6,8 puntos. La causa es la misma que falsifica la ADR 0057 y está en AJ-B
*(Estado 2026-09-06, paquete AL / ADR 0060)*: **el objetivo de separación se alcanza por primera vez**. Con
el castigo del perk mal puesto pagándose en el equipo y la capa de build del rival ordinario bajada a 2/1/2,
el hueco entre build buena y mediocre en el acto 2 pasa de 6,27 a **10,03** puntos (ET 0,50, meta > 9,8) y la
build buena sube de 52,43 a **57,97**. Siguen fuera: el 60% de la build buena (faltan 2,0 y la capa del rival
está agotada, AL-B), la mediocre por arriba (47,94 frente a 42-45), la build mala completando la run (12,00%
frente a < 2%) y el suelo (10,66%). Detalle en `fase2-diseno.md` §28

*(Estado 2026-09-06, paquete AM / ADR 0061)*: **la última palanca de fuerza queda falsificada por
medición**. Bajar el peso de los atributos frente a la build —la P3 al revés, AL-B— no separa perfiles: los
atributos entran en el motor como una **diferencia**, así que curva de nivel, presupuesto de generación y
factores del motor son la misma operación y mueven a las dos builds a la vez. En siete condiciones de 600
runs el hueco se queda entre 9,7 y 10,4 mientras la tasa de victoria recorre veinte puntos. **Los seis
objetivos quedan exactamente donde los dejó la ADR 0060.** Y queda medido lo que hace falta: con la buena en
57,97 y la mediocre en 47,94, alcanzar 60 y 42-45 pide un hueco de **16,5 puntos**, un 65% más que el que
hay. Ninguna palanca de fuerza lo da; la única viva es **AL-A** (el recorrido del catálogo por canal).
Detalle en `fase2-diseno.md` §29

*(Estado 2026-09-06, paquete AO / ADR 0064)*: **la tercera salida de la ADR 0063 queda falsificada por
medición**. Encarecer la derrota de un partido ordinario (RF-002c) no separa perfiles porque **las tres
builds pierden el mismo número de partidos ordinarios**: 4,03 la buena, 4,16 la mediocre y 4,03 la que no
construye —pierde a mayor ritmo pero juega menos, y la truncadura de la run compensa—. Perder ya cuesta 44
de oro y 4 recompensas por run, el 34-39% de la economía, y las tres pagan la misma factura. Medido en cinco
magnitudes hasta "una derrota cuesta más de lo que paga ganar": el suelo se queda en 10,7 sin orden, la
build buena baja su tasa de victoria de la run y suben las derrotas del acto 1. **Los seis objetivos quedan
exactamente donde los dejó la ADR 0060.** Y queda la identidad que ordena lo que falta: **la tasa de
victoria de la run es el producto de las tres puertas de jefe** (17,00 = 0,7533·0,4425·0,5100) y los veinte
partidos ordinarios no aparecen en él. La única palanca viva para los objetivos 4 y 5 es **de puerta**:
cada una discrimina hoy 1,15 y hacen falta 1,26. Detalle en `fase2-diseno.md` §31

*(Estado 2026-09-06, paquete AP / ADR 0065 y ADR 0066)*: **la última palanca que quedaba queda falsificada
por medición, y la métrica de los objetivos 1 y 2 pasa a medir lo que esta ADR describe.** La ADR 0065
mide que la discriminación por puerta **no se puede comprar**: los cuatro tipos de modificador de jefe
borran build —dos de los tres que hay están inertes en los tres escalones que la tabla usa, y el que
muerde le cuesta 8,3 puntos a la celda `buena` y cero a la `correcta`—, así que lo único que queda es la
dificultad, que mueve a los dos perfiles a la vez. La frontera medida: **con la buena al 20% el suelo es
como mínimo 13,29%** y con el suelo al 10% la buena gana como mucho 15,63%; el punto de hoy (17,00 con
suelo 10,66) ya está sobre ella, **revisar la tabla de la ADR 0033 no la desplaza** y **un cuarto evento
filtro tampoco**. Lo que haría falta es un 62% más de separación en log-cuotas, que es catálogo (**AL-A**),
no jefe. Y la ADR 0066 —decisión del revisor— hace que `winRateAct{n}` mida **partidos ordinarios**, como
dice la tabla de abajo: build buena **60,33 / 43,30** (ET 0,85 / 0,73) frente a 57,97 / 44,43 con jefe, y
mediocre 50,42 / 38,65. **El objetivo 1 queda alcanzado en el acto 2** y sigue lejos en el 3; el hueco del
acto 2 pasa de 10,03 a **9,91** (ET 0,87) sobre un suelo de 9,8, y no se compensa. Detalle en
`fase2-diseno.md` §32

*(Estado 2026-09-06, paquete AQ / ADR 0067 y ADR 0068)*: **AL-A deja de ser una sospecha y pasa a tener
presupuesto, y el presupuesto no llega.** La ADR 0068 mide que existe **un** premio que la oposición no
puede cobrar y sólo uno —el **contador** (RF-070): `data/rivals/` no tiene dónde declararlo,
`BossDefinition.ToTeamSetup` genera el equipo del jefe desde cero y `ApplyCounterDeltas` sólo escribe en
la plantilla del jugador, así que los seis perks de acumulación escritos en rivales y jefes valen
`k⁰ = 1`—. Es el primer canal en nueve paquetes que cumple la condición (a) de la ADR 0064 **por
construcción**, y la build buena lo carga 1,7 veces más que la mediocre y 7 más que el suelo (15,82
contadores por run frente a 9,41 y 2,22). Medido en separación de log-cuotas contra el equipo sin build
(`S = Σ ln R`, hoy 0,884, necesaria 1,433): el eje vale hoy **0,072** y, subido al techo que la rareza
permite en los canales con recorrido, **1,132** — potencia **1,281** de la 1,622 que pide la frontera, el
**45% del camino**, con la run de la build buena en **19,50** y el suelo quieto en 10,83. **Y no se puede
gastar sin recalibrar los tres jefes**: el escalón `muy buena` de la ADR 0033 y la build de la run son el
mismo punto del eje (los dos con el contador a 5), así que al techo se salen cuatro celdas por arriba y la
dosis que las salva no compra tasa de victoria. Falsificadas de paso dos lecturas: **quitarle el catálogo
a la oposición baja `S`** (0,884 → 0,695) y **corregir el sesgo del instrumento antes de subir la magnitud
empeora la run** (17,00 → 15,75). Y la ADR 0067 sustituye el guardarraíl del acto 1 —`defeatShareAct1`,
que era una cuota— por `ordinaryDefeatRateAct1 ≤ 30`, hoy en **24,90**. **Los seis objetivos quedan
exactamente donde los dejó la ADR 0060.** Detalle en `fase2-diseno.md` §33

*(Estado 2026-09-06, paquete AR / ADR 0069)*: **por primera vez en diez paquetes la tasa de victoria de
la run sube sin que el suelo la siga.** El eje de acumulación sube al techo de rareza en **seis** de los
siete efectos con contador de los canales con recorrido y **no se toca ningún jefe**: run **17,00 →
19,42** (ET 1,26) con el suelo en **10,58** (ET 0,55), hueco del acto 2 **9,91 → 10,75** y separación
`S` 0,8933 → **1,1750**, que es **más** que la del techo completo de la ADR 0068 (1,1397) y con el suelo
más bajo. Las **doce celdas de la ADR 0033 quedan dentro de banda sin usar el margen de medida**, y la
`buena` del jefe final —clavada en su suelo de 40 desde la ADR 0049— pasa a **43,9**.

Y las dos mitades del plan que la ADR 0068 dejó escrito resultan falsas, las dos por la misma razón: **la
tabla de la ADR 0033 y la run no miran a los mismos perks**. De los siete efectos que el techo mueve, las
veinte builds del instrumento llevan **dos**; los otros cinco reproducen la tabla base al decimal, celda
a celda. El que saca las cuatro celdas de banda es `clean_sheet_legacy`, que la doctrina contextual compra
**2 veces en 1.200 runs** (AN-B). Y el guardarraíl de esta ADR —"si una celda se sale, recalibras el jefe,
nunca la tabla"— **no tiene salida cuando el jefe no puede**: dos puntos de calidad de `eternal_crown` le
cuestan 7,0 puntos a su celda `buena` y 0,8 a la `muy buena`, y ablandar los otros dos al límite que la
tabla permite mide **−1,58** de run (ET 1,42). La tercera salida es la **dosis del catálogo**, y es la que
se aplica. Los objetivos 2 y 3 siguen sin palanca; los 4 y 5 quedan a 0,6 cada uno y siguen siendo los dos
extremos de la misma frontera. Detalle en `fase2-diseno.md` §34

*(Estado 2026-09-06, paquete AS / ADR 0070 y ADR 0071)*: **paquete de medición, no de calibración, y lo
que mide es que parte de lo ganado era del instrumento.** Se corrigen los dos defectos que la ADR 0069
dejó abiertos —`--perk-values` era **estructuralmente ciego** al eje de acumulación y las veinte builds de
`data/balance/builds/` no muestreaban el catálogo que la run reparte— y aparece un tercero que nadie
buscaba: **la tabla de valor llevaba diez paquetes sin regenerarse** y la mitad de sus filas estaba
desfasada más de dos desviaciones. Con el control medido (mismo instrumento, arrastre de contadores
apagado), la atribución es exacta: **quitar la ranciedad cuesta 4,67 puntos de tasa de victoria de la run**
(19,42 → 14,75) y **corregir la ceguera al contador devuelve 1,50** (→ **16,25**), con `S` 1,168 → 0,524 →
**0,682**. La campaña mueve **61,4 unidades de media en los quince perks del eje y 0,0 en los otros 36**,
bit a bit; `clean_sheet_legacy` pasa de −42 a **+247** y la doctrina contextual lo compra en el 10,9% de
las runs en vez de en el 0,2%. Las veinte builds se reescriben contra el catálogo real y **las doce celdas
de la ADR 0033 quedan en banda sin usar el margen de ±2,5 y sin recalibrar ningún jefe**. La ADR 0071
cierra AR-B: el techo de un efecto con contador pasa a acotar la **línea** `k^maxValue` y a distinguir el
ámbito de equipo, y no cuesta nada. **Tres objetivos suben y tres bajan, y no se compensa ninguno**: el
hueco del acto 2 sube a **11,95** (el mejor en once paquetes), la mediocre baja a 47,60 (se pasa 2,6 en vez
de 6,3) y la mala a 10,50; bajan la run (**16,25**), el suelo (10,75) y la buena del acto 2 (59,55). Y
queda al descubierto lo que hay que mirar primero: **con la tabla fresca, la doctrina que elige por valor
medido no le gana a la que sólo acapara** (`contextualAdvantage` 4,17 → **−0,25**), porque su listón es el
cero exacto sobre una medida con desviación 17 (**AS-A**). Detalle en `fase2-diseno.md` §35

**Requisitos:** RT-055, RT-056, RF-032
**Depende de:** ADR 0054 (banda revisada) · **implementa** las P1 y P3 de la ADR 0050

## El problema, medido

Sobre 900 runs, partidos **ordinarios** (perder uno no termina la run, RF-002c):

| Perfil | Acto 1 | Acto 2 | Acto 3 | Run completada |
|---|---|---|---|---|
| Mediocre | 74,4% | 50,6% | 47,1% | **12,7%** |
| Buena | 77,5% | 53,2% | 50,5% | 19-22% |

**La separación en partidos es de tres puntos.** Construir bien casi no se nota partido a partido: se nota solo al acumularse en las puertas. Y una build mediocre completa la run una de cada ocho veces, que es demasiado para algo que no debería llegar.

## Objetivos

| Métrica | Hoy | Objetivo |
|---|---|---|
| Build **buena**, victoria en partidos **ordinarios** de los actos 2 y 3 (ADR 0066) | 50-53% | **60%** |
| Build **mediocre**, victoria en partidos **ordinarios** de los actos 2 y 3 (ADR 0066) | 50,6% / 47,1% | **claramente por debajo**, en torno al 42-45% |
| Build **mala**, completar la run | 12,7% | **menos del 2%** |
| Build buena, completar la run | 19-22% | **sin cambio**: 20-30% |

El motivo del primero, en palabras del revisor: *"es frustrante tenerlo todo planeado y no ganar"*. Con 60% en partidos, una build bien construida **domina el partido a partido** aunque la run siga siendo difícil — la tensión se traslada a las puertas, que es donde debe estar.

Y el último no cambia a propósito: la run sigue ganándose entre el 20% y el 30%, que es la banda coherente con la curva de la ADR 0033 y con lo que gana Slay the Spire de media sobre 240 millones de sesiones. **Lo que sube es cuánto se nota construir bien, no cuánto se gana.**

## Cómo: las dos correcciones que quedaban

No hace falta inventar nada. Las dos piezas pendientes de la ADR 0050 son exactamente palancas de "cuánto pesa la habilidad frente al azar":

- **P1, perks multiplicativos sobre cuotas.** Hoy un perk suma puntos porcentuales y su efecto depende de la base del canal; con cuotas, un perk vale lo mismo en cualquier canal y **el conjunto de la build pesa más y de forma predecible**. Es la palanca principal.
- **P3, curva de nivel más agresiva.** Del +22% actual entre el nivel 1 y el 8 al +39%. Premia sobrevivir y cuidar la plantilla, que es parte de construir bien.

La ADR 0054 ya subió la banda de `betterTeamWinRate` a 70-88 precisamente porque estas dos la habrían roto por hacer justo lo que se pretende. **Ese bloqueo ya está levantado.**

Para el objetivo de la build mala hace falta además que **los jefes de los actos 2 y 3 castiguen la falta de sinergia**, no solo la falta de piezas: sus modificadores ya invalidan ejes de construcción (ADR 0033), y una build sin línea clara debería quedarse sin respuesta ante ellos.

## Lo que hay que vigilar

- **La curva de puertas de la ADR 0033 no puede romperse**: hoy están las doce celdas en banda sin margen. Si al subir el peso de la habilidad se salen por arriba, se recalibran los jefes, nunca la tabla.
- **P1 y P3 no se aplican a la vez** (ADR 0050): juntas harían imposible atribuir un desajuste a su causa.
- Si `betterTeamWinRate` supera **88**, la habilidad domina y el azar deja de dar partidos: esa es la señal de que se ha ido demasiado lejos.
