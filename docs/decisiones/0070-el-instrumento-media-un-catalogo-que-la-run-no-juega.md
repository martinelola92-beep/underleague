# 0070. El instrumento medía un catálogo que la run no juega

**Fecha:** 2026-09-06
**Estado:** Aceptada e **implementada**. **Paquete de medición, no de calibración**: no toca ningún jefe,
ningún número de economía ni ninguna banda. Lo que toca es el instrumento, y el instrumento **empeora la
tasa de victoria de la run**, que es el resultado
**Cierra:** AN-B y AR-A · **abre** AS-A, AS-B y AS-C
**Requisitos:** RT-054, RT-055, RT-056, RT-057, RF-070, RF-071, RF-032
**Relacionada con:** ADR 0033 (la tabla, **intocada**), ADR 0038 (la tabla de valor), ADR 0040 (la
densidad por acto), ADR 0050 P1, ADR 0056 (los objetivos y el guardarraíl), ADR 0059 (la última vez que
se regeneró la tabla de valor), ADR 0060, ADR 0069 (donde se detectaron los dos defectos),
**ADR 0071** (el techo de línea, del mismo paquete)

## El encargo, y por qué era de medición

La ADR 0069 dejó dos defectos medidos que llevaban mucho tiempo actuando y que se refuerzan:

1. **`--perk-values` es estructuralmente ciego a `valuePerCounter`.** Mide un partido suelto con el
   contador a cero, así que **todo perk de acumulación vale `k⁰ = 1`** para la tabla: los 61 perks salen
   bit a bit idénticos antes y después de subir seis magnitudes al techo. Consecuencia medida (AN-B):
   `clean_sheet_legacy` valía **−42**, `WorthASlot` lo rechazaba y la doctrina contextual —**lo que en
   todas nuestras tablas se llama "build buena"**— lo compraba 2 veces en 1.200 runs.
2. **Las veinte builds de `data/balance/builds/` no representan lo que la run juega.** De los siete
   efectos con contador que el paquete anterior movió, esas builds llevaban dos.

> **La "build buena" estaba eligiendo con una tabla que no veía un eje entero del catálogo, y la tabla de
> la ADR 0033 estaba arbitrando con veinte builds que no muestrean lo que la run reparte.**

Y al enderezar el primero apareció un tercer defecto que nadie buscaba, más grande que los dos: **la tabla
de valor llevaba diez paquetes sin regenerarse.**

## 1. Primero, un diagnóstico: cuánto contador produce una run de verdad

`runs.csv` gana una columna, `finalCounters`, con `contador:suma:máximo` por contador (RF-070). Es
diagnóstico puro: no entra en ninguna métrica ni en ninguna puerta, y el banco completo reproduce la
ADR 0069 **al decimal** con ella puesta. Sobre 1.200 runs de la doctrina contextual:

| contador | runs que lo tienen | pico medio | mediana | p75 |
|---|---|---|---|---|
| `captainsVoiceMatches` | 23,0% | **8,07** | 7,5 | 11 |
| `ironLungsMatches` | 35,8% | **7,51** | 7 | 11 |
| `battleReaderMatches` | 26,6% | **7,35** | 7 | 10 |
| `deathlessMarchMatches` | 3,4% | 6,27 | 6 | 7 |
| `longLeashMatches` | 18,4% | 6,14 | 5 | 8 |
| `steadyHandsPasses` | 52,3% | 6,06 | 6 | 8 |
| `pitVeteranTackles` | 0,7% | 5,75 | 5,5 | 12 |
| `sharpshooterShots` | 16,8% | 3,35 | 2 | 5 |
| `bruisedKnucklesFouls` | 12,7% | 2,87 | 2 | 4 |
| **`silkyVeteranDribbles`** | 33,1% | **2,56** | 2 | 3 |
| `scarTissueInjuries` | 12,9% | 1,45 | 1 | 2 |

Dos cosas, y las dos deciden el diseño de lo que viene:

- **Un perk vive del orden de ocho partidos.** Los seis contadores que suman uno por partido llegan a un
  pico **medio ponderado de 7,4** y **mediano de 7**.
- **Cada contador crece a su ritmo, y no se parecen.** `silkyVeteranDribbles` se queda en **2,6** aunque
  el perk esté en un tercio de las runs, y `scarTissueInjuries` en 1,4. **Cebar el contador a un valor
  fijo acertaría en seis de los quince.**

## 2. `--perk-values` pasa a medir en campaña

