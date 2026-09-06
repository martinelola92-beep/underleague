# 0069. El eje de acumulación se gasta donde la tabla mira, no donde la run gana

**Fecha:** 2026-09-06
**Estado:** Aceptada e **implementada**. **Primer paquete en diez que mueve la tasa de victoria de la run
sin mover el suelo**: 17,00 → **19,42** con el suelo en **10,58**. Toca seis números de `data/perks/` y
**ningún jefe**
**Implementa:** AQ-A (la primera mitad) · **cierra** AQ-A como palanca y **corrige** su segunda mitad
**Requisitos:** RT-054, RT-055, RT-056, RT-057, RF-070, RF-032
**Relacionada con:** ADR 0033 (la tabla), ADR 0050 (P1), ADR 0056 (objetivos y el guardarraíl de
recalibración), ADR 0058 (el techo por rareza), ADR 0060 (los canales con recorrido), ADR 0063,
ADR 0064, ADR 0065 (la frontera y la decisión del revisor), ADR 0067, ADR 0068 (el eje)

## De dónde venía el encargo, y en qué se equivocaba

La ADR 0068 midió que el **contador** (RF-070) es el único premio que la oposición no puede cobrar y dejó
el paquete siguiente especificado con números: subir el eje **al techo de rareza** en los canales con
recorrido **y recalibrar los tres jefes a la vez**, porque al techo se salen **cuatro celdas** de la ADR
0033 por arriba. El encargo daba por medido que `eternal_crown` ganaba por primera vez margen para
endurecerse, que `the_hunt` necesitaba unos 4 puntos y que `grimhold_guns` no podía endurecerse.

**Las dos mitades del plan resultaron falsas, y por la misma razón**: la tabla de la ADR 0033 y la run no
miran a los mismos perks.

> **De los seis efectos con contador que el techo mueve, la tabla sólo ve dos** —`battle_reader` y
> `clean_sheet_legacy`—, **y uno de esos dos la run no lo compra nunca.** Todo el desbordamiento de las
> cuatro celdas venía de ahí; ninguno de los cuatro restantes mueve una sola décima de la tabla.

## 0. El banco, y que vuelve a reproducir la ADR 0068 al decimal

Mismo protocolo que §28.8 en adelante: 1.200 runs por perfil (300 × semillas 1/1001/2001/3001),
contextual = "buena", gastadora = "mediocre"/"mala", el suelo con `economy.rewardPerkWeight = 0` y la
política que esquiva mercados. Las tres puertas salen exactas de `BossWinsByAct / BossSamplesByAct`
(ADR 0067). Las doce celdas se miden con las **dos** muestras del proyecto: la de la puerta de
`Sim.Tests` (32 plantillas × 4 partidos = 640 por celda, la que decide) y la sonda de `--boss-gate`
(25 × 8 = 1.000 por celda, la de los experimentos).

Reproducidas al decimal antes de tocar nada: base `alB2`/`alF2` 75,33 · 44,25 · 51,00 y 70,75 · 35,22 ·
43,99, run 17,00 (ET 1,29), suelo 10,66 (ET 0,56), hueco del acto 2 **9,91**; y la condición "eje al
techo" de la ADR 0068 (`accmax`), 19,50 (ET 0,80) con suelo 10,83 y las doce celdas con **cuatro fuera**.

La unidad sigue siendo la separación en log-cuotas `S = Σ ln R_n`, con `R_n` medida contra el suelo:
**base S = 0,8933**.

## 1. Qué ve la tabla de la ADR 0033, perk a perk

Los quince perks con `accumulatesAcrossMatches` no están repartidos por igual entre el instrumento y el
juego. Contados sobre las veinte builds de `data/balance/builds/` que la tabla usa, y sobre las 1.200
runs de la doctrina contextual:

| perk | en `*_incoherent` | `*_correct` | `*_good` | `*_excellent` | runs que terminan con él (de 1.200) |
|---|---|---|---|---|---|
| `clean_sheet_legacy` | — | **5/5** | **5/5** | **5/5** | **2** |
| `battle_reader` | — | — | **5/5** | **10** (dos por raza) | 317 |
| `silky_veteran` | — | — | — | **10** | 514 |
| `poacher_instinct` | — | — | — | 5/5 | 24 |
| `lane_reader` · `captains_voice` · `deathless_march` · `pit_veteran` | — | — | — | — | 41 · 287 · 41 · 31 |

Y se comprueba desmontando la condición del techo un perk cada vez (muestra de la puerta, 32 × 4):

| condición | `grimhold` buena / muy buena | `the_hunt` | `eternal_crown` |
|---|---|---|---|
| hoy | 80,6 / 90,9 | 62,7 / 80,8 | 40,2 / 58,6 |
| sólo los **cuatro que la tabla no lleva** al techo | **80,6 / 90,9** | **62,7 / 80,8** | **40,2 / 58,6** |
| + `battle_reader` y `silky_veteran` | 81,6 / 92,2 | 65,0 / 79,8 | 43,9 / **73,9** |
| + `clean_sheet_legacy` (el techo completo de la ADR 0068) | 87,0 / **95,4** | **71,2** / **85,5** | 48,8 / **77,3** |

- **Los cuatro perks que la tabla no lleva reproducen la tabla base *exactamente*, celda a celda.** Es el
  mismo control que la ADR 0063 usó con los `elseEffects`: mismas semillas, mismos partidos, misma cifra.
- **`clean_sheet_legacy` él solo mueve seis de las doce celdas** —+5,4 y +2,0 en `grimhold`, +6,2 y +5,7
  en `the_hunt`, +4,9 y +3,4 en `eternal_crown`— y es el que **saca las cuatro celdas** que la ADR 0068
  §4 lista fuera de banda (en la muestra de la puerta se salen dos, `the_hunt` muy buena 85,5 y
  `eternal_crown` muy buena 77,3; en la sonda de 25 × 8, cuatro). Es el perk que la doctrina
  contextual compra **2 veces en 1.200 runs**, porque `--perk-values` lo mide con el contador a cero y le
  da **−42**, y `WorthASlot` lo rechaza (AN-B).

> **Subirlo al techo mueve el instrumento y no mueve el juego.** No es una hipótesis: `clean_sheet_legacy`
> aparece en el once final de 2 de 1.200 runs con el eje abajo y de 2 de 1.200 con el eje al techo.

## 2. Y el jefe no puede arreglarlo, porque su mando de dificultad no toca la celda que se sale

El encargo daba por hecho que `eternal_crown` podía endurecerse. Medido, no puede: **la calidad del jefe
final mueve su celda `buena` y no mueve su celda `muy buena`.**

| `eternal_crown` (32 × 4) | incoherente | correcta | **buena** | **muy buena** |
|---|---|---|---|---|
| calidad 31 | 2,3 | 26,4 | **43,9** | **73,9** |
| calidad 33 | 2,7 | 23,8 | **36,9** | **73,1** |

Dos puntos de calidad le cuestan **7,0** a la celda `buena` y **0,8** a la `muy buena`. En log-cuotas:
−0,291 contra −0,037. **Endurecer al jefe final para bajar su escalón superior saca por abajo el
inferior antes de mover nada arriba** — y su celda `buena` es precisamente la que lleva desde la ADR 0049
clavada en su suelo de 40. La escalera `buena`→`muy buena` que el eje deja (1,2858 en log-cuotas) es más
larga que la que la banda permite (1,2528), así que **no existe ninguna dificultad de jefe que meta las
dos celdas a la vez**: el hueco es de 0,033, menos de un punto porcentual y muy por dentro del margen
de medida de ±2,5, pero la dirección no se arregla con dificultad.

Lo mismo por el otro lado, con los otros dos jefes, y también medido:

| | ventana de calidad que la tabla permite | qué pasa al usarla |
|---|---|---|
| `grimhold_guns` | 31 → **30** (a 29 la celda `muy buena` se queda a 1,2 de su techo; a 28 la `buena` se sale: 88,1) | — |
| `the_hunt` | 46 → **45** (a 44 la celda `muy buena` se sale: 85,8) | — |
| `eternal_crown` | ninguna (su `correcta`, 26,4, está a 1,6 de su techo de 28) | — |

