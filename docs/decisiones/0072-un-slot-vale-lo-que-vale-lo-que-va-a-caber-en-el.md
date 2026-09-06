# 0072. Un slot vale lo que vale lo que va a caber en él

**Fecha:** 2026-09-06
**Estado:** Aceptada e **implementada**. Toca la **política automática** (`/Sim/Analysis`), un campo nuevo
de `data/economy/perk-values.json` y la muestra de una puerta. **No toca ningún jefe, ninguna banda,
ninguna magnitud del catálogo ni ningún número de economía**
**Cierra:** AS-A y AJ-D · **abre** AT-A
**Requisitos:** RF-023, RF-071, RF-072, RT-023, RT-054, RT-056, RT-057
**Relacionada con:** ADR 0037 (las tres doctrinas), ADR 0038 (la tabla de valor y su palanca de
frecuencia), ADR 0051 (los arcos de build), ADR 0055 (el objeto es una compra), ADR 0056 (los objetivos),
**ADR 0070** (que dejó AS-A abierta y midió la desviación de fila), **ADR 0073** (la densidad por acto,
del mismo paquete)

## El defecto

`RunPolicy.WorthASlot` aceptaba un perk si su valor medido era **≥ 0 exacto**. Ese número tiene dos
problemas y ninguno de los dos es de calibración:

1. **Discrimina por debajo del ruido.** La tabla de la ADR 0070 tiene una **desviación por fila de 17**
   sobre una dispersión observada entre perks de **73**. Un perk que mide −1 y otro que mide 0 son el
   mismo perk para la medida, y el umbral los separaba. `steady_hands` (−1) y `safety_net` (−1) quedaban
   rechazados de plano.
2. **El cero no es el precio del slot.** Un slot ocupado es irreversible (RF-072) y el once sólo tiene
   quince. Aceptar un perk que vale +3 cuando en lo que queda de run van a pasar cuarenta ofertas por
   delante no es "no perder nada": es gastarse un recurso escaso en el primero que pasa.

> El umbral no estaba mal calibrado. **Estaba midiendo la cosa equivocada**: comparaba el valor del perk
> con cero, cuando lo que hay que comparar es el valor **esperado** del perk con lo que ese slot vale si
> se deja libre.

## 1. Primero medir: cuántos slots hay y cuántas ofertas pasan

`runs.csv` gana una columna de diagnóstico, `slotCensus`, con
`acto:ofertas:ofertasCobrables:slotsLibresSumados:slotsDelOnce:perksDelOnce:objetosDelOnce` por acto
(RF-070, mismo patrón que `finalCounters` en la ADR 0070). No entra en ninguna métrica ni en ninguna
puerta, y con ella puesta el banco reproduce la ADR 0070 **al decimal** —16,25 · −0,25 · 10,75 ·
59,55/47,60 · 47,60/35,82 · 5,29/11,84/12,40 perks · 0,76/1,38/2,87 objetos · `deathsPerRun` 1,35 ·
`ordinaryDefeatRateAct1` 24,97 · `masterDivergence` 9,78—, que es lo que autoriza a atribuir lo que se
mueva después.

"Cobrable" es exacto, no estimado: arco abierto (ADR 0051), portador elegible **dentro del once** con
slot libre y colocación que encaja (`PerkPlacement`), y precio dentro del oro si viene del mercado.

Sobre 1.200 runs de la doctrina contextual (300 × semillas 1/1001/2001/3001):

| | acto 1 | acto 2 | acto 3 |
|---|---|---|---|
| Ofertas de perk vistas, por acto | 33,2 | 34,1 | 28,9 |
| De ésas, **cobrables** | **25,6** | 18,4 | 8,0 |
| Slots libres del once al llegar la oferta | **11,4** | 4,3 | 1,4 |
| En la puerta: slots del once / perks puestos | 15,0 / **5,3** | 15,0 / **11,8** | 14,8 / **12,4** |

Tres hechos, y los tres deciden el diseño:

- **El once tiene quince slots de perk** (2/3/4/5 por rareza sobre siete titulares) y **en el jefe del
  acto 2 lleva 11,8 puestos**: el 79% lleno. El slot no es abundante, es el recurso más escaso de la run.
- **La run ve del orden de cincuenta ofertas cobrables** contra esos quince slots. Está sobresuscrito
  más de tres veces.