Cada plantilla juega **ocho partidos consecutivos** contra su espejo arrastrando los contadores de un
partido al siguiente con el mismo `ProgressionRules.ApplyCounterDeltas` que usa la run, y el valor del
perk es lo que gana sobre **toda** la campaña. Ocho porque es lo que mide §1: el contador recorre 0..7,
que es lo que recorre en una run. Se arrastra **sólo** el contador: la experiencia y las lesiones las
pagarían por igual los dos lados del espejo y sólo añadirían varianza.

La campaña es además lo único que mide cada contador a **su** ritmo, sin declarar ninguno: por eso se
elige frente a cebar el contador o a promediar sobre una distribución declarada a mano.

**Precisión.** `--rosters 192 --runs 8` con las semillas 5 y 11, sumados: **3.072 partidos por perk**, los
mismos que la tabla anterior. La desviación por fila, medida contra la diferencia entre los dos lotes
independientes, es de **17 unidades** frente a las 23 de antes, sobre una dispersión real entre perks de
**73** frente a 50. **Más señal y menos ruido**, y con 192 campañas por perk en vez de 48 plantillas la
varianza de generación entra cuatro veces más repartida.

## 3. Qué filas se mueven, y por qué: el control separa dos causas

`data/economy/perk-values.json` se regenera. **25 de las 51 filas se mueven más de dos desviaciones**, y
para saber de qué se mueven se midió el **control**: el mismo instrumento, las mismas semillas y el mismo
catálogo de hoy **con el arrastre apagado**.

| | movimiento medio absoluto por fila |
|---|---|
| **La tabla llevaba diez paquetes sin regenerarse** (control − tabla entregada) | **29,0** |
| **La campaña** (tabla nueva − control) | **18,1** |

Y la campaña se reparte exactamente donde tiene que repartirse:

> **La campaña mueve 61,4 unidades de media en los quince perks del eje de acumulación y exactamente
> 0,0 en los otros 36.** No "aproximadamente": los 36 salen **bit a bit idénticos**, porque sin contador
> que arrastrar los flujos de RNG son los mismos. Es el mismo control que la ADR 0069 usó al revés.

**Lo que la campaña mueve** (los quince del eje; el resto no se mueve nada):

| perk | tabla entregada | control sin arrastre | **con campaña** | lo que pone la campaña |
|---|---|---|---|---|
| `deathless_march` | 1 | 30 | **308** | **+278** |
| `clean_sheet_legacy` | **−42** | −20 | **+247** | **+267** |
| `battle_reader` | 18 | 27 | **168** | +141 |
| `captains_voice` | 27 | 29 | **89** | +60 |
| `lane_reader` | −8 | 23 | **75** | +52 |
| `sharpshooter_drill` | 31 | 2 | **44** | +42 |
| `poacher_instinct` | −6 | 33 | **48** | +15 |
| `iron_lungs` · `pit_veteran` · `scar_veteran` · `bruised_knuckles` · `silky_veteran` · `steady_hands` · `long_leash_legacy` · `scar_tissue` | | | | todos por debajo del error |

**`clean_sheet_legacy` pasa de −42 a +247, que es exactamente lo que AN-B predijo**: deja de ser el peor
perk de la tabla y la doctrina contextual lo compra en el **10,9%** de las runs en vez de en el 0,2%.

**Lo que la ranciedad mueve** (las dieciséis filas que se mueven más de dos desviaciones sin que la
campaña tenga nada que ver): `forward_line` **−116**, `center_conductor` −75, `fine_touch` −67,
`first_touch_school` −58, `own_third_anchor` +55, `box_predator` +53, `flank_specialist` −51,
`high_press_trigger` +50, `steady_hands` −47, `scar_tissue` −46, `long_range_menace` +41,
`sweeper_keeper` +40, `diagonal_press` +39, `brute_boots` −37, `cold_focus` +35, `pivot_duo` +35,
`safety_net` −35.

> La tabla se regeneró por última vez en el **paquete AL** (ADR 0059). Entre medias: la ADR 0060 puso
> `elseEffects` a ocho perks, la ADR 0062 recalibró la cadena de pases, la ADR 0067 cambió el
> guardarraíl del acto 1 y la ADR 0069 movió seis magnitudes. Su propio `_doc` decía "hay que remedirla
> cuando cambie el motor o el catálogo", y no se hizo. **La mitad de la tabla estaba desfasada.**

## 4. Lo que cuesta, y de qué es cada punto