Se midió el banco completo con `grimhold_guns` a 30 y `the_hunt` a 45, que es **todo** lo que la tabla
permite ablandar:

| | jefes sin tocar | jefes ablandados al límite de la tabla | diferencia pareada |
|---|---|---|---|
| Tasa de victoria de la run | **19,42** (ET 1,26) | 17,84 (ET 1,77) | **−1,58** (ET 1,42) |
| Suelo sin build | **10,66** (ET 0,62) | 11,25 (ET 0,44) | **+0,58** (ET 0,75) |

> **La recalibración que la tabla autoriza está por debajo del ruido, y su signo medido es el
> contrario.** Un punto de calidad de jefe mueve la puerta de la run menos que el error típico de 1.200
> runs, mientras se come todo el margen que la celda `muy buena` tenía. **No se recalibra ningún jefe.**

## Decisión

**1. El eje sube al techo de rareza en seis de los siete efectos con contador de los canales con
recorrido.** `data/perks/`, y nada más:

| perk | rareza · ámbito · canal | hoy | ahora | la línea `k^max` |
|---|---|---|---|---|
| `battle_reader` | poco común · `owner` · `intercept` (base 2,5%) | 50, máx 5 | **100, máx 4** | 7,59 → **16** |
| `lane_reader` | poco común · `actor` · `intercept` | 30, máx 5 | **100** | 3,71 → **32** |
| `captains_voice` | poco común · `team` · `tackle` (base 37,4%) | 30, máx 3 | **100** | 2,20 → **8** |
| `deathless_march` | raro · `team` · `tackle` | 30, máx 5 | **100** | 3,71 → **32** |
| `pit_veteran` | común · `actor` · `tackle` | 30, máx 5 | **50** (su techo) | 3,71 → **7,59** |
| `silky_veteran` | poco común · `actor` · `dribble` | 50, máx 5 | **100** | 7,59 → **32** |
| `clean_sheet_legacy` | poco común · `actor` · `save` | 50, máx 5 | **sin tocar** | |

**2. `clean_sheet_legacy` no sube**, y el motivo es de medición, no de gusto: es el único de los siete
cuyo efecto medible cae **entero** sobre el instrumento (seis celdas de doce, cuatro fuera de banda) y
**cero** sobre el juego (2 de 1.200 runs lo llevan, antes y después). Subirlo consumiría el margen de los
tres jefes a cambio de ninguna tasa de victoria. Queda anotado como **cota**: un jugador humano sí puede
comprarlo, y el día que AN-B se corrija de verdad —midiendo el perk en campaña y no en partido suelto—
este número habrá que volver a mirarlo con la tabla delante.

**3. `battle_reader` se dosifica con `maxValue` (5 → 4) en vez de con el jefe.** Es el único de los seis
que la tabla sí lleva, y lo lleva **dos veces** en el escalón `muy buena`: a `2⁵` sobre un canal de base
2,5% cada portador pasa de interceptar el 2,5% al 45%, y dos portadores así dejan la celda `muy buena` del
jefe final en 73,9 sobre una banda que acaba en 70. A `2⁴` la celda vuelve a 68,0 y la línea del perk
sigue valiendo **más del doble** que hoy (16 frente a 7,59). No es relajar nada: es que el guardarraíl de
la ADR 0056 —"si una celda se sale, recalibras el jefe, nunca la tabla"— **no tiene tercera salida cuando
el jefe no puede**, y la que queda es la dosis del catálogo.