- **El mapa produce 3,0 ofertas de perk por capa** (33,2 por acto sobre las once capas de
  `MapGenerator.DefaultPathLength`) y en el acto 1 el **77,0%** son cobrables. Ese 77% es el ritmo
  **exógeno**: en el acto 1 hay 11,4 slots libres, así que lo que el filtro mide ahí es la
  **elegibilidad** —raza, posición, etiquetas, perk repetido—, no la saturación. En los actos 2 y 3 el
  ratio cae a 2,05 y 1,29 ofertas por capa, pero eso ya es saturación de slots, que es justo lo que el
  listón está decidiendo: usarlo sería medir el termómetro con el termómetro.

## 2. La derivación, y de dónde sale cada número

**Coste de oportunidad.** Con `S` slots libres y `N` ofertas cobrables por delante, el slot marginal se
llena con la mejor `S`-ésima de esas `N`, o sea con el **cuantil `1 − S/N`** de lo que el pool ofrece
(distribución de la ADR 0038, peso inversamente proporcional al valor). `PerkValueTable.ValueAtQuantile`
lo precalcula al cargar, con peso acumulado y aritmética entera (RT-023).

**`N`, medido.** Las capas que le quedan a este acto son seguras; las de los actos siguientes sólo llegan
si la run pasa sus puertas, así que entran **descontadas por la tasa de paso medida**: `bossWinRateAct1`
**71,8%** y `bossWinRateAct2` **43,9%** en el banco de 1.200 runs. Todo por el ritmo de **2,30** ofertas
cobrables por capa del §1.

**`S`, exacto.** `PerkSlots(rareza) − perks puestos`, sumado sobre el once que la política alinearía.

**El ruido, que es la segunda mitad.** El valor de la tabla es `v + ε` con `σ = 17` y la dispersión
observada es `d = 73`, así que la dispersión **real** del catálogo es `τ = √(73² − 17²) = 71`. El valor
esperado de un perk que mide `m` no es `m`, es `μ + (m − μ)·τ²/d²`. Exigirle al **esperado** que llegue al
coste de oportunidad `C` es exigirle al **medido** que llegue a

```
m ≥ μ + (C − μ) · d²/(d² − σ²)      con μ = 33, d = 73, σ = 17  →  factor 1,0573
```

`σ` deja de ser folclore y pasa a `data/economy/perk-values.json` como **`rowDeviation`** (con su entrada
de esquema): la política la necesita, así que es dato.

**Y lo primero que sale de la fórmula es el número que la ADR 0070 señaló**: con coste de oportunidad
cero el listón no es 0, es **−1** — que es exactamente el valor de `steady_hands` y de `safety_net`, los
dos perks que esa ADR nombró. La corrección no los "deja pasar" por indulgencia: dice que un perk que mide
−1 tiene el mismo valor esperado que un slot que no le va a caber nada, y por eso empata con él.

El listón que produce la regla, sobre la tabla de hoy:

| situación | `N` | `S` | cuantil | `C` | **listón** |
|---|---|---|---|---|---|
| acto 1, capa 0 | 49 | 13 | 0,735 | 38 | **38** |
| acto 1, capa 6 | 35 | 11 | 0,686 | 26 | **26** |
| acto 2, capa 0 | 34 | 7 | 0,794 | 44 | **44** |
| acto 2, capa 9 | 13 | 4 | 0,692 | 29 | **29** |
| acto 3, capa 0 | 23 | 4 | 0,826 | 48 | **48** |
| acto 3, capa 9 | 2 | 2 | — | 0 | **−1** |

No es una constante ni son tres constantes por acto: **sube cuando quedan ofertas y baja cuando se
acaban**, y en el último nodo de la run, donde el slot ya no tiene futuro, vale −1.

## 3. La segunda ceguera de la tabla, y el agujero que abrió corregirla mal

La tabla mide lo que gana un equipo por llevar ese perk **solo**. Una pieza de una línea vale además lo
que **abre** (ADR 0051), y eso la tabla no puede verlo: es la misma ceguera que la ADR 0070 corrigió con
el contador. Sin corregirla, el listón mata los arcos —`mastersReached` 25,2 → 14,8— porque las piezas
baratas de la línea no llegan al listón y el maestro nunca se desbloquea.

**La primera versión eximía a la línea perseguida entera, y fue un error que la medición cazó.** Los
**diez** perks de valor negativo del catálogo pertenecen **todos** a una familia con maestro (`aim`,
`wall`, `craft`, `butchery`), así que la exención los readmitía a todos:

| perk | valor | control | con exención | con **crédito acotado** |
|---|---|---|---|---|
| `spearpoint` | **−141** | 2,3% | **38,0%** | **2,6%** |
| `forward_line` | **−115** | 3,2% | **35,8%** | **3,2%** |
| `own_third_anchor` | −24 | 2,1% | 8,2% | 6,8% |

El crédito correcto está **acotado y medido**: vale el valor medido del **maestro** entre las piezas que
su línea exige, que es lo que aporta una pieza al desbloqueo — `killing_range` 209/2 = **104**,
`granite_line` 119/2 = **59**, `blood_tithe` 32/2 = **16**, `first_touch_school` 24/2 = **12**. Con él,
`spearpoint` sigue valiendo −141 + 104 = −37 y se rechaza. Un maestro no lleva crédito: su valor medido
**ya es** el del desbloqueo.

## 4. Lo que cuesta, con las dos mitades separadas

Banco de 1.200 runs por condición (300 × semillas 1/1001/2001/3001), error típico entre los cuatro
bloques:

| | control (hoy) | **+ listón del slot** | **+ crédito de arco** |
|---|---|---|---|
| **Tasa de victoria de la run** | 16,25 (1,44) | 19,42 (0,37) | **20,33 (0,68)** |
| **`contextualAdvantage`** | **−0,25** (2,21) | 2,92 (1,24) | **+3,83 (0,67)** |
| Ahorradora / gastadora | 16,50 / 10,50 | = | = |
| Buena, actos 2/3 (ordinarios) | 59,55 / 47,60 | 61,54 / 49,06 | **61,10 / 49,40** |
| Hueco del acto 2 | 11,95 | 13,94 | **13,50** |
| `masterDivergence` | 9,78 | 19,04 | **23,24** |
| `mastersReached` | 25,16 | 14,84 | **19,25** |
| Perks del once en las tres puertas | 5,29 / 11,84 / 12,40 | 3,83 / 8,53 / 10,55 | **4,34 / 9,48 / 11,34** |
| Objetos del once en las tres puertas | 0,76 / 1,38 / 2,87 | 0,82 / 1,94 / 2,70 | **0,82 / 1,75 / 3,00** |
| Recompensas rechazadas | 10,8% | 29,2% | 22,0% |

**El listón compra 3,17 puntos de run y el crédito de arco 0,91 más**, y el crédito además devuelve los
arcos que el listón se llevaba por delante. La doctrina que construye vuelve a ganarle a la que sólo
acapara por **3,83 puntos** (ET 0,67, cinco desviaciones sobre cero), después de once paquetes en los que
esa cifra iba de +4,17 a −0,25 sin que nadie supiera por qué.

Y **la mediocre y la mala no se mueven ni un decimal**: el listón sólo lo aplica la doctrina contextual,
así que la comparación entre perfiles no está contaminada.

## 5. Lo que se midió y se descartó

- **El listón constante.** Barrido de −20 a 170 sobre 600 runs por punto: sube hasta un máximo en 60
  (21,3) y baja después, pero **a partir de 40 `masterDivergence` cae a 0** y la puerta de RF-032 exige
  ≥ 5. Con el crédito de arco puesto, el barrido de 4 semillas da 30 → **20,92** (1,14), 45 → 18,09,
  60 → 18,58. El listón derivado (20,33) cae dentro de esa meseta. **No se adopta el 30**: es un número
  elegido porque sale bien, que es exactamente lo que RT-057 prohíbe, y el mismo barrido lo desmiente
  (45 y 60 salen peor que 30 y que el derivado, con el orden invertido respecto a lo que cualquier
  historia predeciría).
- **El horizonte de un solo acto** (el slot sólo compite con lo que queda de este acto): 17,33 frente a
  20,33. El slot dura la run entera y la medida lo confirma.
- **El ritmo de ofertas medido por acto** (2,30 / 2,05 / 1,29 por capa): 17,75. Se descarta además por
  principio, no sólo por medida: los ratios de los actos 2 y 3 miden **saturación de slots**, que es lo
  que el listón decide.
- **Acotar la distribución de oferta a lo que el acto puede ofrecer** (`minAct`): comprobado y no hecho,
  porque no mueve el cuantil — en el acto 1 el q80 es 44 y sobre la tabla entera 48.