Banco de 1.200 runs por condición (300 × semillas 1/1001/2001/3001), cuatro condiciones:

| | run (buena) | suelo | `S` | buena 2/3 | mediocre 2/3 | ahorradora |
|---|---|---|---|---|---|---|
| **A** ADR 0069 (tabla rancia y ciega) | **19,42** (1,26) | 10,58 | 1,168 | 62,03 / 46,45 | 51,28 / 39,64 | 15,25 |
| **B** A + `deathless_march` a `maxValue` 4 (ADR 0071) | 19,50 (1,29) | 10,58 | 1,176 | 62,07 / 46,38 | 51,26 / 39,69 | — |
| **C** tabla remedida **sin** arrastre (sólo se quita la ranciedad) | **14,75** (0,60) | 10,75 | 0,524 | 60,03 / 43,77 | 48,84 / 37,40 | **17,42** |
| **D** tabla en **campaña** — lo que se entrega | **16,25** (1,44) | 10,75 | 0,682 | 59,55 / 47,60 | 47,60 / 35,82 | 16,50 |

> **Quitar la ranciedad cuesta 4,67 puntos de run** (19,42 → 14,75) y se lleva por delante media
> separación (`S` 1,168 → 0,524). **Corregir la ceguera al contador devuelve 1,50** (14,75 → 16,25) y
> **+0,158 de `S`**. El techo de línea de la ADR 0071 no cuesta nada.

Es decir: **de los 19,42 que la ADR 0069 entregó, 4,7 puntos eran del instrumento y no del juego**, y la
corrección de este paquete —la que se pedía— es la única de las tres que empuja hacia arriba.

Y el signo de la segunda corrección se ve mejor en la métrica que dice si la doctrina que construye sabe
lo que hace:

| | doctrina contextual | ahorradora | ventaja |
|---|---|---|---|
| A (tabla rancia y ciega) | 19,42 | 15,25 | **+4,17** |
| C (tabla fresca, ciega) | 14,75 | **17,42** | **−2,67** |
| D (tabla fresca, en campaña) | 16,25 | 16,50 | **−0,25** |

**Con la tabla fresca pero ciega, la doctrina que elige por valor medido pierde contra la que sólo
acapara.** Ver el eje de acumulación devuelve 2,4 puntos de esa ventaja, pero no la recupera entera: la
tabla de valor sigue sin ser un buen predictor de lo que gana una run. **Eso es AS-A**, y es lo primero
que hay que mirar en el paquete siguiente.

## 5. Las veinte builds de `data/balance/builds/`

**La tabla de la ADR 0033 no se toca, y ningún jefe se recalibra**: `data/bosses/` queda intacto. Lo que
se revisa es el material con el que se mide.

**La regla**: cada perk de una build tiene que ser un perk que la run entrega. El listón es el **15% de
presencia** en las 240 runs de esa raza con la doctrina contextual y el catálogo de hoy. Lo que **no**
cambia: raza, calidad, rareza, etiqueta de estilo impuesta, número de perks por escalón (14/14/14/17),
rasgos, objetos, `groups.json` y la densidad por acto de la ADR 0040.

Lo que echaba a perder el instrumento, medido en el banco anterior: las builds llevaban
`clean_sheet_legacy` (0,2% de las runs) en tres de los cuatro escalones, `own_third_anchor` (2,1%),
`box_predator` (2,2%), `covering_shadow` (2,4%), `sweeper_keeper` (0,1%) y `spearpoint` (2,3%), y **no
llevaban** `steady_hands` (54,1%), `sharpshooter_drill` (40,4%), `iron_lungs` (35,8%), `scar_tissue`
(35,0%), `captains_voice` (24,2%) ni `long_leash_legacy` (18,4%).

**Tres cosas se aprendieron por el camino y las tres son del instrumento**, no de la tabla:

- **El orden dentro de un slot no es cosmético.** `BuildDensity` recorta por rondas conservando el
  **último** perk de cada slot, así que a la densidad de la ADR 0040 lo que la puerta mide de verdad son
  los últimos: de los 14 perks de una build `correcta` sólo se miden **5**, y de los 17 de una `muy
  buena`, **11**. La primera versión de esta revisión cambió los perks sin cambiar el orden y las tres
  celdas `correcta` salieron **idénticas al decimal**: nueve de los catorce perks eran decoración.