**4. `deathless_march` se queda a un escalón de su techo (100, no 200).** A 200 la línea completa es
`3⁵ = 243` sobre el robo de **todo el equipo**: la cuota pasa de 0,597 a 145 y el motor la clava en su
`probabilityCeiling` del 98% a partir del quinto partido. Es literalmente la patología que el comentario
de `ProbabilityScale.CounterCeilingFor` dice que ese techo existe para evitar —"cinco copias de un raro
clavarían su canal en el 98%"—, y el techo por rareza no la ve porque acota **una copia**, no la línea.
Medido: a 200 no compra nada —aparece en 42 de 1.200 runs y esas runs ganan el 30,95%, frente al 34,15%
que ganaban **antes** del cambio, con `n = 42` en las dos cifras—, y el banco completo a 100 devuelve la
misma run (19,42 en las dos condiciones) con el suelo en 10,58 frente a 10,66 y la separación en 1,1750
frente a 1,1655, las dos diferencias dentro del error. **Se elige el número que no desborda el canal, no
el que hace pasar una celda**, y no cuesta nada.

**5. No se toca ningún jefe.** Ni la calidad, ni el nivel, ni la plantilla, ni los modificadores. Ni
`data/rivals/`, ni `data/economy/`, ni `data/balance/`, ni `data/map/`, ni `/Sim`.

**6. Y la segunda mitad de AQ-A se retira, porque no existe.** La ADR 0068 §5 dejó el orden escrito:
"primero la magnitud, después volver a medir el valor". Medido: **`--perk-values` es ciego a
`valuePerCounter`.** Con las mismas semillas y el mismo lote, la tabla entera —los 61 perks, no sólo los
seis— sale **bit a bit idéntica** (mismas victorias, mismo `valueMilli`) antes y después de subir el eje, porque el instrumento juega **un**
partido y el efecto con contador se evalúa con el contador a **cero**: `k⁰ = 1` sea cual sea `k`.

> **No es que re-medir la valoración después de subir la magnitud dé poco: es que no puede dar nada.**
> El orden que la ADR 0068 proponía no es un orden, es una secuencia vacía. AN-B no se corrige subiendo
> magnitudes; se corrige midiendo el perk a lo largo de una run, que es un instrumento que no existe.
> Por eso `data/economy/perk-values.json` **no se regenera**: sería inyectar ruido de medición sin cambiar
> una sola fila.

## Lo que se ha conseguido y lo que no

| Objetivo (ADR 0056) | ADR 0068 | este paquete | ET | meta | |
|---|---|---|---|---|---|
| Build buena, actos 2/3 (ordinarios) | 60,33 / 43,30 | **62,03 / 46,45** | 0,69 / 0,74 | 60% | acto 2 alcanzado; al 3 le faltan 13,6 |
| Build mediocre, actos 2/3 (ordinarios) | 50,42 / 38,67 | **51,28 / 39,64** | 0,85 / 0,21 | 42-45% | se pasa 6,3 |
| Build mala completa la run | 12,00 | **11,91** | 0,77 | < 2% | no |
| **Suelo sin build** | 10,67 | **10,58** | 0,55 | < 10% | falta 0,6 |
| **Hueco buena/mediocre, acto 2** | **9,91** | **10,75** | **0,73** | **> 9,8** | **sí, con casi un punto de margen** |
| **Tasa de victoria de la run** | **17,00** | **19,42** | **1,26** | **20-30%** | **falta 0,6** |

Y la separación, que es lo que decide la frontera:

| | R₁ · R₂ · R₃ | **S** | potencia | run | suelo |
|---|---|---|---|---|---|
| hoy | 1,263 · 1,460 · 1,325 | 0,8933 | 1,000 | 17,00 | 10,66 |
| eje al techo completo (ADR 0068, `accmax`) | 1,255 · 1,609 · 1,548 | 1,1396 | 1,276 | 19,50 | 10,83 |
| **este paquete** | **1,260 · 1,599 · 1,608** | **1,1750** | **1,315** | **19,42** | **10,58** |
| necesaria para los objetivos 4 y 5 a la vez | | 1,4489 | 1,622 | | |

**La dosis que salva la tabla separa *más* que el techo completo** (1,1750 contra 1,1396) y deja el suelo
más bajo, porque los dos perks que el techo completo añadía —el quinto contador de `battle_reader` y
`clean_sheet_legacy`— no separan: uno satura y al otro la doctrina no lo compra.