- **Poner precio al oro del mercado.** Un perk comprado cuesta el slot **y** el oro, y el oro es lo único
  con lo que se compran objetos (ADR 0055). El canal existe y está medido —listón constante de 40, una semilla, 300 runs, contra su
  propio control: sólo en el mercado **19,33** frente a 15,00 y objetos del once en el jefe del acto 2 de
  1,42 a **2,57**; sólo en la recompensa 18,33 con los objetos **bajando** a 0,95—. **No
  se hace**, porque no se puede hacer sin inventar: `ItemScale.ValueOf` está en una normalización distinta
  —300 a 2.640 milésimas por un +20 repartido entre diez jugadores— de la tabla de perks —−141 a 308 por
  un portador—, así que las dos cifras no se pueden restar. **AT-A.**

## 6. La muestra de una puerta, que era el mismo defecto un nivel más arriba

`FullRunGateTests` juega **60** runs por doctrina. Con esa muestra `deathsPerRun` tiene una desviación de
~0,16, y su cota de no regresión —el 1,0— quedaba a **2,7 desviaciones** del 1,35 que la ADR 0070 midió
sobre 1.200 runs. **Una cota a 2,7 desviaciones no es una cota, es una moneda**, que es literalmente el
defecto que esta ADR corrige en el listón del slot.

Y se cobró: con la política nueva la muestra de 60 midió **0,97** —fuera de la cota— mientras el banco de
1.200 medía **1,42**, o sea el doble de lejos del suelo y **en el signo contrario**. La muestra sube a
**240 runs por doctrina** (`deathsPerRun` 1,35, desviación ~0,08) y **ninguna cota se toca**. Cierra de
paso **AJ-D**, que era la misma fragilidad en `TheThreeDoctrinesBuyDifferently`. La puerta pasa de 51 s a
74 s en Release.

## Decisión

1. **El listón del slot es el coste de oportunidad medido**, no una constante: cuantil `1 − S/N` de la
   distribución de oferta, con `S` los slots libres del once y `N` las ofertas cobrables por delante
   descontadas por la tasa de paso de cada puerta. `RunPolicyOptions.UsesSlotOpportunityCost`.
2. **El valor medido se corrige por el ruido de la medida** antes de compararlo con el listón, con
   `rowDeviation` declarado en `data/economy/perk-values.json`. Con coste de oportunidad cero el listón
   es **−2**, no 0.
3. **Una pieza de la línea perseguida se juzga con el crédito del maestro entre sus piezas**, acotado.
   Un maestro no lleva crédito.
4. **`runs.csv` gana `slotCensus`** como diagnóstico permanente.
5. **La muestra de `FullRunGateTests` sube de 60 a 240 runs por doctrina**, sin tocar ninguna cota.
6. **No se toca ningún jefe, ninguna banda, ninguna magnitud del catálogo ni ningún número de economía.**

## Qué falsificaría esta decisión

- **Que la tabla de valor sea un buen predictor de lo que gana una run.** Hoy no lo es, y el listón lo
  hace visible: un listón constante de 30 mide 20,92 y el derivado 20,33, y los dos están dentro del error
  del otro, pero el barrido completo (30 → 20,9, 45 → 18,1, 60 → 18,6) no tiene forma. Si el orden de la
  tabla fuera fiel, la respuesta al listón sería monótona hasta su óptimo. **No lo es, y eso es del
  instrumento de la ADR 0038, no de esta regla.**
- **Que el ritmo de ofertas por capa cambie.** 2,30 sale del mapa de la ADR 0053 y del surtido de hoy. Si
  cambia el número de capas, el reparto de nodos o el tamaño del mostrador, hay que remedirlo.
- **Que las tasas de paso de puerta cambien.** 71,8% y 43,9% son las de hoy. Están en la política como
  enteros con nombre precisamente para que remedirlas sea una línea.
- **Que el oro se pueda poner en la misma unidad que el slot.** El día que exista una tabla de valor de
  objeto medida como la de perks (`--item-values`), el listón del mercado deja de ser el del slot y pasa a
  ser el del slot más el del oro. **AT-A.**
- **Que el crédito de arco tenga que mirar cuántas piezas faltan.** Hoy vale lo mismo con la línea vacía
  que con una pieza puesta; lo correcto sería que creciera al acercarse el cierre. No se hace porque no
  hay medición que diga cuánto.