- **"Colocado donde no se activa" no basta para hacer una build incoherente.** La mayoría de los perks
  frecuentes no tienen condición de colocación, así que colocarlos mal no les hace nada: la primera
  versión del escalón incoherente subió de 18,4 a **44,0** contra el jefe del acto 1. Lo que hace
  incoherente a una build es el **castigo** (`elseEffects`, ADR 0060), así que el escalón se construye
  con los siete perks frecuentes que **castigan** al estar mal puestos.
- **Cuántos acumuladores lleva una build no es una elección: está medido.** Una run de la doctrina
  contextual termina con **3,24** perks de acumulación distintos (mediana 3) y **4,67** en las runs que
  se ganan; y el 83% de los perks de una plantilla están en el once (10,27 de 12,35). De ahí **tres**
  acumuladores alimentados en `buena` y **cuatro** al pico en `muy buena`. Con siete —la primera
  versión— la celda `buena` del jefe del acto 2 se iba a 85,4 sobre una banda que acaba en 72.

Y los **contadores** dejan de escribirse a ojo: `buena` los lleva a la **mitad** del pico medido —"sus
perks de escalado alimentados"— y `muy buena` al **pico** —"el escalado acumulado durante toda la run"—,
que es la distinción que la ADR 0033 escribe. Por eso `silkyVeteranDribbles` vale 3 en `muy buena` y no
5: un regate ganado no se acumula al ritmo de un partido jugado.

> **Aviso de procedimiento, porque importa.** La cifra de acumuladores de `muy buena` se puso primero en
> **5** (4,67 redondeado) y con ella dos celdas quedaban fuera de banda aunque dentro del margen de medida
> de ±2,5. Al aplicar la corrección de "perks en el once, no en la plantilla" —4,67 × 0,83 = **3,9**— la
> cifra baja a 4 y las doce celdas entran sin margen. La corrección es una medida, no una elección, pero
> **el que la disparó fue ver las dos celdas fuera**, y eso queda escrito aquí para que el revisor lo
> juzgue. No se ha tocado ninguna banda, ninguna tolerancia ni ningún jefe.

## 6. Las doce celdas de la ADR 0033

Muestra de la puerta (32 plantillas × 4 partidos = 640 por celda, semilla 1), que es la que decide:

| jefe | incoherente | correcta | buena | muy buena |
|---|---|---|---|---|
| `grimhold_guns` | **28,4** [20-35] | **70,9** [65-80] | **83,8** [75-88] | **95,0** [85-95] |
| `the_hunt` | **13,3** [<15] | **36,9** [35-50] | **62,7** [60-72] | **84,2** [72-85] |
| `eternal_crown` | **4,8** [<10] | **24,8** [15-28] | **42,8** [40-55] | **62,5** [55-70] |

**Las doce dentro de banda sin usar el margen de medida de ±2,5**, con la escalera monótona en los tres
jefes y **sin tocar ningún jefe**. Con la sonda de 25 × 8: 29,9 / 66,5 / 86,6 / 96,2 · 12,3 / 36,7 /
61,1 / 84,5 · 4,8 / 24,3 / 42,1 / 60,4.

Tres celdas quedan **ajustadas** y hay que vigilarlas: `grimhold_guns` muy buena en 95,0 justo en su
techo, `the_hunt` incoherente en 13,3 sobre un techo de 15 y `the_hunt` correcta en 36,9 sobre un suelo
de 35.

## 7. Los seis objetivos, con el instrumento recto

| Objetivo (ADR 0056) | ADR 0069 | **este paquete** | ET | meta | |
|---|---|---|---|---|---|
| Build buena, actos 2/3 (ordinarios) | 62,03 / 46,45 | **59,55 / 47,60** | 0,73 / 0,80 | 60% | acto 2 a 0,45; el 3 mejora 1,2 |
| Build mediocre, actos 2/3 | 51,28 / 39,64 | **47,60 / 35,82** | 0,45 / 2,58 | 42-45% | se pasa 2,6 (antes 6,3) |
| Build mala completa la run | 11,91 | **10,50** | 0,50 | < 2% | no |
| Suelo sin build | 10,58 | **10,75** | 0,42 | < 10% | falta 0,75 |
| Hueco buena/mediocre, acto 2 | 10,75 | **11,95** | 0,99 | > 9,8 | **sí** |
| **Tasa de victoria de la run** | **19,42** | **16,25** | **1,44** | 20-30% | **falta 3,75** |