Los dos objetivos que quedan a menos de un punto —run 19,42 sobre 20 y suelo 10,58 sobre 10— siguen
siendo **los dos extremos de la misma frontera**, y siguen sin ser alcanzables a la vez. Con el modelo de
la ADR 0065 alimentado con la `S` de este paquete:

| frontera | con la `S` de hoy (0,8933) | con el techo completo (1,1397) | **con este paquete (1,1755)** |
|---|---|---|---|
| Si el suelo se queda en el 10%… la buena gana como mucho | 15,73% | 18,08% | **18,36%** |
| Si la buena gana el 20%… el suelo es como mínimo | 13,42% | 11,62% | **11,17%** |
| Con el suelo medido (10,58%), la buena como mucho | 16,33% | 18,62% | **18,88%** |

**El punto entregado —19,42 con suelo 10,58— está sobre su propia frontera**, no por dentro: el modelo
predice 18,88 y el banco mide 19,42 con ET 1,26. No queda intercambio que hacer entre los dos objetivos;
lo único que los movería sigue siendo más `S`. Y es la primera vez en diez paquetes que la run sube sin
que el suelo la siga.

Guardarraíles, todos verdes: `ordinaryDefeatRateAct1` **24,66** (ET 0,64) sobre un techo de 30 ·
`deathsPerRun` **1,48** (ET 0,02) · `betterTeamWinRate` **79,00** sobre un techo de 88 ·
`matchesPerFullRun` 19,38 · `masterDivergence` 9,51 · 184 ficheros de `/data` validados ·
**599/599 tests en Release, 42/42 puertas**.

Y las doce celdas de la ADR 0033, con la muestra de la puerta (32 × 4), **todas dentro de banda sin usar
el margen de medida de ±2,5**:

| jefe | incoherente | correcta | buena | muy buena |
|---|---|---|---|---|
| `grimhold_guns` | 21,6 [20-35] | 70,5 [65-80] | **81,6** [75-88] | **92,2** [85-95] |
| `the_hunt` | 10,9 [<15] | 38,9 [35-50] | **65,0** [60-72] | **79,8** [72-85] |
| `eternal_crown` | 2,3 [<10] | 26,4 [15-28] | **43,9** [40-55] | **68,0** [55-70] |

La celda que la ADR 0065 dejó señalada como la única clavada en su suelo —la `buena` del jefe final,
40,2 sobre un mínimo de 40— pasa a **43,9**, con 3,9 puntos de margen por primera vez desde la ADR 0049.
Con la sonda de 25 × 8: 18,4 / 67,6 / 86,0 / 93,2 · 8,9 / 39,6 / 67,2 / 81,1 · 4,3 / 25,0 / 41,3 / 69,4.

## Qué falsificaría esta decisión

- **Que las builds de `data/balance/builds/` pasen a llevar los perks de acumulación que hoy no llevan.**
  Toda la §1 se apoya en que la tabla de la ADR 0033 sólo ve dos de los siete. Es un límite del
  **instrumento**, no del juego: si el modelo de build se revisara para muestrear el catálogo como lo
  muestrea la política, la tabla vería el eje entero y volvería a acotarlo. Revisar esas builds es
  revisar el instrumento, no la tabla, y es un paquete propio.
- **Que AN-B se corrija de verdad.** El día que exista un `--perk-values` que mida el perk a lo largo de
  una run, `clean_sheet_legacy` dejará de valer −42, la doctrina contextual lo comprará, y entonces
  subirlo al techo sí movería el juego además del instrumento. Este ADR lo deja fuera porque hoy sólo
  mueve el instrumento, no porque el perk no lo merezca.
- **Que la calidad del jefe final empiece a mover su celda `muy buena`.** Es lo que hace imposible la
  recalibración que el encargo pedía (§2). Si un cambio de plantilla o de modificador le devolviera
  pendiente arriba, `eternal_crown` volvería a tener ventana y `battle_reader` podría ir a `maxValue` 5.
- **Que un punto de calidad de jefe mueva la puerta de la run por encima del ruido.** Hoy no lo hace, y
  por eso "recalibrar los tres jefes" no es una palanca de tasa de victoria: es un mando con una
  resolución más gruesa que el banco.