**Tres objetivos mejoran y tres empeoran, y no se compensa ninguno.** Mejoran el hueco del acto 2
(10,75 → 11,95, el mejor medido en once paquetes), la build mediocre (que se acerca a su banda por primera
vez: de pasarse 6,3 a pasarse 2,6) y la build mala (11,91 → 10,50). Empeoran la tasa de victoria de la run
(−3,17), el suelo (+0,17, dentro del error) y la build buena en el acto 2 (−2,48).

Y la separación, que es lo que decide la frontera:

| | R₁ · R₂ · R₃ | `S` | potencia | run | suelo |
|---|---|---|---|---|---|
| ADR 0069 | 1,260 · 1,603 · 1,592 | 1,1682 | 1,000 | 19,42 | 10,58 |
| tabla fresca **ciega** (control) | 1,091 · 1,311 · 1,181 | 0,5237 | 0,448 | 14,75 | 10,75 |
| **este paquete** | **1,025 · 1,430 · 1,349** | **0,6822** | **0,584** | **16,25** | **10,75** |

**Lo que la ADR 0069 leyó como separación conseguida era, en más de la mitad, una tabla de valor de hace
diez paquetes.** El paquete no la recupera: la mide.

Guardarraíles: `ordinaryDefeatRateAct1` **24,97** (ET 0,73) sobre un techo de 30 · `matchesPerFullRun`
19,39 · `masterDivergence` 9,78 · `betterTeamWinRate` **79,00** (banda 70-88) · `injuriesPerMatch` 0,71 ·
`tacklesPerMatch` 9,78 · `passChainAvgLength` 2,25 · 184 ficheros de `/data` validados ·
**608/608 tests en Release, 42/42 puertas**. Se mueven dos: `deathsPerRun` 1,48 → **1,35** (banda 1,50-3,
ya estaba justo por debajo) y `contextualAdvantage` 4,17 → **−0,25** (banda ≥ 8, ya estaba fuera), que es
AS-A.

## Decisión

1. **`--perk-values` mide en campaña de ocho partidos** arrastrando los contadores (`/Balance`), y ocho
   es lo que mide el banco, no una elección.
2. **`runs.csv` gana `finalCounters`** como diagnóstico permanente: sin él, "cuánto contador produce una
   run" no es medible.
3. **`data/economy/perk-values.json` se regenera** con el instrumento nuevo, 3.072 partidos por perk.
4. **Las veinte builds de `data/balance/builds/` se reescriben** contra el catálogo que la run entrega,
   con el orden dentro del slot como parte del diseño, el escalón incoherente construido sobre el castigo
   y el número de acumuladores y sus contadores tomados de la medición.
5. **No se toca la tabla de la ADR 0033, ni ningún jefe, ni la densidad por acto, ni la economía.**
6. **No se compensa nada.** La run baja de 19,42 a 16,25 y se entrega así.

## Qué falsificaría esta decisión

- **Que ocho partidos no sea el número.** Es el pico medio de los contadores de partido en el banco de
  hoy. Si la run se alargara o se acortara, el instrumento habría que remedirlo con ella.
- **Que la tabla de valor deje de ser el criterio de la doctrina contextual.** Hoy `WorthASlot` acepta un
  perk si su valor medido es **≥ 0**, y el valor tiene una desviación de fila de 17: `steady_hands` a −1
  y `safety_net` a −1 quedan **rechazados de plano**, y con ellos se cae del 54% al 2,4% y del 36% al
  0,2% de las runs. Un umbral en el cero exacto sobre una medida con ruido no es un umbral: es una
  moneda. **AS-A.**
- **Que la densidad por acto de la ADR 0040 se remida.** Está medida en el paquete AA/ADR 0049 y ha
  derivado: el modelo lleva 5,25 / 7,5 / 8,75 perks y 2,0 / 3,5 / 3,75 objetos al once en los tres jefes,
  y el banco de hoy mide **5,3 / 11,8 / 12,4 perks** y **0,75 / 1,4 / 2,9 objetos**. **Faltan perks y
  sobran objetos**, y arreglarlo mueve las doce celdas otra vez. **AS-B**, y no se ha hecho aquí a
  propósito: con tres cambios simultáneos el paquete deja de ser atribuible.
- **Que las builds tengan que dejar de imponer una sola etiqueta de estilo por raza.** Los cinco escalones
  de una raza fuerzan la misma etiqueta a los siete titulares, y una plantilla de verdad las tiene
  mezcladas. Es lo que hace que la run compre `cold_focus` en el 47,6% de las runs —y que esté muerto
  casi siempre— y el instrumento no pueda verlo. **AS-C.**
